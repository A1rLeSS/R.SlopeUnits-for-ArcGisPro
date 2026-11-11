using System;
using System.Diagnostics;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
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
            Application.Run(new InstallerForm(acceptLicense));
        }
    }

    internal sealed class InstallerForm : Form
    {
        private const int StepWelcome = 0;
        private const int StepLicense = 1;
        private const int StepConfirm = 2;
        private const int StepProgress = 3;
        private const int StepFinish = 4;

        private StepIndicator _stepIndicator;
        private readonly Label _pageTitle;
        private readonly Label _pageSubtitle;
        private readonly Panel _content;
        private readonly Panel _welcomePage;
        private readonly Panel _licensePage;
        private readonly Panel _confirmPage;
        private readonly Panel _progressPage;
        private readonly Panel _finishPage;
        private readonly RichTextBox _licenseText;
        private readonly CheckBox _acceptLicense;
        private readonly ProgressBar _progress;
        private readonly Label _status;
        private Label _progressText;
        private RichTextBox _eventLog;
        private Label _finishBody;
        private readonly Button _backButton;
        private readonly Button _nextButton;
        private readonly Button _cancelButton;
        private readonly Button _logButton;
        private int _step = StepWelcome;
        private bool _licenseAccepted;
        private bool _busy;

        public InstallerForm(bool licenseAccepted)
        {
            _licenseAccepted = licenseAccepted;
            Text = "R.slopeunits 斜坡单元 安装程序";
            ClientSize = new Size(920, 720);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = false;
            AutoScaleMode = AutoScaleMode.Dpi;
            BackColor = Color.White;
            Font = new Font("Microsoft YaHei UI", 9F);
            try
            {
                using var appIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                if (appIcon != null) Icon = (Icon)appIcon.Clone();
            }
            catch { }

            var header = BuildHeader();
            _content = new Panel { Dock = DockStyle.Fill, Padding = new Padding(28, 14, 28, 0) };

            _pageTitle = new Label
            {
                Font = new Font("Microsoft YaHei UI", 18F, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 55, 60),
                AutoSize = true,
                Location = new Point(0, 0)
            };
            _pageSubtitle = new Label
            {
                Font = new Font("Microsoft YaHei UI", 9F),
                ForeColor = Color.DimGray,
                AutoSize = true,
                Location = new Point(0, 36)
            };
            _content.Controls.Add(_pageTitle);
            _content.Controls.Add(_pageSubtitle);

            _licenseText = new RichTextBox
            {
                ReadOnly = true,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Consolas", 9F),
                BackColor = Color.FromArgb(250, 251, 252),
                WordWrap = true,
                ScrollBars = RichTextBoxScrollBars.Vertical,
                Text = LicenseInfo.Text,
                Location = new Point(0, 0),
                Size = new Size(820, 340),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
            };
            _acceptLicense = new CheckBox
            {
                Text = "我已阅读并同意 GPL-2.0-or-later 及第三方许可",
                AutoSize = true,
                Location = new Point(0, 350),
                Checked = licenseAccepted
            };

            _welcomePage = BuildWelcomePage();
            _licensePage = BuildLicensePage();
            _confirmPage = BuildConfirmPage();
            _progressPage = BuildProgressPage();
            _finishPage = BuildFinishPage();
            var pageHost = new Panel
            {
                Location = new Point(0, 72),
                Size = new Size(864, 410),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            _content.Controls.Add(pageHost);
            foreach (var page in new[] { _welcomePage, _licensePage, _confirmPage, _progressPage, _finishPage })
            {
                page.Dock = DockStyle.Fill;
                pageHost.Controls.Add(page);
            }

            var footer = BuildFooter(out _backButton, out _nextButton, out _cancelButton,
                                     out _logButton, out _status);
            _progress = (ProgressBar)_progressPage.Controls["progressBar"];

            Controls.Add(_content);
            Controls.Add(footer);
            Controls.Add(header);

            _backButton.Click += (_, _) => ShowStep(_step - 1);
            _nextButton.Click += (_, _) => OnNext();
            _cancelButton.Click += (_, _) => Close();
            _logButton.Click += (_, _) => OpenLog();
            _acceptLicense.CheckedChanged += (_, _) =>
            {
                _licenseAccepted = _acceptLicense.Checked;
                if (_step == StepLicense)
                    _nextButton.Enabled = _licenseAccepted;
            };

            ShowStep(StepWelcome);
        }

        private Panel BuildHeader()
        {
            var header = new GradientHeader
            {
                Dock = DockStyle.Top,
                Height = 132
            };
            var logo = new PictureBox
            {
                Size = new Size(72, 72),
                Location = new Point(28, 26),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Transparent
            };
            try
            {
                using var stream = Assembly.GetExecutingAssembly()
                    .GetManifestResourceStream("BRAND.png");
                if (stream != null)
                {
                    using var image = Image.FromStream(stream);
                    logo.Image = new Bitmap(image);
                }
            }
            catch { }
            header.Controls.Add(logo);
            header.Controls.Add(new Label
            {
                Text = "R.slopeunits 斜坡单元",
                Font = new Font("Microsoft YaHei UI", 22F, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(124, 20),
                BackColor = Color.Transparent
            });
            header.Controls.Add(new Label
            {
                Text = "ArcGIS Pro 插件安装程序 · Version 0.4",
                ForeColor = Color.FromArgb(220, 245, 240),
                AutoSize = true,
                Location = new Point(128, 62),
                BackColor = Color.Transparent
            });
            header.Controls.Add(new Label
            {
                Text = "在 ArcGIS Pro 中直接生成斜坡单元 · 自带 GRASS 8.5.0",
                ForeColor = Color.FromArgb(200, 235, 228),
                AutoSize = true,
                Location = new Point(128, 88),
                BackColor = Color.Transparent
            });
            _stepIndicator = new StepIndicator
            {
                Location = new Point(560, 86),
                Size = new Size(330, 38)
            };
            header.Controls.Add(_stepIndicator);
            return header;
        }

        private Panel BuildWelcomePage()
        {
            var page = new Panel { BackColor = Color.White };
            page.Controls.Add(new Label
            {
                Text = "本向导将安装 ArcGIS Pro Add-in 和内置 GRASS 8.5.0 runtime。\n" +
                       "安装完成后，Pro 顶部会出现“R.slopeunits 斜坡单元”选项卡。",
                AutoSize = true,
                Location = new Point(0, 0),
                ForeColor = Color.FromArgb(55, 70, 70)
            });
            var card = new CardPanel
            {
                Location = new Point(0, 62),
                Size = new Size(820, 125),
                BackColor = Color.FromArgb(242, 248, 247)
            };
            card.Controls.Add(new Label
            {
                Text = "安装内容\n" +
                       "• Add-in：生成斜坡单元、批量、参数扫描、质量报告、关于/版权\n" +
                       "• GRASS 8.5.0 便携 runtime：无需另行安装 GRASS\n" +
                       "• 输出：GeoPackage、可选 GDB、质量报告、汇总 CSV",
                AutoSize = true,
                MaximumSize = new Size(780, 0),
                Location = new Point(16, 14),
                ForeColor = Color.FromArgb(40, 60, 60)
            });
            page.Controls.Add(card);
            page.Controls.Add(new Label
            {
                Text = "系统要求：ArcGIS Pro 3.0–3.7（64-bit） · Windows 10/11 · 至少 1.6 GB 可用空间。\n" +
                       "安装前请关闭 ArcGIS Pro。",
                AutoSize = true,
                Location = new Point(0, 222),
                ForeColor = Color.FromArgb(90, 100, 100)
            });
            var uninstall = new ModernButton
            {
                Text = "卸载现有安装",
                Size = new Size(130, 32),
                Location = new Point(0, 290),
                Primary = false
            };
            uninstall.Click += (_, _) => OnUninstallFromWelcome();
            page.Controls.Add(uninstall);
            return page;
        }

        private Panel BuildLicensePage()
        {
            var page = new Panel { BackColor = Color.White };
            _licenseText.Location = new Point(0, 0);
            _licenseText.Anchor = AnchorStyles.Top | AnchorStyles.Bottom |
                                   AnchorStyles.Left | AnchorStyles.Right;
            _licenseText.Dock = DockStyle.Fill;

            var acceptPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 54,
                BackColor = Color.FromArgb(248, 250, 251)
            };
            _acceptLicense.Location = new Point(8, 16);
            acceptPanel.Controls.Add(_acceptLicense);

            page.Controls.Add(_licenseText);
            page.Controls.Add(acceptPanel);
            acceptPanel.BringToFront();
            return page;
        }

        private Panel BuildConfirmPage()
        {
            var page = new Panel { BackColor = Color.White };
            var info = new Label
            {
                Text = "即将安装以下内容：\n\n" +
                       "  • ArcGIS Pro Add-in\n" +
                       "  • GRASS 8.5.0 portable runtime\n" +
                       "  • Tools 与许可文件\n\n" +
                       "安装位置：\n" +
                       "  Add-in：当前用户 Documents\\ArcGIS\\AddIns\\ArcGISPro\\...\n" +
                       "  Runtime：%LOCALAPPDATA%\\BasinSlopeTools\\runtime\\grass\n\n" +
                       "安装期间请勿关闭安装程序。\n" +
                       "安装失败会自动回滚。",
                AutoSize = true,
                Location = new Point(0, 0),
                ForeColor = Color.FromArgb(45, 60, 60)
            };
            page.Controls.Add(info);
            return page;
        }

        private Panel BuildProgressPage()
        {
            var page = new Panel { BackColor = Color.White };
            page.Controls.Add(new Label
            {
                Text = "正在安装，请稍候（约 1–3 分钟）...",
                Font = new Font("Microsoft YaHei UI", 13F, FontStyle.Bold),
                ForeColor = Color.FromArgb(10, 100, 92),
                AutoSize = true,
                Location = new Point(0, 0)
            });
            _progressText = new Label
            {
                Text = "0%",
                AutoSize = false,
                Size = new Size(820, 24),
                Location = new Point(0, 36),
                ForeColor = Color.FromArgb(70, 85, 85)
            };
            page.Controls.Add(_progressText);
            page.Controls.Add(new ProgressBar
            {
                Name = "progressBar",
                Style = ProgressBarStyle.Continuous,
                Minimum = 0,
                Maximum = 100,
                Value = 0,
                Size = new Size(820, 22),
                Location = new Point(0, 64)
            });
            _eventLog = new RichTextBox
            {
                ReadOnly = true,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.FromArgb(248, 250, 251),
                Font = new Font("Consolas", 8.5F),
                Location = new Point(0, 100),
                Size = new Size(820, 286),
                ScrollBars = RichTextBoxScrollBars.Vertical,
                WordWrap = false
            };
            page.Controls.Add(_eventLog);
            return page;
        }

        private Panel BuildFinishPage()
        {
            var page = new Panel { BackColor = Color.White };
            page.Controls.Add(new Label
            {
                Text = "完成",
                Font = new Font("Microsoft YaHei UI", 16F, FontStyle.Bold),
                ForeColor = Color.FromArgb(10, 100, 92),
                AutoSize = true,
                Location = new Point(0, 10)
            });
            _finishBody = new Label
            {
                AutoSize = true,
                Location = new Point(0, 50),
                MaximumSize = new Size(820, 280),
                ForeColor = Color.FromArgb(45, 60, 60)
            };
            page.Controls.Add(_finishBody);
            return page;
        }

        private Panel BuildFooter(out Button back, out Button next, out Button cancel,
                                  out Button log, out Label status)
        {
            var footer = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 74,
                BackColor = Color.FromArgb(248, 250, 251)
            };
            status = new Label
            {
                AutoSize = false,
                Size = new Size(450, 24),
                Location = new Point(24, 26),
                ForeColor = Color.DimGray
            };
            footer.Controls.Add(status);
            footer.Paint += (_, e) =>
            {
                using var borderPen = new Pen(Color.FromArgb(225, 232, 234));
                e.Graphics.DrawLine(borderPen, 0, 0, footer.Width, 0);
            };

            back = new ModernButton
            {
                Text = "上一步",
                Size = new Size(100, 38),
                Location = new Point(500, 18),
                Primary = false
            };
            next = new ModernButton
            {
                Text = "下一步",
                Size = new Size(120, 38),
                Location = new Point(610, 18),
                Primary = true
            };
            cancel = new ModernButton
            {
                Text = "取消",
                Size = new Size(90, 38),
                Location = new Point(740, 18),
                Primary = false
            };
            log = new ModernButton
            {
                Text = "查看日志",
                Size = new Size(100, 38),
                Location = new Point(610, 18),
                Primary = false,
                Visible = false
            };
            footer.Controls.Add(back);
            footer.Controls.Add(next);
            footer.Controls.Add(cancel);
            footer.Controls.Add(log);
            return footer;
        }

        private void ShowStep(int step)
        {
            _step = step;
            _welcomePage.Visible = step == StepWelcome;
            _licensePage.Visible = step == StepLicense;
            _confirmPage.Visible = step == StepConfirm;
            _progressPage.Visible = step == StepProgress;
            _finishPage.Visible = step == StepFinish;

            _backButton.Visible = step == StepLicense || step == StepConfirm;
            _nextButton.Visible = step <= StepConfirm || step == StepFinish;
            _cancelButton.Visible = step <= StepConfirm;
            _logButton.Visible = step == StepFinish;
            _logButton.Location = step == StepFinish
                ? new Point(500, 18)
                : new Point(610, 18);
            _nextButton.Location = new Point(610, 18);

            _nextButton.Enabled = step != StepLicense || _licenseAccepted;
            _nextButton.Text = step == StepConfirm ? "开始安装" : "下一步";
            if (step == StepFinish)
                _nextButton.Text = "关闭";
            _backButton.Enabled = !_busy;
            _cancelButton.Enabled = !_busy;
            _acceptLicense.Enabled = !_busy;

            switch (step)
            {
                case StepWelcome:
                    _stepIndicator.Step = 0;
                    _pageTitle.Text = "欢迎";
                    _pageSubtitle.Text = "了解安装内容并继续。";
                    _status.Text = "准备就绪。";
                    break;
                case StepLicense:
                    _stepIndicator.Step = 1;
                    _pageTitle.Text = "许可协议";
                    _pageSubtitle.Text = "必须阅读并接受 GPL-2.0-or-later 及第三方许可后才能安装。";
                    _status.Text = _licenseAccepted ? "许可已接受。" : "请勾选同意后继续。";
                    break;
                case StepConfirm:
                    _stepIndicator.Step = 2;
                    _pageTitle.Text = "准备安装";
                    _pageSubtitle.Text = "确认安装内容，然后开始安装。";
                    _status.Text = "点击“开始安装”写入 Add-in、Tools 和 runtime。";
                    break;
                case StepProgress:
                    _stepIndicator.Step = 2;
                    _pageTitle.Text = "正在安装";
                    _pageSubtitle.Text = "请稍候，安装完成前不要关闭本窗口。";
                    _status.Text = "正在安装...";
                    break;
                case StepFinish:
                    _stepIndicator.Step = 2;
                    _pageTitle.Text = "安装结果";
                    _pageSubtitle.Text = "";
                    break;
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_busy)
            {
                e.Cancel = true;
                MessageBox.Show("安装/卸载正在进行中，请等待完成后再关闭窗口。",
                    "请稍候", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            base.OnFormClosing(e);
        }

        private void OnNext()
        {            if (_step == StepFinish)
            {
                Close();
                return;
            }
            if (_step == StepLicense && !_licenseAccepted)
            {
                MessageBox.Show("必须接受许可协议才能继续。", "许可协议",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (_step < StepConfirm)
            {
                ShowStep(_step + 1);
                return;
            }
            if (_step == StepConfirm)
                _ = StartInstallAsync();
        }

        private async Task StartInstallAsync()
        {
            if (!_licenseAccepted)
            {
                MessageBox.Show("必须接受许可协议才能安装。", "许可协议",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            _busy = true;
            ShowStep(StepProgress);
            _logButton.Visible = false;
            _nextButton.Visible = false;
            _cancelButton.Visible = false;
            _backButton.Visible = false;
            _progress.Value = 0;
            _progressText.Text = "0%";
            _eventLog.Clear();
            var progress = new Progress<string>(msg => _status.Text = msg);
            var detailed = new Progress<InstallEvent>(e =>
            {
                int value = Math.Max(0, Math.Min(100, e.Percent));
                _progress.Value = value;
                _progressText.Text = value + "%  ·  " + e.Message;
                _status.Text = e.Message;
                AppendEvent(string.IsNullOrWhiteSpace(e.Detail) ? e.Message : e.Detail);
            });
            try
            {
                await Task.Run(() => InstallerEngine.Install(progress, detailed));
                _progress.Value = 100;
                _progressText.Text = "100%  ·  安装完成";
                _finishBody.Text = "安装完成。\n\n" +
                    "请关闭并重新打开 ArcGIS Pro，顶部会出现“R.slopeunits 斜坡单元”选项卡。\n" +
                    "首次打开 Python 工具时，Pro 可能会询问是否允许运行工具箱代码，点击 Yes 即可。\n\n" +
                    "卸载入口：Windows“应用和功能”，或安装目录中的 uninstall.cmd。";
                _status.Text = "安装完成。";
            }
            catch (Exception ex)
            {
                _finishBody.Text = "安装失败：\n" + ex.Message +
                    "\n\n安装器会尝试回滚；日志：" + InstallerEngine.LogPath;
                _status.Text = "安装失败。";
                InstallerEngine.WriteLog("ERROR: " + ex);
            }
            finally
            {
                _busy = false;
                _progress.Visible = false;
                _logButton.Visible = true;
                ShowStep(StepFinish);
            }
        }

        private async void OnUninstallFromWelcome()
        {
            if (MessageBox.Show(
                    "确定卸载 R.slopeunits 斜坡单元插件和 GRASS runtime？\n" +
                    "不会删除你的 DEM 和结果数据。",
                    "确认卸载", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;

            _busy = true;
            ShowStep(StepProgress);
            _pageTitle.Text = "正在卸载";
            _pageSubtitle.Text = "请稍候。";
            _nextButton.Visible = false;
            _cancelButton.Visible = false;
            _backButton.Visible = false;
            _progress.Value = 0;
            _progressText.Text = "0%";
            _eventLog.Clear();
            var progress = new Progress<string>(msg => _status.Text = msg);
            var detailed = new Progress<InstallEvent>(e =>
            {
                int value = Math.Max(0, Math.Min(100, e.Percent));
                _progress.Value = value;
                _progressText.Text = value + "%  ·  " + e.Message;
                _status.Text = e.Message;
                AppendEvent(string.IsNullOrWhiteSpace(e.Detail) ? e.Message : e.Detail);
            });
            try
            {
                await Task.Run(() => InstallerEngine.Uninstall(progress, detailed));
                _progress.Value = 100;
                _progressText.Text = "100%  ·  卸载完成";
                _finishBody.Text = "卸载完成。\n\n请重新打开 ArcGIS Pro。";
                _status.Text = "卸载完成。";
            }
            catch (Exception ex)
            {
                _finishBody.Text = "卸载失败：\n" + ex.Message +
                    "\n\n日志：" + InstallerEngine.LogPath;
                _status.Text = "卸载失败。";
                InstallerEngine.WriteLog("UNINSTALL ERROR: " + ex);
            }
            finally
            {
                _busy = false;
                _progress.Visible = false;
                _logButton.Visible = true;
                ShowStep(StepFinish);
            }
        }

        private void AppendEvent(string line)
        {
            if (_eventLog == null || string.IsNullOrWhiteSpace(line)) return;
            _eventLog.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + line + "\r\n");
            _eventLog.SelectionStart = _eventLog.TextLength;
            _eventLog.ScrollToCaret();
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

    internal sealed class GradientHeader : Panel
    {
        public GradientHeader()
        {
            DoubleBuffered = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            using var brush = new LinearGradientBrush(
                ClientRectangle,
                Color.FromArgb(9, 96, 89),
                Color.FromArgb(22, 154, 135),
                LinearGradientMode.Horizontal);
            e.Graphics.FillRectangle(brush, ClientRectangle);
        }
    }

    internal sealed class CardPanel : Panel
    {
        public CardPanel()
        {
            DoubleBuffered = true;
            BackColor = Color.White;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = ClientRectangle;
            rect.Width--;
            rect.Height--;
            using var path = RoundedRect(rect, 10);
            using var fill = new SolidBrush(Color.FromArgb(247, 251, 250));
            using var border = new Pen(Color.FromArgb(218, 235, 230), 1);
            e.Graphics.FillPath(fill, path);
            e.Graphics.DrawPath(border, path);
            base.OnPaint(e);
        }

        internal static GraphicsPath RoundedRect(Rectangle rect, int radius)
        {
            int d = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(rect.Left, rect.Top, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Top, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.Left, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    internal sealed class ModernButton : Button
    {
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool Primary { get; set; } = true;
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color AccentColor { get; set; } = Color.FromArgb(10, 110, 100);
        private bool _hover;

        public ModernButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Cursor = Cursors.Hand;
            Font = new Font("Microsoft YaHei UI", 9F);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _hover = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hover = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.Clear(Parent?.BackColor ?? Color.White);
            var rect = ClientRectangle;
            rect.Width--;
            rect.Height--;
            using var path = CardPanel.RoundedRect(rect, 8);
            Color fill;
            Color text;
            if (!Enabled)
            {
                fill = Color.FromArgb(230, 234, 236);
                text = Color.FromArgb(145, 150, 155);
            }
            else if (Primary)
            {
                fill = _hover ? Color.FromArgb(16, 135, 122) : AccentColor;
                text = Color.White;
            }
            else
            {
                fill = _hover ? Color.FromArgb(238, 246, 244) : Color.White;
                text = Color.FromArgb(45, 60, 60);
            }

            using var brush = new SolidBrush(fill);
            e.Graphics.FillPath(brush, path);
            if (!Primary && Enabled)
            {
                using var border = new Pen(Color.FromArgb(190, 204, 201), 1);
                e.Graphics.DrawPath(border, path);
            }
            TextRenderer.DrawText(e.Graphics, Text, Font, rect, text,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis);
        }
    }

    internal sealed class StepIndicator : Control
    {
        private int _step;
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int Step
        {
            get => _step;
            set { _step = Math.Max(0, Math.Min(2, value)); Invalidate(); }
        }

        public StepIndicator()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Font = new Font("Microsoft YaHei UI", 8F);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            string[] labels = { "欢迎", "许可", "确认" };
            int cx = 12;
            int gap = 108;
            for (int i = 0; i < 3; i++)
            {
                int x = cx + i * gap;
                var color = i <= _step
                    ? Color.White
                    : Color.FromArgb(160, 220, 210);
                using var brush = new SolidBrush(color);
                e.Graphics.FillEllipse(brush, x, 0, 20, 20);
                using var textBrush = new SolidBrush(
                    i <= _step ? Color.FromArgb(10, 96, 89) : Color.FromArgb(10, 96, 89));
                using var format = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };
                e.Graphics.DrawString((i + 1).ToString(), Font, textBrush,
                    new RectangleF(x, 0, 20, 20), format);
                using var labelBrush = new SolidBrush(
                    i == _step ? Color.White : Color.FromArgb(190, 230, 222));
                e.Graphics.DrawString(labels[i], Font, labelBrush, x - 6, 24);
                if (i < 2)
                {
                    using var line = new Pen(Color.FromArgb(140, 210, 200), 2);
                    e.Graphics.DrawLine(line, x + 22, 10, x + gap - 2, 10);
                }
            }
        }
    }
}








