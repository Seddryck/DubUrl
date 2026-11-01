import hashlib
import importlib.util
import io
import json
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch
import zipfile

spec = importlib.util.spec_from_file_location("importer", Path(__file__).with_name("import_schema_docs.py"))
importer = importlib.util.module_from_spec(spec)
spec.loader.exec_module(importer)


class BundleTests(unittest.TestCase):
    def make(self, root, entries=None, manifest_update=None):
        entries = entries or {
            "content/_docs/schemas.md": b"---\ntitle: Schemas\n---\n",
            "content/schemas/schema-document/v1/schema.json": b"{}",
        }
        manifest = {
            "bundleFormatVersion": 1, "packageVersion": "0.100.0",
            "sourceCommit": "a" * 40,
            "files": {name: hashlib.sha256(data).hexdigest() for name, data in entries.items()},
        }
        if manifest_update:
            manifest_update(manifest)
        bundle = root / "docs.zip"
        with zipfile.ZipFile(bundle, "w") as archive:
            for name, data in entries.items():
                archive.writestr(name, data)
            archive.writestr("manifest.json", json.dumps(manifest))
        return bundle

    def test_import_preserves_main_content_and_stable_urls(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            source = root / "source"
            source.mkdir()
            (source / "cli.md").write_text("CLI", encoding="utf-8")
            importer.assemble(source, root / "output", self.make(root))
            self.assertEqual((root / "output/cli.md").read_text(), "CLI")
            self.assertTrue((root / "output/schemas/schema-document/v1/schema.json").exists())
            self.assertFalse((source / "schemas").exists())

    def test_accepts_new_release_and_commit_without_a_pin(self):
        with tempfile.TemporaryDirectory() as temp:
            bundle = self.make(Path(temp), manifest_update=lambda m: m.update(
                packageVersion="0.101.0", sourceCommit="b" * 40))
            files, manifest = importer.read_bundle(bundle)
            self.assertEqual(manifest["packageVersion"], "0.101.0")
            self.assertEqual(manifest["sourceCommit"], "b" * 40)
            self.assertEqual(len(files), 2)

    def test_default_download_uses_latest_release_and_manifest_metadata(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            source = root / "source"
            source.mkdir()
            bundle = self.make(root, manifest_update=lambda m: m.update(
                packageVersion="0.102.0", sourceCommit="c" * 40))
            with patch.object(sys, "argv", ["import_schema_docs.py", "--source", str(source),
                                           "--output", str(root / "output")]), patch.object(
                    importer.urllib.request, "urlopen",
                    return_value=io.BytesIO(bundle.read_bytes())) as download:
                importer.main()
            download.assert_called_once_with(
                "https://github.com/Seddryck/DubUrl.Schema/releases/latest/download/schema-docs.zip",
                timeout=60)
            self.assertTrue((root / "output/_docs/schemas.md").is_file())

    def test_rejects_unsupported_format_or_invalid_metadata(self):
        for key, value in [("packageVersion", "../bad"), ("packageVersion", None),
                           ("sourceCommit", "not-a-commit"), ("bundleFormatVersion", 2)]:
            with self.subTest(key=key, value=value), tempfile.TemporaryDirectory() as temp:
                bundle = self.make(Path(temp), manifest_update=lambda m: m.update({key: value}))
                with self.assertRaises(ValueError):
                    importer.read_bundle(bundle)

    def test_rejects_traversal_and_unexpected_paths(self):
        for name in ["content/_docs/../../escape", "/escape", "content\\\\_docs\\\\escape",
                     "content/_config.yml"]:
            with self.subTest(name=name), tempfile.TemporaryDirectory() as temp:
                with self.assertRaises(ValueError):
                    importer.read_bundle(self.make(Path(temp), {name: b"bad"}))

    def test_rejects_manifest_tampering(self):
        with tempfile.TemporaryDirectory() as temp:
            bundle = self.make(Path(temp), manifest_update=lambda m: m["files"].update(
                {"content/_docs/schemas.md": "0" * 64}))
            with self.assertRaisesRegex(ValueError, "entry checksum"):
                importer.read_bundle(bundle)

    def test_rejects_manifest_file_set_mismatch(self):
        with tempfile.TemporaryDirectory() as temp:
            bundle = self.make(Path(temp), manifest_update=lambda m: m["files"].pop(
                "content/_docs/schemas.md"))
            with self.assertRaisesRegex(ValueError, "contents"):
                importer.read_bundle(bundle)

    def test_rejects_collision_and_existing_staging_directory(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            source = root / "source"
            (source / "_docs").mkdir(parents=True)
            (source / "_docs/schemas.md").write_text("owned by main", encoding="utf-8")
            bundle = self.make(root)
            with self.assertRaisesRegex(ValueError, "collides"):
                importer.assemble(source, root / "output", bundle)
            with self.assertRaisesRegex(ValueError, "staging"):
                importer.assemble(source, source, bundle)

    def test_rejects_oversized_latest_download(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            source = root / "source"
            source.mkdir()
            with patch.object(sys, "argv", ["import_schema_docs.py", "--source", str(source),
                                           "--output", str(root / "output")]), patch.object(
                    importer, "MAX_BUNDLE_SIZE", 100), patch.object(
                    importer.urllib.request, "urlopen", return_value=io.BytesIO(b"x" * 101)):
                with self.assertRaisesRegex(ValueError, "Oversized"):
                    importer.main()
            self.assertFalse((root / "output").exists())


if __name__ == "__main__":
    unittest.main()
