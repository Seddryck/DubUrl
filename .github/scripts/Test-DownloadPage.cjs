"use strict";

const assert = require("node:assert/strict");
const test = require("node:test");
const download = require("../../docs/assets/js/download.js");

function asset(name) {
  return { name, browser_download_url: `https://example.test/${name}` };
}

test("parses Windows CLI installers", () => {
  assert.deepEqual(download.parseAsset(asset("duburl-1.1.0-net10.0-win-x64-setup.exe"), "1.1.0"), {
    kind: "cli", framework: "net10.0", os: "win", libc: null, arch: "x64", packageType: "setup",
    asset: asset("duburl-1.1.0-net10.0-win-x64-setup.exe")
  });
});

test("parses Linux musl and future macOS CLI archives", () => {
  assert.equal(download.parseAsset(asset("duburl-1.1.0-net10.0-linux-musl-x64.tar.gz"), "1.1.0").libc, "musl");
  assert.equal(download.parseAsset(asset("duburl-1.1.0-net10.0-osx-arm64.tar.gz"), "1.1.0").os, "osx");
});

test("parses only .NET 10 worker archives and ignores unknown assets", () => {
  assert.equal(download.parseAsset(asset("duburl-worker-1.1.0-net10.0-win-arm64.zip"), "1.1.0").kind, "worker");
  assert.equal(download.parseAsset(asset("duburl-worker-1.1.0-net9.0-linux-x64.tar.gz"), "1.1.0"), null);
  assert.equal(download.parseAsset(asset("checksums.txt"), "1.1.0"), null);
});

test("orders stable semantic versions before prereleases", () => {
  const versions = ["1.1.0-beta.2", "1.1.1", "1.1.0", "1.1.0-beta.10"];
  assert.deepEqual(versions.sort(download.compareVersions), ["1.1.1", "1.1.0", "1.1.0-beta.10", "1.1.0-beta.2"]);
});

test("selects package patches only from the release line", () => {
  const packages = ["1.1.0", "1.1.2", "1.2.0", "2.0.0"].map((version) => ({ version, isTool: false }));
  assert.deepEqual(download.packageVersionsForRelease(packages, "1.1.0"), ["1.1.2", "1.1.0"]);
});

test("filters public libraries and user-facing tools", () => {
  assert.equal(download.isWantedPackage("DubUrl"), true);
  assert.equal(download.isWantedPackage("DubUrl.Worker"), true);
  assert.equal(download.isWantedPackage("DubUrl.Worker.win-x64"), false);
  assert.equal(download.isWantedPackage("DubUrl.ProviderTesting"), false);
});

test("reconciles invalid downstream choices", () => {
  const items = [
    { os: "win", arch: "x64", framework: "net10.0" },
    { os: "linux", arch: "x64", framework: "net9.0" }
  ];
  const result = download.reconcileCandidates(items, { os: "linux", arch: "arm64", framework: "net10.0" }, ["os", "arch", "framework"]);
  assert.deepEqual(result.choices, { os: "linux", arch: "x64", framework: "net9.0" });
});

test("rejects failed API responses", () => {
  assert.throws(() => download.checkResponse({ ok: false, status: 503, statusText: "Unavailable" }), /503 Unavailable/);
});
