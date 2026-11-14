using System;
using System.Threading.Tasks;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;

namespace BasinSlopeTools
{
    internal class UpdateButton : Button
    {
        protected override void OnClick()
        {
            _ = CheckAsync();
        }

        private async Task CheckAsync()
        {
            try
            {
                var result = await UpdateService.CheckAsync(true);
                if (!string.IsNullOrWhiteSpace(result.Error))
                {
                    MessageBox.Show(
                        "检查更新失败：\n" + result.Error,
                        "检查更新");
                    return;
                }
                if (result.Release == null)
                    return;
                if (!result.UpdateAvailable)
                {
                    MessageBox.Show(
                        "当前版本 " + UpdateService.CurrentVersion + " 已是最新版本。",
                        "检查更新");
                    return;
                }
                UpdateWindow.ShowRelease(result.Release);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "检查更新失败");
            }
        }
    }
}
