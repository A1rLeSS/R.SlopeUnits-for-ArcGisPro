using System;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using ArcGIS.Desktop.Framework.Contracts;
using Microsoft.Web.WebView2.Wpf;

namespace BasinSlopeTools
{
    internal class AboutButton : ArcGIS.Desktop.Framework.Contracts.Button
    {
        protected override void OnClick()
        {
            AboutWindow.Show();
        }
    }

    internal static class AboutWindow
    {
        private static string Read(string name)
        {
            var assembly = Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream(name);
            if (stream == null)
                return "(missing resource: " + name + ")";
            using var reader = new StreamReader(stream, Encoding.UTF8);
            return reader.ReadToEnd();
        }

        public static void Show()
        {
            var notices = Read("THIRD_PARTY_NOTICES.txt");
            var source = Read("SOURCE_OFFER.txt");
            var grass = Read("GRASS_COPYRIGHT.txt");
            var gpl = Read("GPL-2.0.txt");

            var plain = new StringBuilder()
                .AppendLine("R.slopeunits 斜坡单元 / BasinSlopeTools")
                .AppendLine("Version " + AddinInfo.DisplayVersion)
                .AppendLine("Copyright (C) 2026 BasinSlopeTools Project")
                .AppendLine("License: GPL-2.0-or-later")
                .AppendLine("Contact: leomilk362@gmail.com")
                .AppendLine()
                .AppendLine(source)
                .AppendLine(notices)
                .AppendLine("GRASS copyright:")
                .AppendLine(grass)
                .AppendLine("GNU GPL v2:")
                .AppendLine(gpl)
                .ToString();

            var html = @"<!DOCTYPE html><html lang=""zh-CN""><head><meta charset=""utf-8"">
<title>R.slopeunits 关于/版权</title>
<style>body{font-family:'Microsoft YaHei',Arial;margin:28px;color:#222;line-height:1.55}
h1{font-size:24px}h2{font-size:17px;margin-top:24px}pre{white-space:pre-wrap;background:#f6f8fb;padding:12px;border:1px solid #ddd;font-family:Consolas,monospace;font-size:12px}
.meta{background:#eef4ff;padding:12px;border-left:4px solid #1c488c}</style></head><body>
<h1>R.slopeunits 斜坡单元</h1>
<div class=""meta"">
<p><b>项目：</b>BasinSlopeTools / R.slopeunits 斜坡单元</p>
<p><b>版本：</b>0.6</p>
<p><b>版权：</b>Copyright (C) 2026 BasinSlopeTools Project</p>
<p><b>许可证：</b>GNU General Public License v2.0 or later (GPL-2.0-or-later)</p>
<p><b>联系方式：</b>leomilk362@gmail.com</p>
</div>
<h2>源码获取</h2><pre>" + WebUtility.HtmlEncode(source) + @"</pre>
<h2>第三方组件与许可</h2><pre>" + WebUtility.HtmlEncode(notices) + @"</pre>
<h2>GRASS 版权声明</h2><pre>" + WebUtility.HtmlEncode(grass) + @"</pre>
<h2>GNU GPL v2 全文</h2><pre>" + WebUtility.HtmlEncode(gpl) + @"</pre>
</body></html>";

            var window = new Window
            {
                Title = "R.slopeunits 关于/版权",
                Width = 980,
                Height = 760,
                WindowStartupLocation = WindowStartupLocation.CenterScreen
            };

            var close = new System.Windows.Controls.Button
            {
                Content = "关闭",
                Width = 72,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(8, 6, 8, 6)
            };
            var bar = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            bar.Children.Add(close);

            var web = new WebView2();
            var fallback = new TextBox
            {
                Text = plain,
                IsReadOnly = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                FontSize = 12,
                Visibility = Visibility.Collapsed,
                Margin = new Thickness(8)
            };
            var panel = new Grid();
            panel.Children.Add(web);
            panel.Children.Add(fallback);

            var dock = new DockPanel();
            DockPanel.SetDock(bar, Dock.Top);
            dock.Children.Add(bar);
            dock.Children.Add(panel);
            window.Content = dock;
            close.Click += (_, __) => window.Close();

            window.Loaded += async (_, __) =>
            {
                try
                {
                    await web.EnsureCoreWebView2Async(null);
                    web.NavigateToString(html);
                }
                catch
                {
                    web.Visibility = Visibility.Collapsed;
                    fallback.Visibility = Visibility.Visible;
                }
            };
            window.ShowDialog();
        }
    }
}




