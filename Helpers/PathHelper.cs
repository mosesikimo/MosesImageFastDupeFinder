using System;
using System.IO;

namespace FastImageDupe.Core.Helpers
{
    public static class PathHelper
    {
        public static string GetLongPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return path;
            if (path.StartsWith(@"\\?\")) return path;
            string fullPath = Path.GetFullPath(path);
            if (fullPath.StartsWith(@"\\")) return @"\\?\UNC\" + fullPath.Substring(2);
            return @"\\?\" + fullPath;
        }

        public static string StripLongPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return path;
            if (path.StartsWith(@"\\?\UNC\")) return @"\\" + path.Substring(8);
            if (path.StartsWith(@"\\?\")) return path.Substring(4);
            return path;
        }

        public static string GetUniqueFilePath(string destPath, string prefix = "")
        {
            if (!File.Exists(GetLongPath(destPath))) return destPath;
            string ext = Path.GetExtension(destPath);
            string name = Path.GetFileNameWithoutExtension(destPath);
            string directory = Path.GetDirectoryName(destPath) ?? string.Empty;
            return Path.Combine(directory, $"{name}_{prefix}{Guid.NewGuid().ToString().Substring(0, 4)}{ext}");
        }

        public static string GetIsolationDestPath(string sourceFilePath)
        {
            string root = Path.GetPathRoot(sourceFilePath) ?? "C:\\";
            string relPath = sourceFilePath.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return Path.Combine(root, "FastImageDupeDel", relPath);
        }

        public static string NormalizePath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return string.Empty;
            path = path.Trim();
            if (path.Length > 3 && (path.EndsWith(Path.DirectorySeparatorChar.ToString()) || path.EndsWith(Path.AltDirectorySeparatorChar.ToString())))
            {
                path = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            return path;
        }
    }
}