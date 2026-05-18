using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace FastImageDupe.Core.Engines
{
    public static class GroupAnalysisEngine
    {
        public static (NamingPatternType Type, string Prefix, int MinNum, int MaxNum) AnalyzeGroupPattern(List<string> fileNames)
        {
            if (fileNames == null || fileNames.Count == 0) return (NamingPatternType.Random, "", 0, 0);

            var regex = new System.Text.RegularExpressions.Regex(@"^(?<prefix>.*\D)?(?<number>\d+)(?<suffix>\D*)$");
            
            string? commonPrefix = null;
            bool allHaveNumbers = true;
            int minNum = int.MaxValue;
            int maxNum = int.MinValue;

            foreach (var name in fileNames)
            {
                string nameWithoutExt = Path.GetFileNameWithoutExtension(name);
                var match = regex.Match(nameWithoutExt);
                
                if (match.Success)
                {
                    string prefix = match.Groups["prefix"].Value;
                    string numStr = match.Groups["number"].Value;

                    if (commonPrefix == null) commonPrefix = prefix;
                    else if (!commonPrefix.Equals(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        int minLen = Math.Min(commonPrefix.Length, prefix.Length);
                        int i = 0;
                        while (i < minLen && char.ToLower(commonPrefix[i]) == char.ToLower(prefix[i])) i++;
                        commonPrefix = commonPrefix.Substring(0, i);
                    }

                    if (int.TryParse(numStr, out int num))
                    {
                        minNum = Math.Min(minNum, num);
                        maxNum = Math.Max(maxNum, num);
                    }
                    else allHaveNumbers = false;
                }
                else
                {
                    allHaveNumbers = false;
                    if (commonPrefix == null) commonPrefix = nameWithoutExt;
                    else
                    {
                        int minLen = Math.Min(commonPrefix.Length, nameWithoutExt.Length);
                        int i = 0;
                        while (i < minLen && char.ToLower(commonPrefix[i]) == char.ToLower(nameWithoutExt[i])) i++;
                        commonPrefix = commonPrefix.Substring(0, i);
                    }
                }
            }

            if (allHaveNumbers && commonPrefix != null)
                return (NamingPatternType.Sequential, commonPrefix, minNum, maxNum);
            
            if (!string.IsNullOrEmpty(commonPrefix) && commonPrefix.Length >= 3)
                return (NamingPatternType.Grouped, commonPrefix, 0, 0);

            return (NamingPatternType.Random, "", 0, 0);
        }

        public static int CleanOrphanedItems(ObservableCollection<DupeFileItem> dupeFiles)
        {
            var orphanedItems = dupeFiles.GroupBy(x => x.GroupId).Where(g => g.Count() < 2).SelectMany(g => g).ToList();
            foreach (var orphan in orphanedItems) dupeFiles.Remove(orphan);
            return orphanedItems.Count;
        }

        public static void RefreshFileGroupIds(IEnumerable<DupeFileItem> dupeFiles)
        {
            int fileGroupId = 1;
            foreach (var group in dupeFiles.GroupBy(x => x.GroupId))
            {
                foreach (var item in group) item.DisplayGroupId = fileGroupId;
                fileGroupId++;
            }
        }

        public static (DupeFileItem Best, List<DupeFileItem> Inferiors) GetBestAndInferiorFiles(IEnumerable<DupeFileItem> files, string? targetDir = null)
        {
            var query = files.OrderByDescending(x => x.Width * x.Height)
                             .ThenByDescending(x => x.FileSize);
            
            if (!string.IsNullOrEmpty(targetDir))
                query = query.ThenByDescending(x => x.DirectoryPath.Equals(targetDir, StringComparison.OrdinalIgnoreCase) ? 1 : 0);

            var bestFile = query.First();
            var inferiorFiles = files.Where(x => x != bestFile).ToList();
            return (bestFile, inferiorFiles);
        }

        public static List<DirGroupItem> CalculateDirGroups(IEnumerable<DupeFileItem> dupeFiles, bool defaultSelectAll)
        {
            var pairs = new Dictionary<string, DirGroupItem>(StringComparer.OrdinalIgnoreCase);
            var filesByGroup = dupeFiles.GroupBy(x => x.GroupId).ToDictionary(g => g.Key, g => g.ToList());

            foreach (var kvp in filesByGroup)
            {
                var groupKey = kvp.Key;
                var files = kvp.Value;
                var dirs = files.Select(x => x.DirectoryPath).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
                
                if (dirs.Count == 1)
                {
                    string key = $"{dirs[0]}|{dirs[0]}";
                    if (!pairs.TryGetValue(key, out var dirGroup)) 
                    {
                        dirGroup = new DirGroupItem { DirA = dirs[0], DirB = dirs[0], IsTargetDirA = true, IsSelected = defaultSelectAll };
                        pairs[key] = dirGroup;
                    }
                    dirGroup.GroupIds.Add(groupKey);
                }
                else
                {
                    for (int i = 0; i < dirs.Count; i++)
                    {
                        for (int j = i + 1; j < dirs.Count; j++)
                        {
                            string key = $"{dirs[i]}|{dirs[j]}";
                            if (!pairs.TryGetValue(key, out var dirGroup)) 
                            {
                                int depthA = dirs[i].Count(c => c == Path.DirectorySeparatorChar || c == Path.AltDirectorySeparatorChar);
                                int depthB = dirs[j].Count(c => c == Path.DirectorySeparatorChar || c == Path.AltDirectorySeparatorChar);
                                
                                string dirA = dirs[i], dirB = dirs[j];
                                if (dirB.Length < dirA.Length) {
                                    dirA = dirs[j]; dirB = dirs[i];
                                    int temp = depthA; depthA = depthB; depthB = temp;
                                }

                                bool aIsBetterTarget = depthA > depthB || (depthA == depthB && dirA.Length >= dirB.Length);

                                dirGroup = new DirGroupItem { 
                                    DirA = dirA, DirB = dirB, IsTargetDirA = aIsBetterTarget, IsTargetDirB = !aIsBetterTarget, IsSelected = defaultSelectAll
                                };
                                pairs[key] = dirGroup;
                            }
                            dirGroup.GroupIds.Add(groupKey);
                        }
                    }
                }
            }

            var dirInvolvementCount = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in pairs.Values.Where(p => !p.IsSameDir))
            {
                if (dirInvolvementCount.ContainsKey(pair.DirA)) dirInvolvementCount[pair.DirA]++; else dirInvolvementCount[pair.DirA] = 1;
                if (dirInvolvementCount.ContainsKey(pair.DirB)) dirInvolvementCount[pair.DirB]++; else dirInvolvementCount[pair.DirB] = 1;
            }

            foreach (var pair in pairs.Values)
            {
                if (!pair.IsSameDir)
                {
                    int countA = dirInvolvementCount.TryGetValue(pair.DirA, out int ca) ? ca : 0;
                    int countB = dirInvolvementCount.TryGetValue(pair.DirB, out int cb) ? cb : 0;
                    
                    if (countB > countA || (countA == countB && string.Compare(pair.DirB, pair.DirA, StringComparison.OrdinalIgnoreCase) < 0))
                    {
                        string tempDir = pair.DirA; pair.DirA = pair.DirB; pair.DirB = tempDir;
                        bool tempTgt = pair.IsTargetDirA; pair.IsTargetDirA = pair.IsTargetDirB; pair.IsTargetDirB = tempTgt;
                        int tempCount = countA; countA = countB; countB = tempCount;
                    }
                    
                    pair.DirAInvolvementCount = countA;
                    pair.HasMultipleConflicts = countA > 1;

                    if (countA > countB)
                    {
                        pair.IsTargetDirB = true;
                        pair.IsTargetDirA = false;
                    }
                    else if (countB > countA)
                    {
                        pair.IsTargetDirA = true;
                        pair.IsTargetDirB = false;
                    }
                    else pair.EvaluateSmartDefaults();

                    if (pair.IsMultiGroup)
                    {
                        var overlappingFilesInA = new List<string>();
                        foreach (var gid in pair.GroupIds)
                        {
                            if (filesByGroup.TryGetValue(gid, out var groupedFiles))
                            {
                                foreach (var f in groupedFiles)
                                {
                                    if (string.Equals(f.DirectoryPath, pair.DirA, StringComparison.OrdinalIgnoreCase))
                                    {
                                        overlappingFilesInA.Add(Path.GetFileName(f.FilePath));
                                    }
                                }
                            }
                        }
                        
                        var pattern = AnalyzeGroupPattern(overlappingFilesInA);
                        pair.PatternType = pattern.Type;
                        pair.PatternPrefix = pattern.Prefix;
                        pair.PatternMin = pattern.MinNum;
                        pair.PatternMax = pattern.MaxNum;
                        
                        // 💡 套用設計：多群組衝突退回保守/特徵精準模式
                        pair.MoveDuplicatesOnly = true; 
                    }
                    else
                    {
                        // 💡 套用設計：一般跨目錄暴力全搬
                        pair.MoveAllFiles = true;
                    }
                }
                else
                {
                    pair.DirAInvolvementCount = 0;
                    pair.HasMultipleConflicts = false;
                }
            }

            var sortedList = pairs.Values
                .OrderByDescending(x => x.IsSameDir ? -1 : x.DirAInvolvementCount)
                .ThenBy(x => x.DirA)
                .ThenByDescending(x => x.DuplicateCount)
                .ThenBy(x => x.DirB)
                .ToList();

            int dirGroupId = 1;
            foreach (var dg in sortedList) dg.GroupId = dirGroupId++;

            var hashToDirGroups = new Dictionary<ulong, List<DirGroupItem>>();
            
            foreach (var g in sortedList)
            {
                foreach (var hashId in g.GroupIds)
                {
                    if (!hashToDirGroups.TryGetValue(hashId, out var list))
                    {
                        list = new List<DirGroupItem>();
                        hashToDirGroups[hashId] = list;
                    }
                    list.Add(g);
                }
            }

            foreach (var file in dupeFiles)
            {
                if (hashToDirGroups.TryGetValue(file.GroupId, out var groups))
                {
                    var matchingIds = new List<int>();
                    foreach (var g in groups)
                    {
                        if (string.Equals(file.DirectoryPath, g.DirA, StringComparison.OrdinalIgnoreCase) || 
                            string.Equals(file.DirectoryPath, g.DirB, StringComparison.OrdinalIgnoreCase))
                        {
                            matchingIds.Add(g.GroupId);
                        }
                    }

                    if (matchingIds.Count > 0)
                    {
                        matchingIds.Sort(); 
                        file.BigGroupIds = string.Join(", ", matchingIds);
                    }
                    else
                    {
                        file.BigGroupIds = "-";
                    }
                }
                else
                {
                    file.BigGroupIds = "-";
                }
            }

            return sortedList;
        }
    }
}