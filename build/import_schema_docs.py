"""Assemble the single docs website from the latest Schema release bundle."""
import argparse
import hashlib
import json
from pathlib import Path, PurePosixPath
import re
import shutil
import tempfile
import urllib.request
import zipfile

ROOT = Path(__file__).resolve().parents[1]
MAX_BUNDLE_SIZE = 32 * 1024 * 1024
LATEST_BUNDLE_URL = "https://github.com/Seddryck/DubUrl.Schema/releases/latest/download/schema-docs.zip"

def read_bundle(bundle):
    with zipfile.ZipFile(bundle) as archive:
        names = archive.namelist()
        if len(names) != len(set(names)) or sum(i.file_size for i in archive.infolist()) > MAX_BUNDLE_SIZE:
            raise ValueError("Duplicate entries or oversized documentation bundle")
        for name in names:
            path = PurePosixPath(name)
            if "\\" in name or ":" in name or path.is_absolute() or any(part in ("", ".", "..") for part in name.split("/")):
                raise ValueError("Unsafe documentation path")
            if name != "manifest.json" and not name.startswith(("content/_docs/", "content/schemas/", "content/assets/schema/")):
                raise ValueError("Unexpected documentation path")
        manifest = json.loads(archive.read("manifest.json"))
        if manifest.get("bundleFormatVersion") != 1:
            raise ValueError("Unsupported Schema documentation bundle format")
        version, commit = manifest.get("packageVersion"), manifest.get("sourceCommit")
        if (not isinstance(version, str) or not re.fullmatch(r"\d+\.\d+\.\d+(?:-[A-Za-z0-9.-]+)?", version)
                or not isinstance(commit, str) or not re.fullmatch(r"[0-9a-f]{40}", commit)):
            raise ValueError("Invalid Schema documentation release metadata")
        if not isinstance(manifest.get("files"), dict):
            raise ValueError("Invalid Schema documentation file manifest")
        if set(names) != {"manifest.json", *manifest["files"]}:
            raise ValueError("Documentation manifest does not match bundle contents")
        required = "content/schemas/schema-document/v1/schema.json"
        if required not in manifest["files"]:
            raise ValueError("Stable specification missing")
        result = {}
        for name, expected in manifest["files"].items():
            data = archive.read(name)
            if hashlib.sha256(data).hexdigest() != expected:
                raise ValueError("Documentation entry checksum mismatch")
            result[name.removeprefix("content/")] = data
        return result, manifest

def assemble(source, output, bundle):
    source, output = Path(source).resolve(), Path(output).resolve()
    if source == output or source in output.parents or output in source.parents or output.exists():
        raise ValueError("Output must be a new staging directory outside the source tree")
    files, manifest = read_bundle(bundle)
    for name in files:
        if (source / name).exists():
            raise ValueError("Schema documentation collides with main content: " + name)
    shutil.copytree(source, output, ignore=shutil.ignore_patterns("_site", ".jekyll-cache", ".sass-cache", "vendor"))
    for name, data in files.items():
        target = output / name
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(data)
    (output / ".schema-docs-manifest.json").write_text(json.dumps(sorted(files)), encoding="utf-8")
    print(f"Imported {len(files)} files from DubUrl.Schema {manifest['packageVersion']} ({manifest['sourceCommit']})")

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--bundle", type=Path, help="Use a local bundle instead of downloading the latest release")
    parser.add_argument("--source", type=Path, default=ROOT / "docs")
    parser.add_argument("--output", type=Path, default=ROOT / "artifacts/docs-site")
    args = parser.parse_args()
    if args.bundle:
        assemble(args.source, args.output, args.bundle)
    else:
        # GitHub resolves the latest published release; no local version/commit/checksum pin.
        with tempfile.TemporaryDirectory() as temp:
            bundle = Path(temp) / "schema-docs.zip"
            with urllib.request.urlopen(LATEST_BUNDLE_URL, timeout=60) as response:
                data = response.read(MAX_BUNDLE_SIZE + 1)
            if len(data) > MAX_BUNDLE_SIZE:
                raise ValueError("Oversized documentation download")
            bundle.write_bytes(data)
            assemble(args.source, args.output, bundle)

if __name__ == "__main__":
    main()
