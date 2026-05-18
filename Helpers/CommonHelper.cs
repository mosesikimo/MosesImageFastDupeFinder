using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace FastImageDupe.Core.Helpers
{
    public static class CommonHelper
    {
        // 💡 1. 處理 JSON 讀寫 (原 JsonHelper)
        public static T? Read<T>(string path)
        {
            try { return File.Exists(path) ? JsonSerializer.Deserialize<T>(File.ReadAllText(path)) : default; }
            catch { return default; }
        }

        public static void Write<T>(string path, T data)
        {
            try { File.WriteAllText(path, JsonSerializer.Serialize(data)); } 
            catch { }
        }

        // 💡 2. 處理外部程式執行 (原 ProcessHelper)
        public static void SafeExecute(string fileName, string args = "")
        {
            try 
            {
                if (string.IsNullOrEmpty(args)) Process.Start(new ProcessStartInfo(fileName) { UseShellExecute = true });
                else Process.Start(fileName, args);
            } 
            catch (Exception ex) { MessageBox.Show($"執行失敗: {ex.Message}"); }
        }

        // 💡 3. 處理實體檔案隔離假刪除 (原 FileActionHelper)
        public static string? SafePseudoDeleteFile(string sourceFilePath, string smartId = "", string groupId = "", string personalId = "")
        {
            try 
            {
                string root = Path.GetPathRoot(sourceFilePath) ?? "C:\\";
                string destPath = PathHelper.GetIsolationDestPath(sourceFilePath);
                
                Directory.CreateDirectory(PathHelper.GetLongPath(Path.GetDirectoryName(destPath)!));
                destPath = PathHelper.GetUniqueFilePath(destPath); 
                
                File.Move(PathHelper.GetLongPath(sourceFilePath), PathHelper.GetLongPath(destPath), true);
                
                RecoveryManager.LogAction(sourceFilePath, destPath, smartId, groupId, personalId);

                return Path.Combine(root, "FastImageDupeDel");
            } 
            catch { return null; }
        }
    }
}