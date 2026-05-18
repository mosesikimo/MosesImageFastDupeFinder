using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FastImageDupe.Core.Helpers;

namespace FastImageDupe.Core
{
    public class ExcludedNode
    {
        public string Path { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public string SourceOfExclusion { get; set; } = string.Empty; 
    }

    public static class DirectoryFilter
    {
        private static readonly HashSet<string> _mediaExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".tiff", ".tif", ".heic", ".raw", ".cr2", ".nef", ".ico",
            ".mp4", ".avi", ".mkv", ".mov", ".wmv", ".flv", ".webm", ".m4v", ".ts", ".rmvb", ".mpg", ".swf", ".rm",
            ".txt", ".url", ".html", ".htm", ".nfo", ".xml", ".srt", ".ass", ".ssa", ".torrent", ".bc!", ".chm", ".mht"
        };

        private static readonly HashSet<string> _implicateJunkDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "node_modules", ".git", ".svn", "obj", "bin"
        };

        private static readonly HashSet<string> _selfOnlyJunkDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Windows", "Program Files", "Program Files (x86)", "ProgramData",
            "AppData", "$RECYCLE.BIN", "System Volume Information", 
            "FastImageDupeDel" // 💡 新增：將隔離資料夾視為系統垃圾目錄直接排除
        };

        public static (List<string> RootsToScan, List<ExcludedNode> SimplifiedExcluded) Process(
            string[] selectedRoots, string[] manualExcludes)
        {
            var manualExcludedSet = new HashSet<string>(
                manualExcludes.Select(PathHelper.NormalizePath).Where(p => !string.IsNullOrEmpty(p)), 
                StringComparer.OrdinalIgnoreCase);

            var rootsToScan = new List<string>();
            var excludedRootsDict = new Dictionary<string, (string Reason, string SourcePath)>(StringComparer.OrdinalIgnoreCase);
            var protectedRootsSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var ex in manualExcludedSet)
            {
                excludedRootsDict[ex] = ("手動排除", ex);
            }

            foreach (var root in selectedRoots)
            {
                string normalizedRoot = PathHelper.NormalizePath(root);
                if (string.IsNullOrEmpty(normalizedRoot) || manualExcludedSet.Contains(normalizedRoot)) continue;

                // 💡 安全防護：即使在 Root 層級選到了 FastImageDupeDel，也直接跳過
                if (new DirectoryInfo(normalizedRoot).Name.Equals("FastImageDupeDel", StringComparison.OrdinalIgnoreCase))
                {
                    excludedRootsDict[normalizedRoot] = ("系統/資源回收目錄 (隔離區)", normalizedRoot);
                    continue;
                }

                rootsToScan.Add(normalizedRoot); 
                protectedRootsSet.Add(normalizedRoot); 

                try
                {
                    foreach (var subDir in new DirectoryInfo(normalizedRoot).EnumerateDirectories())
                    {
                        EvaluateAndExcludeDirectory(subDir.FullName, manualExcludedSet, excludedRootsDict, 1, 3);
                    }
                }
                catch { /* Ignore root-level permission exceptions */ } 
            }

            return (rootsToScan, SimplifyExcludedRoots(excludedRootsDict, protectedRootsSet));
        }

        private static (int State, string Reason, string SourcePath) EvaluateAndExcludeDirectory(
            string dir, HashSet<string> manualExcludedSet, 
            Dictionary<string, (string Reason, string SourcePath)> excludedRootsDict, 
            int currentDepth, int maxDepth)
        {
            if (currentDepth > maxDepth) return (0, "", "");

            string normalizedDir = PathHelper.NormalizePath(dir);
            if (manualExcludedSet.Contains(normalizedDir)) return (0, "", ""); 

            (int, string, string) RegisterExclusion(int state, string reason, string? sourcePath = null)
            {
                sourcePath ??= normalizedDir;
                excludedRootsDict[normalizedDir] = (reason, sourcePath);
                return (state, reason, sourcePath);
            }

            try
            {
                var dirInfo = new DirectoryInfo(dir);
                
                // 💡 這裡會自動擋下 FastImageDupeDel，因為我們已經將它加進 _selfOnlyJunkDirs
                if (_selfOnlyJunkDirs.Contains(dirInfo.Name)) return RegisterExclusion(2, "系統/資源回收目錄");
                if (_implicateJunkDirs.Contains(dirInfo.Name)) return RegisterExclusion(1, "專案或程式核心目錄");

                int totalFiles = 0, nonMediaFiles = 0;
                foreach (var filePath in Directory.EnumerateFiles(dir))
                {
                    totalFiles++;
                    string ext = Path.GetExtension(filePath);

                    if (ext.Equals(".exe", StringComparison.OrdinalIgnoreCase)) return RegisterExclusion(1, "包含執行檔(.exe)");
                    if (!_mediaExts.Contains(ext)) nonMediaFiles++;
                }
                
                if (totalFiles >= 10 && nonMediaFiles >= totalFiles * 0.5) 
                    return RegisterExclusion(1, $"非媒體檔案過多 (佔比: {(double)nonMediaFiles / totalFiles:P0})");

                var subDirs = dirInfo.GetDirectories();
                int totalSubs = subDirs.Length, excludedSubs = 0; 
                string sampleSourcePath = "";

                foreach (var subDir in subDirs)
                {
                    var subResult = EvaluateAndExcludeDirectory(subDir.FullName, manualExcludedSet, excludedRootsDict, currentDepth + 1, maxDepth);
                    
                    if (subResult.State == 1) 
                    {
                        excludedSubs++;
                        sampleSourcePath = string.IsNullOrEmpty(sampleSourcePath) ? subResult.SourcePath : sampleSourcePath;

                        if (totalSubs > 0 && (double)excludedSubs / totalSubs >= 0.3 && !HasAnyMediaFile(dir))
                        {
                            var prefix = normalizedDir + Path.DirectorySeparatorChar;
                            foreach (var key in excludedRootsDict.Keys.Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList())
                            {
                                excludedRootsDict.Remove(key);
                            }
                            return RegisterExclusion(1, "子目錄達30%被排除 (觸發提早結束)", sampleSourcePath);
                        }
                    }
                }

                return (0, "", ""); 
            }
            catch (UnauthorizedAccessException) { return RegisterExclusion(2, "權限不足存取受阻"); } 
            catch (Exception) { return (0, "", ""); }
        }

        private static List<ExcludedNode> SimplifyExcludedRoots(
            Dictionary<string, (string Reason, string SourcePath)> excludedRootsDict, HashSet<string> protectedRoots)
        {
            bool hasChanged = true;
            var currentExcluded = new Dictionary<string, (string Reason, string SourcePath)>(excludedRootsDict, StringComparer.OrdinalIgnoreCase);
            var directoryCache = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

            while (hasChanged)
            {
                hasChanged = false;
                var parentDirs = currentExcluded.Keys
                    .Select(Path.GetDirectoryName)
                    .Where(p => !string.IsNullOrEmpty(p))
                    .Select(p => p!)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                foreach (var parent in parentDirs)
                {
                    if (currentExcluded.ContainsKey(parent) || protectedRoots.Contains(PathHelper.NormalizePath(parent))) continue;

                    try
                    {
                        if (!directoryCache.TryGetValue(parent, out var actualSubDirs))
                        {
                            actualSubDirs = Directory.GetDirectories(parent);
                            directoryCache[parent] = actualSubDirs;
                        }

                        if (actualSubDirs.Length == 0) continue;

                        if (actualSubDirs.All(sub => currentExcluded.ContainsKey(PathHelper.NormalizePath(sub))))
                        {
                            if (!HasAnyMediaFile(parent))
                            {
                                string samplePath = currentExcluded[PathHelper.NormalizePath(actualSubDirs[0])].SourcePath;
                                currentExcluded[PathHelper.NormalizePath(parent)] = ("子目錄全數排除(合併)", samplePath);
                                
                                foreach (var sub in actualSubDirs)
                                {
                                    currentExcluded.Remove(PathHelper.NormalizePath(sub)); 
                                }
                                hasChanged = true;
                            }
                        }
                    }
                    catch { /* Ignore permissions */ } 
                }
            }

            return BuildFinalExcludedNodes(currentExcluded);
        }

        private static List<ExcludedNode> BuildFinalExcludedNodes(Dictionary<string, (string Reason, string SourcePath)> currentExcluded)
        {
            var finalList = new List<ExcludedNode>();
            foreach (var kvp in currentExcluded)
            {
                string dir = kvp.Key;
                bool hasAncestor = false;
                string? parent = Path.GetDirectoryName(dir);
                
                while (!string.IsNullOrEmpty(parent))
                {
                    if (currentExcluded.ContainsKey(PathHelper.NormalizePath(parent)))
                    {
                        hasAncestor = true;
                        break;
                    }
                    parent = Path.GetDirectoryName(parent);
                }

                if (!hasAncestor)
                {
                    finalList.Add(new ExcludedNode 
                    { 
                        Path = dir, 
                        Reason = kvp.Value.Reason,
                        SourceOfExclusion = kvp.Value.SourcePath 
                    });
                }
            }

            return finalList.OrderBy(x => x.Path).ToList();
        }

        private static bool HasAnyMediaFile(string dirPath)
        {
            try
            {
                return Directory.EnumerateFiles(dirPath).Any(f => _mediaExts.Contains(Path.GetExtension(f)));
            }
            catch { return false; }
        }
    }
}