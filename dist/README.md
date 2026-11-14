# R.slopeunits 斜坡单元 · ArcGIS Pro 便携版 0.6.0

[English](README.en.md) | [简体中文](README.md)

> 目标机器：只需安装 ArcGIS Pro 3.0–3.7；**不需要安装 GRASS**  
> 功能：在 ArcGIS Pro 内通过 GRASS `R.slopeunits` 生成斜坡单元  
> 运行时：GRASS 8.5.0 便携版（随包提供）

## 1. 安装

1. 先关闭 ArcGIS Pro。
2. 将压缩包解压到较浅的本地目录。支持中文路径，但应避免路径过深。
3. 双击运行：

```text
install.cmd
```

也可以在 PowerShell 中运行 `install.ps1`。

脚本会：

- 将 `BasinSlopeTools.esriAddinX` 安装到当前用户的 ArcGIS Pro AddIns 目录；
- 将 `runtime\grass` 复制到：

```text
%LOCALAPPDATA%\BasinSlopeTools\runtime\grass
```

- 设置当前用户的 `BASINSLOPE_GRASS` 环境变量；
- 安装前检查 ArcGIS Pro 状态和磁盘空间；
- 无需管理员权限，也不会修改系统 PATH。

4. 重新打开 ArcGIS Pro，顶部应出现 **“R.slopeunits 斜坡单元”** 选项卡。

卸载时可使用：

```cmd
%LOCALAPPDATA%\BasinSlopeTools\uninstall.cmd
```

也可以在 Windows“应用和功能”中卸载。

## 2. 功能

插件提供以下工具：

| 按钮 | 功能 |
|---|---|
| **生成斜坡单元** | 调用 GRASS `r.slopeunits.create + clean` → GeoPackage/GDB |
| **批量生成** | 多 DEM 批量，支持断点续跑 |
| **参数扫描** | 阈值/面积/CVAR/迭代组合扫描，支持断点续跑 |
| **质量报告** | 打开最近一次运行的质量报告窗口 |
| **关于/版权** | 版本、GPL、第三方许可和源码获取方式 |

不包含流域生成、流域—斜坡耦合、ML 等功能。

## 3. 参数

| 参数 | 默认 | 说明 |
|---|---:|---|
| 输入 DEM | — | 投影 DEM，单位米；支持 tif/tiff/img/asc 和 ESRI Grid（选择 `hdr.adf`） |
| 输出 GeoPackage | — | 例如 `slope_units.gpkg` |
| GRASS grass85.bat | 自动 | 默认使用便携 runtime |
| DEM EPSG | 自动 | 自动读取；若填写必须与 DEM 一致 |
| 参数预设 | 自定义 | 13m/2km²、30m/5km²、快速预览 |
| thresh | 250000 m² | 初始阈值 |
| areamin | 200000 m² | 最小面积 |
| areamax | 空 | 最大面积，可选 |
| cvmin | 0.25 | 坡向圆方差阈值 |
| rf | 2 | 阈值递减因子 |
| maxiteration | 50 | 最大迭代 |
| convergence | 5 | MFD 收敛因子 |
| SFD | 否 | 默认 MFD |
| cleansize | 25000 m² | 清理小单元面积（整数） |
| clean method | 快速（方法1） | 快速 / 条纹清理（方法2） / 详细清理（方法3） |
| 同时输出 GDB | 否 | 在 GPKG 旁生成 `<输出名>.gdb` |

运行后会输出：

- `*.gpkg`：含 `AREA_M2`、`CVAR` 字段；
- 可选 `*.gdb`：`slope_units` Feature Class；
- `*_quality_report.html/json/csv`：质量报告；
- `*_run_manifest.json`：参数与版本清单；
- `%TEMP%\R_slopeunits_*.log`：运行日志。

点击插件 **质量报告** 按钮可打开最新质量报告。报告包含 **生成效果综合评分（0–100）**：CVAR 45%、面积 25%、形状紧凑度 20%、clean 10%，并给出等级和结果预览图；该评分用于相对比较，不是绝对地形精度指标。

工具会自动查找便携 GRASS：

```text
%LOCALAPPDATA%\BasinSlopeTools\runtime\grass\bin\grass85.bat
```

也可以手动选择 `grass85.bat`。

## 4. 已验证

- 便携 GRASS 在独立目录下 `--version` 正常；
- 便携 GRASS 已跑通 `r.slopeunits.create + clean`；
- 输出 GPKG 可被 ArcGIS Pro / GeoPandas 读取。

## 5. 说明

- 全分辨率 DEM（7,895 × 7,895）计算量较大，建议先用小范围调参；
- DEM 支持 tif/tiff/img/asc 和 ESRI Grid（选择 `hdr.adf`）；65535/-9999 自动掩膜；支持中文路径；
- GRASS 为 GPL v2+ 软件，分发请保留 `runtime\grass` 内许可证文件及源码获取说明；
- 如果目标机器已有 GRASS，也可在工具中手动指定系统 `grass85.bat`。

## 6. 卸载

删除：

```text
%LOCALAPPDATA%\BasinSlopeTools
```

以及 ArcGIS Pro AddIns 下的 `BasinSlopeTools.esriAddinX` 即可；环境变量 `BASINSLOPE_GRASS` 可手动删除。

















