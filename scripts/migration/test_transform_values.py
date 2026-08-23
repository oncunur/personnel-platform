#!/usr/bin/env python3
from __future__ import annotations

import unittest

from transform_values import (
    apply_transform,
    mask_value,
    normalize_bool_tr,
    normalize_date,
    normalize_decimal_tr,
    normalize_employee_status,
    normalize_iban_tr,
    normalize_phone_tr,
)


class TransformValueTests(unittest.TestCase):
    def test_date_formats_normalize_to_iso(self) -> None:
        self.assertEqual("2026-08-23", normalize_date("23.08.2026"))
        self.assertEqual("2026-08-23", normalize_date("23/08/2026"))
        self.assertEqual("2026-08-23", normalize_date("2026-08-23"))

    def test_turkish_decimal_normalization(self) -> None:
        self.assertEqual("12345.67", normalize_decimal_tr("12.345,67"))
        self.assertEqual("12345.67", normalize_decimal_tr("12,345.67"))
        self.assertEqual("1500.50", normalize_decimal_tr("1500,50"))

    def test_turkish_phone_normalization(self) -> None:
        self.assertEqual("+905321234567", normalize_phone_tr("0532 123 45 67"))
        self.assertEqual("+905321234567", normalize_phone_tr("+90 532 123 45 67"))

    def test_iban_normalization(self) -> None:
        value = "TR33 0006 1005 1978 6457 8413 26"
        self.assertEqual("TR330006100519786457841326", normalize_iban_tr(value))

    def test_employee_status_normalization(self) -> None:
        self.assertEqual("ACTIVE", normalize_employee_status("Aktif"))
        self.assertEqual("TERMINATED", normalize_employee_status("Ayrıldı"))
        self.assertEqual("SUSPENDED", normalize_employee_status("Askıda"))

    def test_bool_normalization(self) -> None:
        self.assertEqual("TRUE", normalize_bool_tr("Evet"))
        self.assertEqual("FALSE", normalize_bool_tr("Hayır"))

    def test_pipeline(self) -> None:
        self.assertEqual("ABC-01", apply_transform("  abc-01 ", "TRIM|UPPER"))
        self.assertEqual("2026-08-01", apply_transform("23.08.2026", "MONTH_START"))

    def test_sensitive_preview_is_masked(self) -> None:
        self.assertEqual("TR***26", mask_value("TR330006100519786457841326", "FINANCIAL"))
        self.assertEqual("ACTIVE", mask_value("ACTIVE", "INTERNAL"))

    def test_invalid_values_fail_closed(self) -> None:
        with self.assertRaises(ValueError):
            normalize_date("31/31/2026")
        with self.assertRaises(ValueError):
            normalize_iban_tr("TR123")
        with self.assertRaises(ValueError):
            normalize_employee_status("BELIRSIZ")


if __name__ == "__main__":
    unittest.main()
