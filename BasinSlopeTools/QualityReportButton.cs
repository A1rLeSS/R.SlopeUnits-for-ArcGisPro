using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using ArcGIS.Desktop.Framework.Contracts;
using Microsoft.Web.WebView2.Wpf;

namespace BasinSlopeTools
{
    internal class QualityReportButton : ArcGIS.Desktop.Framework.Contracts.Button
    {
        protected override void OnClick()
        {
            var local = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "BasinSlopeTools");
            var latest = Path.Combine(local, "latest_quality_report.html");
            var entries = LoadHistory(local);
            if (entries.Count == 0 && !File.Exists(latest))
            {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(
                    "还没有质量报告。\n请先运行一次“生成斜坡单元”。",
                    "质量报告", System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
                return;
            }
            if (entries.Count == 0)
                entries.Add(new HistoryEntry
                {
                    Label = "当前报告",
                    Html = latest,
                    CompareHtml = null
                });

            var window = new Window
            {
                Title = "R.slopeunits 质量报告",
                Width = 1100,
                Height = 780,
                WindowStartupLocation = WindowStartupLocation.CenterScreen
            };

            var history = new System.Windows.Controls.ComboBox
            {
                Width = 360,
                Margin = new Thickness(0, 0, 8, 0),
                DisplayMemberPath = "Label",
                ItemsSource = entries,
                SelectedIndex = entries.Count - 1
            };

            var refresh = MakeButton("刷新", 72);
            var compare = MakeButton("与上一份对比", 120);
            var folder = MakeButton("打开所在文件夹", 120);
            var browser = MakeButton("用浏览器打开", 110);
            var close = MakeButton("关闭", 64);

            var bar = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(8, 6, 8, 6)
            };
            bar.Children.Add(history);
            bar.Children.Add(refresh);
            bar.Children.Add(compare);
            bar.Children.Add(folder);
            bar.Children.Add(browser);
            bar.Children.Add(close);

            var webView = new WebView2();
            var fallback = new WebBrowser { Visibility = Visibility.Collapsed };
            var panel = new Grid();
            panel.Children.Add(webView);
            panel.Children.Add(fallback);

            var dock = new DockPanel();
            DockPanel.SetDock(bar, Dock.Top);
            dock.Children.Add(bar);
            dock.Children.Add(panel);
            window.Content = dock;

            var initialized = false;
            var fallbackMode = false;

            string CurrentPath()
            {
                return history.SelectedItem is HistoryEntry e ? e.Html : latest;
            }

            void Navigate(string path)
            {
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                    return;
                if (fallbackMode)
                {
                    fallback.Navigate(new Uri(path));
                    return;
                }
                if (initialized)
                    webView.Source = new Uri(path);
            }

            void UseFallback(string reason)
            {
                fallbackMode = true;
                webView.Visibility = Visibility.Collapsed;
                fallback.Visibility = Visibility.Visible;
                try
                {
                    ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(
                        "WebView2 不可用，已切换为兼容报告窗口。\n" + reason,
                        "质量报告", System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning);
                }
                catch
                {
                }
                Navigate(CurrentPath());
            }

            history.SelectionChanged += (_, __) => Navigate(CurrentPath());
            refresh.Click += (_, __) => Navigate(CurrentPath());
            compare.Click += (_, __) =>
            {
                if (history.SelectedItem is HistoryEntry e &&
                    !string.IsNullOrWhiteSpace(e.CompareHtml) &&
                    File.Exists(e.CompareHtml))
                    Navigate(e.CompareHtml);
                else
                    ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(
                        "当前报告没有可用的上一份对比记录。");
            };
            folder.Click += (_, __) => OpenFolder(CurrentPath());
            browser.Click += (_, __) => OpenExternal(CurrentPath());
            close.Click += (_, __) => window.Close();

            window.Loaded += async (_, __) =>
            {
                try
                {
                    await webView.EnsureCoreWebView2Async(null);
                    initialized = true;
                    Navigate(CurrentPath());
                }
                catch (Exception ex)
                {
                    UseFallback(ex.Message);
                }
            };
            window.ShowDialog();
        }

        private static System.Windows.Controls.Button MakeButton(string text, double width)
        {
            return new System.Windows.Controls.Button
            {
                Content = text,
                Width = width,
                Margin = new Thickness(0, 0, 8, 0)
            };
        }

        private static List<HistoryEntry> LoadHistory(string local)
        {
            var list = new List<HistoryEntry>();
            var index = Path.Combine(local, "quality_reports.json");
            try
            {
                if (File.Exists(index))
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(index));
                    if (doc.RootElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in doc.RootElement.EnumerateArray())
                        {
                            var html = GetString(item, "html");
                            if (string.IsNullOrWhiteSpace(html) || !File.Exists(html))
                                continue;
                            list.Add(new HistoryEntry
                            {
                                Label = GetString(item, "label") ?? Path.GetFileName(html),
                                Html = html,
                                CompareHtml = GetString(item, "compare_html")
                            });
                        }
                    }
                }
            }
            catch
            {
            }
            return list;
        }

        private static string GetString(JsonElement item, string name)
        {
            return item.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
                ? v.GetString()
                : null;
        }

        private static void OpenFolder(string path)
        {
            try
            {
                if (!File.Exists(path)) return;
                Process.Start(new ProcessStartInfo(
                    "explorer.exe", "/select,\"" + path + "\"")
                { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(
                    "打开文件夹失败：\n" + ex.Message);
            }
        }

        private static void OpenExternal(string path)
        {
            try
            {
                if (!File.Exists(path)) return;
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(
                    "打开浏览器失败：\n" + ex.Message);
            }
        }

        private class HistoryEntry
        {
            public string Label { get; set; }
            public string Html { get; set; }
            public string CompareHtml { get; set; }
        }
    }
}
