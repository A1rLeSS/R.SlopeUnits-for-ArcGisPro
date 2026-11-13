# 参与贡献

[English](CONTRIBUTING.md) | [简体中文](CONTRIBUTING.zh-CN.md)

这是一个科研与教学项目，欢迎提交问题报告和范围明确的 Pull Request。

## 提交 Pull Request 前

每个 PR 尽量聚焦一个问题。请说明问题、用户能看到的变化，以及你做过的检查。涉及用户行为的改动，请同步更新 `CHANGELOG.md`。

在仓库根目录运行 CI 使用的检查：

```powershell
python -m unittest discover -s tests -v
python -m compileall -q BasinSlopeTools/Tools tests scripts
python scripts/check_markdown_links.py
```

改动发布版本信息时，还要运行：

```powershell
./scripts/check-version.ps1
```

准备发布新版本时更新 `VERSION`。只要部署的 Python 工具箱发生变化，就更新 `TOOLS_REVISION`，以便已安装插件刷新缓存。Add-in 和安装脚本中的对应标记也要保持一致；版本检查脚本会核对这些值。

CI 不会启动 ArcGIS Pro，也不会运行完整 GRASS 流程。如果改动影响 GIS 计算，请说明使用的 ArcGIS Pro 和 GRASS 版本，以及是否用真实或合成 DEM 做过验证。无法执行的检查也请明确说明。

## 不要提交的内容

不要提交自动生成或与本机绑定的产物，包括：

- 栅格、矢量、地理数据库和 GIS 工程数据文件；
- 编译程序集、安装器、Add-in 包、压缩包等生成二进制文件；
- 虚拟环境内容、依赖缓存和 IDE 状态文件；
- 含个人路径、凭据、令牌或其他隐私信息的日志。

自动化测试确实需要的小型合成样本可以提交，但必须可复现、许可清楚，且不含私人或受限数据。未经授权，不要附上真实研究数据。

## 代码与文档

- 尽量减少 Python 依赖。条件允许时，将 ArcPy 和 GRASS 调用放在工具箱或引擎层。
- 遵循现有 C#、.NET 和 ArcGIS Pro SDK 写法；新增依赖时说明原因。
- 修改随仓库提供的 GRASS 附加模块代码时，保留上游版权和许可声明。
- 命令、参数、输出或安装行为发生变化时，同步更新用户文档。

## 报告问题

请优先使用 Bug 报告表单。说明 ArcGIS Pro 和 Windows 版本、DEM 格式与坐标系、相关参数，以及路径是否包含空格或非 ASCII 字符。附日志片段前，请删除个人路径和敏感信息。优先提供小型合成 DEM；只有获得授权后才分享真实数据。
