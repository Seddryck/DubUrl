/* Adapted from Seddryck/Expressif's metadata-driven download page. */
(function (factory) {
  "use strict";

  const api = factory();
  if (typeof module !== "undefined" && module.exports) module.exports = api;
  if (typeof window !== "undefined") window.DubUrlDownload = api;
  if (typeof document !== "undefined") api.mount(document);
})(function () {
  "use strict";

  const libraryPackageIds = new Set([
    "DubUrl",
    "DubUrl.Adomd",
    "DubUrl.Application",
    "DubUrl.BulkCopy",
    "DubUrl.Extensions",
    "DubUrl.OleDb",
    "DubUrl.Schema",
    "DubUrl.Schema.Documents"
  ]);
  const toolPackageIds = new Set(["DubUrl-cli", "DubUrl.Worker"]);
  const label = {
    win: "Windows",
    linux: "Linux",
    osx: "macOS",
    x64: "x64",
    arm64: "Arm64",
    glibc: "Standard Linux / glibc",
    musl: "Alpine / musl",
    setup: "Installer",
    portable: "Portable"
  };
  const downloadIcon = '<svg class="download-icon" aria-hidden="true" viewBox="0 0 16 16"><path d="M8 1v9m0 0 3.5-3.5M8 10 4.5 6.5M2 12v2h12v-2" fill="none" stroke="currentColor" stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5"/></svg>';

  function escapeHtml(value) {
    return String(value ?? "").replace(/[&<>'"]/g, (character) => ({
      "&": "&amp;", "<": "&lt;", ">": "&gt;", "'": "&#39;", '"': "&quot;"
    })[character]);
  }

  function versionOf(release) {
    return release.tag_name.replace(/^v/i, "");
  }

  function parseVersion(version) {
    const match = /^(\d+)\.(\d+)\.(\d+)(?:\.(\d+))?(?:-([0-9A-Za-z.-]+))?/.exec(String(version).replace(/^v/i, ""));
    return match ? {
      major: Number(match[1]), minor: Number(match[2]), patch: Number(match[3]),
      revision: Number(match[4] || 0), prerelease: match[5] || ""
    } : null;
  }

  function compareVersions(left, right) {
    const a = parseVersion(left);
    const b = parseVersion(right);
    if (!a || !b) return String(right).localeCompare(String(left), undefined, { numeric: true });
    for (const key of ["major", "minor", "patch", "revision"]) {
      if (a[key] !== b[key]) return b[key] - a[key];
    }
    if (!a.prerelease && b.prerelease) return -1;
    if (a.prerelease && !b.prerelease) return 1;
    return b.prerelease.localeCompare(a.prerelease, undefined, { numeric: true });
  }

  function parseAsset(asset, version) {
    const name = asset.name.toLowerCase();
    const escapedVersion = version.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
    const expression = new RegExp(`^duburl(-worker)?-${escapedVersion}-(net\\d+\\.\\d+)-(win|linux|osx)(-musl)?-(x64|arm64)(-setup)?\\.(zip|tar\\.gz|exe)$`);
    const match = name.match(expression);
    if (!match) return null;

    const kind = match[1] ? "worker" : "cli";
    if (kind === "worker" && match[6]) return null;
    if (kind === "worker" && match[2] !== "net10.0") return null;
    return {
      kind,
      framework: match[2],
      os: match[3],
      libc: match[3] === "linux" ? (match[4] ? "musl" : "glibc") : null,
      arch: match[5],
      packageType: match[6] ? "setup" : "portable",
      asset
    };
  }

  function isWantedPackage(id) {
    return libraryPackageIds.has(id) || toolPackageIds.has(id);
  }

  function reconcileCandidates(items, current, keys) {
    const choices = { ...current };
    let candidates = items;
    for (const key of keys) {
      const available = [...new Set(candidates.map((item) => item[key]))].filter((value) => value !== null);
      if (!available.includes(choices[key])) choices[key] = available[0] || null;
      if (choices[key]) candidates = candidates.filter((item) => item[key] === choices[key]);
    }
    return { choices, candidates };
  }

  function packageVersionsForRelease(packages, releaseVersion) {
    const requested = parseVersion(releaseVersion);
    return [...new Set(packages
      .filter((item) => !item.isTool)
      .map((item) => item.version)
      .filter((version) => {
        const candidate = parseVersion(version);
        return candidate?.major === requested?.major && candidate?.minor === requested?.minor;
      }))].sort(compareVersions);
  }

  function checkResponse(response) {
    if (!response.ok) throw new Error(`${response.status} ${response.statusText}`);
    return response;
  }

  function mount(document) {
    const root = document.getElementById("download-app");
    if (!root) return;

    const repository = root.dataset.repository;
    const cliDocsUrl = root.dataset.cliDocsUrl;
    const githubApi = `https://api.github.com/repos/${repository}/releases`;
    const state = {
      releases: [], release: null, section: "cli",
      methods: { cli: "tool", worker: "tool" },
      choices: { cli: {}, worker: {} },
      packages: [], packageVersions: [], packageVersion: null, packagesStatus: "idle"
    };

    function parsedAssets() {
      const version = versionOf(state.release);
      return state.release.assets.map((asset) => parseAsset(asset, version)).filter(Boolean);
    }

    function button(value, group, text = label[value] || value, product = state.section) {
      const selected = group === "method" ? state.methods[product] : state.choices[product][group];
      return `<button class="download-option" type="button" data-product="${product}" data-choice="${group}" data-value="${escapeHtml(value)}" aria-pressed="${selected === value}">${escapeHtml(text)}</button>`;
    }

    function choiceStep(title, key, candidates, product, description = "", showSingleValue = false) {
      const values = [...new Set(candidates.map((item) => item[key]))].filter((value) => value !== null);
      if (!values.length || (values.length < 2 && !showSingleValue)) return "";
      const options = values.map((value) => button(
        value, key, key === "framework" ? value.replace("net", ".NET ").replace(".0", "") : undefined, product
      )).join("");
      return `<div class="download-step"><div class="download-step__label">${title}</div><div class="download-options">${options}</div>${description ? `<p class="download-help">${description}</p>` : ""}</div>`;
    }

    function renderNative(product) {
      const all = parsedAssets().filter((item) => item.kind === product);
      const productName = product === "cli" ? "CLI" : "Worker";
      if (!all.length) return `<div class="download-notice">No native or portable ${productName} assets are available for this release.</div>`;

      const reconciled = reconcileCandidates(all, state.choices[product], ["os", "arch", "libc", "framework", "packageType"]);
      state.choices[product] = reconciled.choices;
      let candidates = all;
      let html = "";
      for (const [title, key, description] of [
        ["Platform", "os", ""],
        ["Architecture", "arch", ""],
        ["Linux runtime", "libc", "Standard Linux works for Ubuntu, Debian, Fedora, RHEL, and most mainstream distributions. Choose musl for Alpine Linux."],
        [".NET target", "framework", "These distributions require the matching .NET runtime."],
        ["Package type", "packageType", ""]
      ]) {
        html += choiceStep(
          title,
          key,
          candidates,
          product,
          key === "libc" || key === "framework" ? description : "",
          product === "worker"
        );
        if (state.choices[product][key]) candidates = candidates.filter((item) => item[key] === state.choices[product][key]);
      }

      const selected = candidates[0];
      if (!selected) return html + '<div class="download-notice">No download matches these choices.</div>';
      const friendly = [
        label[selected.os], label[selected.arch], selected.libc === "musl" ? "musl" : null,
        selected.framework.replace("net", ".NET ")
      ].filter(Boolean).join(" · ");
      const installerHelp = selected.packageType === "setup" && cliDocsUrl
        ? `<p><a href="${escapeHtml(cliDocsUrl)}">CLI installation and Windows setup guidance</a></p>` : "";
      return html + `<div class="download-card download-card--recommended"><span class="download-badge">Recommended download</span><h3>${productName} ${escapeHtml(versionOf(state.release))} — ${escapeHtml(friendly)}</h3><a class="download-action" href="${escapeHtml(selected.asset.browser_download_url)}">${downloadIcon}<span>Download ${escapeHtml(label[selected.packageType].toLowerCase())}</span></a><div class="download-filename">${escapeHtml(selected.asset.name)}</div>${installerHelp}</div>`;
    }

    function commandBlock(command) {
      return `<div class="download-command"><code>${escapeHtml(command)}</code><button class="download-copy" type="button" data-copy="${escapeHtml(command)}">Copy</button></div>`;
    }

    function renderTool(product) {
      const packageId = product === "cli" ? "DubUrl-cli" : "DubUrl.Worker";
      if (state.packagesStatus === "loading" || state.packagesStatus === "idle") return '<div class="download-loading" role="status">Loading matching .NET tools from NuGet…</div>';
      if (state.packagesStatus === "error") return '<div class="download-notice download-notice--error" role="alert">NuGet package information could not be loaded. Try <a href="https://www.nuget.org/packages?q=DubUrl">NuGet search</a> or choose a native distribution.</div>';
      const item = state.packages.find((candidate) => candidate.isTool && candidate.id === packageId && candidate.version.toLowerCase() === versionOf(state.release).toLowerCase());
      if (!item) return `<div class="download-notice">No ${escapeHtml(packageId)} package matching version ${escapeHtml(versionOf(state.release))} was found on NuGet. Try the native / portable distribution.</div>`;
      const commandName = product === "cli" ? "duburl" : "duburl-worker";
      return `<article class="download-card download-card--recommended download-package"><span class="download-badge">Recommended</span><h3>${escapeHtml(item.id)}</h3><p class="download-package__description">${escapeHtml(item.description)}</p>${commandBlock(`dotnet tool install --global ${item.id} --version ${item.version}`)}<p>The installed command is <code>${commandName}</code>. <a href="${escapeHtml(item.url)}">View on NuGet</a></p></article>`;
    }

    function renderProduct(product) {
      const isCli = product === "cli";
      const title = isCli ? "DubUrl CLI" : "JSON-RPC worker";
      const description = isCli
        ? "Validate schemas, render SQL, and deploy database structures from a terminal, script, or pipeline."
        : "Host persistent DubUrl application services for integrations over JSON-RPC standard input and output.";
      const availability = isCli
        ? '<p class="download-help">The .NET global tool works on macOS today. Native macOS choices will appear automatically when matching release archives are published.</p>'
        : '<p class="download-help">The JSON-RPC worker offers the same installation choices as the CLI and targets .NET 10 only.</p>';
      return `<h2>${title}</h2><p>${description}</p>${availability}<h3>How do you want to install it?</h3><div class="download-options">${button("tool", "method", ".NET global tool", product)}${button("native", "method", "Native / portable", product)}</div>${state.methods[product] === "tool" ? renderTool(product) : renderNative(product)}`;
    }

    function renderPackageCard(item) {
      return `<article class="download-card download-package"><h3>${escapeHtml(item.id)}</h3><p class="download-package__description">${escapeHtml(item.description)}</p>${commandBlock(`dotnet add package ${item.id} --version ${item.version}`)}<p><a href="${escapeHtml(item.url)}">View on NuGet</a></p></article>`;
    }

    function renderPackages() {
      if (state.packagesStatus === "loading" || state.packagesStatus === "idle") return '<h2>.NET libraries</h2><div class="download-loading" role="status">Loading matching packages from NuGet…</div>';
      if (state.packagesStatus === "error") return '<h2>.NET libraries</h2><div class="download-notice download-notice--error" role="alert">NuGet package information could not be loaded. Try <a href="https://www.nuget.org/packages?q=DubUrl">NuGet search</a> or refresh this page.</div>';
      const packages = state.packages.filter((item) => !item.isTool && item.version === state.packageVersion);
      const versionOptions = state.packageVersions.map((version) => `<option value="${escapeHtml(version)}" ${version === state.packageVersion ? "selected" : ""}>${escapeHtml(version)}</option>`).join("");
      const versionPicker = state.packageVersions.length ? `<div class="download-package-version"><label for="download-package-version">Package version</label><select id="download-package-version" data-package-version>${versionOptions}</select><p class="download-help">Patch releases may be published only to NuGet, without a corresponding GitHub Release. The latest available patch in the ${escapeHtml(versionOf(state.release).split(".").slice(0, 2).join("."))}.x line is selected automatically.</p></div>` : "";
      const content = packages.length
        ? `<div class="download-grid">${packages.map(renderPackageCard).join("")}</div>`
        : `<div class="download-notice">No DubUrl library package matching version ${escapeHtml(state.packageVersion || versionOf(state.release))} was found on NuGet.</div>`;
      return `<h2>.NET libraries</h2><p>Use DubUrl from a .NET application. Library packages are independent of operating system and architecture.</p>${versionPicker}${content}`;
    }

    function render() {
      if (!state.release) return;
      const latestStable = state.releases.find((item) => !item.prerelease);
      const releaseOptions = state.releases.map((release) => `<option value="${escapeHtml(release.id)}" ${release.id === state.release.id ? "selected" : ""}>${escapeHtml(versionOf(release))}${release.prerelease ? " — prerelease" : release === latestStable ? " — latest stable" : ""}</option>`).join("");
      const section = state.section === "packages" ? renderPackages() : renderProduct(state.section);
      root.innerHTML = `<div class="download-toolbar"><div class="download-toolbar__row"><label for="download-release-version">Release version <select id="download-release-version" data-version>${releaseOptions}</select></label><div class="download-release-links"><a href="${escapeHtml(state.release.html_url)}">Release notes</a><a href="https://github.com/${repository}/releases/tag/${encodeURIComponent(state.release.tag_name)}">GitHub release</a></div></div></div><div class="download-tabs" role="tablist" aria-label="What do you want to use?">${[["cli", "DubUrl CLI"], ["packages", ".NET libraries"], ["worker", "JSON-RPC worker"]].map(([key, text]) => `<button id="download-tab-${key}" class="download-tab" type="button" role="tab" data-section="${key}" aria-controls="download-panel" aria-selected="${state.section === key}" tabindex="${state.section === key ? "0" : "-1"}">${text}</button>`).join("")}</div><section id="download-panel" class="download-panel" role="tabpanel" aria-labelledby="download-tab-${state.section}">${section}</section>`;
    }

    async function loadPackages() {
      const requestedVersion = versionOf(state.release);
      state.packagesStatus = "loading";
      state.packages = [];
      state.packageVersions = [];
      state.packageVersion = null;
      render();
      try {
        const service = await fetch("https://api.nuget.org/v3/index.json").then(checkResponse).then((response) => response.json());
        const searchResource = service.resources.find((resource) => resource["@type"].split("/").includes("SearchQueryService"));
        if (!searchResource) throw new Error("NuGet search service is unavailable");
        const results = await fetch(`${searchResource["@id"]}?q=${encodeURIComponent("DubUrl")}&prerelease=true&take=50`).then(checkResponse).then((response) => response.json());
        const candidates = results.data.filter((item) => isWantedPackage(item.id));
        const packages = await Promise.all(candidates.map(async (item) => {
          const registrationUrl = `https://api.nuget.org/v3/registration5-gz-semver2/${item.id.toLowerCase()}/index.json`;
          const registration = await fetch(registrationUrl).then(checkResponse).then((response) => response.json());
          const pages = await Promise.all(registration.items.map((page) => page.items ? page : fetch(page["@id"]).then(checkResponse).then((response) => response.json())));
          const requested = parseVersion(requestedVersion);
          return pages.flatMap((page) => page.items || []).map((leaf) => leaf.catalogEntry).filter((entry) => {
            const candidate = parseVersion(entry.version);
            return candidate?.major === requested?.major && candidate?.minor === requested?.minor;
          }).map((entry) => {
            const packageTypes = (entry.packageTypes || item.packageTypes || []).map((type) => type.name);
            return {
              id: entry.id,
              version: entry.version,
              description: entry.description || item.description || "DubUrl package",
              isTool: packageTypes.includes("DotnetTool") || toolPackageIds.has(entry.id),
              url: `https://www.nuget.org/packages/${encodeURIComponent(entry.id)}/${encodeURIComponent(entry.version)}`
            };
          });
        }));
        if (requestedVersion !== versionOf(state.release)) return;
        state.packages = packages.flat().sort((a, b) => a.id.localeCompare(b.id));
        state.packageVersions = packageVersionsForRelease(state.packages, requestedVersion);
        state.packageVersion = state.packageVersions[0] || null;
        state.packagesStatus = "ready";
      } catch (error) {
        state.packagesStatus = "error";
        console.error("Unable to load NuGet packages", error);
      }
      render();
    }

    root.addEventListener("click", async (event) => {
      const section = event.target.closest("[data-section]");
      if (section) { state.section = section.dataset.section; render(); return; }
      const choice = event.target.closest("[data-choice]");
      if (choice) {
        const product = choice.dataset.product;
        if (choice.dataset.choice === "method") state.methods[product] = choice.dataset.value;
        else state.choices[product][choice.dataset.choice] = choice.dataset.value;
        render();
        return;
      }
      const copy = event.target.closest("[data-copy]");
      if (copy) {
        await navigator.clipboard.writeText(copy.dataset.copy);
        copy.textContent = "Copied";
        copy.setAttribute("aria-label", "Command copied");
      }
    });

    root.addEventListener("keydown", (event) => {
      const tabs = [...root.querySelectorAll('[role="tab"]')];
      const index = tabs.indexOf(event.target);
      if (index < 0 || !["ArrowLeft", "ArrowRight"].includes(event.key)) return;
      event.preventDefault();
      const offset = event.key === "ArrowRight" ? 1 : -1;
      tabs[(index + offset + tabs.length) % tabs.length].focus();
    });

    root.addEventListener("change", (event) => {
      if (event.target.matches("[data-package-version]")) {
        state.packageVersion = event.target.value;
        render();
        return;
      }
      if (event.target.matches("[data-version]")) {
        state.release = state.releases.find((release) => String(release.id) === event.target.value);
        state.choices = { cli: {}, worker: {} };
        loadPackages();
      }
    });

    Promise.all([
      fetch(`${githubApi}?per_page=100`).then(checkResponse).then((response) => response.json()),
      fetch(`${githubApi}/latest`).then(checkResponse).then((response) => response.json())
    ]).then(([releases, latest]) => {
      state.releases = releases.filter((release) => !release.draft);
      state.release = state.releases.find((release) => release.id === latest.id) || state.releases[0];
      if (!state.release) throw new Error("No published release was found");
      render();
      loadPackages();
    }).catch((error) => {
      console.error("Unable to load GitHub releases", error);
      root.innerHTML = `<div class="download-notice download-notice--error" role="alert">Current release information could not be loaded. Try the <a href="https://github.com/${repository}/releases">GitHub releases page</a> or refresh this page.</div>`;
    });
  }

  return {
    checkResponse,
    compareVersions,
    isWantedPackage,
    packageVersionsForRelease,
    parseAsset,
    parseVersion,
    reconcileCandidates,
    mount
  };
});
