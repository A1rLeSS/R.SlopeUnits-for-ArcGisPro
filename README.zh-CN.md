# R.slopeunits for ArcGIS Pro

[English](README.md) | [简体中文](README.zh-CN.md) | [English installation guide](docs/Installation.md) | [中文安装说明](docs/安装说明.md)

ArcGIS Pro 插件与 Python 工具箱，调用 GRASS GIS 的 `r.slopeunits.create` 和 `r.slopeunits.clean` 生成斜坡单元。

本项目是面向本地离线、可复现斜坡单元生成的研究/课程项目，不是 Esri 或 GRASS GIS 官方产品。

## 下载

预编译包在 [GitHub Releases](https://github.com/A1rLeSS/R.SlopeUnits-for-ArcGisPro/releases/latest) 发布：

- `BasinSlopeTools_Setup_0.5.exe`：自带 GRASS 8.5 runtime 的单文件安装器；
- `BasinSlopeTools_Portable_20261111.zip`：便携包与手动安装脚本；
- `SHA256SUMS.txt`：发布资产校验值。

安装器未做代码签名，Windows SmartScreen 可能提示“未知发布者”。

## 功能

安装后 ArcGIS Pro 顶部会出现 **“R.slopeunits 斜坡单元”** 选项卡，包含五个按钮：

| 按钮 | 功能 |
|---|---|
| 生成斜坡单元 | 单 DEM → `r.slopeunits.create` + `clean` → GeoPackage，可选 GDB |
| 批量生成 | 多 DEM 批量，支持断点续跑 |
| 参数扫描 | 扫描 `thresh` / `areamin` / `cvmin` / `maxiteration`，支持断点续跑 |
| 质量报告 | 打开最新质量报告，并与上一份对比 |
| 关于/版权 | 版本、GPL、第三方许可和源码获取方式 |

其他行为：

- ESRI Grid DEM：选择 `hdr.adf`，工具会自动解析所在 Grid 目录；
- 中文/非 ASCII 路径：GRASS 启动前自动创建 ASCII 临时链接或副本；
- `65535` / `-9999`：GRASS 阶段通过 `r.null` 自动掩膜；
- 质量报告包含 0–100 **生成效果综合评分**（CVAR 45%、面积 25%、形状紧凑度 20%、clean 10%）和自动生成的结果预览 PNG；
- 批量/扫描结果写入 `batch_summary.csv` / `sweep_summary.csv`；
- 输出包含 `AREA_M2`、`CVAR`、`*_run_manifest.json` 和 HTML/JSON/CSV 质量报告。

## 环境要求

- Windows x64
- ArcGIS Pro **3.0–3.7**（本机在 3.7 实测）
- 投影坐标系、单位为米的 DEM；地理坐标系 DEM 会被拒绝
- 构建插件需要 .NET 6 Windows SDK，构建安装器需要 .NET 10 SDK
- 本地运行需要 GRASS GIS 8.5；安装器已自带 runtime

## 安装

1. 关闭 ArcGIS Pro；
2. 运行 `BasinSlopeTools_Setup_0.5.exe`；
3. 选择 **English** 或 **简体中文**，阅读并接受双语许可协议，按提示查看百分比进度和详细安装事件；
4. 重新打开 ArcGIS Pro，打开 **“R.slopeunits 斜坡单元”** 选项卡；
5. 首次使用 Python Toolbox 时，Pro 询问是否允许运行代码，选择 **Yes**。

静默安装必须显式接受许可：

```powershell
.\BasinSlopeTools_Setup_0.5.exe --silent --accept-license
```

卸载可从 Windows“应用和功能”、安装器或命令行执行：

```powershell
.\BasinSlopeTools_Setup_0.5.exe --uninstall
```

卸载前请关闭 ArcGIS Pro 和正在运行的 GRASS 分析任务。

## 构建

GRASS runtime 和 `Installer\payload.zip` 体积过大，不放入 Git 仓库。构建脚本需要本地 GRASS runtime。

```powershell
# 如 SDK targets 不在默认路径，先设置
$env:ARCGIS_PRO_SDK_TARGETS = 'C:\Program Files\ArcGIS\Pro\bin\Esri.ProApp.SDK.Desktop.targets'

.\scripts\build.ps1 `
  -GrassRuntime 'D:\path\to\grass\bin\grass85.bat' `
  -ArcGISProSDKTargets $env:ARCGIS_PRO_SDK_TARGETS
```

只构建 Add-in：

```powershell
dotnet build .\BasinSlopeTools\BasinSlopeTools.csproj -c Release
```

## 测试

纯 Python 辅助函数不依赖 ArcGIS Pro/ArcPy：

```powershell
python -m unittest discover -s tests -v
```

测试覆盖 ADF 路径规范化、DEM 列表处理和生成质量评分公式。ArcGIS Pro + GRASS 的集成测试见 [docs/VALIDATION.md](docs/VALIDATION.md)，因需要 Esri 许可和本地 GRASS runtime，不在 CI 中运行。

## 目录结构

```text
BasinSlopeTools/        ArcGIS Pro Add-in、Python Toolbox、GRASS add-on 脚本
Installer/              Windows 安装器源码
dist/                   便携安装脚本与发行说明
docs/                   安装、构建、验证和质量评分文档
scripts/                构建与版本检查脚本
tests/                  无外部依赖的 Python 测试
```

## 版本

应用版本和 Tools revision 分别存放在：

- `VERSION`
- `TOOLS_REVISION`

`scripts/check-version.ps1` 会检查 C# 项目、DAML、安装脚本和 Add-in 缓存标记是否一致。

## 已知限制

- 全分辨率 7,895 × 7,895 DEM 计算量很大，调参时请使用小范围或“快速预览”；
- ArcGIS Pro 3.0–3.6 在目标框架上兼容，但未逐一实机验证；
- 生成效果评分是同一地区不同参数的相对比较指标，不是绝对地形精度；
- 批量/扫描目前串行执行，没有任务队列或并行；
- 符号化和“加载到当前地图”依赖 Pro 地图会话，未纳入 CI；
- 安装器未签名；商业分发前应复核 Esri/GRASS 许可边界。

## 许可

项目采用 **GPL-2.0-or-later**。参见 [LICENSE](LICENSE)、[BasinSlopeTools/Licenses](BasinSlopeTools/Licenses)、[THIRD_PARTY_NOTICES.txt](BasinSlopeTools/Licenses/THIRD_PARTY_NOTICES.txt) 和 [SOURCE_OFFER.txt](BasinSlopeTools/Licenses/SOURCE_OFFER.txt)。

GRASS GIS 及其 runtime 组件保留各自许可；ArcGIS Pro SDK 和 ArcPy 是 Esri 产品，本仓库不重新分发。



