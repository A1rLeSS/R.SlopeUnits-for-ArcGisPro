using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace BasinSlopeTools.Setup
{
    internal static class InstallerEngine
    {
        public const string PayloadResource = "payload.zip";
        public const string AppVersion = "0.6";
        private const string AddinId = "{7D2C4A9E-3F1B-4A77-9B6E-6B1C2D9A4E11}";
        private const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\BasinSlopeTools";

        public static string LogPath =>
            Path.Combine(Path.GetTempPath(), "BasinSlopeTools_Setup.log");

        public static bool LogPathExists => File.Exists(LogPath);

        public static string Install(IProgress<string> progress = null,
                                      IProgress<InstallEvent> detailed = null)
        {
            ResetLog();
            Report(progress, detailed, 0, "检查运行环境...", "Installer " + AppVersion);
            WriteLog("Installer version " + AppVersion);
            var proPath = FindArcGisProPath();
            if (proPath == null && !HasArcGisProRegistry())
                throw new InvalidOperationException(
                    "未检测到 ArcGIS Pro。请先安装 ArcGIS Pro 3.x，再运行本安装器。");
            if (ArcGisProRunning())
                throw new InvalidOperationException(
                    "ArcGIS Pro 正在运行。请先关闭 ArcGIS Pro，再重新运行安装器。");
            if (GrassAnalysisRunning())
                throw new InvalidOperationException(
                    "GRASS/Python 分析仍在运行。请等待任务结束后再安装。");
            CheckArcGisProVersion(proPath);
            CheckDiskSpace();
            Report(progress, detailed, 8, "环境检查通过", "ArcGIS Pro / 磁盘检查通过");

            var temp = Path.Combine(Path.GetTempPath(),
                "BasinSlopeTools_Install_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temp);
            try
            {
                Report(progress, detailed, 12, "解压安装包（约 0.8 GB）...", temp);
                WriteLog("Extracting payload...");
                var zipPath = Path.Combine(temp, "payload.zip");
                ExtractPayload(zipPath);
                using (var archive = ZipFile.OpenRead(zipPath))
                {
                    archive.ExtractToDirectory(temp, true);
                }
                File.Delete(zipPath);
                Report(progress, detailed, 28, "安装包解压完成", null);

                var installer = Path.Combine(temp, "install.ps1");
                if (!File.Exists(installer))
                    throw new FileNotFoundException("install.ps1 not found in payload", installer);

                Report(progress, detailed, 32, "安装 Add-in、Tools 和 GRASS runtime...", null);
                WriteLog("Running install.ps1...");
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "-NoLogo -NoProfile -ExecutionPolicy Bypass -File \"" + installer + "\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = temp
                };
                using var proc = Process.Start(psi)
                    ?? throw new InvalidOperationException("cannot start installer process");
                var stdout = new System.Text.StringBuilder();
                var stderr = new System.Text.StringBuilder();
                var sync = new object();
                proc.OutputDataReceived += (_, e) =>
                {
                    if (e.Data == null) return;
                    lock (sync) stdout.AppendLine(e.Data);
                    WriteLog(e.Data);
                    int percent = 55;
                    if (e.Data.IndexOf("Installing Add-in", StringComparison.OrdinalIgnoreCase) >= 0)
                        percent = 38;
                    else if (e.Data.IndexOf("Deploying Tools", StringComparison.OrdinalIgnoreCase) >= 0)
                        percent = 48;
                    else if (e.Data.IndexOf("Copying GRASS", StringComparison.OrdinalIgnoreCase) >= 0)
                        percent = 62;
                    else if (e.Data.IndexOf("Registering uninstall", StringComparison.OrdinalIgnoreCase) >= 0)
                        percent = 92;
                    detailed?.Report(new InstallEvent
                    {
                        Percent = percent,
                        Message = e.Data,
                        Detail = e.Data
                    });
                };
                proc.ErrorDataReceived += (_, e) =>
                {
                    if (e.Data == null) return;
                    lock (sync) stderr.AppendLine(e.Data);
                    WriteLog("ERR: " + e.Data);
                    detailed?.Report(new InstallEvent
                    {
                        Percent = 55,
                        Message = "安装脚本输出错误",
                        Detail = e.Data
                    });
                };
                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();
                proc.WaitForExit();
                var stderrText = stderr.ToString();
                if (!string.IsNullOrWhiteSpace(stderrText))
                    WriteLog("STDERR:\n" + stderrText);
                if (proc.ExitCode != 0)
                    throw new InvalidOperationException(
                        "installer exit code " + proc.ExitCode + "\n" + stderrText);

                Report(progress, detailed, 92, "写入注册表和卸载入口...", null);
                WriteLog("Installation finished.");
                Report(progress, detailed, 100, "安装完成", null);
                return LogPath;
            }
            finally
            {
                try { Directory.Delete(temp, true); } catch { }
            }
        }

        public static void Uninstall(IProgress<string> progress = null,
                                      IProgress<InstallEvent> detailed = null)
        {
            ResetLog();
            Report(progress, detailed, 0, "检查卸载环境...", null);
            if (ArcGisProRunning())
                throw new InvalidOperationException(
                    "ArcGIS Pro 正在运行。请先关闭 ArcGIS Pro，再卸载。");
            if (GrassAnalysisRunning())
                throw new InvalidOperationException(
                    "GRASS/Python 分析仍在运行。请等待任务结束后再卸载。");

            Report(progress, detailed, 20, "删除 Add-in...", null);
            var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var addinDir = Path.Combine(docs, "ArcGIS", "AddIns", "ArcGISPro", AddinId);
            var addin = Path.Combine(addinDir, "BasinSlopeTools.esriAddinX");
            TryDeleteFile(addin);
            TryDeleteDir(addinDir);
            if (File.Exists(addin) || Directory.Exists(addinDir))
                throw new InvalidOperationException(
                    "Add-in 文件仍被占用，无法删除。请关闭 ArcGIS Pro 后重试。");

            Report(progress, detailed, 60, "删除 GRASS runtime...", null);
            var baseDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "BasinSlopeTools");
            TryDeleteDir(baseDir);
            if (Directory.Exists(baseDir))
                throw new InvalidOperationException(
                    "GRASS runtime 目录仍被占用，无法删除。请关闭相关进程后重试。");

            Environment.SetEnvironmentVariable("BASINSLOPE_GRASS", null, EnvironmentVariableTarget.User);
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(UninstallKey, true);
                key?.Close();
                Registry.CurrentUser.DeleteSubKeyTree(UninstallKey, false);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("卸载注册表项删除失败: " + ex.Message);
            }

            WriteLog("Uninstall finished.");
            Report(progress, detailed, 100, "卸载完成", null);
        }

        public static void DryRun()
        {
            var temp = Path.Combine(Path.GetTempPath(),
                "BasinSlopeTools_DryRun_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temp);
            try
            {
                var zipPath = Path.Combine(temp, "payload.zip");
                ExtractPayload(zipPath);
                using (var archive = ZipFile.OpenRead(zipPath))
                {
                    archive.ExtractToDirectory(temp, true);
                }
                var grass = Path.Combine(temp, "runtime", "grass", "bin", "grass85.bat");
                var addin = Path.Combine(temp, "BasinSlopeTools.esriAddinX");
                var install = Path.Combine(temp, "install.ps1");
                var text = new StringBuilder();
                text.AppendLine("payload bytes=" + new FileInfo(zipPath).Length);
                text.AppendLine("install.ps1=" + File.Exists(install));
                text.AppendLine("addin=" + File.Exists(addin));
                text.AppendLine("grass=" + File.Exists(grass));
                text.AppendLine("pro=" + ArcGisProFound());
                text.AppendLine("pro_path=" + (FindArcGisProPath() ?? "not found"));
                text.AppendLine("pro_running=" + ArcGisProRunning());
                text.AppendLine("root entries=" + string.Join(", ",
                    Directory.GetFileSystemEntries(temp).Select(Path.GetFileName)));
                File.WriteAllText(LogPath, text.ToString(), Encoding.UTF8);
            }
            finally
            {
                try { Directory.Delete(temp, true); } catch { }
            }
        }

        private static void ExtractPayload(string zipPath)
        {
            using var input = Assembly.GetExecutingAssembly()
                .GetManifestResourceStream(PayloadResource)
                ?? throw new InvalidOperationException("payload.zip resource not found");
            using var output = File.Create(zipPath);
            input.CopyTo(output);
        }

        private static string FindArcGisProPath()
        {
            var candidates = new System.Collections.Generic.List<string>
            {
                @"C:\Program Files\ArcGIS\Pro\bin\ArcGISPro.exe",
                @"D:\ArcGISPro\bin\ArcGISPro.exe",
                @"C:\Program Files (x86)\ArcGIS\Pro\bin\ArcGISPro.exe"
            };
            foreach (var key in new[]
            {
                @"HKEY_LOCAL_MACHINE\SOFTWARE\ESRI\ArcGISPro",
                @"HKEY_CURRENT_USER\SOFTWARE\ESRI\ArcGISPro"
            })
            {
                try
                {
                    var installDir = Registry.GetValue(key, "InstallDir", null) as string;
                    if (!string.IsNullOrWhiteSpace(installDir))
                        candidates.Add(Path.Combine(installDir, "bin", "ArcGISPro.exe"));
                }
                catch
                {
                }
            }
            return candidates.FirstOrDefault(File.Exists);
        }

        private static bool HasArcGisProRegistry()
        {
            foreach (var root in new[] { Registry.LocalMachine, Registry.CurrentUser })
            {
                try
                {
                    using var key = root.OpenSubKey(@"SOFTWARE\ESRI\ArcGISPro");
                    if (key != null) return true;
                }
                catch { }
            }
            return false;
        }

        private static bool ArcGisProFound()
        {
            return FindArcGisProPath() != null || HasArcGisProRegistry();
        }

        private static bool ArcGisProRunning()
        {
            try
            {
                return Process.GetProcessesByName("ArcGISPro").Length > 0;
            }
            catch
            {
                return false;
            }
        }

        private static bool GrassAnalysisRunning()
        {
            try
            {
                foreach (var process in Process.GetProcesses())
                {
                    var name = process.ProcessName.ToLowerInvariant();
                    if (name == "grass85" ||
                        name.StartsWith("r.slopeunits") ||
                        name.StartsWith("r.slopeunits."))
                        return true;
                }
            }
            catch
            {
            }
            return false;
        }

        private static void CheckArcGisProVersion(string proPath)
        {
            if (string.IsNullOrWhiteSpace(proPath) || !File.Exists(proPath))
                return;
            try
            {
                var versionText = FileVersionInfo.GetVersionInfo(proPath).FileVersion;
                if (Version.TryParse(versionText, out var version)
                    && (version.Major != 3 || version.Minor > 7))
                {
                    throw new InvalidOperationException(
                        "需要 ArcGIS Pro 3.0–3.7，当前检测到 " + versionText + "。");
                }
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch { }
        }

        private static void CheckDiskSpace()
        {
            const long required = 1800L * 1024 * 1024; // 1.8 GB
            foreach (var path in new[]
                     {
                         Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                         Path.GetTempPath()
                     })
            {
                if (string.IsNullOrWhiteSpace(path)) continue;
                var root = Path.GetPathRoot(Path.GetFullPath(path));
                if (string.IsNullOrWhiteSpace(root)) continue;
                var drive = new DriveInfo(root);
                if (!drive.IsReady || drive.AvailableFreeSpace < required)
                    throw new InvalidOperationException(
                        $"磁盘空间不足：{drive.Name} 至少需要 1.8 GB 可用空间。");
            }
        }

        private static void ResetLog()
        {
            try
            {
                File.WriteAllText(LogPath,
                    "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "] BasinSlopeTools setup\r\n",
                    Encoding.UTF8);
            }
            catch { }
        }

        public static void WriteLog(string message)
        {
            try
            {
                File.AppendAllText(LogPath,
                    "[" + DateTime.Now.ToString("HH:mm:ss") + "] " + message + "\r\n",
                    Encoding.UTF8);
            }
            catch { }
        }

        private static void Report(IProgress<string> progress,
                                    IProgress<InstallEvent> detailed,
                                    int percent, string message, string detail)
        {
            try { progress?.Report(message); } catch { }
            try
            {
                detailed?.Report(new InstallEvent
                {
                    Percent = percent,
                    Message = message,
                    Detail = detail
                });
            }
            catch { }
        }

        private static void TryDeleteDir(string path)
        {
            try { if (Directory.Exists(path)) Directory.Delete(path, true); } catch { }
        }

        private static void TryDeleteFile(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }
    }

    internal sealed class InstallEvent
    {
        public int Percent { get; set; }
        public string Message { get; set; }
        public string Detail { get; set; }
    }
}




