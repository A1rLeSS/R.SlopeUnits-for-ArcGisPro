using System;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;

namespace BasinSlopeTools
{
    internal class SlopeUnitsButton : Button
    {
        protected override void OnClick()
        {
            ToolLauncher.OpenTool("SlopeUnits");
        }
    }

    internal class BatchButton : Button
    {
        protected override void OnClick()
        {
            ToolLauncher.OpenTool("BatchSlopeUnits");
        }
    }

    internal class SweepButton : Button
    {
        protected override void OnClick()
        {
            ToolLauncher.OpenTool("SweepSlopeUnits");
        }
    }

    internal static class ToolLauncher
    {
        public static void OpenTool(string toolName)
        {
            var toolbox = AddinPaths.ToolboxPath;
            if (!File.Exists(toolbox))
            {
                var log = Path.Combine(Path.GetTempPath(), "BasinSlopeTools_addin.log");
                MessageBox.Show("找不到 Python Toolbox：\n" + toolbox +
                    "\n\n" + (AddinPaths.LastError ?? "") +
                    "\n日志: " + log +
                    "\n\n请重新运行安装器，或确认插件包中的 Tools 文件夹完整。");
                return;
            }
            try
            {
                Geoprocessing.OpenToolDialog(toolbox + "\\" + toolName, null);
            }
            catch (Exception ex)
            {
                MessageBox.Show("打开工具失败：\n" + ex.Message);
            }
        }
    }

    internal static class AddinPaths
    {
        private const string AddinId = "{7D2C4A9E-3F1B-4A77-9B6E-6B1C2D9A4E11}";
        private const string ToolsRevision = "2026-11-05.9";
        private static string _lastError;

        public static string LastError => _lastError;

        public static string InstallDir
        {
            get
            {
                var asm = Assembly.GetExecutingAssembly().Location;
                return Path.GetDirectoryName(asm) ?? AppDomain.CurrentDomain.BaseDirectory;
            }
        }

        public static string ToolsDir
        {
            get
            {
                _lastError = null;
                var localTools = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "BasinSlopeTools", "tools");
                if (IsToolsCurrent(localTools))
                    return localTools;

                var dir = InstallDir;
                var candidates = new[]
                {
                    Path.Combine(dir, "Tools"),
                    Path.Combine(dir, "..", "Tools"),
                    Path.Combine(dir, "..", "..", "Tools")
                };
                foreach (var c in candidates)
                {
                    if (IsToolsCurrent(c))
                        return Path.GetFullPath(c);
                }

                var extracted = ExtractTools(localTools);
                if (extracted != null)
                    return extracted;

                return Path.GetFullPath(candidates[0]);
            }
        }

        private static bool IsToolsCurrent(string dir)
        {
            try
            {
                if (!File.Exists(Path.Combine(dir, "BasinSlopeTools.pyt")))
                    return false;
                var marker = Path.Combine(dir, ".tools_revision");
                return File.Exists(marker) &&
                       File.ReadAllText(marker).Trim() == ToolsRevision;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Pro 只缓存 Add-in DLL，不缓存 Tools/。
        /// 安装器会优先把 Tools/ 复制到本地；此处作为便携/手工安装的兜底，
        /// 并校验版本，避免升级后继续使用旧 Tools 缓存。
        /// </summary>
        private static string ExtractTools(string localTools)
        {
            var staging = localTools + ".new";
            try
            {
                var package = FindPackage();
                if (package == null)
                {
                    _lastError = "找不到已安装的 BasinSlopeTools.esriAddinX，无法解压 Tools。";
                    WriteLog(_lastError);
                    return null;
                }

                if (Directory.Exists(staging))
                    Directory.Delete(staging, true);
                Directory.CreateDirectory(staging);

                using (var zip = ZipFile.OpenRead(package))
                {
                    foreach (var entry in zip.Entries)
                    {
                        const string prefix = "Tools/";
                        if (!entry.FullName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                            continue;
                        var rel = entry.FullName.Substring(prefix.Length)
                            .Replace('/', Path.DirectorySeparatorChar);
                        if (string.IsNullOrWhiteSpace(rel) || rel.EndsWith(
                                Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
                            continue;
                        var target = Path.Combine(staging, rel);
                        var parent = Path.GetDirectoryName(target);
                        if (!string.IsNullOrEmpty(parent))
                            Directory.CreateDirectory(parent);
                        entry.ExtractToFile(target, true);
                    }
                }

                if (!File.Exists(Path.Combine(staging, "BasinSlopeTools.pyt")))
                    throw new InvalidDataException("Add-in 中缺少 Tools/BasinSlopeTools.pyt");

                File.WriteAllText(Path.Combine(staging, ".tools_revision"), ToolsRevision);
                if (Directory.Exists(localTools))
                    Directory.Delete(localTools, true);
                Directory.Move(staging, localTools);
                WriteLog("Tools extracted: " + localTools + " revision=" + ToolsRevision);
                return localTools;
            }
            catch (Exception ex)
            {
                _lastError = "解压 Tools 失败: " + ex.Message;
                WriteLog(_lastError);
                try { if (Directory.Exists(staging)) Directory.Delete(staging, true); } catch { }
                return null;
            }
        }

        private static string FindPackage()
        {
            var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var candidates = new[]
            {
                Path.Combine(docs, "ArcGIS", "AddIns", "ArcGISPro", AddinId,
                    "BasinSlopeTools.esriAddinX"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "ESRI", "ArcGISPro", "AddIns", AddinId,
                    "BasinSlopeTools.esriAddinX")
            };
            foreach (var c in candidates)
            {
                if (File.Exists(c))
                    return c;
            }
            return null;
        }

        private static void WriteLog(string message)
        {
            try
            {
                File.AppendAllText(
                    Path.Combine(Path.GetTempPath(), "BasinSlopeTools_addin.log"),
                    DateTime.Now.ToString("s") + " " + message + Environment.NewLine);
            }
            catch
            {
            }
        }

        public static string ToolboxPath => Path.Combine(ToolsDir, "BasinSlopeTools.pyt");
    }
}








