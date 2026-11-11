# -*- coding: utf-8 -*-
"""Pro 侧 GRASS r.slopeunits 调度器。"""
import datetime
import json
import os
import shutil
import subprocess
import tempfile
import uuid


HERE = os.path.dirname(os.path.abspath(__file__))


def default_grass():
    local = os.environ.get("LOCALAPPDATA", "")
    candidates = [
        os.environ.get("BASINSLOPE_GRASS", ""),
        os.path.abspath(os.path.join(HERE, "..", "..", "runtime", "grass", "bin", "grass85.bat")),
        os.path.abspath(os.path.join(HERE, "..", "..", "..", "runtime", "grass", "bin", "grass85.bat")),
        os.path.join(local, "BasinSlopeTools", "runtime", "grass", "bin", "grass85.bat") if local else "",
        r"D:\OSGeo4W\bin\grass85.bat",
        r"D:\OSGeo4W\bin\grass.bat",
        r"C:\OSGeo4W\bin\grass85.bat",
        r"C:\OSGeo4W\bin\grass.bat",
    ]
    for p in candidates:
        if p and os.path.exists(p):
            return p
    return candidates[0] or r"D:\OSGeo4W\bin\grass85.bat"


def _append_log(path, message):
    if not path:
        return
    try:
        with open(path, "a", encoding="utf-8") as f:
            f.write(message + "\n")
    except Exception:
        pass


def _create_job():
    """Windows Job Object：父进程消失时自动终止 GRASS 子进程树。"""
    if os.name != "nt":
        return None
    try:
        import ctypes
        import ctypes.wintypes as wt

        class JOBOBJECT_BASIC_LIMIT_INFORMATION(ctypes.Structure):
            _fields_ = [
                ("PerProcessUserTimeLimit", ctypes.c_int64),
                ("PerJobUserTimeLimit", ctypes.c_int64),
                ("LimitFlags", ctypes.c_uint32),
                ("MinimumWorkingSetSize", ctypes.c_size_t),
                ("MaximumWorkingSetSize", ctypes.c_size_t),
                ("ActiveProcessLimit", ctypes.c_uint32),
                ("Affinity", ctypes.c_size_t),
                ("PriorityClass", ctypes.c_uint32),
                ("SchedulingClass", ctypes.c_uint32),
            ]

        class IO_COUNTERS(ctypes.Structure):
            _fields_ = [
                ("ReadOperationCount", ctypes.c_uint64),
                ("WriteOperationCount", ctypes.c_uint64),
                ("OtherOperationCount", ctypes.c_uint64),
                ("ReadTransferCount", ctypes.c_uint64),
                ("WriteTransferCount", ctypes.c_uint64),
                ("OtherTransferCount", ctypes.c_uint64),
            ]

        class JOBOBJECT_EXTENDED_LIMIT_INFORMATION(ctypes.Structure):
            _fields_ = [
                ("BasicLimitInformation", JOBOBJECT_BASIC_LIMIT_INFORMATION),
                ("IoInfo", IO_COUNTERS),
                ("ProcessMemoryLimit", ctypes.c_size_t),
                ("JobMemoryLimit", ctypes.c_size_t),
                ("PeakProcessMemoryUsed", ctypes.c_size_t),
                ("PeakJobMemoryUsed", ctypes.c_size_t),
            ]

        kernel32 = ctypes.WinDLL("kernel32", use_last_error=True)
        kernel32.CreateJobObjectW.restype = wt.HANDLE
        job = kernel32.CreateJobObjectW(None, None)
        if not job:
            return None
        info = JOBOBJECT_EXTENDED_LIMIT_INFORMATION()
        info.BasicLimitInformation.LimitFlags = 0x2000  # KILL_ON_JOB_CLOSE
        if not kernel32.SetInformationJobObject(
                job, 9, ctypes.byref(info), ctypes.sizeof(info)):
            kernel32.CloseHandle(job)
            return None
        return job
    except Exception:
        return None


def _assign_job(job, proc):
    if not job or proc is None:
        return
    try:
        import ctypes
        kernel32 = ctypes.WinDLL("kernel32", use_last_error=True)
        handle = getattr(proc, "_handle", None)
        if handle:
            kernel32.AssignProcessToJobObject(job, handle)
    except Exception:
        pass


def _close_job(job):
    if not job:
        return
    try:
        import ctypes
        ctypes.WinDLL("kernel32", use_last_error=True).CloseHandle(job)
    except Exception:
        pass


def _kill_tree(proc):
    """终止 GRASS 子进程树，避免取消/异常时留下后台进程。"""
    if proc is None or proc.poll() is not None:
        return
    try:
        if os.name == "nt":
            subprocess.run(
                ["taskkill", "/PID", str(proc.pid), "/T", "/F"],
                stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
                creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0),
            )
        else:
            proc.terminate()
    except Exception:
        try:
            proc.kill()
        except Exception:
            pass


def _is_ascii_path(path):
    try:
        str(path).encode("ascii")
        return True
    except UnicodeEncodeError:
        return False


def _ascii_link_name(temp_root, prefix):
    return os.path.join(temp_root, prefix + "_" + uuid.uuid4().hex[:8])


def _prepare_grass_input(dem, temp_root, cleanup):
    """GRASS/GDAL 在中文路径下可能按 ANSI 解码失败，使用 ASCII 临时链接/副本。"""
    if _is_ascii_path(dem):
        return os.path.abspath(dem)
    if os.path.isdir(dem):
        link = _ascii_link_name(temp_root, "dem")
        if os.name == "nt":
            try:
                subprocess.run(
                    ["cmd.exe", "/c", "mklink", "/J", link, os.path.abspath(dem)],
                    stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
                    creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0),
                )
                if os.path.isdir(link):
                    cleanup.append(("junction", link))
                    return link
            except Exception:
                pass
        shutil.copytree(dem, link)
        cleanup.append(("dir", link))
        return link
    ext = os.path.splitext(dem)[1] or ".tif"
    link = _ascii_link_name(temp_root, "dem") + ext
    shutil.copy2(dem, link)
    cleanup.append(("file", link))
    return link


def _cleanup_grass_temp(cleanup):
    for kind, path in reversed(cleanup):
        try:
            if kind == "junction":
                os.rmdir(path)
            elif kind == "file":
                os.remove(path)
            elif kind == "dir":
                shutil.rmtree(path, ignore_errors=True)
        except Exception:
            pass


def run(dem, out_gpkg, thresh, areamin, cvmin, rf, maxiteration,
        areamax=None, convergence=5, sfd=False, cleansize=25000,
        clean_method="快速（方法1）", grass_bat=None, epsg=32648,
        log_path=None, progress=None):
    grass = grass_bat or default_grass()
    if not os.path.exists(grass):
        raise FileNotFoundError(
            "未找到 GRASS。请先安装 GRASS，或在工具参数中指定 grass85.bat。\n"
            f"当前路径: {grass}")

    inside = os.path.join(HERE, "run_inside_grass.py")
    if not os.path.exists(inside):
        raise FileNotFoundError(f"缺少 GRASS 运行脚本: {inside}")
    if log_path is None:
        log_path = os.path.join(
            tempfile.gettempdir(),
            "R_slopeunits_" + datetime.datetime.now().strftime("%Y%m%d_%H%M%S_%f") + ".log")

    temp_root = tempfile.mkdtemp(prefix="R_slopeunits_grass_")
    cleanup = []
    requested_out = os.path.abspath(out_gpkg)
    try:
        dem_arg = _prepare_grass_input(os.path.abspath(dem), temp_root, cleanup)
        out_is_temp = not _is_ascii_path(requested_out)
        out_arg = os.path.join(temp_root, "out.gpkg") if out_is_temp else requested_out
        if out_is_temp:
            cleanup.append(("file", out_arg))

        cmd = [
            grass, "--tmp-project", f"EPSG:{epsg}", "--exec",
            "python", inside,
            "--dem", dem_arg,
            "--out", out_arg,
            "--thresh", str(thresh),
            "--areamin", str(areamin),
            "--cvmin", str(cvmin),
            "--rf", str(rf),
            "--maxiteration", str(maxiteration),
            "--convergence", str(convergence),
            "--cleansize", str(cleansize),
            "--clean-method", str(clean_method),
        ]
        if areamax:
            cmd += ["--areamax", str(areamax)]
        if sfd:
            cmd.append("--sfd")

        env = os.environ.copy()
        env.setdefault("HOME", env.get("USERPROFILE") or tempfile.gettempdir())
        env["GRASS_OVERWRITE"] = "1"

        _append_log(log_path, "COMMAND: " + " ".join(cmd))
        output = []
        proc = None
        job = _create_job()
        try:
            creationflags = getattr(subprocess, "CREATE_NO_WINDOW", 0)
            if os.name == "nt":
                creationflags |= getattr(subprocess, "CREATE_NEW_PROCESS_GROUP", 0)
            proc = subprocess.Popen(
                cmd, stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
                text=True, encoding="utf-8", errors="replace",
                creationflags=creationflags, env=env,
            )
            _assign_job(job, proc)
            for line in proc.stdout:
                line = line.replace("\r", "").rstrip()
                if not line:
                    continue
                output.append(line)
                if len(output) > 4000:
                    output = output[-4000:]
                _append_log(log_path, line)
                if progress:
                    try:
                        progress(line)
                    except Exception:
                        pass
            returncode = proc.wait()
        except BaseException as exc:
            _kill_tree(proc)
            _append_log(log_path, "EXCEPTION: " + repr(exc))
            raise
        finally:
            _close_job(job)

        stdout = "\n".join(output)
        clean_failed = "CLEAN_STATUS=FAILED" in stdout
        if returncode != 0:
            raise RuntimeError(
                "GRASS r.slopeunits 运行失败。\n"
                f"命令: {' '.join(cmd)}\n"
                f"日志: {log_path}\n"
                f"输出尾部:\n{stdout[-4000:]}")
        if out_is_temp:
            if not os.path.exists(out_arg):
                raise RuntimeError("GRASS 未生成输出文件: " + out_arg)
            if os.path.exists(requested_out):
                os.remove(requested_out)
            shutil.copy2(out_arg, requested_out)
        return {
            "stdout": stdout,
            "log_path": log_path,
            "clean_failed": clean_failed,
        }
    finally:
        _cleanup_grass_temp(cleanup)
        shutil.rmtree(temp_root, ignore_errors=True)


if __name__ == "__main__":
    print(json.dumps({
        "grass": default_grass(),
        "inside": os.path.join(HERE, "run_inside_grass.py"),
    }, ensure_ascii=False, indent=2))
