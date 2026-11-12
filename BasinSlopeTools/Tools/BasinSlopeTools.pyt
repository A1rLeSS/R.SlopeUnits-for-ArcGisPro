# -*- coding: utf-8 -*-
"""ArcGIS Pro Python Toolbox: R.slopeunits (GRASS backend)."""
import datetime
import csv
import html
import itertools
import json
import math
import os
import re
import shutil
import statistics
import sys
import tempfile
import traceback

import arcpy

TOOL_VERSION = "0.5.0"
TOOLS_REVISION = "2026-11-11.1"

HERE = os.path.dirname(os.path.abspath(__file__))
ENGINE_DIR = os.path.join(HERE, "grass_addons")
if ENGINE_DIR not in sys.path:
    sys.path.insert(0, ENGINE_DIR)
from r_slopeunits_engine import run, default_grass  # noqa: E402

PRESETS = {
    "自定义": None,
    "13m / 2km²": {
        "thresh": 250000, "areamin": 200000, "cvmin": 0.25, "rf": 2,
        "maxiteration": 50, "convergence": 5, "cleansize": 25000,
    },
    "30m / 5km²": {
        "thresh": 500000, "areamin": 500000, "cvmin": 0.25, "rf": 2,
        "maxiteration": 50, "convergence": 5, "cleansize": 50000,
    },
    "快速预览": {
        "thresh": 50000, "areamin": 50000, "cvmin": 0.20, "rf": 2,
        "maxiteration": 10, "convergence": 5, "cleansize": 5000,
    },
}


def _percentile(values, p):
    if not values:
        return None
    vals = sorted(values)
    if len(vals) == 1:
        return vals[0]
    k = (len(vals) - 1) * p
    lo = int(k)
    hi = min(lo + 1, len(vals) - 1)
    return vals[lo] + (vals[hi] - vals[lo]) * (k - lo)


def _environment_info(grass_bat=None):
    info = {
        "tool_version": TOOL_VERSION,
        "tools_revision": TOOLS_REVISION,
        "python": sys.version.split()[0],
        "platform": sys.platform,
    }
    try:
        install = arcpy.GetInstallInfo()
        info["arcgis_pro_version"] = install.get("Version")
    except Exception:
        pass
    try:
        if grass_bat:
            grass_root = os.path.dirname(os.path.dirname(os.path.abspath(grass_bat)))
            version_file = os.path.join(grass_root, "VERSIONNUMBER")
            if os.path.exists(version_file):
                with open(version_file, "r", encoding="utf-8", errors="replace") as f:
                    info["grass_version"] = f.read().strip()
    except Exception:
        pass
    return info


def _render_preview(fc, output_png, value_field, area_field, max_polygons=30000):
    """Render a slope-unit preview PNG for the quality report."""
    try:
        import matplotlib
        matplotlib.use("Agg")
        import matplotlib.pyplot as plt
        from matplotlib.collections import PolyCollection
        from matplotlib import colors as mcolors
        from matplotlib import colormaps
        import numpy as np

        total = int(arcpy.GetCount_management(fc).getOutput(0))
        stride = max(1, int(math.ceil(total / float(max_polygons))))
        field = value_field or area_field
        if not field:
            return None
        polygons = []
        values = []
        minx = miny = float("inf")
        maxx = maxy = float("-inf")
        with arcpy.da.SearchCursor(fc, ["SHAPE@", field]) as cursor:
            for idx, row in enumerate(cursor):
                if idx % stride:
                    continue
                shape, value = row
                if shape is None:
                    continue
                try:
                    parts = list(shape)
                except Exception:
                    parts = [shape]
                for part in parts:
                    points = []
                    for point in part:
                        if point is None:
                            continue
                        points.append((point.X, point.Y))
                        minx = min(minx, point.X)
                        maxx = max(maxx, point.X)
                        miny = min(miny, point.Y)
                        maxy = max(maxy, point.Y)
                    if len(points) >= 3:
                        polygons.append(points)
                        values.append(float(value) if value is not None else 0.0)
        if not polygons:
            return None

        values = np.asarray(values, dtype=float)
        norm = mcolors.Normalize(vmin=float(np.nanmin(values)),
                                 vmax=float(np.nanmax(values)))
        cmap = colormaps["viridis"]
        facecolors = cmap(norm(values))
        fig, ax = plt.subplots(figsize=(10, 8))
        collection = PolyCollection(polygons, facecolors=facecolors,
                                    edgecolors="none", linewidths=0)
        ax.add_collection(collection)
        ax.set_xlim(minx, maxx)
        ax.set_ylim(miny, maxy)
        ax.set_aspect("equal")
        ax.axis("off")
        title = "Slope units preview"
        if stride > 1:
            title += " (sampled {0}/{1})".format(len(polygons), total)
        ax.set_title(title + "  |  {0} units".format(total))
        sm = plt.cm.ScalarMappable(norm=norm, cmap=cmap)
        sm.set_array([])
        cbar = fig.colorbar(sm, ax=ax, shrink=0.65, pad=0.02)
        cbar.set_label(value_field or area_field)
        fig.tight_layout()
        fig.savefig(output_png, dpi=120, bbox_inches="tight")
        plt.close(fig)
        return output_png
    except Exception as exc:
        try:
            arcpy.AddWarning("生成结果预览图失败: " + str(exc))
        except Exception:
            pass
        return None


def _quality_assessment(areas, cvars, compactness, params, clean_failed):
    """综合生成效果指标：CVAR 合格率 + 面积合格率 + clean 状态。"""
    def number(key):
        try:
            value = params.get(key)
            return float(value) if value is not None else None
        except (TypeError, ValueError):
            return None

    cvmin = number("cvmin")
    areamin = number("areamin")
    areamax = number("areamax")

    cvar_pass_rate = None
    cvar_ok = 0
    if cvars and cvmin is not None:
        cvar_ok = sum(1 for value in cvars if value <= cvmin + 1e-9)
        cvar_pass_rate = cvar_ok / len(cvars)

    area_pass_rate = None
    area_ok = 0
    if areas and areamin is not None:
        if areamax is not None:
            area_ok = sum(1 for value in areas if areamin <= value <= areamax)
        else:
            area_ok = sum(1 for value in areas if value >= areamin)
        area_pass_rate = area_ok / len(areas)

    shape_threshold = 0.20
    shape_pass_rate = None
    shape_ok = 0
    sliver_rate = None
    if compactness:
        shape_ok = sum(1 for value in compactness if value >= shape_threshold)
        shape_pass_rate = shape_ok / len(compactness)
        sliver_rate = sum(1 for value in compactness if value < 0.15) / len(compactness)

    components = []
    if cvar_pass_rate is not None:
        components.append((0.45, cvar_pass_rate))
    if area_pass_rate is not None:
        components.append((0.25, area_pass_rate))
    if shape_pass_rate is not None:
        components.append((0.20, shape_pass_rate))
    if not components:
        return {
            "score": None, "grade": "不可评估",
            "cvar_pass_rate": None, "cvar_ok": 0,
            "area_pass_rate": None, "area_ok": 0,
            "shape_pass_rate": None, "shape_ok": 0,
            "sliver_rate": None, "shape_threshold": shape_threshold,
            "method": "缺少 CVAR/AREA_M2/几何字段，无法计算综合评分",
        }

    # clean 失败使用原始单元，扣 10 分权重。
    components.append((0.10, 0.0 if clean_failed else 1.0))
    weight = sum(item[0] for item in components)
    score = round(100.0 * sum(w * value for w, value in components) / weight, 1)
    if score >= 85:
        grade = "优"
    elif score >= 70:
        grade = "良"
    elif score >= 55:
        grade = "合格"
    else:
        grade = "需检查"
    return {
        "score": score,
        "grade": grade,
        "cvar_pass_rate": cvar_pass_rate,
        "cvar_ok": cvar_ok,
        "area_pass_rate": area_pass_rate,
        "area_ok": area_ok,
        "shape_pass_rate": shape_pass_rate,
        "shape_ok": shape_ok,
        "sliver_rate": sliver_rate,
        "shape_threshold": shape_threshold,
        "method": "CVAR 45% + 面积 25% + 形状紧凑度 20%（PP≥0.20）+ clean 10%",
    }


def _quality_report(out_gpkg, dem, params, clean_failed, elapsed, gdb_fc,
                    grass_bat=None):
    """生成质量报告：HTML/JSON/CSV/PNG，并复制最新报告供插件按钮显示。"""
    fc = os.path.join(out_gpkg, "slope_units")
    if not arcpy.Exists(fc):
        fc = out_gpkg

    fields = [f.name for f in arcpy.ListFields(fc)]
    area_field = next((f for f in fields if f.upper() == "AREA_M2"), None)
    cvar_field = next(
        (f for f in fields if f.upper() in ("CVAR", "CVAR_AVG", "CVAR_AVERAGE")),
        None)
    feature_count = 0
    try:
        feature_count = int(arcpy.GetCount_management(fc).getOutput(0))
    except Exception:
        pass

    areas = []
    cvars = []
    try:
        cursor_fields = [area_field] if area_field else []
        if cvar_field:
            cursor_fields.append(cvar_field)
        if cursor_fields:
            with arcpy.da.SearchCursor(fc, cursor_fields) as cursor:
                for row in cursor:
                    if area_field and row[0] is not None:
                        areas.append(float(row[0]))
                    if cvar_field:
                        idx = 1 if area_field else 0
                        if row[idx] is not None:
                            cvars.append(float(row[idx]))
    except Exception as exc:
        arcpy.AddWarning(f"质量报告读取属性失败: {exc}")

    compactness = []
    try:
        with arcpy.da.SearchCursor(fc, ["SHAPE@AREA", "SHAPE@LENGTH"]) as cursor:
            for shape_area, shape_length in cursor:
                if shape_area and shape_length and shape_length > 0:
                    value = 4.0 * math.pi * float(shape_area) / (
                        float(shape_length) ** 2)
                    compactness.append(max(0.0, min(1.0, value)))
    except Exception as exc:
        arcpy.AddWarning(f"质量报告读取几何失败: {exc}")

    def stat_block(values):
        if not values:
            return {"count": 0}
        return {
            "count": len(values),
            "min": min(values),
            "max": max(values),
            "mean": statistics.mean(values),
            "median": statistics.median(values),
            "p05": _percentile(values, 0.05),
            "p95": _percentile(values, 0.95),
            "sum": sum(values),
        }

    area_stats = stat_block(areas)
    cvar_stats = stat_block(cvars)
    compactness_stats = stat_block(compactness)
    assessment = _quality_assessment(areas, cvars, compactness, params, clean_failed)
    report = {
        "generated": datetime.datetime.now().isoformat(timespec="seconds"),
        "dem": os.path.abspath(dem),
        "output": os.path.abspath(out_gpkg),
        "output_gdb": os.path.abspath(gdb_fc) if gdb_fc else None,
        "feature_count": feature_count,
        "clean_failed": bool(clean_failed),
        "elapsed_seconds": round(elapsed, 2),
        "area_m2": area_stats,
        "cvar": cvar_stats,
        "compactness": compactness_stats,
        "assessment": assessment,
        "environment": _environment_info(grass_bat),
        "parameters": params,
    }

    base = os.path.splitext(out_gpkg)[0]
    json_path = base + "_quality_report.json"
    html_path = base + "_quality_report.html"
    csv_path = base + "_quality_report.csv"
    preview_path = base + "_preview.png"
    preview = _render_preview(fc, preview_path, cvar_field, area_field)
    report["preview"] = preview

    with open(json_path, "w", encoding="utf-8") as f:
        json.dump(report, f, ensure_ascii=False, indent=2)

    with open(csv_path, "w", encoding="utf-8-sig", newline="") as f:
        f.write("metric,value\n")
        f.write(f"feature_count,{report['feature_count']}\n")
        f.write(f"elapsed_seconds,{report['elapsed_seconds']}\n")
        f.write(f"quality_score,{assessment.get('score')}\n")
        f.write(f"quality_grade,{assessment.get('grade')}\n")
        f.write(f"cvar_pass_rate,{assessment.get('cvar_pass_rate')}\n")
        f.write(f"area_pass_rate,{assessment.get('area_pass_rate')}\n")
        f.write(f"shape_pass_rate,{assessment.get('shape_pass_rate')}\n")
        f.write(f"sliver_rate,{assessment.get('sliver_rate')}\n")
        for key, value in area_stats.items():
            f.write(f"area_{key},{value}\n")
        for key, value in cvar_stats.items():
            f.write(f"cvar_{key},{value}\n")
        for key, value in compactness_stats.items():
            f.write(f"compactness_{key},{value}\n")

    def fmt(value, digits=2):
        if value is None:
            return "—"
        if isinstance(value, float):
            return f"{value:.{digits}f}"
        return str(value)

    def pct(value):
        if value is None:
            return "—"
        return f"{value * 100:.1f}%"

    rows = [
        ("生成效果综合评分", "—" if assessment.get("score") is None
         else f"{assessment['score']:.1f} / 100"),
        ("生成质量等级", assessment.get("grade") or "—"),
        ("CVAR 合格率", pct(assessment.get("cvar_pass_rate"))),
        ("面积合格率", pct(assessment.get("area_pass_rate"))),
        ("形状紧凑合格率 (PP≥0.20)", pct(assessment.get("shape_pass_rate"))),
        ("狭长单元比例 (PP<0.15)", pct(assessment.get("sliver_rate"))),
        ("单元数量", report["feature_count"]),
        ("运行耗时 (s)", report["elapsed_seconds"]),
        ("clean 状态", "失败（使用 raw）" if clean_failed else "成功"),
        ("面积 min (m²)", fmt(area_stats.get("min"))),
        ("面积 mean (m²)", fmt(area_stats.get("mean"))),
        ("面积 median (m²)", fmt(area_stats.get("median"))),
        ("面积 P95 (m²)", fmt(area_stats.get("p95"))),
        ("面积 max (m²)", fmt(area_stats.get("max"))),
        ("CVAR mean", fmt(cvar_stats.get("mean"), 4)),
        ("CVAR P95", fmt(cvar_stats.get("p95"), 4)),
        ("紧凑度 mean", fmt(compactness_stats.get("mean"), 4)),
        ("紧凑度 median", fmt(compactness_stats.get("median"), 4)),
    ]
    rows_html = "\n".join(
        f"<tr><th>{html.escape(str(k))}</th><td>{html.escape(str(v))}</td></tr>"
        for k, v in rows)
    params_html = "\n".join(
        f"<tr><th>{html.escape(str(k))}</th><td>{html.escape(str(v))}</td></tr>"
        for k, v in params.items())
    env_html = "\n".join(
        f"<tr><th>{html.escape(str(k))}</th><td>{html.escape(str(v))}</td></tr>"
        for k, v in (report.get("environment") or {}).items())
    preview_html = ""
    if preview and os.path.exists(preview):
        try:
            import base64
            with open(preview, "rb") as image_file:
                encoded = base64.b64encode(image_file.read()).decode("ascii")
            preview_html = (
                '<h2>结果预览</h2>'
                '<p><img alt="Slope units preview" style="max-width:100%;height:auto;" '
                'src="data:image/png;base64,' + encoded + '"></p>')
        except Exception as exc:
            arcpy.AddWarning("嵌入预览图失败: " + str(exc))
    page = f"""<!DOCTYPE html>
<html lang="zh-CN"><head><meta charset="utf-8">
<title>R.slopeunits 质量报告</title>
<style>
body {{ font-family: 'Microsoft YaHei', Arial, sans-serif; margin: 28px; color: #222; }}
h1 {{ font-size: 24px; }} h2 {{ font-size: 17px; margin-top: 24px; }}
table {{ border-collapse: collapse; width: 100%; margin-top: 8px; }}
th, td {{ border: 1px solid #ddd; padding: 7px 10px; text-align: left; }}
th {{ background: #f3f6fa; width: 35%; }}
.warn {{ color: #b00020; font-weight: bold; }}
</style></head><body>
<h1>R.slopeunits 斜坡单元质量报告</h1>
<p>生成时间：{html.escape(report['generated'])}</p>
<p>DEM：{html.escape(report['dem'])}</p>
<p>输出：{html.escape(report['output'])}</p>
<p><b>生成效果综合评分：</b>{html.escape(str(assessment.get('score') if assessment.get('score') is not None else '—'))}
 / 100 &nbsp; <b>等级：</b>{html.escape(str(assessment.get('grade') or '—'))}</p>
<p>评分方法：{html.escape(str(assessment.get('method') or ''))}</p>
{preview_html}
<h2>质量指标</h2><table>{rows_html}</table>
<h2>运行参数</h2><table>{params_html}</table>
<h2>环境与版本</h2><table>{env_html}</table>
</body></html>"""
    with open(html_path, "w", encoding="utf-8") as f:
        f.write(page)

    latest_dir = os.path.join(os.environ.get("LOCALAPPDATA", tempfile.gettempdir()),
                              "BasinSlopeTools")
    history_dir = os.path.join(latest_dir, "reports")
    os.makedirs(history_dir, exist_ok=True)
    stamp = datetime.datetime.now().strftime("%Y%m%d_%H%M%S_%f")
    safe = re.sub(r"[^0-9A-Za-z_\-\u4e00-\u9fff]+", "_",
                  os.path.splitext(os.path.basename(out_gpkg))[0]).strip("_") or "report"
    hist_html = os.path.join(history_dir, f"{stamp}_{safe}.html")
    hist_json = os.path.join(history_dir, f"{stamp}_{safe}.json")
    hist_preview = os.path.join(history_dir, f"{stamp}_{safe}_preview.png") if preview else None
    shutil.copyfile(html_path, hist_html)
    shutil.copyfile(json_path, hist_json)
    if preview and os.path.exists(preview):
        shutil.copyfile(preview, hist_preview)

    history_path = os.path.join(latest_dir, "quality_reports.json")
    entries = []
    try:
        if os.path.exists(history_path):
            with open(history_path, "r", encoding="utf-8") as f:
                entries = json.load(f) or []
            if not isinstance(entries, list):
                entries = []
    except Exception:
        entries = []
    previous = entries[-1] if entries else None
    compare_html = None
    if previous:
        try:
            with open(previous["json"], "r", encoding="utf-8") as f:
                prev = json.load(f)
            cur_rows = [
                ("生成效果评分", assessment.get("score")),
                ("单元数量", report.get("feature_count")),
                ("运行耗时 (s)", report.get("elapsed_seconds")),
                ("面积均值 (m²)", (report.get("area_m2") or {}).get("mean")),
                ("CVAR 均值", (report.get("cvar") or {}).get("mean")),
            ]
            prev_rows = [
                (prev.get("assessment") or {}).get("score"),
                prev.get("feature_count"),
                prev.get("elapsed_seconds"),
                (prev.get("area_m2") or {}).get("mean"),
                (prev.get("cvar") or {}).get("mean"),
            ]
            trs = []
            for (label, cur), old in zip(cur_rows, prev_rows):
                if isinstance(cur, float) and isinstance(old, float):
                    delta = f"{cur - old:+.3f}"
                else:
                    delta = "—"
                trs.append(
                    f"<tr><th>{html.escape(label)}</th>"
                    f"<td>{html.escape(str(old))}</td>"
                    f"<td>{html.escape(str(cur))}</td>"
                    f"<td>{html.escape(delta)}</td></tr>")
            compare_html = os.path.join(history_dir, f"{stamp}_{safe}_compare.html")
            with open(compare_html, "w", encoding="utf-8") as f:
                f.write(f"""<!DOCTYPE html><html lang="zh-CN"><head><meta charset="utf-8">
<title>质量报告对比</title>
<style>body{{font-family:'Microsoft YaHei',Arial;margin:28px}}
table{{border-collapse:collapse;width:100%}}th,td{{border:1px solid #ddd;padding:7px 10px;text-align:left}}
th{{background:#f3f6fa;width:28%}}</style></head><body>
<h1>质量报告对比</h1>
<p>当前：{html.escape(stamp)}</p>
<p>上一份：{html.escape(str(previous.get('label', '')))}</p>
<table><tr><th>指标</th><th>上一份</th><th>当前</th><th>变化</th></tr>
{''.join(trs)}</table></body></html>""")
        except Exception:
            compare_html = None
    entries.append({
        "label": f"{safe} · {datetime.datetime.now().strftime('%Y-%m-%d %H:%M:%S')}",
        "created": datetime.datetime.now().isoformat(timespec="seconds"),
        "html": hist_html,
        "json": hist_json,
        "compare_html": compare_html,
    })
    dropped = entries[:-50] if len(entries) > 50 else []
    entries = entries[-50:]
    history_written = False
    try:
        with open(history_path, "w", encoding="utf-8") as f:
            json.dump(entries, f, ensure_ascii=False, indent=2)
        history_written = True
    except Exception as exc:
        arcpy.AddWarning("质量报告历史写入失败: " + str(exc))
    # 历史索引只保留 50 条，同时清理被淘汰的历史文件，避免无限增长。
    if history_written:
        history_root = os.path.abspath(history_dir) + os.sep
        for old in dropped:
            for key in ("html", "json", "compare_html"):
                old_path = old.get(key)
                if not old_path:
                    continue
                try:
                    if os.path.abspath(old_path).startswith(history_root):
                        os.remove(old_path)
                except Exception:
                    pass
    shutil.copyfile(html_path, os.path.join(latest_dir, "latest_quality_report.html"))
    shutil.copyfile(json_path, os.path.join(latest_dir, "latest_quality_report.json"))
    if preview and os.path.exists(preview):
        shutil.copyfile(preview, os.path.join(latest_dir, "latest_preview.png"))

    report["paths"] = {
        "html": html_path, "json": json_path, "csv": csv_path,
        "preview": preview,
        "history_html": hist_html, "history_json": hist_json,
        "history_preview": hist_preview,
        "compare_html": compare_html, "history_index": history_path,
        "label": entries[-1]["label"],
    }
    return report


class Toolbox(object):
    def __init__(self):
        self.label = "R.slopeunits 斜坡单元"
        self.alias = "r_slopeunits"
        self.tools = [SlopeUnits, BatchSlopeUnits, SweepSlopeUnits]


class SlopeUnits(object):
    def __init__(self):
        self.label = "生成斜坡单元 (R.slopeunits)"
        self.description = "在 ArcGIS Pro 内调用 GRASS r.slopeunits.create/clean 生成斜坡单元"
        self.canRunInBackground = False
        self._last_preset = None

    def getParameterInfo(self):
        dem = arcpy.Parameter(
            displayName="输入 DEM（ESRI Grid 请选择 hdr.adf）", name="dem", datatype="DEFile",
            parameterType="Required", direction="Input")
        dem.filter.list = ["tif", "tiff", "img", "asc", "adf"]

        out_gpkg = arcpy.Parameter(
            displayName="输出 GeoPackage", name="out_gpkg", datatype="DEFile",
            parameterType="Required", direction="Output")
        out_gpkg.filter.list = ["gpkg"]

        grass_bat = arcpy.Parameter(
            displayName="GRASS grass85.bat（可选）", name="grass_bat",
            datatype="DEFile", parameterType="Optional", direction="Input")
        grass_bat.filter.list = ["bat"]

        epsg = arcpy.Parameter(
            displayName="DEM EPSG（可选，自动读取；必须与 DEM 一致）", name="epsg",
            datatype="GPLong", parameterType="Optional", direction="Input")

        preset = arcpy.Parameter(
            displayName="参数预设", name="preset", datatype="GPString",
            parameterType="Required", direction="Input")
        preset.filter.list = list(PRESETS.keys())
        preset.value = "自定义"

        thresh = arcpy.Parameter(
            displayName="初始阈值 thresh (m²)", name="thresh",
            datatype="GPDouble", parameterType="Required", direction="Input")
        thresh.value = 250000

        areamin = arcpy.Parameter(
            displayName="最小面积 areamin (m²)", name="areamin",
            datatype="GPDouble", parameterType="Required", direction="Input")
        areamin.value = 200000

        areamax = arcpy.Parameter(
            displayName="最大面积 areamax (m²，可选)", name="areamax",
            datatype="GPDouble", parameterType="Optional", direction="Input")

        cvmin = arcpy.Parameter(
            displayName="坡向圆方差阈值 cvmin (0-1)", name="cvmin",
            datatype="GPDouble", parameterType="Required", direction="Input")
        cvmin.value = 0.25

        rf = arcpy.Parameter(
            displayName="阈值递减因子 rf", name="rf",
            datatype="GPLong", parameterType="Required", direction="Input")
        rf.value = 2

        maxiteration = arcpy.Parameter(
            displayName="最大迭代次数 maxiteration", name="maxiteration",
            datatype="GPLong", parameterType="Required", direction="Input")
        maxiteration.value = 50

        convergence = arcpy.Parameter(
            displayName="MFD 收敛因子 convergence (1-10)", name="convergence",
            datatype="GPLong", parameterType="Required", direction="Input")
        convergence.value = 5

        sfd = arcpy.Parameter(
            displayName="使用 SFD (D8)，默认 MFD", name="sfd",
            datatype="GPBoolean", parameterType="Optional", direction="Input")
        sfd.value = False

        cleansize = arcpy.Parameter(
            displayName="清理小单元面积 cleansize (m²)", name="cleansize",
            datatype="GPLong", parameterType="Required", direction="Input")
        cleansize.value = 25000

        clean_method = arcpy.Parameter(
            displayName="清理方法 clean method", name="clean_method",
            datatype="GPString", parameterType="Required", direction="Input")
        clean_method.filter.list = [
            "快速（方法1）", "条纹清理（方法2）", "详细清理（方法3）"]
        clean_method.value = "快速（方法1）"

        make_gdb = arcpy.Parameter(
            displayName="同时输出 File Geodatabase", name="make_gdb",
            datatype="GPBoolean", parameterType="Optional", direction="Input")
        make_gdb.value = False

        symbology = arcpy.Parameter(
            displayName="自动按 CVAR 符号化", name="symbology",
            datatype="GPBoolean", parameterType="Optional", direction="Input")
        symbology.value = True

        map_output = arcpy.Parameter(
            displayName="加载到当前地图", name="map_output",
            datatype="GPBoolean", parameterType="Optional", direction="Input")
        map_output.value = True

        return [dem, out_gpkg, grass_bat, epsg, preset, thresh, areamin,
                areamax, cvmin, rf, maxiteration, convergence, sfd, cleansize,
                clean_method, make_gdb, symbology, map_output]

    def isLicensed(self):
        return True

    def updateParameters(self, parameters):
        if not parameters[2].altered:
            parameters[2].value = default_grass()

        # DEM 已有 EPSG 时自动填入，但仍允许手动覆盖。
        if parameters[0].valueAsText and not parameters[3].altered:
            try:
                dem_path = _normalize_dem_path(parameters[0].valueAsText)
                sr = arcpy.Describe(dem_path).spatialReference
                if sr and sr.factoryCode:
                    parameters[3].value = int(sr.factoryCode)
            except Exception:
                pass

        preset = parameters[4].valueAsText or "自定义"
        if preset != self._last_preset:
            self._last_preset = preset
            values = PRESETS.get(preset)
            if values:
                parameters[5].value = values["thresh"]
                parameters[6].value = values["areamin"]
                parameters[7].value = None
                parameters[8].value = values["cvmin"]
                parameters[9].value = values["rf"]
                parameters[10].value = values["maxiteration"]
                parameters[11].value = values["convergence"]
                parameters[13].value = values["cleansize"]
        if parameters[16].value and parameters[17].value and not parameters[15].value:
            parameters[15].value = True
        return

    def updateMessages(self, parameters):
        def err(index, message):
            if message:
                parameters[index].setErrorMessage(message)
            else:
                parameters[index].clearMessage()

        for i in range(len(parameters)):
            parameters[i].clearMessage()

        dem = parameters[0].valueAsText
        out_gpkg = parameters[1].valueAsText
        grass_bat = parameters[2].valueAsText
        epsg = parameters[3].value
        thresh = parameters[5].value
        areamin = parameters[6].value
        areamax = parameters[7].value
        cvmin = parameters[8].value
        rf = parameters[9].value
        maxiteration = parameters[10].value
        convergence = parameters[11].value
        cleansize = parameters[13].value
        clean_method = parameters[14].valueAsText

        if dem and not os.path.exists(dem):
            err(0, "输入 DEM 不存在")
        if out_gpkg:
            if not out_gpkg.lower().endswith(".gpkg"):
                err(1, "输出必须是 .gpkg 文件")
            out_dir = os.path.dirname(os.path.abspath(out_gpkg))
            if out_dir and not os.path.isdir(out_dir):
                err(1, "输出目录不存在")
        if grass_bat and not os.path.exists(grass_bat):
            err(2, "grass85.bat 不存在")
        if epsg is not None and int(epsg) <= 0:
            err(3, "EPSG 必须是正整数")
        if thresh is not None and float(thresh) <= 0:
            err(5, "thresh 必须大于 0")
        if areamin is not None and float(areamin) <= 0:
            err(6, "areamin 必须大于 0")
        if areamax is not None and float(areamax) <= 0:
            err(7, "areamax 必须大于 0")
        if areamin is not None and areamax is not None and float(areamax) <= float(areamin):
            err(7, "areamax 必须大于 areamin")
        if cvmin is not None and not (0.0 <= float(cvmin) <= 1.0):
            err(8, "cvmin 必须在 0–1 之间")
        if rf is not None and int(rf) < 1:
            err(9, "rf 必须 ≥ 1")
        if maxiteration is not None and int(maxiteration) < 1:
            err(10, "maxiteration 必须 ≥ 1")
        if convergence is not None and not (1 <= int(convergence) <= 10):
            err(11, "convergence 必须在 1–10 之间")
        if cleansize is not None and float(cleansize) < 0:
            err(13, "cleansize 不能为负数")
        if clean_method and clean_method not in [
                "快速（方法1）", "条纹清理（方法2）", "详细清理（方法3）"]:
            err(14, "未知的 clean method")
        return

    def _apply_symbology(self, aprx, layer):
        """按 CVAR（缺失时 AREA_M2）应用分级色彩。"""
        if layer is None:
            return
        try:
            fields = [f.name for f in arcpy.ListFields(layer)]
            field = None
            for candidate in ("CVAR", "CVAR_AVG", "CVAR_AVERAGE", "AREA_M2"):
                if candidate in fields:
                    field = candidate
                    break
            if not field:
                return
            sym = layer.symbology
            sym.updateRenderer("GraduatedColorsRenderer")
            renderer = sym.renderer
            renderer.classificationField = field
            renderer.breakCount = 5
            renderer.classificationMethod = "NaturalBreaks"
            ramps = aprx.listColorRamps("Yellow-Green (5 Classes)")
            if ramps:
                renderer.colorRamp = ramps[0]
            layer.symbology = sym
            arcpy.AddMessage("已应用符号化: " + field)
            try:
                aprx.save()
            except Exception:
                pass
        except Exception as exc:
            arcpy.AddWarning(f"符号化失败，已保留默认样式: {exc}")

    def _validate_dem(self, dem):
        """检查投影坐标系、单位、Nodata 和异常高程。"""
        desc = arcpy.Describe(dem)
        sr = desc.spatialReference
        if not sr or sr.type != "Projected":
            raise arcpy.ExecuteError(
                "DEM 必须是投影坐标系（Projected），当前不是。"
                "请先投影到米制坐标系，例如 EPSG:32648。")
        meters_per_unit = getattr(sr, "metersPerUnit", None)
        if meters_per_unit is not None and abs(float(meters_per_unit) - 1.0) > 0.01:
            raise arcpy.ExecuteError(
                f"DEM 线性单位不是米（metersPerUnit={meters_per_unit}），"
                "面积阈值 m² 会失真。请先转换为米制投影坐标系。")

        raster = arcpy.Raster(dem)
        nodata = raster.noDataValue
        cell_w = raster.meanCellWidth
        cell_h = raster.meanCellHeight
        if nodata is not None:
            arcpy.AddWarning(f"DEM 带 Nodata 值 {nodata}，GRASS 将按 Nodata 处理。")
        del raster
        try:
            minv = float(arcpy.GetRasterProperties_management(dem, "MINIMUM").getOutput(0))
            maxv = float(arcpy.GetRasterProperties_management(dem, "MAXIMUM").getOutput(0))
        except Exception:
            try:
                arcpy.CalculateStatistics_management(dem)
                minv = float(arcpy.GetRasterProperties_management(dem, "MINIMUM").getOutput(0))
                maxv = float(arcpy.GetRasterProperties_management(dem, "MAXIMUM").getOutput(0))
            except Exception as exc:
                # 统计创建失败时，用左下角最多 2000×2000 的抽样做兜底检查。
                try:
                    import numpy as np
                    ext = desc.extent
                    ncols = min(2000, max(1, int(ext.width / cell_w)))
                    nrows = min(2000, max(1, int(ext.height / cell_h)))
                    arr = arcpy.RasterToNumPyArray(
                        dem, (ext.XMin, ext.YMin), ncols, nrows).astype(float)
                    arr = arr.ravel()
                    if nodata is not None:
                        arr = arr[arr != float(nodata)]
                    arr = arr[np.isfinite(arr)]
                    if arr.size == 0:
                        raise ValueError("抽样没有有效像元")
                    minv = float(arr.min())
                    maxv = float(arr.max())
                    arcpy.AddWarning(
                        f"无法创建 DEM 统计，已用 {ncols}×{nrows} 抽样检查: {exc}")
                except Exception as exc2:
                    arcpy.AddWarning(
                        f"无法读取 DEM 最小/最大值，跳过异常值检查: {exc2}")
                    return
        if maxv >= 60000 or minv <= -9999:
            arcpy.AddWarning(
                f"DEM 含疑似异常高程（min={minv}, max={maxv}），"
                "GRASS 阶段会自动把 65535/-9999 掩膜为 Nodata。")
        if maxv <= minv:
            raise arcpy.ExecuteError("DEM 最大/最小值异常，无法运行。")
        arcpy.AddMessage(f"DEM 检查通过: min={minv:.2f}, max={maxv:.2f}, "
                         f"cell={cell_w:.3f}×{cell_h:.3f}")

    def _validate_params(self, thresh, areamin, areamax, cvmin, rf,
                         maxiteration, convergence, cleansize):
        if thresh <= 0:
            raise arcpy.ExecuteError("thresh 必须大于 0")
        if areamin <= 0:
            raise arcpy.ExecuteError("areamin 必须大于 0")
        if areamax is not None and areamax <= areamin:
            raise arcpy.ExecuteError("areamax 必须大于 areamin")
        if not (0.0 <= cvmin <= 1.0):
            raise arcpy.ExecuteError("cvmin 必须在 0–1 之间")
        if rf < 1:
            raise arcpy.ExecuteError("rf 必须 ≥ 1")
        if maxiteration < 1:
            raise arcpy.ExecuteError("maxiteration 必须 ≥ 1")
        if not (1 <= convergence <= 10):
            raise arcpy.ExecuteError("convergence 必须在 1–10 之间")
        if cleansize < 0:
            raise arcpy.ExecuteError("cleansize 不能为负数")

    def execute(self, parameters, messages):
        dem = _normalize_dem_path(parameters[0].valueAsText)
        out_gpkg = parameters[1].valueAsText
        grass_bat = parameters[2].valueAsText or default_grass()
        epsg = int(parameters[3].value) if parameters[3].value else None
        thresh = float(parameters[5].value)
        areamin = float(parameters[6].value)
        areamax = float(parameters[7].value) if parameters[7].value else None
        cvmin = float(parameters[8].value)
        rf = int(parameters[9].value)
        maxiteration = int(parameters[10].value)
        convergence = int(parameters[11].value)
        sfd = bool(parameters[12].value)
        cleansize = int(parameters[13].value)
        clean_method = parameters[14].valueAsText or "快速（方法1）"
        make_gdb = bool(parameters[15].value)
        symbology = bool(parameters[16].value)
        map_output = bool(parameters[17].value)
        if symbology and not map_output:
            symbology = False
            arcpy.AddMessage("未选择加载到当前地图，自动符号化已跳过")
        if symbology and not make_gdb:
            make_gdb = True
            arcpy.AddMessage("自动符号化需要 GDB 图层，已同时生成 File Geodatabase")
        start_time = datetime.datetime.now()

        if not grass_bat or not os.path.exists(grass_bat):
            raise arcpy.ExecuteError(
                "找不到 GRASS grass85.bat。请确认安装器已安装 runtime，"
                "或在参数中手动选择 grass85.bat。")

        desc = arcpy.Describe(dem)
        sr = desc.spatialReference
        self._validate_dem(dem)
        self._validate_params(thresh, areamin, areamax, cvmin, rf,
                              maxiteration, convergence, cleansize)
        if clean_method not in [
                "快速（方法1）", "条纹清理（方法2）", "详细清理（方法3）"]:
            raise arcpy.ExecuteError("未知的 clean method: " + str(clean_method))

        if epsg is None:
            epsg = int(sr.factoryCode) if sr and sr.factoryCode else 0
        if not epsg:
            raise arcpy.ExecuteError(
                "DEM 没有 EPSG 代号。请在“DEM EPSG”参数中填写投影坐标系 EPSG，"
                "例如 32648。")
        sr_epsg = int(sr.factoryCode) if sr and sr.factoryCode else 0
        if sr_epsg and int(epsg) != sr_epsg:
            raise arcpy.ExecuteError(
                f"DEM EPSG 与填写值不一致：DEM={sr_epsg}，输入={epsg}。"
                "请清空 DEM EPSG 参数或填写与 DEM 一致的 EPSG。")

        out_dir = os.path.dirname(os.path.abspath(out_gpkg))
        os.makedirs(out_dir, exist_ok=True)
        if not os.access(out_dir, os.W_OK):
            raise arcpy.ExecuteError(f"输出目录不可写: {out_dir}")
        if os.path.exists(out_gpkg):
            arcpy.AddWarning("输出文件已存在，将被覆盖。")

        log_path = os.path.join(
            tempfile.gettempdir(),
            "R_slopeunits_" + datetime.datetime.now().strftime("%Y%m%d_%H%M%S_%f")
            + "_" + str(os.getpid()) + ".log")

        arcpy.AddMessage(f"DEM: {dem}")
        arcpy.AddMessage(f"输出: {out_gpkg}")
        arcpy.AddMessage(f"GRASS: {grass_bat}")
        arcpy.AddMessage(f"EPSG: {epsg}")
        arcpy.AddMessage(f"日志: {log_path}")
        if make_gdb:
            arcpy.AddMessage("将同时输出 File Geodatabase。")

        def progress(line):
            line = line.replace("\r", "").strip()
            if not line:
                return
            # GRASS 的进度条是一行大量百分比；不要把整行塞进 Pro 消息窗。
            if line.count("%") >= 5:
                return
            arcpy.AddMessage(line)

        try:
            result = run(
                dem=dem, out_gpkg=out_gpkg, thresh=thresh, areamin=areamin,
                cvmin=cvmin, rf=rf, maxiteration=maxiteration, areamax=areamax,
                convergence=convergence, sfd=sfd, cleansize=cleansize,
                clean_method=clean_method,
                grass_bat=grass_bat, epsg=epsg, log_path=log_path,
                progress=progress)
        except Exception:
            arcpy.AddError("R.slopeunits 运行失败，详情见日志: " + log_path)
            raise

        if result.get("clean_failed"):
            arcpy.AddWarning("r.slopeunits.clean 失败，已输出 create 的原始斜坡单元。"
                             "请检查日志中的 clean 错误。")

        gdb_fc = None
        if make_gdb:
            try:
                gdb_path = os.path.splitext(out_gpkg)[0] + ".gdb"
                gdb_name = os.path.splitext(os.path.basename(gdb_path))[0]
                if arcpy.Exists(gdb_path):
                    arcpy.management.Delete(gdb_path)
                arcpy.management.CreateFileGDB(out_dir, gdb_name)
                src_layer = os.path.join(out_gpkg, "slope_units")
                if not arcpy.Exists(src_layer):
                    src_layer = out_gpkg
                gdb_fc = os.path.join(gdb_path, "slope_units")
                arcpy.management.CopyFeatures(src_layer, gdb_fc)
                arcpy.AddMessage("GDB: " + gdb_fc)
            except Exception as exc:
                gdb_fc = None
                arcpy.AddWarning(f"GDB 输出失败，GPKG 仍可用: {exc}")

        param_dict = {
            "thresh": thresh, "areamin": areamin, "areamax": areamax,
            "cvmin": cvmin, "rf": rf, "maxiteration": maxiteration,
            "convergence": convergence, "sfd": sfd, "cleansize": cleansize,
            "clean_method": clean_method, "make_gdb": make_gdb,
            "symbology": symbology, "map_output": map_output,
        }
        clean_failed = bool(result.get("clean_failed"))
        elapsed = (datetime.datetime.now() - start_time).total_seconds()
        quality = None
        try:
            quality = _quality_report(
                out_gpkg, dem, param_dict, clean_failed, elapsed, gdb_fc,
                grass_bat=grass_bat)
            arcpy.AddMessage("质量报告: " + quality["paths"]["html"])
            arcpy.AddMessage(
                f"质量摘要: 单元 {quality['feature_count']}，"
                f"面积均值 {quality['area_m2'].get('mean', 0):.2f} m²，"
                f"CVAR 均值 {quality['cvar'].get('mean', 0):.4f}，"
                f"评分 {quality.get('assessment', {}).get('score')} "
                f"({quality.get('assessment', {}).get('grade')})")
        except Exception as exc:
            arcpy.AddWarning(f"质量报告生成失败: {exc}")

        manifest = {
            "tool": "R.slopeunits",
            "version": TOOL_VERSION,
            "tools_revision": TOOLS_REVISION,
            "created": datetime.datetime.now().isoformat(timespec="seconds"),
            "dem": os.path.abspath(dem),
            "output": os.path.abspath(out_gpkg),
            "output_gdb": os.path.abspath(gdb_fc) if gdb_fc else None,
            "epsg": epsg,
            "grass_bat": grass_bat,
            "environment": _environment_info(grass_bat),
            "parameters": param_dict,
            "quality_report": quality["paths"] if quality else None,
            "log": log_path,
        }
        manifest_path = os.path.splitext(out_gpkg)[0] + "_run_manifest.json"
        with open(manifest_path, "w", encoding="utf-8") as f:
            json.dump(manifest, f, ensure_ascii=False, indent=2)
        arcpy.AddMessage("参数清单: " + manifest_path)

        try:
            if not map_output:
                arcpy.AddMessage("已跳过加载到当前地图")
                aprx = None
            else:
                aprx = arcpy.mp.ArcGISProject("CURRENT")
            if aprx and aprx.activeMap:
                target = gdb_fc if gdb_fc else out_gpkg
                layer = None
                try:
                    layer = aprx.activeMap.addDataFromPath(target)
                except Exception:
                    if not gdb_fc:
                        layer = aprx.activeMap.addDataFromPath(
                            os.path.join(out_gpkg, "slope_units"))
                if symbology:
                    self._apply_symbology(aprx, layer)
                arcpy.AddMessage("已添加到当前地图")
        except Exception as exc:
            arcpy.AddWarning(f"结果已生成，但自动加载失败: {exc}")

        arcpy.AddMessage("完成: " + out_gpkg)


# ---------------------------------------------------------------------------
# 批量 / 参数扫描工具
# ---------------------------------------------------------------------------

def _load_state(path):
    try:
        with open(path, "r", encoding="utf-8") as f:
            return json.load(f)
    except Exception:
        return {}


def _save_state(path, state):
    tmp = path + ".tmp"
    with open(tmp, "w", encoding="utf-8") as f:
        json.dump(state, f, ensure_ascii=False, indent=2)
    os.replace(tmp, path)


def _run_slope_tool(template, values):
    params = template.getParameterInfo()
    for p in params:
        if p.name in values:
            p.value = values[p.name]
    template.execute(params, None)


def _normalize_dem_path(value):
    """ESRI Grid 的 .adf 是目录中的分块文件；统一转换为 Grid 目录。"""
    if value is None:
        return None
    path = os.path.abspath(str(value))
    if path.lower().endswith(".adf") and os.path.isfile(path):
        parent = os.path.dirname(path)
        if os.path.isfile(os.path.join(parent, "hdr.adf")):
            return parent
    return path


def _dem_list(value):
    if value is None:
        return []
    if isinstance(value, str):
        return [_normalize_dem_path(v) for v in value.split(";") if v]
    try:
        return [_normalize_dem_path(str(v)) for v in value]
    except Exception:
        return []


def _safe_stem(path):
    stem = os.path.splitext(os.path.basename(path))[0]
    stem = re.sub(r"[^0-9A-Za-z_\-\u4e00-\u9fff]+", "_", stem).strip("_")
    return stem or "dem"


def _quality_metrics(out_gpkg):
    path = os.path.splitext(out_gpkg)[0] + "_quality_report.json"
    try:
        with open(path, "r", encoding="utf-8") as f:
            report = json.load(f)
        return {
            "units": report.get("feature_count"),
            "area_mean": (report.get("area_m2") or {}).get("mean"),
            "cvar_mean": (report.get("cvar") or {}).get("mean"),
            "compactness_mean": (report.get("compactness") or {}).get("mean"),
            "quality_score": (report.get("assessment") or {}).get("score"),
            "quality_grade": (report.get("assessment") or {}).get("grade"),
        }
    except Exception:
        return {
            "units": None, "area_mean": None, "cvar_mean": None,
            "compactness_mean": None, "quality_score": None, "quality_grade": None,
        }


def _write_summary(path, rows, fields):
    if not rows:
        return
    # Include optional score/shape columns without requiring every caller to
    # repeat the full header list.
    fields = list(fields)
    for row in rows:
        for key in row:
            if key not in fields:
                fields.append(key)
    try:
        with open(path, "w", encoding="utf-8-sig", newline="") as f:
            writer = csv.DictWriter(f, fieldnames=fields)
            writer.writeheader()
            writer.writerows(rows)
    except Exception as exc:
        arcpy.AddWarning("写入汇总失败: " + str(exc))

def _parse_number_list(text, integer=False):
    values = []
    for raw in re.split(r"[,;\s]+", str(text or "").strip()):
        if not raw:
            continue
        try:
            values.append(int(raw) if integer else float(raw))
        except ValueError:
            raise arcpy.ExecuteError(f"无法解析数值: {raw}")
    return values


class BatchSlopeUnits(object):
    def __init__(self):
        self.label = "批量生成斜坡单元"
        self.description = "对多个 DEM 批量运行 R.slopeunits；支持断点续跑"
        self.canRunInBackground = False

    def getParameterInfo(self):
        dems = arcpy.Parameter(
            displayName="输入 DEM（多选；ESRI Grid 请选择 hdr.adf）", name="dems", datatype="DEFile",
            parameterType="Required", direction="Input", multiValue=True)
        dems.filter.list = ["tif", "tiff", "img", "asc", "adf"]
        out_folder = arcpy.Parameter(
            displayName="输出文件夹", name="out_folder", datatype="DEFolder",
            parameterType="Required", direction="Input")
        grass_bat = arcpy.Parameter(
            displayName="GRASS grass85.bat（可选）", name="grass_bat",
            datatype="DEFile", parameterType="Optional", direction="Input")
        preset = arcpy.Parameter(
            displayName="参数预设", name="preset", datatype="GPString",
            parameterType="Required", direction="Input")
        preset.filter.list = ["13m / 2km²", "30m / 5km²", "快速预览"]
        preset.value = "13m / 2km²"
        clean_method = arcpy.Parameter(
            displayName="清理方法", name="clean_method", datatype="GPString",
            parameterType="Required", direction="Input")
        clean_method.filter.list = [
            "快速（方法1）", "条纹清理（方法2）", "详细清理（方法3）"]
        clean_method.value = "快速（方法1）"
        make_gdb = arcpy.Parameter(
            displayName="同时输出 File Geodatabase", name="make_gdb",
            datatype="GPBoolean", parameterType="Optional", direction="Input")
        make_gdb.value = False
        symbology = arcpy.Parameter(
            displayName="自动符号化", name="symbology", datatype="GPBoolean",
            parameterType="Optional", direction="Input")
        symbology.value = False
        map_output = arcpy.Parameter(
            displayName="加载到当前地图", name="map_output",
            datatype="GPBoolean", parameterType="Optional", direction="Input")
        map_output.value = False
        resume = arcpy.Parameter(
            displayName="断点续跑（跳过已完成）", name="resume",
            datatype="GPBoolean", parameterType="Optional", direction="Input")
        resume.value = True
        return [dems, out_folder, grass_bat, preset, clean_method,
                make_gdb, symbology, map_output, resume]

    def isLicensed(self):
        return True

    def updateParameters(self, parameters):
        if not parameters[2].altered:
            parameters[2].value = default_grass()
        if parameters[6].value and parameters[7].value and not parameters[5].value:
            parameters[5].value = True

    def updateMessages(self, parameters):
        if not parameters[0].values:
            parameters[0].setErrorMessage("至少选择一个 DEM")
        if not parameters[1].valueAsText:
            parameters[1].setErrorMessage("请选择输出文件夹")

    def execute(self, parameters, messages):
        dems = _dem_list(parameters[0].values)
        out_folder = parameters[1].valueAsText
        grass_bat = parameters[2].valueAsText or default_grass()
        preset = parameters[3].valueAsText or "13m / 2km²"
        clean_method = parameters[4].valueAsText or "快速（方法1）"
        make_gdb = bool(parameters[5].value)
        symbology = bool(parameters[6].value)
        map_output = bool(parameters[7].value)
        resume = bool(parameters[8].value)
        if not dems:
            raise arcpy.ExecuteError("没有输入 DEM")
        if preset not in PRESETS or PRESETS[preset] is None:
            raise arcpy.ExecuteError("批量工具只支持内置预设: " + preset)
        os.makedirs(out_folder, exist_ok=True)
        state_path = os.path.join(out_folder, ".r_slopeunits_batch_state.json")
        state = _load_state(state_path) if resume else {}
        values = dict(PRESETS[preset])
        template = SlopeUnits()
        errors = []
        used = set()
        summary_rows = []
        try:
            arcpy.SetProgressor("step", "批量生成斜坡单元", 0, len(dems), 1)
        except Exception:
            pass
        for task_index, dem in enumerate(dems, 1):
            try:
                arcpy.SetProgressorPosition(task_index)
                arcpy.SetProgressorLabel("批量任务 {0}/{1}: {2}".format(
                    task_index, len(dems), os.path.basename(dem)))
            except Exception:
                pass
            stem = _safe_stem(dem)
            base = stem
            idx = 2
            while base.lower() in used:
                base = f"{stem}_{idx}"
                idx += 1
            used.add(base.lower())
            out = os.path.join(out_folder, base + "_slopeunits.gpkg")
            task_id = os.path.abspath(dem) + "|" + os.path.abspath(out)
            manifest = os.path.splitext(out)[0] + "_run_manifest.json"
            if resume and state.get(task_id, {}).get("status") == "done" \
                    and os.path.exists(out) and os.path.exists(manifest):
                arcpy.AddMessage("跳过已完成: " + dem)
                m = _quality_metrics(out)
                summary_rows.append({
                    "dem": dem, "output": out, "status": "skipped",
                    "units": m["units"], "area_mean": m["area_mean"],
                    "cvar_mean": m["cvar_mean"], "quality_score": m["quality_score"], "quality_grade": m["quality_grade"]})
                _write_summary(
                    os.path.join(out_folder, "batch_summary.csv"), summary_rows,
                    ["dem", "output", "status", "units", "area_mean", "cvar_mean"])
                continue
            arcpy.AddMessage(f"开始批量任务: {os.path.basename(dem)}")
            try:
                _run_slope_tool(template, {
                    "dem": dem, "out_gpkg": out, "grass_bat": grass_bat,
                    "epsg": None, "preset": "自定义",
                    "thresh": values["thresh"], "areamin": values["areamin"],
                    "areamax": None, "cvmin": values["cvmin"],
                    "rf": values["rf"], "maxiteration": values["maxiteration"],
                    "convergence": values["convergence"], "sfd": False,
                    "cleansize": values["cleansize"],
                    "clean_method": clean_method, "make_gdb": make_gdb,
                    "symbology": symbology, "map_output": map_output,
                })
                state[task_id] = {
                    "status": "done", "output": out,
                    "time": datetime.datetime.now().isoformat(timespec="seconds")}
                _save_state(state_path, state)
                m = _quality_metrics(out)
                summary_rows.append({
                    "dem": dem, "output": out, "status": "done",
                    "units": m["units"], "area_mean": m["area_mean"],
                    "cvar_mean": m["cvar_mean"], "quality_score": m["quality_score"], "quality_grade": m["quality_grade"]})
                _write_summary(
                    os.path.join(out_folder, "batch_summary.csv"), summary_rows,
                    ["dem", "output", "status", "units", "area_mean", "cvar_mean"])
            except Exception as exc:
                errors.append(f"{dem}: {exc}")
                state[task_id] = {
                    "status": "failed", "output": out, "error": str(exc),
                    "time": datetime.datetime.now().isoformat(timespec="seconds")}
                _save_state(state_path, state)
                summary_rows.append({
                    "dem": dem, "output": out, "status": "failed",
                    "units": None, "area_mean": None, "cvar_mean": None, "quality_score": None, "quality_grade": None})
                _write_summary(
                    os.path.join(out_folder, "batch_summary.csv"), summary_rows,
                    ["dem", "output", "status", "units", "area_mean", "cvar_mean"])
                arcpy.AddError("批量任务失败: " + str(exc))
        _write_summary(
            os.path.join(out_folder, "batch_summary.csv"), summary_rows,
            ["dem", "output", "status", "units", "area_mean", "cvar_mean"])
        try:
            arcpy.ResetProgressor()
        except Exception:
            pass
        if errors:
            raise arcpy.ExecuteError("批量完成但有 {} 个失败；可再次运行断点续跑".format(len(errors)))
        arcpy.AddMessage("批量完成: " + str(len(dems)) + " 个 DEM")


class SweepSlopeUnits(object):
    def __init__(self):
        self.label = "参数扫描生成斜坡单元"
        self.description = "对阈值/面积/CVAR/迭代次数组合扫描；支持断点续跑"
        self.canRunInBackground = False

    def getParameterInfo(self):
        dem = arcpy.Parameter(
            displayName="输入 DEM（ESRI Grid 请选择 hdr.adf）", name="dem", datatype="DEFile",
            parameterType="Required", direction="Input")
        dem.filter.list = ["tif", "tiff", "img", "asc", "adf"]
        out_folder = arcpy.Parameter(
            displayName="输出文件夹", name="out_folder", datatype="DEFolder",
            parameterType="Required", direction="Input")
        grass_bat = arcpy.Parameter(
            displayName="GRASS grass85.bat（可选）", name="grass_bat",
            datatype="DEFile", parameterType="Optional", direction="Input")
        thresh_list = arcpy.Parameter(
            displayName="阈值 thresh 列表（逗号分隔）", name="thresh_list",
            datatype="GPString", parameterType="Required", direction="Input")
        thresh_list.value = "250000,500000"
        areamin_list = arcpy.Parameter(
            displayName="最小面积 areamin 列表", name="areamin_list",
            datatype="GPString", parameterType="Required", direction="Input")
        areamin_list.value = "200000,500000"
        cvmin_list = arcpy.Parameter(
            displayName="CVAR 阈值 cvmin 列表", name="cvmin_list",
            datatype="GPString", parameterType="Required", direction="Input")
        cvmin_list.value = "0.20,0.25"
        maxiteration_list = arcpy.Parameter(
            displayName="最大迭代次数列表", name="maxiteration_list",
            datatype="GPString", parameterType="Required", direction="Input")
        maxiteration_list.value = "50"
        clean_method = arcpy.Parameter(
            displayName="清理方法", name="clean_method", datatype="GPString",
            parameterType="Required", direction="Input")
        clean_method.filter.list = [
            "快速（方法1）", "条纹清理（方法2）", "详细清理（方法3）"]
        clean_method.value = "快速（方法1）"
        make_gdb = arcpy.Parameter(
            displayName="同时输出 File Geodatabase", name="make_gdb",
            datatype="GPBoolean", parameterType="Optional", direction="Input")
        make_gdb.value = False
        symbology = arcpy.Parameter(
            displayName="自动符号化", name="symbology", datatype="GPBoolean",
            parameterType="Optional", direction="Input")
        symbology.value = False
        map_output = arcpy.Parameter(
            displayName="加载到当前地图", name="map_output",
            datatype="GPBoolean", parameterType="Optional", direction="Input")
        map_output.value = False
        resume = arcpy.Parameter(
            displayName="断点续跑（跳过已完成）", name="resume",
            datatype="GPBoolean", parameterType="Optional", direction="Input")
        resume.value = True
        return [dem, out_folder, grass_bat, thresh_list, areamin_list,
                cvmin_list, maxiteration_list, clean_method, make_gdb,
                symbology, map_output, resume]

    def isLicensed(self):
        return True

    def updateParameters(self, parameters):
        if not parameters[2].altered:
            parameters[2].value = default_grass()
        if parameters[9].value and parameters[10].value and not parameters[8].value:
            parameters[8].value = True

    def updateMessages(self, parameters):
        if not parameters[0].valueAsText:
            parameters[0].setErrorMessage("请选择 DEM")
        if not parameters[1].valueAsText:
            parameters[1].setErrorMessage("请选择输出文件夹")
        try:
            count = 1
            for idx in (3, 4, 5, 6):
                count *= len(_parse_number_list(parameters[idx].valueAsText, idx == 6))
            parameters[0].setWarningMessage(f"将运行 {count} 个参数组合")
        except Exception:
            pass

    def execute(self, parameters, messages):
        dem = _normalize_dem_path(parameters[0].valueAsText)
        out_folder = parameters[1].valueAsText
        grass_bat = parameters[2].valueAsText or default_grass()
        thresh_vals = _parse_number_list(parameters[3].valueAsText)
        areamin_vals = _parse_number_list(parameters[4].valueAsText)
        cvmin_vals = _parse_number_list(parameters[5].valueAsText)
        iter_vals = _parse_number_list(parameters[6].valueAsText, integer=True)
        clean_method = parameters[7].valueAsText or "快速（方法1）"
        make_gdb = bool(parameters[8].value)
        symbology = bool(parameters[9].value)
        map_output = bool(parameters[10].value)
        resume = bool(parameters[11].value)
        if not (thresh_vals and areamin_vals and cvmin_vals and iter_vals):
            raise arcpy.ExecuteError("参数扫描列表不能为空")
        os.makedirs(out_folder, exist_ok=True)
        state_path = os.path.join(out_folder, ".r_slopeunits_sweep_state.json")
        state = _load_state(state_path) if resume else {}
        template = SlopeUnits()
        errors = []
        summary_rows = []
        combos = list(itertools.product(
            thresh_vals, areamin_vals, cvmin_vals, iter_vals))
        try:
            arcpy.SetProgressor("step", "参数扫描生成斜坡单元", 0, len(combos), 1)
        except Exception:
            pass
        for task_index, (thresh, areamin, cvmin, maxiter) in enumerate(combos, 1):
            tag = "t{}_a{}_c{}_i{}".format(
                int(thresh), int(areamin),
                str(cvmin).replace(".", "p"), int(maxiter))
            try:
                arcpy.SetProgressorPosition(task_index)
                arcpy.SetProgressorLabel("参数组合 {0}/{1}: {2}".format(
                    task_index, len(combos), tag))
            except Exception:
                pass
            out = os.path.join(out_folder, tag + "_slopeunits.gpkg")
            task_id = os.path.abspath(dem) + "|" + os.path.abspath(out)
            manifest = os.path.splitext(out)[0] + "_run_manifest.json"
            if resume and state.get(task_id, {}).get("status") == "done" \
                    and os.path.exists(out) and os.path.exists(manifest):
                arcpy.AddMessage("跳过已完成: " + tag)
                m = _quality_metrics(out)
                summary_rows.append({
                    "thresh": thresh, "areamin": areamin, "cvmin": cvmin,
                    "maxiteration": maxiter, "output": out, "status": "skipped",
                    "units": m["units"], "area_mean": m["area_mean"],
                    "cvar_mean": m["cvar_mean"], "quality_score": m["quality_score"], "quality_grade": m["quality_grade"]})
                _write_summary(
                    os.path.join(out_folder, "sweep_summary.csv"), summary_rows,
                    ["thresh", "areamin", "cvmin", "maxiteration", "output",
                     "status", "units", "area_mean", "cvar_mean"])
                continue
            arcpy.AddMessage("开始参数组合: " + tag)
            try:
                _run_slope_tool(template, {
                    "dem": dem, "out_gpkg": out, "grass_bat": grass_bat,
                    "epsg": None, "preset": "自定义",
                    "thresh": thresh, "areamin": areamin, "areamax": None,
                    "cvmin": cvmin, "rf": 2, "maxiteration": maxiter,
                    "convergence": 5, "sfd": False, "cleansize": 25000,
                    "clean_method": clean_method, "make_gdb": make_gdb,
                    "symbology": symbology, "map_output": map_output,
                })
                state[task_id] = {
                    "status": "done", "output": out,
                    "time": datetime.datetime.now().isoformat(timespec="seconds")}
                _save_state(state_path, state)
                m = _quality_metrics(out)
                summary_rows.append({
                    "thresh": thresh, "areamin": areamin, "cvmin": cvmin,
                    "maxiteration": maxiter, "output": out, "status": "done",
                    "units": m["units"], "area_mean": m["area_mean"],
                    "cvar_mean": m["cvar_mean"], "quality_score": m["quality_score"], "quality_grade": m["quality_grade"]})
                _write_summary(
                    os.path.join(out_folder, "sweep_summary.csv"), summary_rows,
                    ["thresh", "areamin", "cvmin", "maxiteration", "output",
                     "status", "units", "area_mean", "cvar_mean"])
            except Exception as exc:
                errors.append(f"{tag}: {exc}")
                state[task_id] = {
                    "status": "failed", "output": out, "error": str(exc),
                    "time": datetime.datetime.now().isoformat(timespec="seconds")}
                _save_state(state_path, state)
                summary_rows.append({
                    "thresh": thresh, "areamin": areamin, "cvmin": cvmin,
                    "maxiteration": maxiter, "output": out, "status": "failed",
                    "units": None, "area_mean": None, "cvar_mean": None, "quality_score": None, "quality_grade": None})
                _write_summary(
                    os.path.join(out_folder, "sweep_summary.csv"), summary_rows,
                    ["thresh", "areamin", "cvmin", "maxiteration", "output",
                     "status", "units", "area_mean", "cvar_mean"])
                arcpy.AddError("参数组合失败: " + str(exc))
        _write_summary(
            os.path.join(out_folder, "sweep_summary.csv"), summary_rows,
            ["thresh", "areamin", "cvmin", "maxiteration", "output", "status",
             "units", "area_mean", "cvar_mean"])
        try:
            arcpy.ResetProgressor()
        except Exception:
            pass
        if errors:
            raise arcpy.ExecuteError("参数扫描完成但有 {} 个失败；可再次运行断点续跑".format(len(errors)))
        arcpy.AddMessage("参数扫描完成: " + str(len(combos)) + " 个组合")



