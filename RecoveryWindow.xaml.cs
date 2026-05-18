using System;
using System.Linq;
using System.Windows;
using System.IO;
using System.Windows.Data;
using System.Globalization;
using System.Windows.Media.Imaging;
using System.Collections.Generic;
using FastImageDupe.Core;

namespace FastImageDupe
{
    public partial class RecoveryWindow : Window
    {
        // 💡 新增：紀錄是否有執行任何紀錄隱藏、還原、徹底刪除或清理動作，以便回頭時自動掃描
        public bool AnyActionExecuted { get; private set; } = false;

        public RecoveryWindow()
        {
            InitializeComponent();
            I18nManager.TranslateWindow(this); 
            RecoveryManager.Load();
            RefreshGrid();
        }

        #region 1. 顯示篩選邏輯 (View Filter)

        private void Filter_Changed(object sender, RoutedEventArgs e)
        {
            if (LogDataGrid != null) RefreshGrid();
        }

        private void RefreshGrid()
        {
            var allLogs = RecoveryManager.Logs;
            var query = allLogs.AsEnumerable();

            var smartGroups = allLogs.Where(x => !string.IsNullOrEmpty(x.SmartBatchId)).OrderBy(x => x.Timestamp).Select(x => x.SmartBatchId).Distinct().ToList();
            var groupGroups = allLogs.Where(x => !string.IsNullOrEmpty(x.GroupBatchId)).OrderBy(x => x.Timestamp).Select(x => x.GroupBatchId).Distinct().ToList();
            var personalGroups = allLogs.Where(x => !string.IsNullOrEmpty(x.PersonalBatchId)).OrderBy(x => x.Timestamp).Select(x => x.PersonalBatchId).Distinct().ToList();

            foreach (var item in allLogs)
            {
                item.DisplaySmartId = string.IsNullOrEmpty(item.SmartBatchId) ? "-" : (smartGroups.IndexOf(item.SmartBatchId) + 1).ToString();
                item.DisplayGroupId = string.IsNullOrEmpty(item.GroupBatchId) ? "-" : (groupGroups.IndexOf(item.GroupBatchId) + 1).ToString();
                item.DisplayPersonalId = string.IsNullOrEmpty(item.PersonalBatchId) ? "-" : (personalGroups.IndexOf(item.PersonalBatchId) + 1).ToString();
                
                item.IsSelected = item.IsSelected; 
            }

            bool showNormal = Rec_ChkNormal.IsChecked == true;
            bool showHidden = Rec_ChkHidden.IsChecked == true;
            query = query.Where(x => (showNormal && !x.IsHidden) || (showHidden && x.IsHidden));

            bool showSmart = Rec_ChkSmart.IsChecked == true;
            bool showGroup = Rec_ChkGroup.IsChecked == true;
            bool showPersonal = Rec_ChkPersonal.IsChecked == true;

            query = query.Where(x => 
                (showSmart && !string.IsNullOrEmpty(x.SmartBatchId)) ||
                (showGroup && string.IsNullOrEmpty(x.SmartBatchId) && !string.IsNullOrEmpty(x.GroupBatchId)) ||
                (showPersonal && string.IsNullOrEmpty(x.SmartBatchId) && string.IsNullOrEmpty(x.GroupBatchId)));

            LogDataGrid.ItemsSource = query.OrderByDescending(x => x.Timestamp).ToList();
        }

        #endregion

        #region 2. 批次快速勾選邏輯 (Batch Selection)

        private void ChkSelectAllVisible_Click(object sender, RoutedEventArgs e)
        {
            bool isChecked = (sender as System.Windows.Controls.CheckBox)?.IsChecked ?? false;

            if (!isChecked)
            {
                foreach (var item in RecoveryManager.Logs) item.IsSelected = false;
            }
            else
            {
                if (LogDataGrid.ItemsSource != null)
                {
                    foreach (ActionLogItem item in LogDataGrid.ItemsSource) item.IsSelected = true;
                }
            }
            LogDataGrid.Items.Refresh(); 
        }

        private void BtnSelectSameSmart_Click(object sender, RoutedEventArgs e) => SelectSameBatch("Smart");
        private void BtnSelectSameGroup_Click(object sender, RoutedEventArgs e) => SelectSameBatch("Group");
        private void BtnSelectSamePersonal_Click(object sender, RoutedEventArgs e) => SelectSameBatch("Personal");

        private void SelectSameBatch(string batchType)
        {
            if (LogDataGrid.SelectedItem is not ActionLogItem selectedItem)
            {
                MessageBox.Show(I18nManager.Get("Rec_MsgSelectBase"), I18nManager.Get("MsgPrompt"), MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (LogDataGrid.ItemsSource == null) return;

            int count = 0;
            foreach (ActionLogItem item in LogDataGrid.ItemsSource)
            {
                bool isMatch = false;
                if (batchType == "Smart" && !string.IsNullOrEmpty(selectedItem.SmartBatchId) && item.SmartBatchId == selectedItem.SmartBatchId) isMatch = true;
                else if (batchType == "Group" && !string.IsNullOrEmpty(selectedItem.GroupBatchId) && item.GroupBatchId == selectedItem.GroupBatchId) isMatch = true;
                else if (batchType == "Personal" && !string.IsNullOrEmpty(selectedItem.PersonalBatchId) && item.PersonalBatchId == selectedItem.PersonalBatchId) isMatch = true;

                if (isMatch)
                {
                    item.IsSelected = true;
                    count++;
                }
            }

            if (count == 0) 
            {
                string typeName = batchType == "Smart" ? I18nManager.Get("Rec_TypeSmart") : batchType == "Group" ? I18nManager.Get("Rec_TypeGroup") : I18nManager.Get("Rec_TypePersonal");
                MessageBox.Show(string.Format(I18nManager.Get("Rec_MsgNotSameBatch"), typeName), I18nManager.Get("MsgPrompt"), MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else LogDataGrid.Items.Refresh();
        }

        #endregion

        #region 3. 實際執行操作邏輯 (Actions)

        private void BtnHide_Click(object sender, RoutedEventArgs e)
        {
            var items = RecoveryManager.Logs.Where(x => x.IsSelected).ToList();
            if (!items.Any()) 
            {
                MessageBox.Show(I18nManager.Get("Rec_MsgSelectHide"), I18nManager.Get("MsgWarning"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            foreach (var item in items)
            {
                item.IsHidden = true;
                item.IsSelected = false; 
            }
            RecoveryManager.Save();
            AnyActionExecuted = true; // 💡 標記變更，回頭時自動掃描
            RefreshGrid();
            MessageBox.Show(string.Format(I18nManager.Get("Rec_MsgHideSuccess"), items.Count), I18nManager.Get("MsgPrompt"), MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnUnhide_Click(object sender, RoutedEventArgs e)
        {
            var items = RecoveryManager.Logs.Where(x => x.IsSelected).ToList();
            if (!items.Any()) 
            {
                MessageBox.Show(I18nManager.Get("Rec_MsgSelectUnhide"), I18nManager.Get("MsgWarning"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int count = 0;
            foreach (var item in items)
            {
                if (item.IsHidden)
                {
                    item.IsHidden = false;
                    item.IsSelected = false; 
                    count++;
                }
            }

            if (count > 0)
            {
                RecoveryManager.Save();
                AnyActionExecuted = true; // 💡 標記變更，回頭時自動掃描
                RefreshGrid();
                MessageBox.Show(string.Format(I18nManager.Get("Rec_MsgUnhideSuccess"), count), I18nManager.Get("MsgPrompt"), MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show(I18nManager.Get("Rec_MsgNotHidden"), I18nManager.Get("MsgPrompt"), MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnRemoveMissing_Click(object sender, RoutedEventArgs e)
        {
            var missingItems = RecoveryManager.Logs.Where(x => x.IsFileMissing).ToList();

            if (!missingItems.Any()) 
            {
                MessageBox.Show(I18nManager.Get("Rec_MsgNoMissing"), I18nManager.Get("MsgPrompt"), MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (MessageBox.Show(string.Format(I18nManager.Get("Rec_MsgClearMissingConfirm"), missingItems.Count), I18nManager.Get("Rec_TitleClearMissing"), MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) 
                return;

            var missingIds = new HashSet<string>(missingItems.Select(x => x.Id));
            RecoveryManager.Logs.RemoveAll(x => missingIds.Contains(x.Id));

            RecoveryManager.Save();
            AnyActionExecuted = true; // 💡 標記變更，回頭時自動掃描
            RefreshGrid(); 
            MessageBox.Show(string.Format(I18nManager.Get("Rec_MsgClearMissingSuccess"), missingItems.Count), I18nManager.Get("Rec_TitleClearComplete"), MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnRecover_Click(object sender, RoutedEventArgs e)
        {
            var items = RecoveryManager.Logs.Where(x => x.IsSelected).ToList();
            if (!items.Any()) 
            {
                MessageBox.Show(I18nManager.Get("Rec_MsgSelectRecover"), I18nManager.Get("MsgWarning"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (MessageBox.Show(string.Format(I18nManager.Get("Rec_MsgRecoverConfirm"), items.Count), I18nManager.Get("Rec_TitleRecover"), MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

            int successCount = 0;
            int failCount = 0;
            
            var destPathsToRemove = new List<string>();

            foreach (var item in items)
            {
                if (File.Exists(item.DestPath))
                {
                    try
                    {
                        string? dir = Path.GetDirectoryName(item.SourcePath);
                        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                        
                        File.Move(item.DestPath, item.SourcePath, true);
                        successCount++;
                        
                        destPathsToRemove.Add(item.DestPath);
                    }
                    catch 
                    {
                        failCount++;
                    }
                }
                else 
                {
                    destPathsToRemove.Add(item.DestPath);
                }
            }

            if (destPathsToRemove.Count > 0)
            {
                var pathsSet = new HashSet<string>(destPathsToRemove);
                RecoveryManager.Logs.RemoveAll(x => pathsSet.Contains(x.DestPath));
            }

            RecoveryManager.Save();
            AnyActionExecuted = true; // 💡 標記變更，回頭時自動掃描
            RefreshGrid(); 

            if (failCount > 0)
            {
                MessageBox.Show(string.Format(I18nManager.Get("Rec_MsgRecoverPartial"), successCount, failCount), I18nManager.Get("Rec_TitleRecoverResult"), MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            else
            {
                MessageBox.Show(string.Format(I18nManager.Get("Rec_MsgRecoverSuccess"), successCount), I18nManager.Get("Rec_TitleRecoverComplete"), MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnRecycle_Click(object sender, RoutedEventArgs e)
        {
            var items = RecoveryManager.Logs.Where(x => x.IsSelected).ToList();
            if (!items.Any()) 
            {
                MessageBox.Show(I18nManager.Get("Rec_MsgSelectRecycle"), I18nManager.Get("MsgWarning"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (MessageBox.Show(string.Format(I18nManager.Get("Rec_MsgRecycleConfirm"), items.Count), I18nManager.Get("Rec_TitleRecycle"), MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

            int successCount = 0;
            int failCount = 0;
            
            var destPathsToRemove = new List<string>();

            foreach (var item in items)
            {
                if (File.Exists(item.DestPath))
                {
                    try 
                    { 
                        RecoveryManager.SendToRecycleBin(item.DestPath); 
                        successCount++; 
                        
                        destPathsToRemove.Add(item.DestPath);
                    }
                    catch 
                    {
                        failCount++; 
                    }
                }
                else
                {
                    destPathsToRemove.Add(item.DestPath);
                }
            }

            if (destPathsToRemove.Count > 0)
            {
                var pathsSet = new HashSet<string>(destPathsToRemove);
                RecoveryManager.Logs.RemoveAll(x => pathsSet.Contains(x.DestPath));
            }

            RecoveryManager.Save();
            AnyActionExecuted = true; // 💡 標記變更，回頭時自動掃描
            RefreshGrid(); 

            if (failCount > 0)
            {
                MessageBox.Show(string.Format(I18nManager.Get("Rec_MsgRecyclePartial"), successCount, failCount), I18nManager.Get("Rec_TitleRecycleResult"), MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            else
            {
                MessageBox.Show(string.Format(I18nManager.Get("Rec_MsgRecycleSuccess"), successCount), I18nManager.Get("Rec_TitleRecycleComplete"), MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        #endregion
    }

    public class PathToImageConverter : IValueConverter
    {
        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not string path || string.IsNullOrEmpty(path) || !File.Exists(path)) 
                return null;
            
            try 
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad; 
                bmp.DecodePixelWidth = 100; 
                
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    bmp.StreamSource = stream;
                    bmp.EndInit();
                }
                bmp.Freeze();
                return bmp;
            } 
            catch { return null; }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}