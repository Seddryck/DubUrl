"""Verify imported Schema page URLs and their local links in the assembled site."""
import argparse
from html.parser import HTMLParser
import json
from pathlib import Path
import re
from urllib.parse import unquote, urljoin, urlsplit


class Links(HTMLParser):
    def __init__(self):
        super().__init__()
        self.urls = []

    def handle_starttag(self, tag, attrs):
        self.urls.extend(value for name, value in attrs if name in ("href", "src") and value)


def validate(source, site, baseurl):
    source, site = Path(source), Path(site)
    manifest = json.loads((source / ".schema-docs-manifest.json").read_text(encoding="utf-8"))
    missing = []
    pages = 0
    for name in manifest:
        if not name.startswith("_docs/") or not name.endswith(".md"):
            continue
        text = (source / name).read_text(encoding="utf-8-sig")
        permalink = re.search(r"^permalink:\s*['\"]?([^'\"\s]+)", text, re.MULTILINE)
        url = permalink.group(1) if permalink else f"/docs/{Path(name).stem}/"
        page = site / url.lstrip("/") / "index.html" if url.endswith("/") else site / url.lstrip("/")
        if not page.is_file():
            missing.append(f"Missing original page URL: {url}")
            continue
        pages += 1
        parser = Links()
        parser.feed(page.read_text(encoding="utf-8"))
        for link in parser.urls:
            target = urlsplit(urljoin(baseurl + url, link))
            if target.scheme or target.netloc or not target.path.startswith(baseurl + "/"):
                continue
            relative = unquote(target.path[len(baseurl):]).lstrip("/")
            path = site / relative
            if not path.exists() and not (path / "index.html").is_file():
                missing.append(f"{url} links to missing {target.path}")
    if not (site / "schemas/schema-document/v1/schema.json").is_file():
        missing.append("Stable JSON specification URL is missing")
    if missing:
        raise ValueError("\n".join(sorted(set(missing))))
    print(f"Validated {pages} imported Schema page URLs, local links and the stable JSON specification URL")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", default="artifacts/docs-site")
    parser.add_argument("--site", default="artifacts/docs-site/_site")
    parser.add_argument("--baseurl", default="/DubUrl")
    args = parser.parse_args()
    validate(args.source, args.site, args.baseurl)
