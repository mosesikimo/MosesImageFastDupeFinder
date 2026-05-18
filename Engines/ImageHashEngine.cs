using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FastImageDupe.Core.Helpers;

namespace FastImageDupe.Core
{
    // =========================================================================
    // 💡 系統資源節流閥：防止 WPF 併發解碼導致 GDI 句柄耗盡與記憶體崩潰
    // =========================================================================
    internal static class WpfResourceManager
    {
        public static readonly SemaphoreSlim DecodeSemaphore = new SemaphoreSlim(Math.Max(2, Environment.ProcessorCount / 2));
    }

    public class PreciseSignature
    {
        // 💡 完全拔除 R, G, B！只留下最純粹的四個 64-bit 數字，省下大量記憶體！
        public ulong H0 { get; set; }
        public ulong H1 { get; set; }
        public ulong H2 { get; set; }
        public ulong H3 { get; set; }

        public string? Hash { get; set; }

        public void UpgradeLegacyHash()
        {
            if (Hash == null) return;
            
            lock (this)
            {
                if (Hash != null && Hash.Length >= 256)
                {
                    ulong h0 = 0, h1 = 0, h2 = 0, h3 = 0;
                    for (int i = 0; i < 64; i++)
                    {
                        if (Hash[i] == '1') h0 |= (1UL << i);
                        if (Hash[64 + i] == '1') h1 |= (1UL << i);
                        if (Hash[128 + i] == '1') h2 |= (1UL << i);
                        if (Hash[192 + i] == '1') h3 |= (1UL << i);
                    }
                    H0 = h0; H1 = h1; H2 = h2; H3 = h3;
                    Hash = null; 
                }
            }
        }
    }

    public static class FastImageHasher
    {
        public static bool TryGetCanonicalKey(string filePath, bool isPersonalAlbumMode, out ulong canonicalKey, out int width, out int height, out PreciseSignature? preciseSig)
        {
            canonicalKey = 0;
            width = 0;
            height = 0;
            preciseSig = null;

            try
            {
                string longPath = PathHelper.GetLongPath(filePath);

                using (var fs = new FileStream(longPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    if (fs.Length == 0 || fs.Length > 100 * 1024 * 1024) return false;

                    using (var ms = new MemoryStream((int)fs.Length))
                    {
                        fs.CopyTo(ms);
                        ms.Position = 0;

                        WpfResourceManager.DecodeSemaphore.Wait();
                        try
                        {
                            var decoder = BitmapDecoder.Create(ms, BitmapCreateOptions.DelayCreation | BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.None);
                            if (decoder.Frames.Count > 0)
                            {
                                width = decoder.Frames[0].PixelWidth;
                                height = decoder.Frames[0].PixelHeight;
                            }

                            // 只解碼 16x16 一次，拔除所有彩色負擔
                            ms.Position = 0;
                            var bmp16 = new BitmapImage();
                            bmp16.BeginInit();
                            bmp16.CacheOption = BitmapCacheOption.OnLoad;
                            bmp16.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                            bmp16.DecodePixelWidth = 16;
                            bmp16.DecodePixelHeight = 16;
                            bmp16.StreamSource = ms;
                            bmp16.EndInit();
                            bmp16.Freeze();

                            var converted16 = new FormatConvertedBitmap();
                            converted16.BeginInit();
                            converted16.Source = bmp16;
                            converted16.DestinationFormat = PixelFormats.Gray8; // 💡 直接以灰階載入
                            converted16.EndInit();
                            converted16.Freeze();

                            int stride16 = converted16.PixelWidth; 
                            byte[] gray16 = new byte[converted16.PixelHeight * stride16];
                            converted16.CopyPixels(gray16, stride16, 0);

                            long totalLuma16 = 0;
                            for (int i = 0; i < 256; i++) totalLuma16 += gray16[i];

                            long totalLuma8 = 0;
                            byte[] gray8 = new byte[64];
                            for (int y = 0; y < 8; y++)
                            {
                                for (int x = 0; x < 8; x++)
                                {
                                    int p1 = (y * 2) * 16 + (x * 2);
                                    byte avg = (byte)((gray16[p1] + gray16[p1 + 1] + gray16[p1 + 16] + gray16[p1 + 17]) >> 2);
                                    gray8[y * 8 + x] = avg;
                                    totalLuma8 += avg;
                                }
                            }

                            int avgLuma8 = (int)(totalLuma8 / 64);
                            ulong baseHash = 0;
                            for (int i = 0; i < 64; i++)
                            {
                                if (gray8[i] >= avgLuma8) baseHash |= (1UL << (63 - i));
                            }
                            
                            // 💡 模式感知：個人相簿模式不處理旋轉容錯，防止直橫照片被誤判
                            if (isPersonalAlbumMode)
                            {
                                canonicalKey = baseHash;
                            }
                            else
                            {
                                canonicalKey = GetCanonicalHash(baseHash);
                            }

                            ulong h0 = 0, h1 = 0, h2 = 0, h3 = 0;
                            int avgLuma16 = (int)(totalLuma16 / 256);
                            for (int i = 0; i < 64; i++)
                            {
                                if (gray16[i] >= avgLuma16) h0 |= (1UL << i);
                                if (gray16[64 + i] >= avgLuma16) h1 |= (1UL << i);
                                if (gray16[128 + i] >= avgLuma16) h2 |= (1UL << i);
                                if (gray16[192 + i] >= avgLuma16) h3 |= (1UL << i);
                            }

                            preciseSig = new PreciseSignature { H0 = h0, H1 = h1, H2 = h2, H3 = h3 };
                        }
                        finally
                        {
                            WpfResourceManager.DecodeSemaphore.Release();
                        }
                    }
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        private static ulong GetCanonicalHash(ulong originalHash)
        {
            ulong minHash = originalHash;
            ulong current = originalHash;
            for (int i = 0; i < 3; i++) { current = Rotate90(current); if (current < minHash) minHash = current; }
            current = FlipHorizontal(originalHash); if (current < minHash) minHash = current;
            for (int i = 0; i < 3; i++) { current = Rotate90(current); if (current < minHash) minHash = current; }
            return minHash;
        }

        private static ulong Rotate90(ulong hash)
        {
            ulong result = 0;
            for (int y = 0; y < 8; y++) {
                for (int x = 0; x < 8; x++) {
                    int srcIndex = y * 8 + x;
                    int destIndex = x * 8 + (7 - y);
                    if ((hash & (1UL << (63 - srcIndex))) != 0) result |= (1UL << (63 - destIndex));
                }
            }
            return result;
        }

        private static ulong FlipHorizontal(ulong hash)
        {
            ulong result = 0;
            for (int y = 0; y < 8; y++) {
                for (int x = 0; x < 8; x++) {
                    int srcIndex = y * 8 + x;
                    int destIndex = y * 8 + (7 - x);
                    if ((hash & (1UL << (63 - srcIndex))) != 0) result |= (1UL << (63 - destIndex));
                }
            }
            return result;
        }
    }

    public static class PreciseHashEngine
    {
        private struct FastSigItem
        {
            public DupeFileItem File;
            public PreciseSignature Sig;
        }

        public static async Task ProcessCollisionsAsync(ScanResult result, ConcurrentDictionary<string, PreciseSignature> cache, bool isPersonalAlbumMode, IProgress<string> progress, CancellationToken ct)
        {
            if (result.Duplicates.Count == 0) return;

            await Task.Run(() => {
                long totalFiles = result.Duplicates.Count;
                long processedFiles = 0; 
                long diskReads = 0;

                long lastUpdateTicks = Stopwatch.GetTimestamp();
                long intervalTicks = Stopwatch.Frequency * 5; // 💡 統一改為 5 秒更新以釋放全速效能

                int fetchParallel = Math.Max(2, Environment.ProcessorCount / 2);
                var fetchOptions = new ParallelOptions { MaxDegreeOfParallelism = fetchParallel, CancellationToken = ct };
                
                var fastItemsArray = new FastSigItem[totalFiles];
                var duplicatesArray = result.Duplicates.ToArray();

                Parallel.For(0, duplicatesArray.Length, fetchOptions, i => {
                    ct.ThrowIfCancellationRequested();
                    var file = duplicatesArray[i];
                    PreciseSignature? sig = null;
                    
                    string cacheKey = $"{file.FileSize}_{file.FileTicks}";
                    
                    if (cache.TryGetValue(cacheKey, out var cachedSig)) {
                        sig = cachedSig;
                        sig.UpgradeLegacyHash();
                    } else {
                        Interlocked.Increment(ref diskReads); 
                        var rawSig = GetPreciseSignature(file.FilePath);
                        if (rawSig != null) {
                            sig = rawSig;
                            if (file.FileTicks > 0) cache[cacheKey] = sig; 
                        }
                    }

                    if (sig != null)
                    {
                        fastItemsArray[i] = new FastSigItem { File = file, Sig = sig };
                    }

                    long p = Interlocked.Increment(ref processedFiles);
                    long currentTicks = Stopwatch.GetTimestamp();
                    long lastTicks = Interlocked.Read(ref lastUpdateTicks);
                    
                    if (currentTicks - lastTicks > intervalTicks && Interlocked.CompareExchange(ref lastUpdateTicks, currentTicks, lastTicks) == lastTicks) 
                    {
                        long currentReads = Interlocked.Read(ref diskReads);
                        string msg = currentReads > 100 
                            ? (I18nManager.IsEnglish ? $"🔍 Building Cache (Disk I/O)... {p}/{totalFiles}" : $"🔍 重建高精度快取(讀取硬碟)... {p}/{totalFiles}")
                            : (I18nManager.IsEnglish ? $"🔍 Preparing Data... {p}/{totalFiles}" : $"🔍 準備資料中... {p}/{totalFiles}");
                        progress.Report(msg);
                    }
                });

                if (ct.IsCancellationRequested) return;

                var groupedItems = fastItemsArray.Where(x => x.File != null).GroupBy(x => x.File.GroupId).ToList();
                var refinedDuplicates = new List<DupeFileItem>((int)totalFiles);
                ulong maxGroupCounter = result.Duplicates.Count > 0 ? (ulong)result.Duplicates.Max(x => x.GroupId) + 1 : 1;
                
                long totalGroups = groupedItems.Count;
                long processedGroups = 0;

                // 💡 模式感知：嚴格模式 (連拍防護) 時容錯值為 0，網路模式容忍輕微雜訊設為 5
                int diffThreshold = isPersonalAlbumMode ? 0 : 5;

                foreach (var group in groupedItems)
                {
                    ct.ThrowIfCancellationRequested();

                    var exactMap = new Dictionary<(ulong, ulong, ulong, ulong), List<FastSigItem>>();
                    foreach (var item in group)
                    {
                        var key = (item.Sig.H0, item.Sig.H1, item.Sig.H2, item.Sig.H3);
                        if (!exactMap.TryGetValue(key, out var list)) {
                            list = new List<FastSigItem>();
                            exactMap[key] = list;
                        }
                        list.Add(item);
                    }

                    var subGroups = new List<List<FastSigItem>>();

                    foreach (var exactList in exactMap.Values)
                    {
                        var item = exactList[0]; 
                        bool placed = false;
                        
                        int limitIdx = Math.Max(0, subGroups.Count - 50);
                        
                        for (int j = subGroups.Count - 1; j >= limitIdx; j--)
                        {
                            var sg = subGroups[j];
                            var refItem = sg[0]; 

                            if (isPersonalAlbumMode)
                            {
                                if (item.File.Width > 0 && item.File.Height > 0 && refItem.File.Width > 0 && refItem.File.Height > 0)
                                {
                                    double ratioItem = (double)item.File.Width / item.File.Height;
                                    double ratioRef = (double)refItem.File.Width / refItem.File.Height;
                                    
                                    double ratioDiff = Math.Abs(ratioItem - ratioRef) / Math.Max(ratioItem, ratioRef);
                                    if (ratioDiff > 0.01) continue; 
                                }
                            }

                            int diff = PopCount(item.Sig.H0 ^ refItem.Sig.H0) + 
                                       PopCount(item.Sig.H1 ^ refItem.Sig.H1) + 
                                       PopCount(item.Sig.H2 ^ refItem.Sig.H2) + 
                                       PopCount(item.Sig.H3 ^ refItem.Sig.H3);

                            if (diff <= diffThreshold)
                            {
                                sg.AddRange(exactList);
                                placed = true;
                                break;
                            }
                        }

                        if (!placed)
                        {
                            subGroups.Add(exactList);
                        }
                    }

                    for (int i = 0; i < subGroups.Count; i++)
                    {
                        if (subGroups[i].Count >= 2)
                        {
                            ulong newGroupId = (i == 0) ? group.Key : maxGroupCounter++; 
                            foreach (var item in subGroups[i])
                            {
                                item.File.GroupId = newGroupId;
                                refinedDuplicates.Add(item.File);
                            }
                        }
                    }

                    processedGroups++;
                    long currentTicksCluster = Stopwatch.GetTimestamp();
                    if (currentTicksCluster - lastUpdateTicks > intervalTicks) 
                    {
                        lastUpdateTicks = currentTicksCluster;
                        string msg = I18nManager.IsEnglish 
                            ? $"🔍 Advanced Clustering... {processedGroups}/{totalGroups} groups" 
                            : $"🔍 XOR 深度分析中... {processedGroups}/{totalGroups} 群組";
                        progress.Report(msg);
                    }
                }

                if (!ct.IsCancellationRequested)
                {
                    result.Duplicates = refinedDuplicates;
                }
            });
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int PopCount(ulong value)
        {
            value = value - ((value >> 1) & 0x5555555555555555UL);
            value = (value & 0x3333333333333333UL) + ((value >> 2) & 0x3333333333333333UL);
            value = (value + (value >> 4)) & 0x0F0F0F0F0F0F0F0FUL;
            return (int)((value * 0x0101010101010101UL) >> 56);
        }

        private static PreciseSignature? GetPreciseSignature(string filePath)
        {
            try
            {
                using (var fs = new FileStream(PathHelper.GetLongPath(filePath), FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    if (fs.Length == 0 || fs.Length > 100 * 1024 * 1024) return null;
                    
                    using (var ms = new MemoryStream((int)fs.Length))
                    {
                        fs.CopyTo(ms);
                        ms.Position = 0;

                        WpfResourceManager.DecodeSemaphore.Wait();
                        try
                        {
                            var bmp = new BitmapImage();
                            bmp.BeginInit();
                            bmp.CacheOption = BitmapCacheOption.OnLoad;
                            bmp.CreateOptions = BitmapCreateOptions.IgnoreColorProfile; 
                            bmp.DecodePixelWidth = 16;
                            bmp.DecodePixelHeight = 16;
                            bmp.StreamSource = ms;
                            bmp.EndInit();
                            bmp.Freeze();

                            var converted = new FormatConvertedBitmap();
                            converted.BeginInit();
                            converted.Source = bmp;
                            converted.DestinationFormat = PixelFormats.Gray8; 
                            converted.EndInit();
                            converted.Freeze();

                            int stride = converted.PixelWidth; 
                            byte[] gray = new byte[converted.PixelHeight * stride];
                            converted.CopyPixels(gray, stride, 0);

                            long totalLuma = 0;
                            for (int i = 0; i < 256; i++) totalLuma += gray[i];

                            ulong h0 = 0, h1 = 0, h2 = 0, h3 = 0;
                            int avgLuma = (int)(totalLuma / 256);
                            for (int i = 0; i < 64; i++)
                            {
                                if (gray[i] >= avgLuma) h0 |= (1UL << i);
                                if (gray[64 + i] >= avgLuma) h1 |= (1UL << i);
                                if (gray[128 + i] >= avgLuma) h2 |= (1UL << i);
                                if (gray[192 + i] >= avgLuma) h3 |= (1UL << i);
                            }
                            
                            return new PreciseSignature { H0 = h0, H1 = h1, H2 = h2, H3 = h3 };
                        }
                        finally
                        {
                            WpfResourceManager.DecodeSemaphore.Release();
                        }
                    }
                }
            }
            catch
            {
                return null;
            }
        }
    }
}