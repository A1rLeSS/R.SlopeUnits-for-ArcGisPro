# Generation Quality Score

The quality report adds a **0–100** score and a grade. It is intended for comparing parameter sets on the same DEM/area, not for absolute accuracy assessment.

## Formula

```text
score = 100 × (0.45 × CVAR_pass_rate
             + 0.25 × area_pass_rate
             + 0.20 × shape_pass_rate
             + 0.10 × clean_ok) / (sum of available weights)
```

- `CVAR_pass_rate`: fraction of slope units with `CVAR <= cvmin`.
- `area_pass_rate`: fraction with `areamin <= AREA_M2 <= areamax`; if no `areamax`, only the lower bound is applied.
- `shape_pass_rate`: fraction of units with Polsby-Popper compactness >= 0.20.
- `sliver_rate`: fraction with compactness < 0.15 (reported, not separately weighted).
- `clean_ok`: `1` if `r.slopeunits.clean` succeeded, otherwise `0`.
- If neither CVAR nor area is available, the score is `null` and the grade is `不可评估`.
- If clean failed, the clean component contributes 0 but the other components still apply.

The weights (45/25/20/10), compactness thresholds (0.20 and 0.15), and grade cutoffs are engineering heuristics chosen for this project. They have not been calibrated against expert-labelled boundaries or an independent reference dataset. Polsby–Popper describes polygon compactness; it does not measure whether a boundary follows a geomorphological divide. Report the component metrics alongside the score and do not present the score as positional accuracy or scientific validation.

## Grades

| Score | Grade |
|---:|---|
| 85–100 | 优 (Excellent) |
| 70–84.9 | 良 (Good) |
| 55–69.9 | 合格 (Acceptable) |
| <55 | 需检查 (Needs review) |

## Interpretation

A high score is evidence that:

1. most units satisfy the circular-variance threshold;
2. most units satisfy the requested area constraints;
3. the clean step completed.

It does **not** prove that the boundaries are geomorphologically correct. The score has no reference polygon or ground truth. For a scientific claim, compare multiple parameter sets, inspect representative units visually, and report the score only as a relative heuristic.

## Where it appears

- quality report HTML: score, grade, CVAR pass rate and area pass rate;
- JSON: `assessment.score`, `assessment.grade`, `assessment.cvar_pass_rate`, `assessment.area_pass_rate`, `assessment.method`;
- CSV: `quality_score`, `quality_grade`, `cvar_pass_rate`, `area_pass_rate`;
- report comparison page: score delta versus the previous report.

## Suggested validation

For a course/thesis evaluation, run at least three parameter sets on two or three small catchments and record score, unit count, median area, median CVAR and runtime. Do not tune parameters solely to maximise the score.

