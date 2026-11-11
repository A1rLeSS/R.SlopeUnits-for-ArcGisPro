# -*- coding: utf-8 -*-
"""GRASS r.slopeunits 运行器（在 GRASS 会话内执行）。"""
import argparse
import os
import subprocess
import sys
import grass.script as gs

try:
    sys.stdout.reconfigure(line_buffering=True)
except Exception:
    pass

HERE = os.path.dirname(os.path.abspath(__file__))
ADDONS = HERE


def run_addon(script, args):
    """在 GRASS 会话内运行 addon Python 脚本。"""
    env = os.environ.copy()
    env["GRASS_OVERWRITE"] = "1"
    proc = subprocess.run(
        [sys.executable, script] + args,
        capture_output=True, text=True, encoding="utf-8", errors="replace", env=env,
    )
    if proc.stdout:
        gs.message(proc.stdout[-2000:])
    if proc.returncode != 0:
        raise RuntimeError(
            f"addon failed: {script}\nSTDOUT:\n{proc.stdout[-3000:]}\nSTDERR:\n{proc.stderr[-3000:]}"
        )
    if proc.stderr:
        gs.message(proc.stderr[-1000:])


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--dem", required=True)
    ap.add_argument("--out", required=True)
    ap.add_argument("--thresh", type=float, required=True)
    ap.add_argument("--areamin", type=float, required=True)
    ap.add_argument("--areamax", type=float, default=None)
    ap.add_argument("--cvmin", type=float, required=True)
    ap.add_argument("--rf", type=int, required=True)
    ap.add_argument("--maxiteration", type=int, required=True)
    ap.add_argument("--convergence", type=int, default=5)
    ap.add_argument("--sfd", action="store_true")
    ap.add_argument("--cleansize", type=float, default=25000)
    ap.add_argument("--clean-method", default="快速（方法1）")
    args = ap.parse_args()

    gs.run_command("r.in.gdal", input=args.dem, output="dem", overwrite=True)
    gs.run_command("g.region", raster="dem")
    # ESRI Grid / 常见 DEM 可能用 65535、-9999 表示空洞；统一掩膜后再计算。
    for sentinel in ("65535", "-9999"):
        try:
            gs.run_command("r.null", map="dem", setnull=sentinel)
        except Exception as exc:
            gs.message(f"warning: r.null setnull={sentinel} failed: {exc}")
    print("DEM_SENTINEL_MASK=65535,-9999", flush=True)

    create_args = [
        "demmap=dem",
        "slumap=slu_raw",
        f"thresh={args.thresh}",
        f"areamin={args.areamin}",
        f"cvmin={args.cvmin}",
        f"rf={args.rf}",
        f"maxiteration={args.maxiteration}",
        f"convergence={args.convergence}",
    ]
    if args.areamax:
        create_args.append(f"areamax={args.areamax}")
    if args.sfd:
        create_args.append("-s")
    run_addon(os.path.join(ADDONS, "r.slopeunits.create.py"), create_args)

    clean_args = [
        "demmap=dem",
        "slumap=slu_raw",
        "slumapclean=slu_clean",
        f"cleansize={int(args.cleansize)}",
    ]
    if args.clean_method == "条纹清理（方法2）":
        clean_args.append("-m")
    elif args.clean_method == "详细清理（方法3）":
        clean_args.append("-n")
    try:
        run_addon(os.path.join(ADDONS, "r.slopeunits.clean.py"), clean_args)
        final = "slu_clean"
        print("CLEAN_STATUS=OK", flush=True)
    except Exception as exc:
        print("CLEAN_STATUS=FAILED: " + str(exc), flush=True)
        gs.message(f"clean failed, using raw slope units: {exc}")
        final = "slu_raw"

    gs.run_command("r.to.vect", input=final, output="slu_vect", type="area", overwrite=True)
    try:
        gs.run_command("r.slope.aspect", elevation="dem", aspect="aspect", overwrite=True)
        gs.mapcalc("coseno = cos(aspect)", overwrite=True)
        gs.mapcalc("seno = sin(aspect)", overwrite=True)
        gs.run_command("r.stats.zonal", base=final, cover="coseno",
                       method="sum", output="sumcos", overwrite=True)
        gs.run_command("r.stats.zonal", base=final, cover="seno",
                       method="sum", output="sumsin", overwrite=True)
        gs.run_command("r.stats.zonal", base=final, cover=final,
                       method="count", output="su_count", overwrite=True)
        gs.mapcalc(
            "cvar_slu = 1 - sqrt(sumcos*sumcos + sumsin*sumsin) / su_count",
            overwrite=True)
        gs.run_command("v.rast.stats", map="slu_vect", raster="cvar_slu",
                       column_prefix="cvar", method="average")
        try:
            gs.run_command("v.db.renamecolumn", map="slu_vect",
                           column="cvar_average,CVAR")
        except Exception:
            pass
        print("CVAR_FIELD=OK", flush=True)
    except Exception as exc:
        print("CVAR_FIELD=FAILED: " + str(exc), flush=True)
        gs.message(f"CVAR field failed: {exc}")
    try:
        gs.run_command("v.db.addcolumn", map="slu_vect",
                       columns="AREA_M2 double precision")
        gs.run_command("v.to.db", map="slu_vect", option="area",
                       columns="AREA_M2", units="meters")
        print("AREA_FIELD=OK", flush=True)
    except Exception as exc:
        print("AREA_FIELD=FAILED: " + str(exc), flush=True)
        gs.message(f"area field failed: {exc}")

    out = os.path.abspath(args.out).replace("\\", "/")
    if os.path.exists(args.out):
        os.remove(args.out)
    gs.run_command(
        "v.out.ogr",
        input="slu_vect",
        output=out,
        format="GPKG",
        output_layer="slope_units",
        overwrite=True,
    )
    print(f"WROTE {args.out}", flush=True)
    gs.message(f"WROTE {args.out}")


if __name__ == "__main__":
    main()


