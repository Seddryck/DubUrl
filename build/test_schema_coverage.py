from contextlib import redirect_stderr
import io
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
from urllib.error import HTTPError

import import_schema_coverage as importer


FIXTURE = Path(__file__).parent / "fixtures/schema-coverage.v1.json"


class CoverageImportTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.output = Path(temporary.name)

    def test_download_stages_the_exact_validated_report(self):
        data = FIXTURE.read_bytes()
        with patch.object(importer, "urlopen", return_value=io.BytesIO(data)) as download:
            importer.stage(self.output)
        download.assert_called_once_with(importer.REPORT_URL, timeout=60)
        self.assertEqual((self.output / importer.REPORT_PATH).read_bytes(), data)

    def test_invalid_feature_coverage_is_rejected(self):
        report = json.loads(FIXTURE.read_bytes())
        report["providers"][0]["targets"][0]["coverage"]["native-array"].pop("inspect")
        data = json.dumps(report).encode()
        with self.assertRaisesRegex(ValueError, "native-array must contain inspection and DDL generation"):
            importer.validate(data)

    def test_unknown_result_state_is_rejected(self):
        report = json.loads(FIXTURE.read_bytes())
        report["providers"][0]["targets"][0]["coverage"]["native-array"]["inspect"]["status"] = "green"
        data = json.dumps(report).encode()
        with self.assertRaisesRegex(ValueError, "unknown status"):
            importer.validate(data)

    def test_untrusted_run_link_is_rejected(self):
        report = json.loads(FIXTURE.read_bytes())
        report["runUrl"] = "https://example.com/Seddryck/DubUrl.Providers/actions/runs/123"
        data = json.dumps(report).encode()
        with self.assertRaisesRegex(ValueError, "Invalid provider QA run URL"):
            importer.validate(data)

    def test_bad_inputs_explain_the_build_failure(self):
        bad = self.output / "invalid.json"
        bad.write_text("{}", encoding="utf-8")
        missing = self.output / "missing.json"
        for path, expected in [(bad, "version 1"), (missing, "missing.json")]:
            with self.subTest(path=path):
                stderr = io.StringIO()
                with patch("sys.argv", ["import_schema_coverage.py", "--output", str(self.output), "--report", str(path)]):
                    with redirect_stderr(stderr), self.assertRaises(SystemExit) as result:
                        importer.main()
                self.assertEqual(result.exception.code, 1)
                self.assertIn("Schema coverage report", stderr.getvalue())
                self.assertIn(expected, stderr.getvalue())
                self.assertFalse((self.output / importer.REPORT_PATH).exists())

    def test_missing_published_report_explains_the_build_failure(self):
        error = HTTPError(importer.REPORT_URL, 404, "Not Found", None, None)
        stderr = io.StringIO()
        with patch("sys.argv", ["import_schema_coverage.py", "--output", str(self.output)]):
            with patch.object(importer, "urlopen", side_effect=error):
                with redirect_stderr(stderr), self.assertRaises(SystemExit) as result:
                    importer.main()
        self.assertEqual(result.exception.code, 1)
        self.assertIn(importer.REPORT_URL, stderr.getvalue())
        self.assertIn("404", stderr.getvalue())

    def test_built_page_uses_the_staged_report_and_public_path(self):
        importer.stage(self.output, FIXTURE)
        site = self.output / "_site"
        site.mkdir()
        importer.stage(site, FIXTURE)
        page = site / importer.PAGE_PATH
        page.parent.mkdir(parents=True)
        url = f"/DubUrl/{importer.REPORT_PATH.as_posix()}"
        page.write_text(f'<div data-report-url="{url}"><a href="{url}">JSON</a></div>', encoding="utf-8")
        importer.verify_site(self.output, site, "/DubUrl")
        with self.assertRaisesRegex(ValueError, "does not use the published report URL"):
            importer.verify_site(self.output, site, "")
