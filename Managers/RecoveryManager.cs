using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using FastImageDupe.Core.Helpers;

namespace FastImageDupe.Core
{
    public static class RecoveryManager
    {
        private static readonly string LogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "recovery_log.json");
        public static List<ActionLogItem> Logs { get; private set; } = new List<ActionLogItem>();
        private static bool _isLoaded = false;

        public static void Load()
        {
            if (_isLoaded) return;
            Logs = CommonHelper.Read<List<ActionLogItem>>(LogPath) ?? new List<ActionLogItem>();
            _isLoaded = true;
        }

        public static void Save()
        {
            CommonHelper.Write(LogPath, Logs);
        }

        public static void LogAction(string source, string dest, string smartId = "", string groupId = "", string personalId = "")
        {
            Load(); 
            Logs.Add(new ActionLogItem
            {
                SourcePath = source,
                DestPath = dest,
                SmartBatchId = smartId,
                GroupBatchId = groupId,
                PersonalBatchId = personalId,
                Timestamp = DateTime.Now
            });
            Save();
        }

        // 呼叫 Windows Shell API 將檔案移至回收桶
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct SHFILEOPSTRUCT
        {
            public IntPtr hwnd;
            public uint wFunc;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string pFrom;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string pTo;
            public ushort fFlags;
            public bool fAnyOperationsAborted;
            public IntPtr hNameMappings;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string lpszProgressTitle;
        }

        private const uint FO_DELETE = 0x0003;
        private const ushort FOF_ALLOWUNDO = 0x0040;
        private const ushort FOF_NOCONFIRMATION = 0x0010;

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHFileOperation(ref SHFILEOPSTRUCT lpFileOp);

        public static void SendToRecycleBin(string path)
        {
            var fileOp = new SHFILEOPSTRUCT
            {
                wFunc = FO_DELETE,
                pFrom = path + '\0' + '\0', // 必須以雙 null 結尾
                fFlags = FOF_ALLOWUNDO | FOF_NOCONFIRMATION
            };
            SHFileOperation(ref fileOp);
        }
    }
}