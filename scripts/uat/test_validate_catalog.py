from __future__ import annotations

import csv
import tempfile
import unittest
from pathlib import Path

from validate_catalog import HEADERS, validate

ROOT = Path(__file__).resolve().parents[2]
CATALOG = ROOT / "docs" / "uat" / "uat-scenario-catalog.csv"


class UatCatalogValidationTests(unittest.TestCase):
    def test_canonical_catalog_is_valid(self) -> None:
        errors, rows = validate(CATALOG)
        self.assertEqual([], errors)
        self.assertGreaterEqual(len(rows), 30)

    def test_duplicate_scenario_id_is_rejected(self) -> None:
        with CATALOG.open("r", encoding="utf-8-sig", newline="") as handle:
            reader = csv.DictReader(handle)
            rows = list(reader)

        duplicate = dict(rows[0])
        rows.append(duplicate)

        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "catalog.csv"
            with path.open("w", encoding="utf-8", newline="") as handle:
                writer = csv.DictWriter(handle, fieldnames=HEADERS)
                writer.writeheader()
                writer.writerows(rows)
            errors, _ = validate(path)

        self.assertTrue(any("Duplicate scenario IDs" in error for error in errors))

    def test_api_route_is_rejected_as_primary_uat_route(self) -> None:
        with CATALOG.open("r", encoding="utf-8-sig", newline="") as handle:
            reader = csv.DictReader(handle)
            rows = list(reader)

        rows[0]["route"] = "/api/v1/auth/me"

        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "catalog.csv"
            with path.open("w", encoding="utf-8", newline="") as handle:
                writer = csv.DictWriter(handle, fieldnames=HEADERS)
                writer.writeheader()
                writer.writerows(rows)
            errors, _ = validate(path)

        self.assertTrue(any("web UAT surface" in error for error in errors))


if __name__ == "__main__":
    unittest.main()
