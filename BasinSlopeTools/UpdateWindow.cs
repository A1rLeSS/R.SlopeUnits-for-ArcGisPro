using System;
using System.Net;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Web.WebView2.Wpf;
using WpfButton = System.Windows.Controls.Button;
using WpfProgressBar = System.Windows.Controls.ProgressBar;

namespace BasinSlopeTools
{
    internal sealed class UpdateWindow : Window
    {
        private readonly ReleaseInfo _release;
        private readonly WpfButton _download;
        private readonly WpfButton _open;
        private readonly Label _status;
        private readonly WpfProgressBar _progress;

        private UpdateWindow(ReleaseInfo release)
        {
            _release = release;
            Title = "R.slopeunits 更新";
            Width = 820;
            Height = 650;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = Brushes.White;

            var title = new TextBlock
            {
                Text = "发现新版本 " + release.Version,
                FontSize = 20,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(16, 14, 16, 8)
            };

            var web = new WebView2();
            var fallback = new TextBox
            {
                Text = release.Body ?? "",
                IsReadOnly = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                FontFamily = new FontFamily("Consolas"),
                Visibility = Visibility.Collapsed,
                Margin = new Thickness(16)
            };
            var content = new Grid();
            content.Children.Add(web);
            content.Children.Add(fallback);

            _status = new Label
            {
                Content = "更新日志来自 GitHub Release。",
                Margin = new Thickness(16, 4, 16, 4),
                Foreground = Brushes.DimGray
            };
            _progress = new WpfProgressBar
            {
                Minimum = 0,
                Maximum = 100,
                Height = 16,
                Margin = new Thickness(16, 0, 16, 8),
                Visibility = Visibility.Collapsed
            };

            _download = new WpfButton
            {
                Content = "下载并安装",
                Width = 120,
                Height = 32,
                Margin = new Thickness(6)
            };
            _open = new WpfButton
            {
                Content = "打开 Release",
                Width = 110,
                Height = 32,
                Margin = new Thickness(6)
            };
            var later = new WpfButton
            {
                Content = "稍后",
                Width = 72,
                Height = 32,
                Margin = new Thickness(6)
            };
            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(10, 0, 10, 8)
            };
            buttons.Children.Add(_download);
            buttons.Children.Add(_open);
            buttons.Children.Add(later);

            var root = new DockPanel();
            DockPanel.SetDock(title, Dock.Top);
            DockPanel.SetDock(_status, Dock.Bottom);
            DockPanel.SetDock(_progress, Dock.Bottom);
            DockPanel.SetDock(buttons, Dock.Bottom);
            root.Children.Add(title);
            root.Children.Add(buttons);
            root.Children.Add(_progress);
            root.Children.Add(_status);
            root.Children.Add(content);
            Content = root;

            later.Click += (_, _) => Close();
            _open.Click += (_, _) => OpenRelease();
            _download.Click += async (_, _) => await DownloadAsync();

            Loaded += async (_, _) =>
            {
                try
                {
                    await web.EnsureCoreWebView2Async(null);
                    web.NavigateToString(BuildHtml(release));
                }
                catch
                {
                    web.Visibility = Visibility.Collapsed;
                    fallback.Visibility = Visibility.Visible;
                }
            };
        }

        public static void ShowRelease(ReleaseInfo release)
        {
            if (release == null)
                return;
            var window = new UpdateWindow(release);
            window.ShowDialog();
        }

        private static string BuildHtml(ReleaseInfo release)
        {
            var notes = WebUtility.HtmlEncode(release.Body ?? "");
            return @"<!DOCTYPE html><html lang=""zh-CN""><head><meta charset=""utf-8"">
<style>
body{font-family:'Microsoft YaHei',Arial,Segoe UI;margin:16px;color:#202428;line-height:1.6}
h1{font-size:20px}pre{white-space:pre-wrap;font-family:Consolas,'Microsoft YaHei';font-size:13px;background:#f6f8fa;padding:14px;border:1px solid #dde3e8}
.meta{color:#5a6a72}
</style></head><body>
<h1>R.slopeunits " + WebUtility.HtmlEncode(release.Version) + @"</h1>
<p class=""meta"">当前版本：" + WebUtility.HtmlEncode(UpdateService.CurrentVersion) +
                   @" &nbsp;|&nbsp; 来源：GitHub Release</p>
<pre>" + notes + @"</pre>
</body></html>";
        }

        private async Task DownloadAsync()
        {
            try
            {
                _download.IsEnabled = false;
                _open.IsEnabled = false;
                _progress.Visibility = Visibility.Visible;
                _status.Content = "正在下载并校验安装器...";
                var progress = new Progress<int>(p =>
                {
                    _progress.Value = p;
                    _status.Content = p + "%  正在下载...";
                });
                var path = await UpdateService.DownloadAndVerifyAsync(
                    _release, progress, System.Threading.CancellationToken.None);
                _status.Content = "SHA256 校验通过。";
                var answer = MessageBox.Show(
                    "安装器已下载并通过 SHA256 校验。\n\n" +
                    "请先保存工程，然后关闭 ArcGIS Pro。\n" +
                    "关闭后，更新器会自动运行安装器并重启 Pro。\n\n" +
                    "现在启动更新器吗？",
                    "安装更新", MessageBoxButton.OKCancel, MessageBoxImage.Question);
                if (answer != MessageBoxResult.OK)
                    return;
                UpdateService.LaunchDeferredInstaller(path);
                MessageBox.Show(
                    "更新器已启动。请关闭 ArcGIS Pro 以继续安装。",
                    "等待关闭 Pro", MessageBoxButton.OK, MessageBoxImage.Information);
                Close();
            }
            catch (Exception ex)
            {
                _download.IsEnabled = true;
                _open.IsEnabled = true;
                _status.Content = "更新失败：" + ex.Message;
                MessageBox.Show(ex.Message, "更新失败",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenRelease()
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = _release.HtmlUrl ?? AddinInfo.ReleasesPage,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "打开 Release 失败");
            }
        }
    }
}
