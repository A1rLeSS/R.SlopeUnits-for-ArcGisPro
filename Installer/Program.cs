using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BasinSlopeTools.Setup
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            var acceptLicense = Array.Exists(
                args, a => a.Equals("--accept-license", StringComparison.OrdinalIgnoreCase));
            if (args.Length > 0 && args[0].Equals("--dry-run", StringComparison.OrdinalIgnoreCase))
            {
                InstallerEngine.DryRun();
                return;
            }
            if (args.Length > 0 && args[0].Equals("--uninstall", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    InstallerEngine.Uninstall();
                    Environment.Exit(0);
                }
                catch (Exception ex)
                {
                    InstallerEngine.WriteLog("ERROR: " + ex);
                    Environment.Exit(1);
                }
            }
            if (args.Length > 0 && args[0].Equals("--silent", StringComparison.OrdinalIgnoreCase))
            {
                if (!acceptLicense)
                {
                    InstallerEngine.WriteLog(
                        "ERROR: license not accepted. Use --silent --accept-license.");
                    Environment.Exit(2);
                }
                try
                {
                    InstallerEngine.Install();
                    Environment.Exit(0);
                }
                catch (Exception ex)
                {
                    InstallerEngine.WriteLog("ERROR: " + ex);
                    Environment.Exit(1);
                }
            }

            ApplicationConfiguration.Initialize();
            Application.Run(new InstallerForm());
        }
    }

    internal sealed class InstallerForm : Form
    {
        private string _language = "en";
        private bool _accepted;
        private bool _busy;
        private int _step;

        private readonly Label _heading;
        private readonly Panel _page;
        private readonly Label _status;
        private readonly Button _back;
        private readonly Button _next;
        private readonly Button _cancel;
        private readonly Button _log;

        private RadioButton _english;
        private RadioButton _chinese;
        private RichTextBox _licenseBox;
        private CheckBox _acceptBox;
        private ProgressBar _progress;
        private Label _progressText;
        private RichTextBox _eventLog;
        private bool _settingLanguage;

        public InstallerForm()
        {
            Text = "R.slopeunits Setup";
            ClientSize = new Size(640, 520);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = false;
            AutoScaleMode = AutoScaleMode.Dpi;
            BackColor = Color.White;
            Font = new Font("Microsoft YaHei UI", 9F);

            _heading = new Label
            {
                AutoSize = true,
                Location = new Point(20, 12),
                Font = new Font("Microsoft YaHei UI", 14F, FontStyle.Bold),
                ForeColor = Color.FromArgb(35, 45, 48)
            };
            _page = new Panel
            {
                Location = new Point(20, 50),
                Size = new Size(600, 396),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            var footer = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 64,
                BackColor = Color.FromArgb(249, 250, 251)
            };
            _status = new Label
            {
                AutoSize = false,
                Location = new Point(16, 18),
                Size = new Size(300, 24),
                ForeColor = Color.DimGray
            };
            _back = MakeButton("Back", 80);
            _back.Location = new Point(326, 12);
            _back.Click += (_, _) => ShowLanguage();

            _next = MakeButton("Next", 100);
            _next.Location = new Point(414, 12);
            _next.Click += (_, _) => OnNext();

            _cancel = MakeButton("Cancel", 70);
            _cancel.Location = new Point(522, 12);
            _cancel.Click += (_, _) => Close();

            _log = MakeButton("Log", 90);
            _log.Location = new Point(414, 12);
            _log.Visible = false;
            _log.Click += (_, _) => OpenLog();

            footer.Controls.Add(_status);
            footer.Controls.Add(_back);
            footer.Controls.Add(_next);
            footer.Controls.Add(_cancel);
            footer.Controls.Add(_log);
            Controls.Add(_page);
            Controls.Add(_heading);
            Controls.Add(footer);

            ShowLanguage();
        }

        private static Button MakeButton(string text, int width)
        {
            return new Button
            {
                Text = text,
                Size = new Size(width, 32),
                FlatStyle = FlatStyle.System
            };
        }

        private string T(string en, string zh)
        {
            return _language == "zh" ? zh : en;
        }

        private void ShowLanguage()
        {
            _step = 0;
            _heading.Text = T("Setup language", "安装语言");
            _back.Visible = false;
            _next.Visible = true;
            _next.Text = T("Next", "下一步");
            _next.Enabled = true;
            _cancel.Visible = true;
            _log.Visible = false;
            _status.Text = "";
            _page.Controls.Clear();

            _english = new RadioButton
            {
                Text = "English",
                AutoSize = true,
                Location = new Point(4, 70),
                Checked = _language == "en",
                Font = new Font("Microsoft YaHei UI", 10F)
            };
            _chinese = new RadioButton
            {
                Text = "简体中文",
                AutoSize = true,
                Location = new Point(4, 104),
                Checked = _language == "zh",
                Font = new Font("Microsoft YaHei UI", 10F)
            };
            _english.CheckedChanged += (_, _) => SelectLanguage("en");
            _chinese.CheckedChanged += (_, _) => SelectLanguage("zh");

            _page.Controls.Add(new Label
            {
                Text = T("Choose the language for this installer.",
                        "请选择安装程序语言。"),
                AutoSize = true,
                Location = new Point(4, 16),
                ForeColor = Color.DimGray
            });
            _page.Controls.Add(_english);
            _page.Controls.Add(_chinese);
            SelectLanguage(_language);
        }

        private void SelectLanguage(string language)
        {
            if (_settingLanguage)
                return;
            _settingLanguage = true;
            try
            {
                _language = language;
                if (_english != null) _english.Checked = language == "en";
                if (_chinese != null) _chinese.Checked = language == "zh";
                Text = T("R.slopeunits Setup", "R.slopeunits 安装程序");
                if (_step == 0)
                {
                    _heading.Text = T("Setup language", "安装语言");
                    _back.Text = T("Back", "上一步");
                    _next.Text = T("Next", "下一步");
                    _cancel.Text = T("Cancel", "取消");
                    _status.Text = "";
                }
                else if (_step == 1)
                {
                    LoadLicense();
                }
            }
            finally
            {
                _settingLanguage = false;
            }
        }

        private void ShowLicense()
        {
            _step = 1;
            _heading.Text = T("Licence agreement", "许可协议");
            _back.Visible = true;
            _back.Text = T("Back", "上一步");
            _next.Visible = true;
            _next.Text = T("Install", "安装");
            _cancel.Visible = true;
            _log.Visible = false;
            _status.Text = T("Accept the licence to continue.", "请接受许可协议后继续。");
            _page.Controls.Clear();

            _licenseBox = new RichTextBox
            {
                ReadOnly = true,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Consolas", 8.5F),
                WordWrap = true,
                ScrollBars = RichTextBoxScrollBars.Vertical,
                Location = new Point(0, 0),
                Size = new Size(600, 300),
                BackColor = Color.FromArgb(250, 251, 252)
            };
            _acceptBox = new CheckBox
            {
                AutoSize = true,
                Location = new Point(0, 312),
                Checked = _accepted
            };
            _acceptBox.CheckedChanged += (_, _) =>
            {
                _accepted = _acceptBox.Checked;
                _next.Enabled = _accepted;
                _status.Text = _accepted
                    ? T("Licence accepted.", "许可已接受。")
                    : T("Accept the licence to continue.", "请接受许可协议后继续。");
            };
            _page.Controls.Add(_licenseBox);
            _page.Controls.Add(_acceptBox);
            LoadLicense();
        }

        private void LoadLicense()
        {
            if (_licenseBox == null)
                return;
            _licenseBox.Text = ReadResource(
                _language == "zh" ? "AGREEMENT_zh.txt" : "AGREEMENT_en.txt")
                + Environment.NewLine + Environment.NewLine
                + ReadResource("GPL-2.0.txt");
            _licenseBox.SelectionStart = 0;
            if (_acceptBox != null)
            {
                _acceptBox.Text = T("I have read and accept the licence agreement",
                                    "我已阅读并同意许可协议");
            }
            _next.Enabled = _accepted;
        }

        private void ShowInstall()
        {
            _step = 2;
            _heading.Text = T("Installing...", "正在安装...");
            _back.Visible = false;
            _next.Visible = false;
            _cancel.Visible = false;
            _log.Visible = false;
            _status.Text = T("Installing. Please wait.", "正在安装，请稍候。");
            _page.Controls.Clear();
            _progressText = new Label
            {
                AutoSize = false,
                Location = new Point(0, 0),
                Size = new Size(600, 22),
                Text = "0%",
                ForeColor = Color.FromArgb(60, 75, 80)
            };
            _progress = new ProgressBar
            {
                Style = ProgressBarStyle.Continuous,
                Minimum = 0,
                Maximum = 100,
                Value = 0,
                Size = new Size(600, 18),
                Location = new Point(0, 28)
            };
            _eventLog = new RichTextBox
            {
                ReadOnly = true,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Consolas", 8.5F),
                WordWrap = false,
                ScrollBars = RichTextBoxScrollBars.Vertical,
                Location = new Point(0, 56),
                Size = new Size(600, 300),
                BackColor = Color.FromArgb(250, 251, 252)
            };
            _page.Controls.Add(_progressText);
            _page.Controls.Add(_progress);
            _page.Controls.Add(_eventLog);
        }

        private void ShowFinish(bool success, string message)
        {
            _step = 3;
            _heading.Text = success
                ? T("Installation complete", "安装完成")
                : T("Installation failed", "安装失败");
            _back.Visible = false;
            _next.Visible = true;
            _next.Text = T("Close", "关闭");
            _next.Enabled = true;
            _cancel.Visible = false;
            _log.Visible = true;
            _log.Location = new Point(414, 12);
            _status.Text = "";
            _page.Controls.Clear();
            _page.Controls.Add(new Label
            {
                Text = message,
                AutoSize = false,
                Size = new Size(600, 150),
                Location = new Point(0, 20),
                ForeColor = success ? Color.FromArgb(20, 100, 70) : Color.FromArgb(160, 30, 30)
            });
        }

        private void OnNext()
        {
            if (_step == 0)
            {
                ShowLicense();
                return;
            }
            if (_step == 1)
            {
                if (!_accepted)
                {
                    MessageBox.Show(
                        T("Please accept the licence agreement.",
                          "请先勾选同意许可协议。"),
                        T("Licence", "许可协议"),
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                _ = StartInstallAsync();
                return;
            }
            Close();
        }

        private async Task StartInstallAsync()
        {
            _busy = true;
            ShowInstall();
            var progress = new Progress<string>(text =>
            {
                _status.Text = Localize(text);
            });
            var detailed = new Progress<InstallEvent>(e =>
            {
                int value = Math.Max(0, Math.Min(100, e.Percent));
                _progress.Value = value;
                _progressText.Text = value + "%  " + Localize(e.Message);
                _status.Text = Localize(e.Message);
                if (!string.IsNullOrWhiteSpace(e.Detail))
                    AppendEvent(Localize(e.Detail, true));
            });
            try
            {
                await Task.Run(() => InstallerEngine.Install(progress, detailed));
                _progress.Value = 100;
                _progressText.Text = "100%  " + T("Installation complete", "安装完成");
                ShowFinish(true, T(
                    "Add-in, Tools and GRASS runtime are installed.\n\n" +
                    "Restart ArcGIS Pro and open the R.slopeunits tab.\n" +
                    "On first use, allow the Python Toolbox when Pro asks.",
                    "Add-in、Tools 和 GRASS runtime 已安装。\n\n" +
                    "请重新启动 ArcGIS Pro，打开 R.slopeunits 选项卡。\n" +
                    "首次使用 Python Toolbox 时，按 Pro 提示允许运行。"));
            }
            catch (Exception ex)
            {
                InstallerEngine.WriteLog("ERROR: " + ex);
                ShowFinish(false, T(
                    "Installation failed. See the log for details.",
                    "安装失败。请查看日志了解详情。"));
            }
            finally
            {
                _busy = false;
            }
        }

        private void AppendEvent(string text)
        {
            if (_eventLog == null || string.IsNullOrWhiteSpace(text))
                return;
            _eventLog.AppendText(DateTime.Now.ToString("HH:mm:ss") + "  " + text + "\r\n");
            _eventLog.SelectionStart = _eventLog.TextLength;
            _eventLog.ScrollToCaret();
        }

        private string Localize(string text, bool detail = false)
        {
            if (string.IsNullOrWhiteSpace(text))
                return text;
            if (_language == "en")
            {
                text = text.Replace("检查运行环境...", "Checking environment...")
                    .Replace("环境检查通过", "Environment check passed")
                    .Replace("解压安装包（约 0.8 GB）...", "Extracting package (~0.8 GB)...")
                    .Replace("安装包解压完成", "Package extracted")
                    .Replace("安装 Add-in、Tools 和 GRASS runtime...", "Installing add-in, Tools and GRASS runtime...")
                    .Replace("写入注册表和卸载入口...", "Writing registry and uninstall entry...")
                    .Replace("安装完成", "Installation complete")
                    .Replace("正在安装...", "Installing...");
            }
            else
            {
                text = text.Replace("Installing Add-in...", "正在安装 Add-in...")
                    .Replace("Deploying Tools...", "正在部署 Tools...")
                    .Replace("Copying GRASS runtime (this may take a few minutes)...", "正在复制 GRASS runtime（可能需要几分钟）...")
                    .Replace("Registering uninstall entry...", "正在写入卸载注册表...")
                    .Replace("Installation complete.", "安装完成。")
                    .Replace("Add-in:", "Add-in：")
                    .Replace("GRASS:", "GRASS：")
                    .Replace("Uninstall:", "卸载入口：")
                    .Replace("Close and reopen ArcGIS Pro. The 'R.slopeunits Slope Units' tab should appear.", "请关闭并重新打开 ArcGIS Pro，顶部应出现 R.slopeunits 选项卡。")
                    .Replace("If not, enable BasinSlopeTools in the ArcGIS Pro Add-In Manager.", "如果没有出现，请在 ArcGIS Pro Add-In Manager 中启用 BasinSlopeTools。");
            }
            return text;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_busy)
            {
                e.Cancel = true;
                MessageBox.Show(
                    T("Installation is still running. Please wait.",
                      "安装仍在进行，请稍候。"),
                    T("Please wait", "请稍候"),
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            base.OnFormClosing(e);
        }

        private static string ReadResource(string name)
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
            if (stream == null)
                return "";
            using var reader = new StreamReader(stream, Encoding.UTF8);
            return reader.ReadToEnd();
        }

        private static void OpenLog()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = InstallerEngine.LogPath,
                    UseShellExecute = true
                });
            }
            catch { }
        }
    }
}


