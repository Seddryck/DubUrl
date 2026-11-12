import hashlib
import io
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
from urllib.error import URLError
from zipfile import ZipFile

import import_connection_url_catalog as importer


class CatalogImportTests(unittest.TestCase):
    def bundle(self, version="0.69.0", extra=None):
        manifest = {
            "bundleFormatVersion": 1,
            "schemaVersion": 2,
            "catalogVersion": version,
            "sourceCommit": "a" * 40,
        }
        content = io.BytesIO()
        with ZipFile(content, "w") as archive:
            archive.writestr("catalog.json", b'{"schemaVersion":2}\n')
            archive.writestr("schema.json", b'{}\n')
            archive.writestr("manifest.json", json.dumps(manifest))
            if extra:
                archive.writestr(extra, b"unexpected")
        data = content.getvalue()
        return data, {"version": version, "sha256": hashlib.sha256(data).hexdigest()}

    def test_pinned_download_stages_both_public_json_files(self):
        data, pin = self.bundle()
        with tempfile.TemporaryDirectory() as temp, patch.object(importer, "build_opener") as opener:
            output = Path(temp)
            opener.return_value.open.return_value = io.BytesIO(data)
            importer.stage(output, pin)
            opener.return_value.open.assert_called_once_with(
                f"{importer.RELEASE_URL}/connection-url-catalog-v0.69.0/{importer.BUNDLE}", timeout=60
            )
            self.assertEqual((output / importer.CATALOG_PATH / "catalog.json").read_bytes(), b'{"schemaVersion":2}\n')
            self.assertEqual((output / importer.CATALOG_PATH / "schema.json").read_bytes(), b'{}\n')

    def test_rejects_checksum_mismatch_before_staging(self):
        data, pin = self.bundle()
        pin["sha256"] = "0" * 64
        with tempfile.TemporaryDirectory() as temp, patch.object(importer, "build_opener") as opener:
            output = Path(temp)
            opener.return_value.open.return_value = io.BytesIO(data)
            with self.assertRaisesRegex(ValueError, "checksum mismatch"):
                importer.stage(output, pin)
            self.assertFalse((output / importer.CATALOG_PATH).exists())

    def test_rejects_valid_checksum_for_wrong_release_version(self):
        data, pin = self.bundle("0.68.0")
        pin["version"] = "0.69.0"
        with self.assertRaisesRegex(ValueError, "release metadata"):
            importer.read_bundle(data, pin)

    def test_rejects_unexpected_archive_entry(self):
        data, pin = self.bundle(extra="../unexpected.json")
        with self.assertRaisesRegex(ValueError, "archive contents"):
            importer.read_bundle(data, pin)

    def test_reports_unavailable_catalog(self):
        with patch.object(importer, "build_opener") as opener:
            opener.return_value.open.side_effect = URLError("unavailable")
            with self.assertRaisesRegex(ValueError, "Could not download catalog 0.69.0"):
                importer.download("0.69.0")

    def test_rejects_redirect_away_from_https(self):
        handler = importer.HttpsRedirectHandler()
        with self.assertRaisesRegex(URLError, "redirected away from HTTPS"):
            handler.redirect_request(None, None, 302, "Found", {}, "http://example.invalid/catalog.zip")


if __name__ == "__main__":
    unittest.main()
