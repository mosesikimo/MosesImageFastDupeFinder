using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Windows;

namespace FastImageDupe.Core
{
    public class AppSettings 
    {
        public string TargetPaths { get; set; } = "";
        public string ExcludePaths { get; set; } = "";
        public bool IncludeSubDir { get; set; } = true;
        public bool IsEnglish { get; set; } = false;
        public bool IsPersonalAlbumMode { get; set; } = true;
    }

    public enum NamingPatternType
    {
        Sequential,     // 完全流水號
        Grouped,        // 有群組性命名 (前綴)
        Random          // 隨機命名
    }

    public class DirGroupItem : INotifyPropertyChanged
    {
        private bool _isSelected = true;
        public bool IsSelected
        {
            get => _isSelected;
            set { if (_isSelected != value) { _isSelected = value; OnPropertyChanged(nameof(IsSelected)); } }
        }

        public int GroupId { get; set; }
        public string DirA { get; set; } = "";
        public string DirB { get; set; } = "";
        
        public List<ulong> GroupIds { get; set; } = new List<ulong>();

        public int DuplicateCount => GroupIds.Count;
        public bool IsSameDir => DirA.Equals(DirB, StringComparison.OrdinalIgnoreCase);
        
        public Visibility VisSameDir => IsSameDir ? Visibility.Visible : Visibility.Collapsed;
        public Visibility VisCrossDir => !IsSameDir ? Visibility.Visible : Visibility.Collapsed;

        public bool IsMultiGroup => DirAInvolvementCount >= 4;
        public Visibility VisNormalAction => !IsMultiGroup && !IsSameDir ? Visibility.Visible : Visibility.Collapsed;
        public Visibility VisMultiGroupAction => IsMultiGroup && !IsSameDir ? Visibility.Visible : Visibility.Collapsed;

        public NamingPatternType PatternType { get; set; } = NamingPatternType.Random;
        public string PatternPrefix { get; set; } = "";
        public int PatternMin { get; set; }
        public int PatternMax { get; set; }

        public string MultiGroupActionText => PatternType switch {
            NamingPatternType.Sequential => I18nManager.Get("MultiGroupActionSequential"),
            NamingPatternType.Grouped => I18nManager.Get("MultiGroupActionGrouped"),
            _ => I18nManager.Get("MultiGroupActionRandom")
        };
        public string MultiGroupActionColor => PatternType == NamingPatternType.Random ? "#6B7280" : "#D946EF";

        private bool _isTargetDirA;
        public bool IsTargetDirA
        {
            get => _isTargetDirA;
            set { 
                if (_isTargetDirA != value) { 
                    _isTargetDirA = value; 
                    _isTargetDirB = !value; 
                    OnPropertyChanged(nameof(IsTargetDirA)); 
                    OnPropertyChanged(nameof(IsTargetDirB)); 
                } 
            }
        }

        private bool _isTargetDirB;
        public bool IsTargetDirB
        {
            get => _isTargetDirB;
            set { 
                if (_isTargetDirB != value) { 
                    _isTargetDirB = value; 
                    _isTargetDirA = !value; 
                    OnPropertyChanged(nameof(IsTargetDirB)); 
                    OnPropertyChanged(nameof(IsTargetDirA)); 
                } 
            }
        }

        private bool _moveDuplicatesOnly = true; 
        public bool MoveDuplicatesOnly
        {
            get => _moveDuplicatesOnly;
            set { 
                if (_moveDuplicatesOnly != value) { 
                    _moveDuplicatesOnly = value; 
                    if (value) { _moveAllFiles = false; _moveMediaOnly = false; }
                    OnPropertyChanged(nameof(MoveDuplicatesOnly)); 
                } 
            }
        }

        private bool _moveAllFiles = false;
        public bool MoveAllFiles
        {
            get => _moveAllFiles;
            set { 
                if (_moveAllFiles != value) { 
                    _moveAllFiles = value; 
                    if (value) { _moveDuplicatesOnly = false; _moveMediaOnly = false; }
                    OnPropertyChanged(nameof(MoveAllFiles)); 
                } 
            }
        }

        private bool _moveMediaOnly = false;
        public bool MoveMediaOnly
        {
            get => _moveMediaOnly;
            set { 
                if (_moveMediaOnly != value) { 
                    _moveMediaOnly = value; 
                    if (value) { _moveDuplicatesOnly = false; _moveAllFiles = false; }
                    OnPropertyChanged(nameof(MoveMediaOnly)); 
                } 
            }
        }

        private int _dirAInvolvementCount;
        public int DirAInvolvementCount
        {
            get => _dirAInvolvementCount;
            set { 
                if (_dirAInvolvementCount != value) { 
                    _dirAInvolvementCount = value; 
                    OnPropertyChanged(nameof(DirAInvolvementCount)); 
                    OnPropertyChanged(nameof(ConflictWarningLocalized)); 
                    OnPropertyChanged(nameof(IsMultiGroup)); 
                    OnPropertyChanged(nameof(VisNormalAction)); 
                    OnPropertyChanged(nameof(VisMultiGroupAction)); 
                    OnPropertyChanged(nameof(MoveModeDuplicatesOnlyText)); // 💡 動態更新文字
                } 
            }
        }

        private bool _hasMultipleConflicts;
        public bool HasMultipleConflicts
        {
            get => _hasMultipleConflicts;
            set { if (_hasMultipleConflicts != value) { _hasMultipleConflicts = value; OnPropertyChanged(nameof(HasMultipleConflicts)); OnPropertyChanged(nameof(VisMultipleConflicts)); OnPropertyChanged(nameof(ConflictWarningLocalized)); } }
        }

        public Visibility VisMultipleConflicts => HasMultipleConflicts ? Visibility.Visible : Visibility.Collapsed;

        public string ConflictWarningLocalized => HasMultipleConflicts ? string.Format(I18nManager.Get("MsgConflict"), DirAInvolvementCount) : "";
        
        // 💡 動態根據群組屬性切換第一選項的顯示文字
        public string MoveModeDuplicatesOnlyText => (IsMultiGroup && PatternType != NamingPatternType.Random) 
            ? I18nManager.Get("MoveModeDuplicatesOnly_Pattern") 
            : I18nManager.Get("MoveModeDuplicatesOnly_Normal");
            
        public string MoveModeAllText => I18nManager.Get("MoveModeAll");
        public string MoveModeMediaText => I18nManager.Get("MoveModeMedia");
        
        public string BtnPreviewPlanText => I18nManager.Get("BtnPreviewPlan");
        public string BtnMergeSelectedText => I18nManager.Get("BtnMergeSelected");
        public string BtnMultiGroupPreviewText => I18nManager.Get("BtnMultiGroupPreview");
        public string BtnCleanSameDirText => I18nManager.Get("BtnCleanSameDir");

        public void RefreshI18n()
        {
            OnPropertyChanged(nameof(ConflictWarningLocalized));
            OnPropertyChanged(nameof(MoveModeDuplicatesOnlyText));
            OnPropertyChanged(nameof(MoveModeAllText));
            OnPropertyChanged(nameof(MoveModeMediaText));
            
            OnPropertyChanged(nameof(BtnPreviewPlanText));
            OnPropertyChanged(nameof(BtnMergeSelectedText));
            OnPropertyChanged(nameof(BtnMultiGroupPreviewText));
            OnPropertyChanged(nameof(BtnCleanSameDirText));
            OnPropertyChanged(nameof(MultiGroupActionText));
        }

        public void EvaluateSmartDefaults()
        {
            if (IsSameDir) return;
            if (DirA.Length <= DirB.Length) IsTargetDirA = true;
            else IsTargetDirB = true;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public class DupeFileItem : INotifyPropertyChanged
    {
        private bool _isSelected;
        public bool IsSelected 
        { 
            get => _isSelected; 
            set { if (_isSelected != value) { _isSelected = value; OnPropertyChanged(nameof(IsSelected)); } }
        }

        private bool _isPreviewA;
        [System.Text.Json.Serialization.JsonIgnore]
        public bool IsPreviewA 
        { 
            get => _isPreviewA; 
            set { if (_isPreviewA != value) { _isPreviewA = value; OnPropertyChanged(nameof(IsPreviewA)); } } 
        }

        private bool _isPreviewB;
        [System.Text.Json.Serialization.JsonIgnore]
        public bool IsPreviewB 
        { 
            get => _isPreviewB; 
            set { if (_isPreviewB != value) { _isPreviewB = value; OnPropertyChanged(nameof(IsPreviewB)); } } 
        }

        private int _displayGroupId;
        public int DisplayGroupId 
        { 
            get => _displayGroupId; 
            set { if (_displayGroupId != value) { _displayGroupId = value; OnPropertyChanged(nameof(DisplayGroupId)); } }
        }

        private string _bigGroupIds = "";
        public string BigGroupIds 
        { 
            get => _bigGroupIds; 
            set { if (_bigGroupIds != value) { _bigGroupIds = value; OnPropertyChanged(nameof(BigGroupIds)); } }
        }

        private string _filePath = "";
        public string FilePath 
        { 
            get => _filePath; 
            set 
            { 
                if (_filePath != value) 
                { 
                    _filePath = value; 
                    OnPropertyChanged(nameof(FilePath));
                    OnPropertyChanged(nameof(DirectoryPath));
                    OnPropertyChanged(nameof(FileName));
                } 
            } 
        }

        public ulong GroupId { get; set; }
        
        public string DirectoryPath => Path.GetDirectoryName(FilePath) ?? "";
        public string FileName => Path.GetFileName(FilePath);
        
        public int Width { get; set; }
        public int Height { get; set; }
        public string Resolution => (Width > 0 && Height > 0) ? $"{Width} x {Height}" : "未知";

        public long FileSize { get; set; }
        public long FileTicks { get; set; } 
        public string SizeStr => $"{FileSize / 1024.0 / 1024.0:F2} MB";

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public class ScanResult
    {
        public List<DupeFileItem> Duplicates { get; set; } = new List<DupeFileItem>();
        public int TotalFound { get; set; }
        public int SuccessCount { get; set; }
        public int FailCount { get; set; }
        public int CacheHitCount { get; set; }
        public bool IsCanceled { get; set; }
        public int DuplicateGroupsCount { get; set; }
    }

    public class ActionLogItem : INotifyPropertyChanged
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string SmartBatchId { get; set; } = "";
        public string GroupBatchId { get; set; } = "";
        public string PersonalBatchId { get; set; } = ""; 
        public string SourcePath { get; set; } = "";
        public string DestPath { get; set; } = "";
        public bool IsHidden { get; set; } = false;
        public DateTime Timestamp { get; set; } = DateTime.Now;

        [System.Text.Json.Serialization.JsonIgnore]
        public string ActionType => !string.IsNullOrEmpty(SmartBatchId) ? I18nManager.Get("Rec_TypeSmart") :
                                    !string.IsNullOrEmpty(GroupBatchId) ? I18nManager.Get("Rec_TypeGroup") : 
                                    !string.IsNullOrEmpty(PersonalBatchId) ? I18nManager.Get("Rec_TypePersonal") : I18nManager.Get("Rec_TypeSingle");

        [System.Text.Json.Serialization.JsonIgnore]
        public string MissingFileText => I18nManager.Get("Rec_TxtMissing");
        
        [System.Text.Json.Serialization.JsonIgnore]
        public string PreviewTooltip => I18nManager.Get("Rec_TipPreview");

        [System.Text.Json.Serialization.JsonIgnore]
        public string DisplaySmartId { get; set; } = "-";
        
        [System.Text.Json.Serialization.JsonIgnore]
        public string DisplayGroupId { get; set; } = "-";
        
        [System.Text.Json.Serialization.JsonIgnore]
        public string DisplayPersonalId { get; set; } = "-";

        [System.Text.Json.Serialization.JsonIgnore]
        public bool IsFileMissing => !File.Exists(DestPath);

        private bool _isSelected;
        [System.Text.Json.Serialization.JsonIgnore]
        public bool IsSelected 
        { 
            get => _isSelected; 
            set { if (_isSelected != value) { _isSelected = value; OnPropertyChanged(nameof(IsSelected)); } } 
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}