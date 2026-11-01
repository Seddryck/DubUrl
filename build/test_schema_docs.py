import hashlib
import importlib.util
import io
import json
from pathlib import Path
import tempfile
import unittest
import zipfile

spec = importlib.util.spec_from_file_location("importer", Path(__file__).with_name("import_schema_docs.py"))
importer = importlib.util.module_from_spec(spec)
spec.loader.exec_module(importer)

class BundleTests(unittest.TestCase):
    def make(self, root, entries=None, manifest_update=None):
        entries = entries or {"content/_docs/schemas.md": b"---\ntitle: Schemas\n---\n",
                              "content/schemas/schema-document/v1/schema.json": b"{}"}
        manifest = {"bundleFormatVersion": 1, "packageVersion": "0.100.0", "sourceCommit": "a" * 40,
                    "files": {n: hashlib.sha256(d).hexdigest() for n, d in entries.items()}}
        if manifest_update: manifest_update(manifest)
        bundle = root / "docs.zip"
        with zipfile.ZipFile(bundle, "w") as archive:
            for n, d in entries.items(): archive.writestr(n, d)
            archive.writestr("manifest.json", json.dumps(manifest))
        return bundle, {"version": "0.100.0", "sourceCommit": "a" * 40,
                        "sha256": hashlib.sha256(bundle.read_bytes()).hexdigest()}

    def test_import_preserves_main_content_and_stable_urls(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp); source = root / "source"; source.mkdir(); (source / "cli.md").write_text("CLI")
            bundle, pin = self.make(root)
            importer.assemble(source, root / "output", bundle, pin)
            self.assertEqual((root / "output/cli.md").read_text(), "CLI")
            self.assertTrue((root / "output/schemas/schema-document/v1/schema.json").exists())
            self.assertFalse((source / "schemas").exists())

    def test_rejects_bad_checksum(self):
        with tempfile.TemporaryDirectory() as temp:
            bundle, pin = self.make(Path(temp)); pin["sha256"] = "0" * 64
            with self.assertRaisesRegex(ValueError, "checksum"): importer.read_bundle(bundle, pin)

    def test_rejects_wrong_release_or_source_or_format(self):
        for key, value in [("packageVersion", "2.0.0"), ("sourceCommit", "b" * 40), ("bundleFormatVersion", 2)]:
            with self.subTest(key=key), tempfile.TemporaryDirectory() as temp:
                bundle, pin = self.make(Path(temp), manifest_update=lambda m: m.update({key: value}))
                with self.assertRaisesRegex(ValueError, "version or source"): importer.read_bundle(bundle, pin)

    def test_rejects_traversal_and_unexpected_paths(self):
        for name in ["content/_docs/../../escape", "/escape", "content\\_docs\\escape", "content/_config.yml"]:
            with self.subTest(name=name), tempfile.TemporaryDirectory() as temp:
                bundle, pin = self.make(Path(temp), {name: b"bad"})
                with self.assertRaises(ValueError): importer.read_bundle(bundle, pin)

    def test_rejects_manifest_tampering(self):
        with tempfile.TemporaryDirectory() as temp:
            bundle, pin = self.make(Path(temp), manifest_update=lambda m: m["files"].update({"content/_docs/schemas.md": "0" * 64}))
            with self.assertRaisesRegex(ValueError, "entry checksum"): importer.read_bundle(bundle, pin)

    def test_rejects_collision_and_existing_staging_directory(self):
        with tempfile.TemporaryDirectory() as temp:
            root=Path(temp); source=root/"source"; (source/"_docs").mkdir(parents=True)
            (source/"_docs/schemas.md").write_text("owned by main")
            bundle,pin=self.make(root)
            with self.assertRaisesRegex(ValueError,"collides"): importer.assemble(source,root/"output",bundle,pin)
            with self.assertRaisesRegex(ValueError,"staging"): importer.assemble(source,source,bundle,pin)

if __name__ == "__main__": unittest.main()
