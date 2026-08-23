import unittest

from run_dry_run import canonical_json, ensure_safe_evidence, sha256_bytes, totp


class DryRunHelperTests(unittest.TestCase):
    def test_totp_matches_rfc6238_sha1_vector_truncated_to_six_digits(self):
        secret = "GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ"
        self.assertEqual("287082", totp(secret, at=59))

    def test_canonical_json_is_order_stable(self):
        first = canonical_json({"b": "iki", "a": "bir"})
        second = canonical_json({"a": "bir", "b": "iki"})
        self.assertEqual(first, second)
        self.assertEqual(sha256_bytes(first.encode("utf-8")), sha256_bytes(second.encode("utf-8")))

    def test_evidence_rejects_sensitive_keys(self):
        with self.assertRaises(ValueError):
            ensure_safe_evidence({"runs": [{"accessToken": "secret"}]})
        with self.assertRaises(ValueError):
            ensure_safe_evidence({"source_payload_json": "{}"})

    def test_evidence_accepts_sanitized_counts_and_hashes(self):
        ensure_safe_evidence(
            {
                "source": {"sourceContentHash": "A" * 64, "rowCount": 2},
                "runs": [{"runId": "00000000-0000-0000-0000-000000000001", "counts": {"valid": 2}}],
            }
        )


if __name__ == "__main__":
    unittest.main()
