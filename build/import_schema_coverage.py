"""Stage the provider QA coverage snapshot and verify its public Jekyll URLs."""
import argparse
import json
from pathlib import Path
import re
from urllib.request import urlopen

ROOT = Path(__file__).resolve().parents[1]
REPORT_PATH = Path("schemas/schema-coverage/v1/schema-coverage.v1.json")
PAGE_PATH = Path("docs/schema-coverage/index.html")
REPORT_URL = "https://raw.githubusercontent.com/Seddryck/DubUrl.Providers/schema-coverage/schema-coverage.v1.json"
MAX_REPORT_SIZE = 8 * 1024 * 1024
STATUSES = {"supported", "limited", "unsupported", "unverified", "failed"}
OPERATIONS = {"inspect", "generate"}
RUN_URL = re.compile(
    r"https://github\.com/Seddryck/DubUrl\.Providers/actions/runs/\d+(?:/attempts/\d+)?", re.ASCII
)


def require(condition, message):
    if not condition:
        raise ValueError(message)


def nonempty(value):
    return isinstance(value, str) and bool(value.strip())


def records(items, location):
    require(isinstance(items, list) and items, f"{location} must be a nonempty array")
    ids = set()
    for item in items:
        require(isinstance(item, dict) and nonempty(item.get("id")), f"{location} contains an invalid ID")
        require(item["id"] not in ids, f"{location} repeats ID {item['id']!r}")
        ids.add(item["id"])
    return ids


def validate_cell(cell, location):
    require(isinstance(cell, dict), f"{location} must be an object")
    status = cell.get("status")
    evidence = cell.get("evidence")
    require(isinstance(status, str) and status in STATUSES, f"{location} has an unknown status")
    require(isinstance(evidence, list), f"{location} is missing its evidence array")
    for item in evidence:
        require(isinstance(item, dict) and nonempty(item.get("test")), f"{location} has invalid evidence")
        status = item.get("status")
        require(isinstance(status, str) and status in STATUSES, f"{location} has an unknown evidence status")
        outcomes = item.get("outcomes")
        require(isinstance(outcomes, list) and all(map(nonempty, outcomes)), f"{location} has invalid test outcomes")
        require("note" not in item or nonempty(item["note"]), f"{location} has an invalid evidence note")


def validate_target(target, location, feature_ids):
    for field in ("variant", "artifact"):
        require(field in target and (target[field] is None or nonempty(target[field])),
                f"{location} has an invalid {field}")
    coverage = target.get("coverage")
    require(isinstance(coverage, dict) and coverage.keys() == feature_ids,
            f"{location} does not match the report's feature IDs")
    for feature, cells in coverage.items():
        require(isinstance(cells, dict) and cells.keys() == OPERATIONS,
                f"{location}/{feature} must contain inspection and DDL generation")
        for operation, cell in cells.items():
            validate_cell(cell, f"{location}/{feature}/{operation}")


def validate(data):
    require(len(data) <= MAX_REPORT_SIZE, "Report exceeds the download size limit")
    report = json.loads(data.decode("utf-8"))
    require(isinstance(report, dict) and type(report.get("schemaVersion")) is int
            and report["schemaVersion"] == 1, "Expected schema coverage report version 1")
    require(nonempty(report.get("providerCommit")) and re.fullmatch(r"[0-9a-f]{40}", report["providerCommit"]),
            "Invalid provider commit")
    require(nonempty(report.get("runUrl")) and RUN_URL.fullmatch(report["runUrl"]), "Invalid provider QA run URL")
    for field in ("dubUrlVersion", "dubUrlSchemaVersion"):
        require(nonempty(report.get(field)), f"Missing {field}")

    features = report.get("features")
    feature_ids = records(features, "Features")
    for feature in features:
        require(nonempty(feature.get("label")) and nonempty(feature.get("group")),
                f"Missing label or group for feature {feature['id']!r}")

    providers = report.get("providers")
    records(providers, "Providers")
    for provider in providers:
        require(nonempty(provider.get("label")), f"Missing label for provider {provider['id']!r}")
        targets = provider.get("targets")
        records(targets, f"Targets for provider {provider['id']!r}")
        for target in targets:
            validate_target(target, f"{provider['id']}/{target['id']}", feature_ids)
    return report


def stage(output, source=None):
    require(output.is_dir(), f"Assemble the documentation site before importing coverage: {output}")
    if source is None:
        stream = urlopen(REPORT_URL, timeout=60)
    else:
        stream = source.open("rb")
    with stream:
        data = stream.read(MAX_REPORT_SIZE + 1)
    report = validate(data)
    destination = output / REPORT_PATH
    destination.parent.mkdir(parents=True, exist_ok=True)
    destination.write_bytes(data)
    print(f"Imported schema coverage from DubUrl.Providers {report['providerCommit']}")


def verify_site(output, site, baseurl):
    require((site / REPORT_PATH).read_bytes() == (output / REPORT_PATH).read_bytes(),
            "Built site does not serve the staged schema coverage report")
    page = (site / PAGE_PATH).read_text(encoding="utf-8")
    url = f"{baseurl.rstrip('/')}/{REPORT_PATH.as_posix()}"
    require(f'data-report-url="{url}"' in page and f'href="{url}"' in page,
            f"Schema coverage page does not use the published report URL {url}")
    print("Verified the schema coverage page and public JSON")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=ROOT / "artifacts/docs-site")
    mode = parser.add_mutually_exclusive_group()
    mode.add_argument("--report", type=Path, help="Stage a local report for development")
    mode.add_argument("--verify-site", type=Path, help="Check the report and page in a built Jekyll site")
    parser.add_argument("--baseurl", default="/DubUrl")
    args = parser.parse_args()
    try:
        if args.verify_site:
            verify_site(args.output, args.verify_site, args.baseurl)
        else:
            stage(args.output, args.report)
    except (OSError, ValueError) as error:
        location = args.verify_site or args.report or REPORT_URL
        parser.exit(1, f"Schema coverage report ({location}): {error}\n")


if __name__ == "__main__":
    main()
