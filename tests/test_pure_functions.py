import os
import sys
import tempfile
import types
import unittest
from importlib.machinery import SourceFileLoader
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
TOOLS = ROOT / "BasinSlopeTools" / "Tools"

# The toolbox imports arcpy at module import time. The pure helpers under test
# do not call ArcPy, so a minimal stub is enough to load the module on CI.
if "arcpy" not in sys.modules:
    sys.modules["arcpy"] = types.ModuleType("arcpy")

sys.path.insert(0, str(TOOLS / "grass_addons"))
MODULE = SourceFileLoader("basin_slope_tools_pyt", str(TOOLS / "BasinSlopeTools.pyt")).load_module()


class TestAdfPathNormalisation(unittest.TestCase):
    def test_hdr_adf_resolves_to_grid_directory(self):
        with tempfile.TemporaryDirectory() as tmp:
            grid = Path(tmp) / "dem"
            grid.mkdir()
            hdr = grid / "hdr.adf"
            hdr.write_text("header", encoding="utf-8")
            self.assertEqual(
                Path(MODULE._normalize_dem_path(str(hdr))),
                grid.resolve(),
            )

    def test_normal_file_is_unchanged(self):
        with tempfile.TemporaryDirectory() as tmp:
            tif = Path(tmp) / "dem.tif"
            tif.write_bytes(b"not a real raster")
            self.assertEqual(
                Path(MODULE._normalize_dem_path(str(tif))),
                tif.resolve(),
            )

    def test_dem_list_normalises_adf(self):
        with tempfile.TemporaryDirectory() as tmp:
            grid = Path(tmp) / "dem"
            grid.mkdir()
            hdr = grid / "hdr.adf"
            hdr.write_text("header", encoding="utf-8")
            values = MODULE._dem_list(str(hdr))
            self.assertEqual([Path(v) for v in values], [grid.resolve()])


class TestQualityAssessment(unittest.TestCase):
    def test_expected_weighted_score(self):
        result = MODULE._quality_assessment(
            [200000, 300000, 100000],
            [0.1, 0.3, 0.2],
            [0.8, 0.1, 0.3],
            {"cvmin": 0.25, "areamin": 150000, "areamax": 400000},
            False,
        )
        self.assertEqual(result["cvar_ok"], 2)
        self.assertEqual(result["area_ok"], 2)
        self.assertEqual(result["shape_ok"], 2)
        self.assertAlmostEqual(result["score"], 70.0, places=1)
        self.assertEqual(result["grade"], "良")

    def test_missing_fields_are_not_assessable(self):
        result = MODULE._quality_assessment([], [], [], {}, False)
        self.assertIsNone(result["score"])
        self.assertEqual(result["grade"], "不可评估")

    def test_clean_failure_reduces_score(self):
        ok = MODULE._quality_assessment(
            [200000], [0.1], [0.8],
            {"cvmin": 0.25, "areamin": 150000}, False
        )
        failed = MODULE._quality_assessment(
            [200000], [0.1], [0.8],
            {"cvmin": 0.25, "areamin": 150000}, True
        )
        self.assertAlmostEqual(ok["score"], 100.0, places=1)
        self.assertLess(failed["score"], ok["score"])

    def test_sliver_rate_is_reported(self):
        result = MODULE._quality_assessment(
            [200000], [0.1], [0.10, 0.30],
            {"cvmin": 0.25, "areamin": 150000}, False
        )
        self.assertAlmostEqual(result["sliver_rate"], 0.5, places=3)
        self.assertAlmostEqual(result["shape_pass_rate"], 0.5, places=3)


class TestRepositoryContract(unittest.TestCase):
    def test_version_file_exists(self):
        version = (ROOT / "VERSION").read_text(encoding="utf-8").strip()
        self.assertRegex(version, r"^\d+\.\d+\.\d+$")

    def test_tools_revision_is_consistent(self):
        revision = (ROOT / "TOOLS_REVISION").read_text(encoding="utf-8").strip()
        self.assertTrue(revision)
        self.assertIn(revision, (ROOT / "BasinSlopeTools" / "Buttons.cs").read_text(encoding="utf-8"))
        self.assertIn(revision, (ROOT / "dist" / "install.ps1").read_text(encoding="utf-8"))


if __name__ == "__main__":
    unittest.main()
