using System.Windows;

namespace FastImageDupe
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 💡 註冊全域未處理例外事件，防止程式在極端情況下無預警閃退
            this.DispatcherUnhandledException += (sender, args) =>
            {
                MessageBox.Show($"系統發生未預期錯誤，請聯絡開發者。\n\n詳細資訊: {args.Exception.Message}", 
                                "全域異常防護", MessageBoxButton.OK, MessageBoxImage.Error);
                args.Handled = true; // 攔截例外，不讓程式崩潰
            };
        }
    }
}