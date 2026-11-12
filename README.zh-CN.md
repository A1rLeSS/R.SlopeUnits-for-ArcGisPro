# R.slopeunits for ArcGIS Pro

[English](README.md) | [简体中文](README.zh-CN.md) | [English installation guide](docs/Installation.md) | [中文安装说明](docs/安装说明.md)

ArcGIS Pro 插件与 Python 工具箱，使用 GRASS GIS 的 `r.slopeunits.create` 和 `r.slopeunits.clean` 模块生成斜坡单元。

## 目录结构

- `BasinSlopeTools/`：ArcGIS Pro 插件源码、Python 工具箱、GRASS 附加模块脚本和许可文件。
- `Installer/`：Windows 安装程序源码。
- `dist/`：安装脚本和发行版说明。
- `docs/`：提供[中文](docs/安装说明.md)和[英文](docs/Installation.md)安装与使用说明。

GRASS 运行时和安装器 payload 合计超过 1 GB，因此不会存入 Git 源码仓库。请从 GitHub Releases 下载打包版本，或按下方构建说明自行生成。

## 环境要求

- Windows x64
- ArcGIS Pro 3.0–3.7
- 构建插件需要 .NET 6 Windows SDK
- 构建安装器需要 .NET 10 SDK
- 运行打包工具箱需要 GRASS GIS 8.5 运行时

插件项目当前引用的 ArcGIS Pro SDK targets 路径为 `D:\ArcGISPro\bin\Esri.ProApp.SDK.Desktop.targets`。在其他计算机上构建前，请按本机 ArcGIS Pro 安装位置修改该引用。

## 功能

插件提供斜坡单元生成、批量生成、参数扫描和质量报告等工具。详细参数及使用步骤请查看[安装与使用说明](docs/安装说明.md)。

## 构建

在已配置 ArcGIS Pro SDK 的环境中构建插件：

```powershell
dotnet build .\BasinSlopeTools\BasinSlopeTools.csproj -c Release
```

构建单文件安装器前，先按发行打包流程准备 `Installer\payload.zip`，然后运行：

```powershell
dotnet publish .\Installer\BasinSlopeToolsInstaller.csproj `
  -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true `
  -o .\Installer\publish
```

## 许可

许可和第三方声明见 `BasinSlopeTools/Licenses/` 与 `dist/`。GRASS GIS 及其捆绑依赖分别遵循其各自许可；重新分发打包版本时，须同时提供适用的声明文件和源码获取说明。
