#!/usr/bin/env python3
from __future__ import annotations

import re
from datetime import datetime
from decimal import Decimal, InvalidOperation

EMPTY_VALUES = {"", "NULL", "NONE", "N/A", "NA", "-"}
EMPLOYEE_STATUS_MAP = {
    "ACTIVE": "ACTIVE",
    "AKTIF": "ACTIVE",
    "AKTİF": "ACTIVE",
    "CALISIYOR": "ACTIVE",
    "ÇALIŞIYOR": "ACTIVE",
    "SUSPENDED": "SUSPENDED",
    "ASKIDA": "SUSPENDED",
    "IZINLI": "SUSPENDED",
    "İZİNLİ": "SUSPENDED",
    "TERMINATED": "TERMINATED",
    "PASIF": "TERMINATED",
    "PASİF": "TERMINATED",
    "AYRILDI": "TERMINATED",
    "AYRILMIŞ": "TERMINATED",
}


def clean_empty(value: str | None) -> str:
    if value is None:
        return ""
    text = str(value).strip()
    return "" if text.upper() in EMPTY_VALUES else text


def apply_transform(value: str | None, pipeline: str | None) -> str:
    result = clean_empty(value)
    for raw_step in (pipeline or "").split("|"):
        step = raw_step.strip().upper()
        if not step:
            continue
        result = _step(result, step)
    return result


def _step(value: str, step: str) -> str:
    if step == "TRIM":
        return value.strip()
    if step == "UPPER":
        return value.upper()
    if step == "LOWER":
        return value.lower()
    if step == "DIGITS":
        return re.sub(r"\D", "", value)
    if step == "PHONE_TR":
        return normalize_phone_tr(value)
    if step == "IBAN_TR":
        return normalize_iban_tr(value)
    if step == "DATE_AUTO":
        return normalize_date(value)
    if step == "MONTH_START":
        normalized = normalize_date(value)
        if not normalized:
            return ""
        parsed = datetime.strptime(normalized, "%Y-%m-%d")
        return parsed.replace(day=1).strftime("%Y-%m-%d")
    if step == "DECIMAL_TR":
        return normalize_decimal_tr(value)
    if step == "STATUS_EMPLOYEE":
        return normalize_employee_status(value)
    if step == "BOOL_TR":
        return normalize_bool_tr(value)
    if step == "CURRENCY":
        return value.strip().upper()
    if step == "LOOKUP":
        return value.strip()
    raise ValueError(f"Unknown transformation step: {step}")


def normalize_date(value: str) -> str:
    value = clean_empty(value)
    if not value:
        return ""
    candidates = (
        "%Y-%m-%d",
        "%d.%m.%Y",
        "%d/%m/%Y",
        "%d-%m-%Y",
        "%Y/%m/%d",
        "%Y.%m.%d",
    )
    for pattern in candidates:
        try:
            return datetime.strptime(value, pattern).strftime("%Y-%m-%d")
        except ValueError:
            pass
    raise ValueError(f"Unsupported date value: {value}")


def normalize_decimal_tr(value: str) -> str:
    value = clean_empty(value)
    if not value:
        return ""
    normalized = value.replace(" ", "")
    if "," in normalized and "." in normalized:
        if normalized.rfind(",") > normalized.rfind("."):
            normalized = normalized.replace(".", "").replace(",", ".")
        else:
            normalized = normalized.replace(",", "")
    elif "," in normalized:
        normalized = normalized.replace(",", ".")
    try:
        number = Decimal(normalized)
    except InvalidOperation as exc:
        raise ValueError(f"Unsupported decimal value: {value}") from exc
    return format(number, "f")


def normalize_phone_tr(value: str) -> str:
    value = clean_empty(value)
    if not value:
        return ""
    digits = re.sub(r"\D", "", value)
    if digits.startswith("0090"):
        digits = digits[4:]
    elif digits.startswith("90") and len(digits) > 10:
        digits = digits[2:]
    if digits.startswith("0") and len(digits) == 11:
        digits = digits[1:]
    if len(digits) == 10:
        return f"+90{digits}"
    if value.startswith("+") and 8 <= len(digits) <= 15:
        return f"+{digits}"
    raise ValueError(f"Unsupported phone value: {value}")


def normalize_iban_tr(value: str) -> str:
    value = clean_empty(value)
    if not value:
        return ""
    normalized = re.sub(r"\s+", "", value).upper()
    if not re.fullmatch(r"TR\d{24}", normalized):
        raise ValueError("Invalid Turkish IBAN format")
    return normalized


def normalize_employee_status(value: str) -> str:
    value = clean_empty(value)
    if not value:
        return ""
    key = value.strip().upper()
    if key not in EMPLOYEE_STATUS_MAP:
        raise ValueError(f"Unknown employee status: {value}")
    return EMPLOYEE_STATUS_MAP[key]


def normalize_bool_tr(value: str) -> str:
    value = clean_empty(value)
    if not value:
        return ""
    key = value.strip().upper()
    if key in {"1", "TRUE", "YES", "Y", "EVET", "E"}:
        return "TRUE"
    if key in {"0", "FALSE", "NO", "N", "HAYIR", "H"}:
        return "FALSE"
    raise ValueError(f"Unknown boolean value: {value}")


def mask_value(value: str, sensitivity: str) -> str:
    if not value:
        return ""
    if sensitivity.upper() not in {"PERSONAL", "SENSITIVE-HR", "FINANCIAL"}:
        return value
    if len(value) <= 4:
        return "****"
    return f"{value[:2]}***{value[-2:]}"
