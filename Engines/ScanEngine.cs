using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastImageDupe.Core.Helpers;

namespace FastImageDupe.Core
{
    public struct FastFileInfo
    {
        public string Path;
        public long Length;
        public long Ticks;
    }

    public static class ScanEngine
    {
        private class ScanCounters
        {
            public int FoundCount;
            public int SuccessCount;
            public int FailCount;
            public int CacheHit;
            public int ProcessedCount;
        }

        public static async Task<ScanResult> RunScanAsync(
            string[] targets, string[] excludes, bool subDir,
            string[] supportedExts, ConcurrentDictionary<string, ulong> featureCache,
            ConcurrentDictionary<string, PreciseSignature> preciseCache, 
            bool isPersonalAlbumMode, 
            IProgress<string> progress, CancellationToken ct)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();

            var result = new ScanResult();
            
            Process currentProcess = Process.GetCurrentProcess();
            ProcessPriorityClass originalPriority = currentProcess.PriorityClass;
            
            try
            {
                currentProcess.PriorityClass = ProcessPriorityClass.High;

                var localDict = new ConcurrentDictionary<ulong, ConcurrentBag<DupeFileItem>>();
                var counters = new ScanCounters();
                
                var excludeSet = new HashSet<string>(
                    excludes.Select(p => PathHelper.NormalizePath(p)).Where(p => !string.IsNullOrEmpty(p)), 
                    StringComparer.OrdinalIgnoreCase);

                var supportedExtSet = new HashSet<string>(supportedExts, StringComparer.OrdinalIgnoreCase);

                long lastUpdateTicks = Stopwatch.GetTimestamp();
                long intervalTicks = Stopwatch.Frequency * 5; // 💡 統一改為 5 秒更新以釋放全速效能

                Action<bool> updateUI = (force) => {
                    long currentTicks = Stopwatch.GetTimestamp();
                    if (force || currentTicks - Interlocked.Read(ref lastUpdateTicks) > intervalTicks)
                    {
                        Interlocked.Exchange(ref lastUpdateTicks, currentTicks);
                        string status = I18nManager.IsEnglish 
                            ? $"⚡ Processing | Found: {counters.FoundCount:N0} | Hash: {counters.SuccessCount:N0} | Cache: {counters.CacheHit:N0} | Fail: {counters.FailCount:N0}"
                            : $"⚡ 同步處理中 | 尋獲：{counters.FoundCount:N0} 檔 | 解析：{counters.SuccessCount:N0} | 快取：{counters.CacheHit:N0} | 失敗：{counters.FailCount:N0}";
                        progress.Report(status);
                    }
                };

                // =================================================================================
                // 💡 階段 1：極速檔案搜尋
                // =================================================================================
                var allFiles = new List<FastFileInfo>(500000); 
                var dirsToScan = new Queue<string>();
                var processedDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var target in targets)
                {
                    string normTarget = PathHelper.NormalizePath(target);
                    if (!excludeSet.Contains(normTarget) && processedDirs.Add(normTarget))
                    {
                        dirsToScan.Enqueue(normTarget);
                    }
                }

                await Task.Run(() => {
                    Thread.CurrentThread.Priority = ThreadPriority.AboveNormal;

                    while (dirsToScan.Count > 0)
                    {
                        if (ct.IsCancellationRequested) break;
                        
                        string currentDir = dirsToScan.Dequeue();

                        try
                        {
                            var dirInfo = new DirectoryInfo(currentDir);
                            bool isRootDirectory = dirInfo.Parent == null;
                            
                            if (!isRootDirectory)
                            {
                                foreach (var fi in dirInfo.EnumerateFiles("*.*", SearchOption.TopDirectoryOnly))
                                {
                                    if (ct.IsCancellationRequested) break;
                                    
                                    if (supportedExtSet.Contains(fi.Extension))
                                    {
                                        allFiles.Add(new FastFileInfo { 
                                            Path = fi.FullName, 
                                            Length = fi.Length, 
                                            Ticks = fi.LastWriteTimeUtc.Ticks 
                                        });
                                        
                                        counters.FoundCount++;
                                        updateUI(false); 
                                    }
                                }
                            }
                        }
                        catch { }

                        if (subDir)
                        {
                            try
                            {
                                foreach (var sub in Directory.EnumerateDirectories(currentDir, "*", SearchOption.TopDirectoryOnly))
                                {
                                    if (ct.IsCancellationRequested) break;

                                    string normSub = PathHelper.NormalizePath(sub);
                                    
                                    if (!excludeSet.Contains(normSub) && processedDirs.Add(normSub))
                                    {
                                        dirsToScan.Enqueue(sub);
                                    }
                                }
                            }
                            catch { }
                        }
                    }
                });

                if (ct.IsCancellationRequested) result.IsCanceled = true;
                if (allFiles.Count == 0 || result.IsCanceled) return result;

                // =================================================================================
                // 💡 階段 2：無鎖化平行處理 
                // =================================================================================
                updateUI(true); 

                await Task.Run(() => {
                    int maxParallel = Math.Max(1, Environment.ProcessorCount - 1);
                    var options = new ParallelOptions { MaxDegreeOfParallelism = maxParallel, CancellationToken = ct };
                    
                    try
                    {
                        Parallel.ForEach(allFiles, options, fileData => {
                            try {
                                ct.ThrowIfCancellationRequested();
                                
                                string cacheKey = $"{fileData.Length}_{fileData.Ticks}";

                                ulong key = 0;
                                bool gotKey = false;
                                int w = 0, h = 0;

                                if (featureCache.TryGetValue(cacheKey, out var cachedKey)) {
                                    key = cachedKey; 
                                    gotKey = true;
                                    Interlocked.Increment(ref counters.CacheHit);
                                } else {
                                    if (FastImageHasher.TryGetCanonicalKey(fileData.Path, isPersonalAlbumMode, out key, out w, out h, out var preciseSig)) {
                                        featureCache[cacheKey] = key; 
                                        if (preciseSig != null) preciseCache[cacheKey] = preciseSig; 
                                        gotKey = true;
                                    }
                                }

                                if (gotKey) {
                                    Interlocked.Increment(ref counters.SuccessCount);
                                    
                                    var item = new DupeFileItem { 
                                        GroupId = key, 
                                        FilePath = fileData.Path, 
                                        FileSize = fileData.Length, 
                                        FileTicks = fileData.Ticks, 
                                        Width = w, 
                                        Height = h 
                                    };
                                    
                                    localDict.GetOrAdd(key, _ => new ConcurrentBag<DupeFileItem>()).Add(item);
                                } else { Interlocked.Increment(ref counters.FailCount); }
                            } catch (OperationCanceledException) {
                                throw;
                            } catch { Interlocked.Increment(ref counters.FailCount); }

                            Interlocked.Increment(ref counters.ProcessedCount);
                            updateUI(false); 
                        });
                    }
                    catch (OperationCanceledException) { result.IsCanceled = true; }

                    updateUI(true); 

                    var duplicates = localDict.Values.Where(g => g.Count > 1).Select(g => g.ToList()).ToList();
                    result.DuplicateGroupsCount = duplicates.Count;
                    foreach (var group in duplicates) result.Duplicates.AddRange(group);
                });

                result.TotalFound = counters.FoundCount;
                result.SuccessCount = counters.SuccessCount;
                result.FailCount = counters.FailCount;
                result.CacheHitCount = counters.CacheHit;

                return result;
            }
            finally
            {
                try { currentProcess.PriorityClass = originalPriority; } catch { }
            }
        }
    }
}