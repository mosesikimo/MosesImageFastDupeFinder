using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FastImageDupe.Core;
using FastImageDupe.Core.Helpers;
using FastImageDupe.Core.Engines;

namespace FastImageDupe
{
    public partial class MainWindow : Window
    {
        private readonly string _configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
        private readonly string _cachePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "image_fingerprint_cache.json");
        private readonly string _preciseCachePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "image_precise_cache.json");
        
        public ObservableCollection<DupeFileItem> DupeFiles { get; set; } = new ObservableCollection<DupeFileItem>();
        public ObservableCollection<DirGroupItem> DirGroups { get; set; } = new ObservableCollection<DirGroupItem>();

        private readonly string[] _supportedExts = { ".jpg", ".jpeg", ".png", ".bmp", ".webp", ".jfif", ".gif" };
        private readonly HashSet<string> _allMediaExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase) 
        { 
            ".jpg", ".jpeg", ".png", ".bmp", ".webp", ".jfif", ".gif", ".tiff", ".tif", ".heic", ".raw", ".cr2", ".nef", ".ico",
            ".mp4", ".avi", ".mkv", ".mov", ".wmv", ".flv", ".webm", ".m4v", ".ts", ".rmvb", ".mpg", ".swf", ".rm"
        };
        
        private ConcurrentDictionary<string, ulong> _featureCache = new ConcurrentDictionary<string, ulong>();
        private ConcurrentDictionary<string, PreciseSignature> _preciseFeatureCache = new ConcurrentDictionary<string, PreciseSignature>();
        
        private CancellationTokenSource? _cts; 
        private bool _isScanning = false;
        private bool _isSimpleMode = false;

        private List<ExcludedNode> _lastExcludedNodes = new List<ExcludedNode>();
        private string _baseStatusText = "";
        private DirGroupItem? _lastClickedGroupItem;

        public MainWindow()
        {
            InitializeComponent();
            FileListView.ItemsSource = DupeFiles;
            
            DirGroupListView.AddHandler(System.Windows.Controls.Primitives.ToggleButton.CheckedEvent, new RoutedEventHandler(OnDirGroupRadioButtonChecked));
            DirGroupListView.AddHandler(System.Windows.Controls.Primitives.ToggleButton.ClickEvent, new RoutedEventHandler(OnDirGroupRadioButtonChecked));

            if (!LoadSettings()) SetupDefaultSearchPath();
            UpdateLanguageUI(); 
            
            LoadFeatureCache(); 
            RecoveryManager.Load();
            
            this.Closing += (s, e) => { 
                SaveSettings(); 
                SaveFeatureCache(); 
                
                _featureCache.Clear();
                _preciseFeatureCache.Clear();
                DupeFiles.Clear();
                DirGroups.Clear();
                ForceGarbageCollection(); 
            };
            
            var menu = new ContextMenu();
            var menuViewReason = new MenuItem { Header = "📊 檢視詳細排除原因 (View Exclusion Grid)" };
            menuViewReason.Click += (s, e) => ShowExclusionReportWindow();
            menu.Items.Add(menuViewReason);
            ExcludePathTextBox.ContextMenu = menu;
            ExcludePathTextBox.ToolTip = "點擊右鍵可查看詳細的排除原因 Grid 清單";
        }

        #region 共用記憶體與 UI 全域防護鎖

        private void SetUiInteractiveState(bool isEnabled, bool isScanning = false)
        {
            Application.Current.Dispatcher.Invoke(() => {
                GrpScanSettings.IsEnabled = isEnabled;
                
                RbPersonalAlbum.IsEnabled = isEnabled;
                RbInternetMode.IsEnabled = isEnabled;
                BtnLang.IsEnabled = isEnabled;
                BtnReadMe.IsEnabled = isEnabled;
                BtnToggleMode.IsEnabled = isEnabled;
                BtnRecovery.IsEnabled = isEnabled;
                BtnSmartSelect.IsEnabled = isEnabled;
                
                GrpDirGroupSettings.IsEnabled = isEnabled;
                FileListView.IsEnabled = isEnabled;
                BtnDeleteChecked.IsEnabled = isEnabled;
                BtnManualDelete.IsEnabled = isEnabled;

                if (isScanning) BtnScan.IsEnabled = true;
                else BtnScan.IsEnabled = isEnabled;
            });
        }

        private void ForceGarbageCollection()
        {
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true, true);
            GC.WaitForPendingFinalizers();
        }

        private List<string> GetPathList(TextBox tb) => 
            tb.Text.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()).ToList();

        private void PostActionUIRefresh(string? selectedDirA, string? selectedDirB, bool defaultSelectAll = false)
        {
            GroupAnalysisEngine.CleanOrphanedItems(DupeFiles);
            GroupAnalysisEngine.RefreshFileGroupIds(DupeFiles);

            var sortedGroups = GroupAnalysisEngine.CalculateDirGroups(DupeFiles, defaultSelectAll);
            DirGroups = new ObservableCollection<DirGroupItem>(sortedGroups);
            DirGroupListView.ItemsSource = DirGroups;

            if (ChkSelectAllGroups != null) ChkSelectAllGroups.IsChecked = defaultSelectAll;

            if (selectedDirA != null && selectedDirB != null)
            {
                var target = DirGroups.FirstOrDefault(g => g.DirA == selectedDirA && g.DirB == selectedDirB);
                if (target != null) DirGroupListView.SelectedItem = target;
            }

            if (!_isSimpleMode)
            {
                if (DirGroupListView.SelectedItem is DirGroupItem dg)
                {
                    CollectionViewSource.GetDefaultView(DupeFiles).Filter = item => item is DupeFileItem file && dg.GroupIds.Contains(file.GroupId) &&
                                          (file.DirectoryPath == dg.DirA || file.DirectoryPath == dg.DirB);
                }
                else CollectionViewSource.GetDefaultView(DupeFiles).Filter = null;
            }
            else CollectionViewSource.GetDefaultView(DupeFiles).Filter = null;

            UpdateStatusTextWithGroup();
        }

        private void AddExcludeDirIfNeeded(string? excludeDir)
        {
            if (string.IsNullOrEmpty(excludeDir)) return;

            Application.Current.Dispatcher.Invoke(() => {
                var freshExcludes = GetPathList(ExcludePathTextBox);
                if (!freshExcludes.Contains(excludeDir, StringComparer.OrdinalIgnoreCase))
                {
                    freshExcludes.Add(excludeDir);
                    ExcludePathTextBox.Text = string.Join("; ", freshExcludes);
                    SaveSettings();
                }
            });
        }

        private async Task AutoRescanAsync()
        {
            if (_isScanning) return;
            ReportProgress(I18nManager.Get("StatusAutoRescan"));
            
            bool isPersonalAlbumMode = false;
            await Application.Current.Dispatcher.InvokeAsync(() => {
                isPersonalAlbumMode = RbPersonalAlbum.IsChecked == true;
            });
            await ExecuteScanCoreAsync(isPersonalAlbumMode);
        }

        #endregion

        #region 大群組 CheckBox 範圍勾選 (Shift-click)
        private void DirGroupCheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox cb && cb.DataContext is DirGroupItem currentItem)
            {
                bool isChecked = cb.IsChecked == true;

                if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
                {
                    if (_lastClickedGroupItem != null && DirGroups != null)
                    {
                        var list = DirGroups.ToList();
                        int index1 = list.IndexOf(_lastClickedGroupItem);
                        int index2 = list.IndexOf(currentItem);

                        if (index1 != -1 && index2 != -1)
                        {
                            int start = Math.Min(index1, index2);
                            int end = Math.Max(index1, index2);
                            for (int i = start; i <= end; i++) list[i].IsSelected = isChecked;
                        }
                    }
                }
                _lastClickedGroupItem = currentItem;
            }
        }
        #endregion

        #region 切換 UI 模式 (輕度/專業)
        private void BtnToggleMode_Click(object sender, RoutedEventArgs e)
        {
            _isSimpleMode = !_isSimpleMode;
            if (_isSimpleMode)
            {
                GrpDirGroupSettings.Visibility = Visibility.Collapsed;
                BtnSmartSelect.Visibility = Visibility.Collapsed;
                RowDirGroup.Height = GridLength.Auto;
                MainSplitter.Visibility = Visibility.Collapsed;
                RowSplitter.Height = new GridLength(0);
                CollectionViewSource.GetDefaultView(DupeFiles).Filter = null;
            }
            else
            {
                GrpDirGroupSettings.Visibility = Visibility.Visible;
                BtnSmartSelect.Visibility = Visibility.Visible;
                RowDirGroup.Height = new GridLength(4, GridUnitType.Star);
                RowFileList.Height = new GridLength(7, GridUnitType.Star);
                MainSplitter.Visibility = Visibility.Visible;
                RowSplitter.Height = new GridLength(5);
                
                if (DirGroupListView.SelectedItem is DirGroupItem dg)
                    CollectionViewSource.GetDefaultView(DupeFiles).Filter = item => item is DupeFileItem file && dg.GroupIds.Contains(file.GroupId) && (file.DirectoryPath == dg.DirA || file.DirectoryPath == dg.DirB);
            }
            
            BtnToggleMode.Content = I18nManager.Get(_isSimpleMode ? "BtnToggleToPro" : "BtnToggleToSimple");
            UpdateStatusTextWithGroup();
        }

        private void OnDirGroupRadioButtonChecked(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is FrameworkElement fe && fe.DataContext is DirGroupItem item) DirGroupListView.SelectedItem = item;
        }
        #endregion

        #region 多國語系與內建說明檔
        private void BtnLang_Click(object sender, RoutedEventArgs e)
        {
            I18nManager.IsEnglish = !I18nManager.IsEnglish;
            UpdateLanguageUI();
            SaveSettings(); 
        }

        private void UpdateLanguageUI()
        {
            I18nManager.TranslateWindow(this);
            if (_isScanning) BtnScan.Content = I18nManager.Get("BtnCancel");
            BtnToggleMode.Content = I18nManager.Get(_isSimpleMode ? "BtnToggleToPro" : "BtnToggleToSimple");

            if (ReadMeOverlay.Visibility == Visibility.Visible) ShowReadMeContent();
            if (DirGroups != null) foreach (var group in DirGroups) group.RefreshI18n();

            UpdatePreviewPanel(true, _previewItemA);
            UpdatePreviewPanel(false, _previewItemB);

            if (string.IsNullOrEmpty(_baseStatusText)) StatusText.Text = I18nManager.Get("StatusReady");
        }

        private void BtnReadMe_Click(object sender, RoutedEventArgs e) { ShowReadMeContent(); ReadMeOverlay.Visibility = Visibility.Visible; }
        private void ShowReadMeContent() => TxtReadMeContent.Text = I18nManager.Get("ReadMeContent");
        private void BtnCloseReadMe_Click(object sender, RoutedEventArgs e) => ReadMeOverlay.Visibility = Visibility.Collapsed;
        #endregion

        #region 設定與快取存取
        private void SaveSettings() => CommonHelper.Write(_configPath, new AppSettings { 
            TargetPaths = PathTextBox.Text, 
            ExcludePaths = ExcludePathTextBox.Text, 
            IncludeSubDir = ChkSubDir.IsChecked ?? true, 
            IsEnglish = I18nManager.IsEnglish,
            IsPersonalAlbumMode = RbPersonalAlbum.IsChecked == true 
        });

        private bool LoadSettings()
        {
            var s = CommonHelper.Read<AppSettings>(_configPath);
            if (s == null) return false;
            PathTextBox.Text = s.TargetPaths; 
            ExcludePathTextBox.Text = s.ExcludePaths;
            ChkSubDir.IsChecked = s.IncludeSubDir; 
            I18nManager.IsEnglish = s.IsEnglish; 
            if (s.IsPersonalAlbumMode) RbPersonalAlbum.IsChecked = true;
            else RbInternetMode.IsChecked = true;
            return true;
        }

        private void LoadFeatureCache()
        {
            var dict = CommonHelper.Read<Dictionary<string, ulong>>(_cachePath);
            if (dict != null) _featureCache = new ConcurrentDictionary<string, ulong>(dict);

            var preciseDict = CommonHelper.Read<Dictionary<string, PreciseSignature>>(_preciseCachePath);
            if (preciseDict != null) _preciseFeatureCache = new ConcurrentDictionary<string, PreciseSignature>(preciseDict);
        }

        private void SaveFeatureCache() 
        {
            CommonHelper.Write(_cachePath, _featureCache.ToDictionary(k => k.Key, v => v.Value));
            CommonHelper.Write(_preciseCachePath, _preciseFeatureCache.ToDictionary(k => k.Key, v => v.Value));
        }
        #endregion

        #region UI 路徑事件與即時過濾
        private void SetupDefaultSearchPath()
        {
            var paths = new[] { "D:\\", "E:\\", "F:\\" }.Where(Directory.Exists).ToList();
            PathTextBox.Text = paths.Count > 0 ? string.Join("; ", paths) : "C:\\";
        }

        private async Task UpdateDirectoryFilterUIAsync()
        {
            var targets = GetPathList(PathTextBox).Where(Directory.Exists).ToArray();
            var excludes = GetPathList(ExcludePathTextBox).ToArray();
            if (targets.Length == 0) return;

            SetUiInteractiveState(false);
            try {
                StatusText.Text = "🔍 正在背景自動整理排除清單...";
                var filterResult = await Task.Run(() => DirectoryFilter.Process(targets, excludes));
                
                _lastExcludedNodes = filterResult.SimplifiedExcluded;
                ExcludePathTextBox.Text = string.Join("; ", filterResult.SimplifiedExcluded.Select(x => x.Path));
                SaveSettings();
                
                StatusText.Text = "✅ 排除清單已自動更新與簡化";
                await Task.Delay(2000);
                if (StatusText.Text == "✅ 排除清單已自動更新與簡化") UpdateStatusTextWithGroup();
            }
            finally {
                SetUiInteractiveState(true);
            }
        }

        private void UpdateStatusTextWithGroup()
        {
            if (DirGroupListView?.SelectedItem is DirGroupItem dg && !_isSimpleMode)
            {
                string grpIdText = I18nManager.IsEnglish ? $" | Current Group ID: {dg.GroupId}" : $" | 目前大群組 ID: {dg.GroupId}";
                StatusText.Text = string.IsNullOrEmpty(_baseStatusText) ? grpIdText.TrimStart(' ', '|') : _baseStatusText + grpIdText;
            }
            else StatusText.Text = string.IsNullOrEmpty(_baseStatusText) ? I18nManager.Get("StatusReady") : _baseStatusText;
        }

        private void ReportProgress(string msg) => Application.Current.Dispatcher.InvokeAsync(() => { StatusText.Text = msg; });

        private async Task HandleFolderBrowseAsync(TextBox targetTextBox)
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog { Multiselect = true };
            if (dialog.ShowDialog() == true) {
                var current = GetPathList(targetTextBox);
                foreach (var f in dialog.FolderNames) if (!current.Contains(f, StringComparer.OrdinalIgnoreCase)) current.Add(f);
                targetTextBox.Text = string.Join("; ", current);
                SaveSettings();
                await UpdateDirectoryFilterUIAsync();
            }
        }

        private async void BrowseFolder_Click(object sender, RoutedEventArgs e) => await HandleFolderBrowseAsync(PathTextBox);
        private async void BrowseExcludeFolder_Click(object sender, RoutedEventArgs e) => await HandleFolderBrowseAsync(ExcludePathTextBox);
        private void ClearPath_Click(object sender, RoutedEventArgs e) { PathTextBox.Text = string.Empty; SaveSettings(); }
        private async void ClearExcludePath_Click(object sender, RoutedEventArgs e) { ExcludePathTextBox.Text = string.Empty; SaveSettings(); await UpdateDirectoryFilterUIAsync(); }
        #endregion

        #region 核心掃描
        private async void BtnScan_Click(object sender, RoutedEventArgs e)
        {
            if (_isScanning)
            {
                if (_cts != null && !_cts.IsCancellationRequested)
                {
                    BtnScan.IsEnabled = false; 
                    StatusText.Text = "🛑 正在發送中斷訊號，請稍候...";
                    _cts.Cancel();
                }
                return;
            }
            
            bool isPersonalAlbumMode = RbPersonalAlbum.IsChecked == true;
            string modeName = isPersonalAlbumMode ? I18nManager.Get("RbPersonalAlbum") : I18nManager.Get("RbInternetMode");
            
            if (MessageBox.Show(string.Format(I18nManager.Get("MsgScanModeConfirm"), modeName), 
                                I18nManager.Get("TitleScanModeConfirm"), 
                                MessageBoxButton.YesNo, 
                                MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                return;
            }

            await ExecuteScanCoreAsync(isPersonalAlbumMode);
        }

        private async Task ExecuteScanCoreAsync(bool isPersonalAlbumMode)
        {
            SaveSettings();
            var targets = GetPathList(PathTextBox).Where(Directory.Exists).ToArray();
            var excludes = GetPathList(ExcludePathTextBox).ToArray();
            if (targets.Length == 0) return;

            bool subDir = ChkSubDir.IsChecked == true;
            
            SetUiInteractiveState(false, true); 
            _isScanning = true;
            DupeFiles.Clear();
            DirGroups.Clear();
            ForceGarbageCollection();
            
            Application.Current.Dispatcher.Invoke(() => {
                BtnScan.Content = I18nManager.Get("BtnCancel");
                BtnScan.Background = (SolidColorBrush)new BrushConverter().ConvertFrom("#EF4444")!; 
            });

            _cts = new CancellationTokenSource();
            Stopwatch sw = Stopwatch.StartNew();
            ScanResult? result = null;

            try
            {
                IProgress<string> progress = new Progress<string>(msg => { StatusText.Text = msg; });
                progress.Report("🔍 正在執行智慧過濾，排除非媒體目錄...");
                var filterResult = await Task.Run(() => DirectoryFilter.Process(targets, excludes));
                
                _lastExcludedNodes = filterResult.SimplifiedExcluded;
                var actualTargets = filterResult.RootsToScan.ToArray();
                var actualExcludes = filterResult.SimplifiedExcluded.Select(x => x.Path).ToArray();

                Application.Current.Dispatcher.Invoke(() => {
                    ExcludePathTextBox.Text = string.Join("; ", actualExcludes);
                    SaveSettings();
                });

                if (actualTargets.Length == 0)
                {
                    Application.Current.Dispatcher.Invoke(() => {
                        MessageBox.Show("智慧過濾完成：所有目標目錄皆被判定為非媒體目錄或已手動排除，無須進行掃描。", I18nManager.Get("MsgPrompt"), MessageBoxButton.OK, MessageBoxImage.Information);
                        StatusText.Text = "✅ 掃描已取消 (無可用目標目錄)";
                        _baseStatusText = StatusText.Text;
                    });
                    return;
                }
                
                result = await ScanEngine.RunScanAsync(actualTargets, actualExcludes, subDir, _supportedExts, _featureCache, _preciseFeatureCache, isPersonalAlbumMode, progress, _cts.Token);
                
                if (!result.IsCanceled && result.Duplicates.Count > 0)
                {
                    progress.Report(I18nManager.IsEnglish ? "🔍 Precise secondary validation..." : "🔍 正在進行二次精確校驗 (並行快取模式)...");
                    await PreciseHashEngine.ProcessCollisionsAsync(result, _preciseFeatureCache, isPersonalAlbumMode, progress, _cts.Token);
                }
                
                sw.Stop();
                SaveFeatureCache();

                Application.Current.Dispatcher.Invoke(() =>
                {
                    FileListView.ItemsSource = null;
                    DupeFiles = new ObservableCollection<DupeFileItem>(result.Duplicates);
                    PostActionUIRefresh(null, null, true);

                    FileListView.ItemsSource = DupeFiles;
                    if (DirGroups.Count > 0) DirGroupListView.SelectedIndex = 0;
                });
            }
            finally
            {
                _isScanning = false;
                SetUiInteractiveState(true);

                Application.Current.Dispatcher.Invoke(() => {
                    BtnScan.Content = I18nManager.Get("BtnScan");
                    BtnScan.Background = (SolidColorBrush)new BrushConverter().ConvertFrom("#FFFFEBF2")!;

                    if (result != null)
                    {
                        string timeStr = sw.Elapsed.TotalSeconds.ToString("F1");
                        string titleMsg = result.IsCanceled ? I18nManager.Get("MsgCanceled") : I18nManager.Get("MsgScanFinished");
                        string timeMsg = I18nManager.IsEnglish ? $" ({timeStr}s)" : $" (耗時: {timeStr}秒)";
                        _baseStatusText = (result.IsCanceled ? "⚠️ " : "✅ ") + titleMsg + timeMsg;
                    }
                    UpdateStatusTextWithGroup();
                });

                ForceGarbageCollection();
            }
        }
        #endregion

        #region 大群組一對多歸併邏輯 (Multi-Group Merge)
        
        private void BtnMultiGroupPreview_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.DataContext is DirGroupItem dg)
            {
                var previewList = new List<PreviewActionItem>();
                string targetDir = dg.IsTargetDirA ? dg.DirA : dg.DirB;
                string sourceDir = dg.IsTargetDirA ? dg.DirB : dg.DirA;

                bool moveRemaining = !dg.MoveDuplicatesOnly && dg.MoveAllFiles; 
                bool moveMediaOnly = !dg.MoveDuplicatesOnly && dg.MoveMediaOnly;

                foreach (var gid in dg.GroupIds)
                {
                    var filesInGroup = DupeFiles.Where(x => x.GroupId == gid && (x.DirectoryPath.Equals(targetDir, StringComparison.OrdinalIgnoreCase) || x.DirectoryPath.Equals(sourceDir, StringComparison.OrdinalIgnoreCase))).ToList();
                    if (filesInGroup.Count < 2) continue;

                    var (bestFile, inferiorFiles) = GroupAnalysisEngine.GetBestAndInferiorFiles(filesInGroup, targetDir);

                    foreach (var inf in inferiorFiles)
                    {
                        string destPath = PathHelper.GetIsolationDestPath(inf.FilePath);
                        previewList.Add(new PreviewActionItem { ActionType = "🗑️ 隔離排除", SourcePath = inf.FilePath, DestPath = destPath });
                    }

                    if (!bestFile.DirectoryPath.Equals(targetDir, StringComparison.OrdinalIgnoreCase))
                    {
                        string newPath = PathHelper.GetUniqueFilePath(Path.Combine(targetDir, bestFile.FileName), "M");
                        previewList.Add(new PreviewActionItem { ActionType = "📂 搬移正本", SourcePath = bestFile.FilePath, DestPath = newPath });
                    }
                }

                if (dg.PatternType != NamingPatternType.Random)
                {
                    try
                    {
                        if (Directory.Exists(PathHelper.GetLongPath(sourceDir)))
                        {
                            var handledFiles = new HashSet<string>(previewList.Select(x => x.SourcePath), StringComparer.OrdinalIgnoreCase);
                            var allFilesInSource = Directory.GetFiles(PathHelper.GetLongPath(sourceDir));
                            var regex = new System.Text.RegularExpressions.Regex(@"^(?<prefix>.*\D)?(?<number>\d+)(?<suffix>\D*)$");

                            foreach (var fileLong in allFilesInSource)
                            {
                                string file = PathHelper.StripLongPath(fileLong);
                                if (handledFiles.Contains(file)) continue;

                                string name = Path.GetFileName(file);
                                string nameWithoutExt = Path.GetFileNameWithoutExtension(name);

                                bool isMatch = false;

                                if (dg.PatternType == NamingPatternType.Sequential)
                                {
                                    var match = regex.Match(nameWithoutExt);
                                    if (match.Success && match.Groups["prefix"].Value.Equals(dg.PatternPrefix, StringComparison.OrdinalIgnoreCase))
                                    {
                                        if (int.TryParse(match.Groups["number"].Value, out int num))
                                        {
                                            if (num >= dg.PatternMin && num <= dg.PatternMax) isMatch = true;
                                        }
                                    }
                                }
                                else if (dg.PatternType == NamingPatternType.Grouped)
                                {
                                    if (nameWithoutExt.StartsWith(dg.PatternPrefix, StringComparison.OrdinalIgnoreCase)) isMatch = true;
                                }

                                if (isMatch)
                                {
                                    string destPath = PathHelper.GetUniqueFilePath(Path.Combine(targetDir, name));
                                    previewList.Add(new PreviewActionItem { ActionType = I18nManager.Get("ActionMultiGroupMerge"), SourcePath = file, DestPath = destPath });
                                }
                            }
                        }
                    }
                    catch { } 
                }

                if (moveRemaining && Directory.Exists(PathHelper.GetLongPath(sourceDir)))
                {
                    try {
                        var handledFiles = new HashSet<string>(previewList.Select(x => x.SourcePath), StringComparer.OrdinalIgnoreCase);
                        var allRemaining = Directory.GetFiles(PathHelper.GetLongPath(sourceDir), "*.*", SearchOption.AllDirectories);
                        
                        foreach (var fileLong in allRemaining)
                        {
                            string file = PathHelper.StripLongPath(fileLong);
                            if (handledFiles.Contains(file)) continue; 
                            if (moveMediaOnly && !_allMediaExts.Contains(Path.GetExtension(file))) continue;
                            
                            string relPath = file.Substring(sourceDir.Length).TrimStart('\\', '/');
                            string destPath = PathHelper.GetUniqueFilePath(Path.Combine(targetDir, relPath));
                            previewList.Add(new PreviewActionItem { ActionType = "📦 搬移剩餘", SourcePath = file, DestPath = destPath });
                        }
                    } catch { }
                }

                if (previewList.Count == 0) { MessageBox.Show(I18nManager.Get("MsgNoPreviewAction"), I18nManager.Get("MsgPrompt"), MessageBoxButton.OK, MessageBoxImage.Information); return; }

                var window = new Window { Title = string.Format(I18nManager.Get("TitleMultiGroupPreview"), previewList.Count), Width = 1000, Height = 600, WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = this, Background = Brushes.WhiteSmoke, WindowState = WindowState.Maximized };
                var grid = new DataGrid { ItemsSource = previewList, AutoGenerateColumns = false, IsReadOnly = true, Margin = new Thickness(10), RowHeight = 60, AlternatingRowBackground = Brushes.AliceBlue, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto };

                var previewFactory = new FrameworkElementFactory(typeof(Image));
                var binding = new Binding("SourcePath") { Converter = new PathToImageConverter(), IsAsync = true };
                previewFactory.SetBinding(Image.SourceProperty, binding);
                previewFactory.SetValue(Image.MaxHeightProperty, 50.0);
                previewFactory.SetValue(Image.MaxWidthProperty, 50.0);
                previewFactory.SetValue(Image.StretchProperty, Stretch.Uniform);
                var cellTemplate = new DataTemplate { VisualTree = previewFactory };

                grid.Columns.Add(new DataGridTemplateColumn { Header = "預覽", CellTemplate = cellTemplate, Width = new DataGridLength(70) });
                grid.Columns.Add(new DataGridTextColumn { Header = "操作", Binding = new Binding("ActionType"), Width = new DataGridLength(120) });
                grid.Columns.Add(new DataGridTextColumn { Header = "原始路徑 (Source)", Binding = new Binding("SourcePath"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
                grid.Columns.Add(new DataGridTextColumn { Header = "目標路徑 (Destination)", Binding = new Binding("DestPath"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });

                window.Content = grid;
                window.ShowDialog();
            }
        }

        private async void BtnMultiGroupProcess_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.DataContext is DirGroupItem dg)
            {
                string tDir = dg.IsTargetDirA ? dg.DirA : dg.DirB;
                string sDir = dg.IsTargetDirA ? dg.DirB : dg.DirA;

                if (dg.PatternType == NamingPatternType.Random)
                {
                    if (MessageBox.Show(I18nManager.IsEnglish ? "No matching pattern found. The system will process duplicate files and respect your move options. Proceed?" : "找不到相鄰特徵，系統將為您處理群組內的「重複檔案」並執行您勾選的搬移模式。\n確定要執行嗎？", I18nManager.Get("TitleManualDecision"), MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
                }
                else
                {
                    if (MessageBox.Show(string.Format(I18nManager.Get("MsgMultiGroupConfirm"), dg.PatternPrefix, sDir, tDir), I18nManager.Get("TitleMultiGroupConfirm"), MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
                }

                _cts = new CancellationTokenSource();
                _isScanning = true;
                SetUiInteractiveState(false, true);
                
                Application.Current.Dispatcher.Invoke(() => {
                    BtnScan.Content = I18nManager.Get("BtnCancel");
                    BtnScan.Background = (SolidColorBrush)new BrushConverter().ConvertFrom("#EF4444")!;
                });

                try
                {
                    string groupBatchId = Guid.NewGuid().ToString("N");
                    await ProcessMultiGroupMergeCoreAsync(dg, "", groupBatchId, _cts.Token);
                }
                finally
                {
                    _isScanning = false;
                    SetUiInteractiveState(true);
                    
                    Application.Current.Dispatcher.Invoke(() => {
                        BtnScan.Content = I18nManager.Get("BtnScan");
                        BtnScan.Background = (SolidColorBrush)new BrushConverter().ConvertFrom("#FFFFEBF2")!;
                    });

                    if (_cts.Token.IsCancellationRequested) {
                        MessageBox.Show("處理已中斷，即將重新掃描以同步畫面。", "中斷", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }

                    await AutoRescanAsync();
                }
            }
        }

        private async Task ProcessMultiGroupMergeCoreAsync(DirGroupItem dg, string smartId, string groupBatchId, CancellationToken ct = default)
        {
            string targetDir = dg.IsTargetDirA ? dg.DirA : dg.DirB;
            string sourceDir = dg.IsTargetDirA ? dg.DirB : dg.DirA;

            bool moveRemaining = !dg.MoveDuplicatesOnly && dg.MoveAllFiles; 
            bool moveMediaOnly = !dg.MoveDuplicatesOnly && dg.MoveMediaOnly;
            
            // 💡 使用 smartId 來判斷是否為「一鍵處理」的批次模式
            bool isSmartBatch = !string.IsNullOrEmpty(smartId);

            await ProcessSmartMergeAsync(targetDir, sourceDir, dg.GroupIds, false, moveRemaining, moveMediaOnly, dg, smartId, groupBatchId, true, ct);

            if (ct.IsCancellationRequested) return;

            int movedCount = 0;
            
            if (dg.PatternType != NamingPatternType.Random)
            {
                await Task.Run(() => {
                    long lastUpdateTicks = 0; 
                    long intervalTicks = Stopwatch.Frequency * 5; 

                    try 
                    {
                        if (!Directory.Exists(PathHelper.GetLongPath(sourceDir))) return;

                        var allFilesInSource = Directory.GetFiles(PathHelper.GetLongPath(sourceDir));
                        var regex = new System.Text.RegularExpressions.Regex(@"^(?<prefix>.*\D)?(?<number>\d+)(?<suffix>\D*)$");

                        foreach (var fileLong in allFilesInSource)
                        {
                            if (ct.IsCancellationRequested) break;

                            string file = PathHelper.StripLongPath(fileLong);
                            string name = Path.GetFileName(file);
                            string nameWithoutExt = Path.GetFileNameWithoutExtension(name);
                            bool isMatch = false;

                            if (dg.PatternType == NamingPatternType.Sequential) {
                                var match = regex.Match(nameWithoutExt);
                                if (match.Success && match.Groups["prefix"].Value.Equals(dg.PatternPrefix, StringComparison.OrdinalIgnoreCase)) {
                                    if (int.TryParse(match.Groups["number"].Value, out int num)) {
                                        if (num >= dg.PatternMin && num <= dg.PatternMax) isMatch = true;
                                    }
                                }
                            } else if (dg.PatternType == NamingPatternType.Grouped) {
                                if (nameWithoutExt.StartsWith(dg.PatternPrefix, StringComparison.OrdinalIgnoreCase)) isMatch = true;
                            }

                            if (isMatch) {
                                try {
                                    string destPath = Path.Combine(targetDir, name);
                                    Directory.CreateDirectory(PathHelper.GetLongPath(Path.GetDirectoryName(destPath)!));
                                    destPath = PathHelper.GetUniqueFilePath(destPath);
                                    File.Move(PathHelper.GetLongPath(file), PathHelper.GetLongPath(destPath), true);
                                    RecoveryManager.LogAction(file, destPath, smartId, groupBatchId);
                                    movedCount++;
                                    
                                    long currentTicks = Stopwatch.GetTimestamp();
                                    // 💡 一鍵處理時隱藏這裡的進度更新，避免覆蓋掉「群組分析」的進度狀態
                                    if (!isSmartBatch && (currentTicks - lastUpdateTicks > intervalTicks || lastUpdateTicks == 0)) {
                                        lastUpdateTicks = currentTicks;
                                        ReportProgress(string.Format(I18nManager.Get("StatusMultiGroupMoving"), movedCount));
                                    }
                                } catch { }
                            }
                        }
                    }
                    catch { } 
                });
            }
            
            // 💡 一鍵處理時隱藏這裡的完成提示
            if (movedCount > 0 && !ct.IsCancellationRequested && !isSmartBatch) 
                ReportProgress(string.Format(I18nManager.Get("StatusMultiGroupDone"), movedCount));
        }
        #endregion

        #region 大群組正常模式：預覽計畫、智慧實體合併與安全隔離移動
        private void DirGroupListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isSimpleMode) return;

            var view = CollectionViewSource.GetDefaultView(DupeFiles);
            if (DirGroupListView.SelectedItem is DirGroupItem dg)
            {
                view.Filter = item => item is DupeFileItem file && dg.GroupIds.Contains(file.GroupId) && 
                                      (file.DirectoryPath == dg.DirA || file.DirectoryPath == dg.DirB);
            }
            else view.Filter = null;
            UpdateStatusTextWithGroup();
        }

        private void CheckBoxSelectAllGroups_Click(object sender, RoutedEventArgs e)
        {
            bool isChecked = (sender as CheckBox)?.IsChecked ?? false;
            foreach (var group in DirGroups) group.IsSelected = isChecked;
        }

        private void BtnPreviewPlan_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.DataContext is DirGroupItem dg)
            {
                bool isSameDir = dg.IsSameDir;
                string targetDir = dg.IsTargetDirA ? dg.DirA : dg.DirB;
                string sourceDir = dg.IsTargetDirA ? dg.DirB : dg.DirA;
                bool moveRemaining = !isSameDir && !dg.MoveDuplicatesOnly && dg.MoveAllFiles; 
                bool moveMediaOnly = !dg.MoveDuplicatesOnly && dg.MoveMediaOnly;

                var previewList = new List<PreviewActionItem>();

                foreach (var gid in dg.GroupIds)
                {
                    var filesInGroup = DupeFiles.Where(x => x.GroupId == gid && (x.DirectoryPath.Equals(targetDir, StringComparison.OrdinalIgnoreCase) || x.DirectoryPath.Equals(sourceDir, StringComparison.OrdinalIgnoreCase))).ToList();
                    if (filesInGroup.Count < 2) continue;

                    var (bestFile, inferiorFiles) = GroupAnalysisEngine.GetBestAndInferiorFiles(filesInGroup, targetDir);

                    foreach (var inf in inferiorFiles)
                    {
                        string destPath = PathHelper.GetIsolationDestPath(inf.FilePath);
                        previewList.Add(new PreviewActionItem { ActionType = "🗑️ 隔離排除", SourcePath = inf.FilePath, DestPath = destPath });
                    }

                    if (!isSameDir && !bestFile.DirectoryPath.Equals(targetDir, StringComparison.OrdinalIgnoreCase))
                    {
                        string newPath = PathHelper.GetUniqueFilePath(Path.Combine(targetDir, bestFile.FileName), "M");
                        previewList.Add(new PreviewActionItem { ActionType = "📂 搬移正本", SourcePath = bestFile.FilePath, DestPath = newPath });
                    }
                }

                if (!isSameDir && moveRemaining && Directory.Exists(PathHelper.GetLongPath(sourceDir)))
                {
                    try {
                        var handledFiles = new HashSet<string>(previewList.Select(x => x.SourcePath), StringComparer.OrdinalIgnoreCase);
                        var allRemaining = Directory.GetFiles(PathHelper.GetLongPath(sourceDir), "*.*", SearchOption.AllDirectories);
                        
                        foreach (var fileLong in allRemaining)
                        {
                            string file = PathHelper.StripLongPath(fileLong);
                            if (handledFiles.Contains(file)) continue; 
                            if (moveMediaOnly && !_allMediaExts.Contains(Path.GetExtension(file))) continue;
                            
                            string relPath = file.Substring(sourceDir.Length).TrimStart('\\', '/');
                            string destPath = PathHelper.GetUniqueFilePath(Path.Combine(targetDir, relPath));
                            previewList.Add(new PreviewActionItem { ActionType = "📦 搬移剩餘", SourcePath = file, DestPath = destPath });
                        }
                    } catch { }
                }

                if (previewList.Count == 0) { MessageBox.Show(I18nManager.Get("MsgNoPreviewAction"), I18nManager.Get("TitlePreviewPlan"), MessageBoxButton.OK, MessageBoxImage.Information); return; }

                var window = new Window { Title = string.Format(I18nManager.Get("TitlePreviewPlan"), previewList.Count), Width = 1000, Height = 600, WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = this, Background = Brushes.WhiteSmoke, WindowState = WindowState.Maximized };
                var grid = new DataGrid { ItemsSource = previewList, AutoGenerateColumns = false, IsReadOnly = true, Margin = new Thickness(10), RowHeight = 60, AlternatingRowBackground = Brushes.AliceBlue, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto };

                var previewFactory = new FrameworkElementFactory(typeof(Image));
                var binding = new Binding("SourcePath") { Converter = new PathToImageConverter(), IsAsync = true };
                previewFactory.SetBinding(Image.SourceProperty, binding);
                previewFactory.SetValue(Image.MaxHeightProperty, 50.0);
                previewFactory.SetValue(Image.MaxWidthProperty, 50.0);
                previewFactory.SetValue(Image.StretchProperty, Stretch.Uniform);
                var cellTemplate = new DataTemplate { VisualTree = previewFactory };

                grid.Columns.Add(new DataGridTemplateColumn { Header = "預覽", CellTemplate = cellTemplate, Width = new DataGridLength(70) });
                grid.Columns.Add(new DataGridTextColumn { Header = "操作", Binding = new Binding("ActionType"), Width = new DataGridLength(120) });
                grid.Columns.Add(new DataGridTextColumn { Header = "原始路徑 (Source)", Binding = new Binding("SourcePath"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
                grid.Columns.Add(new DataGridTextColumn { Header = "目標路徑 (Destination)", Binding = new Binding("DestPath"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });

                window.Content = grid;
                window.ShowDialog();
            }
        }

        private async void BtnCleanSameDir_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.DataContext is DirGroupItem dg) 
            {
                _cts = new CancellationTokenSource();
                _isScanning = true;
                SetUiInteractiveState(false, true);
                
                Application.Current.Dispatcher.Invoke(() => {
                    BtnScan.Content = I18nManager.Get("BtnCancel");
                    BtnScan.Background = (SolidColorBrush)new BrushConverter().ConvertFrom("#EF4444")!;
                });

                try {
                    await ProcessSmartMergeAsync(dg.DirA, dg.DirA, dg.GroupIds, true, false, false, dg, "", Guid.NewGuid().ToString("N"), false, _cts.Token);
                } 
                finally {
                    _isScanning = false;
                    SetUiInteractiveState(true);
                    
                    Application.Current.Dispatcher.Invoke(() => {
                        BtnScan.Content = I18nManager.Get("BtnScan");
                        BtnScan.Background = (SolidColorBrush)new BrushConverter().ConvertFrom("#FFFFEBF2")!;
                    });

                    if (_cts.Token.IsCancellationRequested) {
                        await AutoRescanAsync();
                    }
                }
            }
        }

        private async void BtnMergeSelected_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.DataContext is DirGroupItem dg) 
            {
                _cts = new CancellationTokenSource();
                _isScanning = true;
                SetUiInteractiveState(false, true);
                
                Application.Current.Dispatcher.Invoke(() => {
                    BtnScan.Content = I18nManager.Get("BtnCancel");
                    BtnScan.Background = (SolidColorBrush)new BrushConverter().ConvertFrom("#EF4444")!;
                });

                try {
                    await ProcessSmartMergeAsync(dg.IsTargetDirA ? dg.DirA : dg.DirB, dg.IsTargetDirA ? dg.DirB : dg.DirA, dg.GroupIds, false, !dg.MoveDuplicatesOnly && dg.MoveAllFiles, !dg.MoveDuplicatesOnly && dg.MoveMediaOnly, dg, "", Guid.NewGuid().ToString("N"), false, _cts.Token);
                } 
                finally {
                    _isScanning = false;
                    SetUiInteractiveState(true);
                    
                    Application.Current.Dispatcher.Invoke(() => {
                        BtnScan.Content = I18nManager.Get("BtnScan");
                        BtnScan.Background = (SolidColorBrush)new BrushConverter().ConvertFrom("#FFFFEBF2")!;
                    });

                    if (_cts.Token.IsCancellationRequested) {
                        await AutoRescanAsync();
                    }
                }
            }
        }

        private async Task ProcessSmartMergeAsync(string targetDir, string sourceDir, List<ulong> groupIds, bool isSameDir, bool moveRemaining, bool moveMediaOnly, DirGroupItem dg, string smartId = "", string groupId = "", bool suppressUiFinish = false, CancellationToken ct = default)
        {
            int movedCount = 0, pseudoDeletedCount = 0, remainingMovedCount = 0;
            string? selectedDirA = (DirGroupListView.SelectedItem as DirGroupItem)?.DirA;
            string? selectedDirB = (DirGroupListView.SelectedItem as DirGroupItem)?.DirB;

            var itemsToRemove = new List<DupeFileItem>();
            var itemsToUpdate = new List<(DupeFileItem Item, string NewPath)>();
            var newExcludes = new List<string>();
            
            // 💡 根據 smartId 判定是否屬於批次處理
            bool isSmartBatch = !string.IsNullOrEmpty(smartId);

            await Task.Run(() => {
                long lastUpdateTicks = 0; 
                long intervalTicks = Stopwatch.Frequency * 5; 
                
                foreach (var gid in groupIds.ToList())
                {
                    if (ct.IsCancellationRequested) break;
                    
                    var filesInGroup = Application.Current.Dispatcher.Invoke(() => 
                        DupeFiles.Where(x => x.GroupId == gid && (x.DirectoryPath.Equals(targetDir, StringComparison.OrdinalIgnoreCase) || x.DirectoryPath.Equals(sourceDir, StringComparison.OrdinalIgnoreCase))).ToList()
                    );
                    
                    if (filesInGroup.Count < 2) continue;

                    var (bestFile, inferiorFiles) = GroupAnalysisEngine.GetBestAndInferiorFiles(filesInGroup, targetDir);

                    foreach (var inf in inferiorFiles)
                    {
                        if (ct.IsCancellationRequested) break;
                        
                        string? baseExclude = CommonHelper.SafePseudoDeleteFile(inf.FilePath, smartId, groupId);
                        if (baseExclude != null && !newExcludes.Contains(baseExclude)) newExcludes.Add(baseExclude);
                        itemsToRemove.Add(inf);
                        pseudoDeletedCount++;
                        
                        long currentTicks = Stopwatch.GetTimestamp();
                        // 💡 只有在非一鍵處理的單一群組合併時，才更新這些搬移的詳細進度
                        if (!isSmartBatch && (currentTicks - lastUpdateTicks > intervalTicks || lastUpdateTicks == 0)) {
                            lastUpdateTicks = currentTicks;
                            ReportProgress($"處理重複檔案... 已隔離排除 {pseudoDeletedCount} 個");
                        }
                    }

                    if (!isSameDir && !bestFile.DirectoryPath.Equals(targetDir, StringComparison.OrdinalIgnoreCase))
                    {
                        try {
                            string newPath = PathHelper.GetUniqueFilePath(Path.Combine(targetDir, bestFile.FileName), "M");
                            File.Move(PathHelper.GetLongPath(bestFile.FilePath), PathHelper.GetLongPath(newPath), true);
                            RecoveryManager.LogAction(bestFile.FilePath, newPath, smartId, groupId);
                            itemsToUpdate.Add((bestFile, newPath));
                            movedCount++;
                            
                            long currentTicks = Stopwatch.GetTimestamp();
                            if (!isSmartBatch && (currentTicks - lastUpdateTicks > intervalTicks || lastUpdateTicks == 0)) {
                                lastUpdateTicks = currentTicks;
                                ReportProgress($"移動最佳正本... 已搬移 {movedCount} 個");
                            }
                        } catch { } 
                    }
                }

                if (!ct.IsCancellationRequested && !isSameDir && moveRemaining && Directory.Exists(PathHelper.GetLongPath(sourceDir)))
                {
                    if (!isSmartBatch) ReportProgress($"正在搬移剩餘檔案...");
                    try {
                        foreach (var fileLong in Directory.GetFiles(PathHelper.GetLongPath(sourceDir), "*.*", SearchOption.AllDirectories))
                        {
                            if (ct.IsCancellationRequested) break;
                            
                            string file = PathHelper.StripLongPath(fileLong);
                            if (moveMediaOnly && !_allMediaExts.Contains(Path.GetExtension(file))) continue;
                            try {
                                string relPath = file.Substring(sourceDir.Length).TrimStart('\\', '/');
                                string destPath = Path.Combine(targetDir, relPath);
                                Directory.CreateDirectory(PathHelper.GetLongPath(Path.GetDirectoryName(destPath)!));
                                destPath = PathHelper.GetUniqueFilePath(destPath); 
                                File.Move(PathHelper.GetLongPath(file), PathHelper.GetLongPath(destPath), true);
                                RecoveryManager.LogAction(file, destPath, smartId, groupId);
                                remainingMovedCount++;
                                
                                long currentTicks = Stopwatch.GetTimestamp();
                                if (!isSmartBatch && (currentTicks - lastUpdateTicks > intervalTicks || lastUpdateTicks == 0)) {
                                    lastUpdateTicks = currentTicks;
                                    ReportProgress($"正在搬移剩餘檔案... 已搬移 {remainingMovedCount} 個");
                                }
                            } catch { }
                        }
                        if (!ct.IsCancellationRequested) {
                            if (moveMediaOnly) { if (!Directory.EnumerateFileSystemEntries(PathHelper.GetLongPath(sourceDir)).Any()) Directory.Delete(PathHelper.GetLongPath(sourceDir), true); } 
                            else Directory.Delete(PathHelper.GetLongPath(sourceDir), true);
                        }
                    } catch { } 
                }
            });

            Application.Current.Dispatcher.Invoke(() => {
                if (!suppressUiFinish)
                {
                    foreach (var up in itemsToUpdate) up.Item.FilePath = up.NewPath;
                    foreach (var rm in itemsToRemove) DupeFiles.Remove(rm);
                }
                
                if (newExcludes.Count > 0)
                {
                    var freshExcludes = GetPathList(ExcludePathTextBox);
                    bool hasNew = false;
                    foreach (var dir in newExcludes)
                    {
                        if (!freshExcludes.Contains(dir, StringComparer.OrdinalIgnoreCase))
                        {
                            freshExcludes.Add(dir);
                            hasNew = true;
                        }
                    }
                    if (hasNew)
                    {
                        ExcludePathTextBox.Text = string.Join("; ", freshExcludes);
                        SaveSettings();
                    }
                }

                if (!suppressUiFinish)
                {
                    PostActionUIRefresh(selectedDirA, selectedDirB, false);
                }
            });

            if (!suppressUiFinish && !ct.IsCancellationRequested)
            {
                StatusText.Text = $"✅ 處理完成！搬移正本: {movedCount} | 隔離排除: {pseudoDeletedCount} | 搬移剩餘: {remainingMovedCount}";
                _baseStatusText = StatusText.Text;
                ForceGarbageCollection();
                MessageBox.Show(string.Format(I18nManager.Get("MsgMergeComplete"), movedCount, pseudoDeletedCount, remainingMovedCount), 
                                I18nManager.Get("TitleMergeReport"), MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        #endregion

        #region 智慧一鍵處理
        private async void BtnSmartSelect_Click(object sender, RoutedEventArgs e)
        {
            var selectedGroups = DirGroups.Where(g => g.IsSelected).ToList();
            if (selectedGroups.Count == 0)
            {
                MessageBox.Show(I18nManager.Get("MsgSelectGroupFirst"), I18nManager.Get("MsgPrompt"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (MessageBox.Show(string.Format(I18nManager.Get("MsgSmartConfirm"), selectedGroups.Count), I18nManager.Get("TitleSmartProcess"), MessageBoxButton.YesNo, MessageBoxImage.Information) != MessageBoxResult.Yes) 
                return;

            _cts = new CancellationTokenSource();
            _isScanning = true; 
            SetUiInteractiveState(false, true);
            
            Application.Current.Dispatcher.Invoke(() => {
                BtnScan.Content = I18nManager.Get("BtnCancel");
                BtnScan.Background = (SolidColorBrush)new BrushConverter().ConvertFrom("#EF4444")!;
            });

            try
            {
                string smartBatchId = Guid.NewGuid().ToString("N");
                
                int currentGroupIdx = 0;
                int totalGroups = selectedGroups.Count;
                
                // 💡 在外層設定 5 秒計時器，只在分析群組時跳動狀態
                long lastGroupUpdateTicks = 0;
                long groupIntervalTicks = Stopwatch.Frequency * 5;

                foreach (var dg in selectedGroups)
                {
                    if (_cts.Token.IsCancellationRequested) break; 

                    currentGroupIdx++;
                    long currentTicks = Stopwatch.GetTimestamp();
                    
                    // 💡 第一個、最後一個，或是每滿 5 秒才更新畫面狀態
                    if (currentTicks - lastGroupUpdateTicks > groupIntervalTicks || lastGroupUpdateTicks == 0 || currentGroupIdx == totalGroups)
                    {
                        lastGroupUpdateTicks = currentTicks;
                        ReportProgress($"🧹 智慧一鍵處理：正在分析群組 {currentGroupIdx} / {totalGroups} ...");
                    }

                    string groupBatchId = Guid.NewGuid().ToString("N");
                    
                    if (dg.IsMultiGroup) 
                    {
                        await ProcessMultiGroupMergeCoreAsync(dg, smartBatchId, groupBatchId, _cts.Token);
                    }
                    else 
                    {
                        if (dg.IsSameDir) await ProcessSmartMergeAsync(dg.DirA, dg.DirA, dg.GroupIds, true, false, false, dg, smartBatchId, groupBatchId, true, _cts.Token);
                        else await ProcessSmartMergeAsync(dg.IsTargetDirA ? dg.DirA : dg.DirB, dg.IsTargetDirA ? dg.DirB : dg.DirA, dg.GroupIds, false, !dg.MoveDuplicatesOnly && dg.MoveAllFiles, !dg.MoveDuplicatesOnly && dg.MoveMediaOnly, dg, smartBatchId, groupBatchId, true, _cts.Token);
                    }
                }
            }
            finally
            {
                _isScanning = false;
                SetUiInteractiveState(true);
                
                Application.Current.Dispatcher.Invoke(() => {
                    BtnScan.Content = I18nManager.Get("BtnScan");
                    BtnScan.Background = (SolidColorBrush)new BrushConverter().ConvertFrom("#FFFFEBF2")!;
                });

                if (_cts.Token.IsCancellationRequested) {
                    StatusText.Text = "🛑 處理已中斷！正在同步畫面狀態...";
                    MessageBox.Show("處理已中斷，部分檔案可能已搬移。\n\n畫面即將自動重新整理，以確保清單正確。", "中斷", MessageBoxButton.OK, MessageBoxImage.Warning);
                }

                await AutoRescanAsync();

                if (!_cts.Token.IsCancellationRequested) {
                    MessageBox.Show(I18nManager.Get("MsgSmartDone"), I18nManager.Get("TitleSmartReport"), MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }
        #endregion

        #region 復原管理 (自動重掃)
        private async void BtnRecovery_Click(object sender, RoutedEventArgs e)
        {
            var window = new RecoveryWindow { Owner = this };
            window.ShowDialog();
            
            if (window.AnyActionExecuted)
            {
                SetUiInteractiveState(false);
                try {
                    await AutoRescanAsync();
                } catch {
                    SetUiInteractiveState(true);
                }
            }
        }
        #endregion

        #region 輔助工具
        private void ShowExclusionReportWindow()
        {
            if (_lastExcludedNodes == null || _lastExcludedNodes.Count == 0) { MessageBox.Show(I18nManager.Get("MsgNoExclusions"), I18nManager.Get("MsgPrompt"), MessageBoxButton.OK, MessageBoxImage.Information); return; }

            var window = new Window { Title = I18nManager.Get("TitleExclusionReport"), Width = 1000, Height = 600, WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = this, Background = Brushes.WhiteSmoke };
            var grid = new DataGrid { ItemsSource = _lastExcludedNodes, AutoGenerateColumns = false, IsReadOnly = true, Margin = new Thickness(10), RowHeight = 32, AlternatingRowBackground = Brushes.AliceBlue, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto };

            grid.Columns.Add(new DataGridTextColumn { Header = "排除路徑 (Excluded Path)", Binding = new Binding("Path"), Width = new DataGridLength(4, DataGridLengthUnitType.Star) });
            grid.Columns.Add(new DataGridTextColumn { Header = "判定死因 (Reason)", Binding = new Binding("Reason"), Width = new DataGridLength(3, DataGridLengthUnitType.Star) });

            var buttonTemplate = new DataTemplate();
            var buttonFactory = new FrameworkElementFactory(typeof(Button));
            buttonFactory.SetValue(Button.ContentProperty, "📂 開啟元兇");
            buttonFactory.SetValue(Button.ToolTipProperty, new Binding("SourceOfExclusion"));
            buttonFactory.SetValue(Button.MarginProperty, new Thickness(2));
            buttonFactory.SetValue(Button.PaddingProperty, new Thickness(5,0,5,0));
            
            buttonFactory.AddHandler(Button.ClickEvent, new RoutedEventHandler((s, e) => {
                if (s is Button btn && btn.DataContext is ExcludedNode node) {
                    if (Directory.Exists(node.SourceOfExclusion)) CommonHelper.SafeExecute("explorer.exe", $"\"{node.SourceOfExclusion}\"");
                    else MessageBox.Show(string.Format(I18nManager.Get("MsgPathNotFound"), node.SourceOfExclusion), I18nManager.Get("MsgError"), MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }));

            buttonTemplate.VisualTree = buttonFactory;
            grid.Columns.Add(new DataGridTemplateColumn { Header = "查看觸發源", CellTemplate = buttonTemplate, Width = new DataGridLength(100) });
            window.Content = grid;
            window.Show();
        }
        #endregion

        #region 預覽、清單互動與個人批次刪除

        private void UpdatePreviewPanel(bool isA, DupeFileItem? item)
        {
            var img = isA ? PreviewImageA : PreviewImageB;
            var txtRes = isA ? TextResA : TextResB;
            var txtSize = isA ? TextSizeA : TextSizeB;
            var txtDir = isA ? TextDirA : TextDirB;
            var txtName = isA ? TextNameA : TextNameB;

            if (isA) _previewItemA = item; else _previewItemB = item;

            if (item == null)
            {
                img.Source = null;
                txtRes.Text = I18nManager.Get("LblRes") + " -";
                txtSize.Text = I18nManager.Get("LblSize") + " -";
                txtDir.Text = I18nManager.Get("LblDir") + " -";
                txtName.Text = I18nManager.Get("LblName") + " -";
            }
            else
            {
                try {
                    var bmp = new BitmapImage();
                    bmp.BeginInit(); 
                    bmp.CacheOption = BitmapCacheOption.OnLoad; 
                    bmp.DecodePixelWidth = 800; 
                    using (var stream = new FileStream(PathHelper.GetLongPath(item.FilePath), FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) {
                        bmp.StreamSource = stream; 
                        bmp.EndInit();
                    }
                    bmp.Freeze();
                    
                    img.Source = bmp; 
                    txtRes.Text = $"{I18nManager.Get("LblRes")} {(item.Width > 0 ? item.Resolution : $"{bmp.PixelWidth}x{bmp.PixelHeight}")}"; 
                    txtSize.Text = $"{I18nManager.Get("LblSize")} {item.SizeStr}"; 
                    txtDir.Text = $"{I18nManager.Get("LblDir")} {item.DirectoryPath}";
                    txtName.Text = $"{I18nManager.Get("LblName")} {item.FileName}";
                } catch { img.Source = null; txtRes.Text = "失敗"; }
            }
        }

        private void FileListView_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (_previewItemA != null) _previewItemA.IsPreviewA = false;
            if (_previewItemB != null) _previewItemB.IsPreviewB = false;

            if (FileListView.SelectedItem is DupeFileItem selectedItem) {
                var visibleGroupFiles = FileListView.Items.Cast<DupeFileItem>()
                                        .Where(x => x.GroupId == selectedItem.GroupId).ToList();

                if (visibleGroupFiles.Count > 0) {
                    var referenceItem = visibleGroupFiles[0];
                    UpdatePreviewPanel(true, referenceItem);

                    if (selectedItem != referenceItem) UpdatePreviewPanel(false, selectedItem);
                    else if (visibleGroupFiles.Count > 1) UpdatePreviewPanel(false, visibleGroupFiles[1]);
                    else UpdatePreviewPanel(false, null);
                }
            }
            else {
                UpdatePreviewPanel(true, null);
                UpdatePreviewPanel(false, null);
            }

            if (_previewItemA != null) _previewItemA.IsPreviewA = true;
            if (_previewItemB != null) _previewItemB.IsPreviewB = true;

            if (RbPreviewA != null) RbPreviewA.IsChecked = false;
            if (RbPreviewB != null) RbPreviewB.IsChecked = false;
        }

        private void ExecuteIsolation(List<DupeFileItem> itemsToIsolate, bool showCompletionMsg)
        {
            if (itemsToIsolate.Count == 0) return;

            string personalBatchId = Guid.NewGuid().ToString("N");
            string? selectedDirA = (DirGroupListView.SelectedItem as DirGroupItem)?.DirA;
            string? selectedDirB = (DirGroupListView.SelectedItem as DirGroupItem)?.DirB;

            var newExcludes = new List<string>();

            foreach (var item in itemsToIsolate)
            {
                string? baseExclude = CommonHelper.SafePseudoDeleteFile(item.FilePath, "", "", personalBatchId);
                if (baseExclude != null && !newExcludes.Contains(baseExclude)) newExcludes.Add(baseExclude);
            }

            foreach (var item in itemsToIsolate) DupeFiles.Remove(item);

            if (newExcludes.Count > 0)
            {
                var freshExcludes = GetPathList(ExcludePathTextBox);
                bool hasNew = false;
                foreach (var dir in newExcludes)
                {
                    if (!freshExcludes.Contains(dir, StringComparer.OrdinalIgnoreCase))
                    {
                        freshExcludes.Add(dir);
                        hasNew = true;
                    }
                }
                if (hasNew)
                {
                    ExcludePathTextBox.Text = string.Join("; ", freshExcludes);
                    SaveSettings();
                }
            }

            PostActionUIRefresh(selectedDirA, selectedDirB, false);

            if (showCompletionMsg)
                MessageBox.Show(string.Format(I18nManager.Get("MsgIsolateSuccess"), itemsToIsolate.Count), I18nManager.Get("TitleIsolateComplete"), MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void BtnDeleteChecked_Click(object sender, RoutedEventArgs e)
        {
            var itemsToIsolate = DupeFiles.Where(x => x.IsSelected).ToList();
            if (itemsToIsolate.Count == 0) { MessageBox.Show(I18nManager.Get("MsgSelectFileToIsolate"), I18nManager.Get("MsgPrompt"), MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            if (MessageBox.Show(string.Format(I18nManager.Get("MsgIsolateConfirm"), itemsToIsolate.Count), I18nManager.Get("TitleIsolateConfirm"), MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

            SetUiInteractiveState(false);
            await Task.Delay(10); 
            try {
                ExecuteIsolation(itemsToIsolate, true);
            } finally {
                SetUiInteractiveState(true);
            }
        }

        private async void BtnManualIsolate_Click(object sender, RoutedEventArgs e)
        {
            var itemsToIsolate = new List<DupeFileItem>();
            if (RbPreviewA?.IsChecked == true && _previewItemA != null) itemsToIsolate.Add(_previewItemA);
            else if (RbPreviewB?.IsChecked == true && _previewItemB != null) itemsToIsolate.Add(_previewItemB);
            else { MessageBox.Show(I18nManager.Get("MsgSelectRadioFirst"), I18nManager.Get("MsgPrompt"), MessageBoxButton.OK, MessageBoxImage.Warning); return; }

            if (RbPreviewA != null) RbPreviewA.IsChecked = false;
            if (RbPreviewB != null) RbPreviewB.IsChecked = false;

            SetUiInteractiveState(false);
            await Task.Delay(10); 
            try {
                ExecuteIsolation(itemsToIsolate, false);
            } finally {
                SetUiInteractiveState(true);
            }
        }

        private void OpenFileOrDirectory(bool openDir)
        {
            if (FileListView.SelectedItem is DupeFileItem item) {
                if (openDir) CommonHelper.SafeExecute("explorer.exe", $"/select,\"{item.FilePath}\"");
                else CommonHelper.SafeExecute(item.FilePath);
            }
        }

        private void FileListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (((FrameworkElement)e.OriginalSource).DataContext is DupeFileItem item) CommonHelper.SafeExecute(item.FilePath);
        }

        private void MenuItem_OpenDirectory_Click(object sender, RoutedEventArgs e) => OpenFileOrDirectory(true);
        private void MenuItem_OpenFile_Click(object sender, RoutedEventArgs e) => OpenFileOrDirectory(false);
        
        private DupeFileItem? _previewItemA;
        private DupeFileItem? _previewItemB;
        #endregion
    }

    public class PreviewActionItem
    {
        public string ActionType { get; set; } = "";
        public string SourcePath { get; set; } = "";
        public string DestPath { get; set; } = "";
    }
}