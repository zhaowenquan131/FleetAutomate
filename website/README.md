# Fleet Automate website

Static product pages in English, Simplified Chinese, Traditional Chinese, Japanese, and Korean. HTML, CSS, icons, and screenshots are versioned together; no site build step is required.

To preview locally from the repository root:

```powershell
py -m http.server 8000 --directory website
```

Open `http://localhost:8000/`. Keep local image references, language links, section anchors, and release links valid when editing the pages.

## Release downloads and deployment

The pages link to `releases/FleetAutomate-0.1.0-setup.exe`. The `releases/` directory is generated and ignored by Git. Follow [the installer instructions](../installer/README.md) to build the matching installer before uploading the website directory. A source-only checkout does not contain the download file.

For Cloudflare Pages, use `website` as the output directory. If deploying from Git, the deployment must build or retrieve the installer and place it at the linked path before publishing. A direct upload must include the generated `releases/` directory. Local `.wrangler/` caches are ignored and are not part of the site.
