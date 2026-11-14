# Performance and Acceleration Assessment

## 中文摘要

当前插件是**串行单进程**架构：单 DEM 运行一个 GRASS 子进程；批量/扫描按任务逐个执行，没有利用多核并行。实测 300×300 DEM（869 个单元）单次约 **89 秒**，按像素数粗略外推，7,895×7,895 DEM 可能需要十余小时，但该估算不是线性基准，需实测确认。

本插件目前**没有 GPU 计算路径**。GRASS 8.5 的 `r.slopeunits.create`、`r.slopeunits.clean`、`r.slope.aspect` 等模块是 CPU 实现，ArcGIS Pro 的 GPU 主要用于显示/渲染，不能自动加速 Python Toolbox + GRASS。用 GPU 重写斜坡单元算法属于研究型改造，且会改变与 GRASS 结果的一致性，不建议作为近期优化。

优先方向是：批量/扫描的**多进程并行**、参数扫描复用同一 GRASS 项目/DEM、批量默认关闭质量详情/成果包、全分辨率 DEM 分块。

## Test environment

- CPU: AMD Ryzen 9 7945HX, 16 cores / 32 logical processors
- RAM: 31.2 GB
- GPU: NVIDIA GeForce RTX 4080 Laptop GPU, AMD Radeon 610M
- ArcGIS Pro 3.7
- GRASS GIS 8.5 portable runtime
- Add-in 0.6.0, Tools revision 2026-11-14.1

## Measured result

Two 300×300 clipped DEMs (EPSG:32648) were processed end-to-end:

```text
create + clean + CVAR + AREA_M2
+ quality report + preview
+ Shapefile/GeoJSON/CSV
+ ZIP bundle
```

| Metric | Result |
|---|---:|
| DEM size | 300 × 300 |
| Units per DEM | 869 |
| Runtime per DEM | 89.26 s |
| Quality score | 80.1 / 良 |
| Execution model | one GRASS process per DEM, sequential |

The two-DEM test was run one DEM after another, not concurrently. It validates correctness of the workflow, not parallel throughput.

## Where time goes

1. **GRASS DEM import** — each run starts a temporary GRASS project and imports the DEM.
2. **`r.slopeunits.create`** — iterative threshold/watershed loop; inherently sequential across iterations.
3. **`r.slopeunits.clean`** — vector topology/cleanup, also sequential.
4. **CVAR/area statistics** — `r.stats.zonal` and `v.to.db`.
5. **Quality report** — `CheckGeometry`, dissolve-based overlap check, preview rendering.
6. **Exports/bundle** — Shapefile/GeoJSON/CSV and ZIP duplicate I/O; default bundle is convenient but costly for batch runs.

For parameter sweeps, the same DEM is imported and preprocessed repeatedly for each parameter combination. This is likely the largest avoidable overhead.

## Multithreading / multiprocessing status

- `SlopeUnits.execute` runs synchronously; `canRunInBackground = False`.
- Batch and sweep use a Python `for` loop over tasks; there is no worker pool.
- Each engine call creates its own ASCII temp root and `--tmp-project`, so independent tasks do not share a GRASS mapset. That makes **process-level** parallelism technically plausible.
- GRASS warns that Windows mapset locking is not supported; workers must not share a mapset/project.
- ArcPy is not thread-safe; a thread pool inside one Pro Python process is not a good design. Separate Python/GRASS processes are safer.

Potential parallel design:

```text
queue of independent DEM/parameter tasks
→ worker pool (default 2–4, configurable)
→ one GRASS temp project per worker/task
→ collect summary + quality report
→ merge summaries under a file lock
```

Practical limits:

- Each GRASS worker may use hundreds of MB to multiple GB of RAM; 31 GB RAM suggests 4–8 workers can be considered, but must measure.
- Disk I/O and GPKG/GDB writes can become the bottleneck.
- ArcGIS Pro/ArcPy process startup and licensing overhead reduce gains for small DEMs.
- Batch output collisions and progress/cancel need a queue design.

## GPU acceleration verdict

**Not currently feasible as a drop-in optimisation.**

Reasons:

1. GRASS `r.slopeunits` and its raster/vector modules are CPU implementations; there is no CUDA/OpenCL switch.
2. The plugin calls GRASS through a subprocess; Pro's GPU rendering does not accelerate that subprocess.
3. ArcGIS Pro GPU tools do not cover this custom Python/GRASS workflow.
4. Reimplementing slope-unit delineation on GPU (CuPy/RAPIDS/CUDA) is a research project, would need to reproduce watershed/aspect/cleaning semantics, and would produce a different result path from GRASS.
5. The machine has a discrete NVIDIA GPU, but the current CPU pipeline does not use it.

GPU could be considered later for auxiliary raster work (for example, preview rasterisation), but that would not change the core slope-unit runtime enough to justify the complexity.

## Recommended optimisation order

1. **Batch/sweep process pool** — 2–4 workers, disjoint temp projects, file-locked summaries.
2. **Reuse DEM/project in parameter sweep** — import/preprocess once, run multiple parameter sets; largest expected gain.
3. **Make detailed quality checks optional** — `CheckGeometry`, dissolve overlap, preview and bundle can be skipped in batch/sweep.
4. **Disable ZIP bundle by default for batch/sweep** — keep one-click bundle for single runs.
5. **Tile full-resolution DEMs** with overlap and merge — needed for 7,895×7,895, but higher risk.
6. **Profile before/after** with the same DEM, parameters, disk and Pro version.
7. GPU only if a separate research prototype demonstrates equivalent results.

## Expected performance envelope

A rough pixel-based extrapolation from 300×300 / 89 s gives:

```text
7,895 × 7,895 ≈ 62.3 million pixels
300 × 300      ≈ 0.09 million pixels
ratio          ≈ 692×
naive estimate ≈ 17 hours
```

This is only an order-of-magnitude warning. Watershed iteration count, topology complexity, memory and disk caching mean the real time may differ substantially. A full benchmark on the production DEM is still required.

## Conclusion

- **Multithreading:** not implemented; batch/sweep are sequential. Process-level parallel batch is the most realistic near-term improvement.
- **GPU:** no usable acceleration path through GRASS/ArcGIS Pro for this algorithm; not recommended for the current plugin.
- **Biggest practical win:** avoid re-importing/re-preprocessing the same DEM in parameter sweeps and run independent batch tasks in parallel.
