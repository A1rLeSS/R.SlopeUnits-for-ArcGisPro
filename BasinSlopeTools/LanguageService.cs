using System;
using System.Globalization;
using System.IO;
using ArcGIS.Desktop.Framework;

namespace BasinSlopeTools
{
    internal static class LanguageService
    {
        private static string _language;

        public static string Current
        {
            get
            {
                if (!string.IsNullOrEmpty(_language))
                    return _language;
                try
                {
                    var path = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "BasinSlopeTools", "install_language.txt");
                    if (File.Exists(path))
                    {
                        var value = File.ReadAllText(path).Trim().ToLowerInvariant();
                        _language = value == "en" ? "en" : value == "zh" ? "zh" : null;
                    }
                }
                catch
                {
                }
                if (string.IsNullOrEmpty(_language))
                {
                    _language = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "zh"
                        ? "zh" : "en";
                }
                return _language;
            }
        }

        public static bool IsChinese => Current == "zh";

        public static string T(string en, string zh)
        {
            return IsChinese ? zh : en;
        }

        public static void ApplyRibbonCaptions()
        {
            try
            {
                Set("BasinSlopeTools_Tab",
                    T("R.slopeunits", "R.slopeunits 斜坡单元"),
                    T("Slope-unit tools", "斜坡单元工具"));
                Set("BasinSlopeTools_SlopeUnitsButton",
                    T("Generate slope units", "生成斜坡单元"),
                    T("Generate slope units from one DEM",
                      "从单个 DEM 生成斜坡单元"));
                Set("BasinSlopeTools_BatchButton",
                    T("Batch generate", "批量生成"),
                    T("Process multiple DEMs; parallel child processes",
                      "处理多个 DEM；支持并行子进程"));
                Set("BasinSlopeTools_SweepButton",
                    T("Parameter sweep", "参数扫描"),
                    T("Scan threshold/area/CVAR/iteration combinations",
                      "扫描阈值/面积/CVAR/迭代组合"));
                Set("BasinSlopeTools_QualityButton",
                    T("Quality report", "质量报告"),
                    T("Open the latest quality report",
                      "打开最新质量报告"));
                Set("BasinSlopeTools_UpdateButton",
                    T("Check for updates", "检查更新"),
                    T("Check GitHub Release and download a verified installer",
                      "检查 GitHub Release 并下载校验后的安装器"));
                Set("BasinSlopeTools_AboutButton",
                    T("About / licenses", "关于/版权"),
                    T("Version, licence and source information",
                      "版本、许可和源码信息"));
            }
            catch
            {
                // Ribbon caption localisation is best-effort.
            }
        }

        private static void Set(string id, string caption, string tooltip)
        {
            var wrapper = FrameworkApplication.GetPlugInWrapper(id);
            if (wrapper == null)
                return;
            wrapper.Caption = caption;
            wrapper.TooltipHeading = caption;
            wrapper.Tooltip = tooltip;
        }
    }
}
