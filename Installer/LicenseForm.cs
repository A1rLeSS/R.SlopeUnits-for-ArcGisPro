using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace BasinSlopeTools.Setup
{
    internal static class LicenseInfo
    {
        public static string Text
        {
            get
            {
                var sb = new StringBuilder();
                sb.AppendLine("R.slopeunits 斜坡单元 / BasinSlopeTools");
                sb.AppendLine("Version 0.4");
                sb.AppendLine("Copyright (C) 2026 BasinSlopeTools Project");
                sb.AppendLine("License: GNU GPL v2.0 or later (GPL-2.0-or-later)");
                sb.AppendLine("Contact: leomilk362@gmail.com");
                sb.AppendLine();
                sb.AppendLine(Read("SOURCE_OFFER.txt"));
                sb.AppendLine();
                sb.AppendLine(Read("THIRD_PARTY_NOTICES.txt"));
                sb.AppendLine();
                sb.AppendLine("GRASS copyright:");
                sb.AppendLine(Read("GRASS_COPYRIGHT.txt"));
                sb.AppendLine();
                sb.AppendLine("GNU GPL v2:");
                sb.AppendLine(Read("GPL-2.0.txt"));
                return sb.ToString();
            }
        }

        private static string Read(string name)
        {
            var asm = Assembly.GetExecutingAssembly();
            using var stream = asm.GetManifestResourceStream(name);
            if (stream == null)
                return "(missing license resource: " + name + ")";
            using var reader = new StreamReader(stream, Encoding.UTF8);
            return reader.ReadToEnd();
        }
    }

    internal sealed class LicenseForm : Form
    {
        public LicenseForm()
        {
            Text = "R.slopeunits 许可协议 / Third-Party Notices";
            ClientSize = new Size(980, 720);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimizeBox = false;
            MaximizeBox = true;
            Font = new Font("Microsoft YaHei UI", 9F);
            BackColor = Color.White;

            var header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 76,
                BackColor = Color.FromArgb(10, 110, 100)
            };
            header.Controls.Add(new Label
            {
                Text = "许可协议 / Third-Party Notices",
                Font = new Font("Microsoft YaHei UI", 15F, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(20, 14)
            });
            header.Controls.Add(new Label
            {
                Text = "GPL-2.0-or-later · BasinSlopeTools Project · 请向下阅读完整条款",
                ForeColor = Color.FromArgb(220, 245, 240),
                AutoSize = true,
                Location = new Point(22, 46)
            });

            var box = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                Font = new Font("Consolas", 9F),
                Text = LicenseInfo.Text,
                WordWrap = true,
                BackColor = Color.White,
                Margin = new Padding(18)
            };

            var footer = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 52,
                BackColor = Color.FromArgb(245, 248, 250)
            };
            var close = new Button
            {
                Text = "关闭",
                DialogResult = DialogResult.OK,
                Size = new Size(90, 30),
                Location = new Point(870, 11)
            };
            footer.Controls.Add(close);
            footer.Controls.Add(new Label
            {
                Text = "源码获取：leomilk362@gmail.com",
                AutoSize = true,
                Location = new Point(20, 18),
                ForeColor = Color.DimGray
            });

            Controls.Add(box);
            Controls.Add(footer);
            Controls.Add(header);
            AcceptButton = close;
        }
    }
}
