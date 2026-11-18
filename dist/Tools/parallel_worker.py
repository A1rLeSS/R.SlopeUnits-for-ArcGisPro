# -*- coding: utf-8 -*-
"""Child-process entry point for parallel batch/sweep tasks.

Run with the same ArcGIS Pro Python that hosts the Python Toolbox. Each child
imports a private copy of the toolbox and executes one complete task.
"""
import json
import os
import sys
import traceback
from importlib.machinery import SourceFileLoader


def main():
    if len(sys.argv) != 3:
        raise SystemExit("usage: parallel_worker.py TASK_JSON RESULT_JSON")
    task_path, result_path = sys.argv[1], sys.argv[2]
    result = {"ok": False}
    try:
        with open(task_path, "r", encoding="utf-8") as stream:
            payload = json.load(stream)
        module = SourceFileLoader(
            "basin_slope_parallel", payload["module"]).load_module()
        tool = module.SlopeUnits()
        params = tool.getParameterInfo()
        values = payload.get("values") or {}
        for parameter in params:
            if parameter.name in values:
                parameter.value = values[parameter.name]
        tool.execute(params, None)
        result = {"ok": True}
    except BaseException as exc:
        result = {
            "ok": False,
            "error": str(exc),
            "traceback": traceback.format_exc()[-6000:],
        }
    finally:
        with open(result_path, "w", encoding="utf-8") as stream:
            json.dump(result, stream, ensure_ascii=False)
    return 0 if result.get("ok") else 1


if __name__ == "__main__":
    sys.exit(main())
