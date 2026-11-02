"""Prevent host dependencies from returning to the library repository."""
from pathlib import Path
import unittest
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
HOSTS = {"DubUrl.Application", "DubUrl.Cli", "DubUrl-cli", "DubUrl.Worker"}


class ToolsExtractionTests(unittest.TestCase):
    def test_library_projects_do_not_reference_tool_hosts(self):
        for project in ROOT.rglob("*.csproj"):
            if any(part in project.parts for part in (".worktrees", ".artifacts", "artifacts", "bin", "obj")):
                continue
            for reference in ET.parse(project).iter():
                if reference.tag not in ("ProjectReference", "PackageReference"):
                    continue
                name = reference.attrib.get("Include", "").replace("\\", "/")
                self.assertNotIn(Path(name).stem if name.endswith(".csproj") else name, HOSTS, str(project))

    def test_hosts_have_moved_out_of_the_solution(self):
        text = (ROOT / "DubUrl.sln").read_text(encoding="utf-8-sig")
        for name in HOSTS:
            self.assertNotIn(f'= "{name}",', text)
            self.assertFalse((ROOT / name).exists())

    def test_core_does_not_publish_tools(self):
        workflow = (ROOT / ".github/workflows/ci-release.yml").read_text(encoding="utf-8")
        for name in ("package-cli-nuget", "package-worker-nuget", "publish-cli-nuget", "publish-worker-nuget"):
            self.assertNotIn(f"\n  {name}:", workflow)
        for package in ("DubUrl.Authentication.SqlServer", "DubUrl.Authentication.Snowflake"):
            self.assertIn(f"package-id: {package}", workflow)


if __name__ == "__main__":
    unittest.main()
