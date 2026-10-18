# Documentation website

The site uses Jekyll 4.4 and Just the Docs 0.12.0, the same theme as Expressif.
DubUrl's custom palette lives in `_sass/color_schemes/duburl.scss`; presentation overrides live in `_sass/custom/custom.scss`.

From the repository root, run:

```powershell
docker compose -f jekyll-compose.yml up -d
```

Open http://localhost:4001/DubUrl/docs/installation-duburl/ to preview a documentation page, or http://localhost:4001/DubUrl/ for the simple homepage.
The first launch installs the locked gems in a Docker volume; later launches reuse that cache.
Stop the preview with `docker compose -f jekyll-compose.yml down`.
Use `JEKYLL_PORT` to override the default host port (links use the browser origin).

For a production build:

```powershell
docker compose -f jekyll-compose.yml run --rm jekyll bash -lc "bundle install && JEKYLL_ENV=production bundle exec jekyll build --strict_front_matter"
```

Keep `/DubUrl` as the base path when testing: it matches GitHub Pages and catches broken absolute links.
Existing `/docs/<name>/` URLs and schema document permalinks are retained.
Navigation metadata is in each page's front matter; add child pages using `parent` and `nav_order`.

GitHub Pages must use **GitHub Actions** as its build source to deploy this gem-based theme. The `docs-pages.yml` workflow builds pull requests and publishes only main-branch changes. The repository setting is not changed by this branch.
