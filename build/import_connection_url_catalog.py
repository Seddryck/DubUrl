"""Stage a pinned Providers connection URL catalog for Jekyll and the builder tests."""
import argparse
import hashlib
import io
import json
from pathlib import Path
import re
from urllib.error import URLError
from urllib.parse import urlsplit
from urllib.request import HTTPRedirectHandler, build_opener
from zipfile import BadZipFile, ZipFile


ROOT = Path(__file__).resolve().parents[1]
CATALOG_PATH = Path("schemas/connection-url-builder/v2")
BUILDER_PATH = Path("docs/connection-url-builder/index.html")
FILES = ("catalog.json", "schema.json")
MAX_BUNDLE_SIZE = 2 * 1024 * 1024
RELEASE_URL = "https://github.com/Seddryck/DubUrl.Providers/releases/download"
BUNDLE = "connection-url-catalog.zip"


class HttpsRedirectHandler(HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        if urlsplit(newurl).scheme != "https":
            raise URLError("The download redirected away from HTTPS")
        return super().redirect_request(req, fp, code, msg, headers, newurl)


def read_pin(path):
    pin = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(pin, dict):
        raise ValueError("Expected a connection URL catalog lock object")
    version = pin.get("version")
    checksum = pin.get("sha256")
    if not isinstance(version, str) or not re.fullmatch(r"0\.[0-9]+\.[0-9]+(?:-[A-Za-z0-9.-]+)?", version):
        raise ValueError("Invalid pinned provider package version")
    if not isinstance(checksum, str) or not re.fullmatch(r"[0-9a-f]{64}", checksum):
        raise ValueError("Invalid pinned catalog SHA-256 checksum")
    return pin


def download(version):
    url = f"{RELEASE_URL}/connection-url-catalog-v{version}/{BUNDLE}"
    try:
        with build_opener(HttpsRedirectHandler()).open(url, timeout=60) as response:
            return response.read(MAX_BUNDLE_SIZE + 1)
    except (URLError, TimeoutError) as error:
        raise ValueError(f"Could not download catalog {version} from {url}: {error}") from error


def read_bundle(data, pin):
    if len(data) > MAX_BUNDLE_SIZE:
        raise ValueError("Oversized connection URL catalog download")
    if hashlib.sha256(data).hexdigest() != pin["sha256"]:
        raise ValueError(f"Connection URL catalog {pin['version']} checksum mismatch")

    with ZipFile(io.BytesIO(data)) as archive:
        entries = archive.infolist()
        if sorted(entry.filename for entry in entries) != sorted((*FILES, "manifest.json")):
            raise ValueError("Unexpected connection URL catalog archive contents")
        if sum(entry.file_size for entry in entries) > MAX_BUNDLE_SIZE:
            raise ValueError("Oversized connection URL catalog archive")
        manifest = json.loads(archive.read("manifest.json"))
        if not isinstance(manifest, dict):
            raise ValueError("Unexpected connection URL catalog release metadata")
        commit = manifest.get("sourceCommit")
        if (manifest.get("bundleFormatVersion") != 1 or manifest.get("schemaVersion") != 2
                or manifest.get("catalogVersion") != pin["version"]
                or not isinstance(commit, str) or not re.fullmatch(r"[0-9a-f]{40}", commit)):
            raise ValueError("Unexpected connection URL catalog release metadata")
        files = {name: archive.read(name) for name in FILES}
    return files, manifest


def stage(output, pin, bundle=None):
    if not output.is_dir():
        raise ValueError(f"Stage the documentation site before importing the catalog: {output}")
    if bundle is None:
        data = download(pin["version"])
    else:
        with bundle.open("rb") as stream:
            data = stream.read(MAX_BUNDLE_SIZE + 1)
    files, manifest = read_bundle(data, pin)
    target = output / CATALOG_PATH
    if any((target / name).exists() for name in FILES):
        raise ValueError(f"Connection URL catalog collides with staged content in {target}")
    target.mkdir(parents=True, exist_ok=True)
    for name, content in files.items():
        (target / name).write_bytes(content)
    print(f"Imported Providers connection URL catalog {pin['version']} ({manifest['sourceCommit']})")


def verify_site(output, site, baseurl):
    for name in FILES:
        if (site / CATALOG_PATH / name).read_bytes() != (output / CATALOG_PATH / name).read_bytes():
            raise ValueError(f"Built site does not serve the pinned {name}")
    page = (site / BUILDER_PATH).read_text(encoding="utf-8")
    urls = {name: f"{baseurl.rstrip('/')}/{CATALOG_PATH.as_posix()}/{name}" for name in FILES}
    for name, url in urls.items():
        if f'href="{url}"' not in page:
            raise ValueError(f"The connection URL builder is missing a link to {name}")
    if f'data-catalog-url="{urls["catalog.json"]}"' not in page:
        raise ValueError("The connection URL builder does not load the pinned catalog URL")
    print("Verified the connection URL builder and both public JSON URLs")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--pin", type=Path, default=ROOT / "build/connection-url-catalog.lock.json")
    parser.add_argument("--output", type=Path, default=ROOT / "artifacts/docs-site")
    parser.add_argument("--bundle", type=Path, help="Verify and stage a local copy of the pinned release")
    parser.add_argument("--verify-site", type=Path, help="Check the JSON files and links in a built Jekyll site")
    parser.add_argument("--baseurl", default="/DubUrl")
    args = parser.parse_args()
    try:
        if args.verify_site:
            verify_site(args.output, args.verify_site, args.baseurl)
        else:
            stage(args.output, read_pin(args.pin), args.bundle)
    except (OSError, BadZipFile, ValueError) as error:
        parser.exit(1, f"{error}\n")


if __name__ == "__main__":
    main()
