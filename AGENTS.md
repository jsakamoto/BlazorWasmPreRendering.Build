# AGENTS.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this repository is

**BlazorWasmPreRendering.Build** — a NuGet package that hooks into `dotnet publish` of a Blazor WebAssembly app and pre-renders every reachable page into static HTML files in the publish output. Published as `BlazorWasmPreRendering.Build` on NuGet (MPL-2.0).

## Requirements & build commands

- Requires the .NET 11 preview SDK (`global.json`: 11.0.0, `allowPrerelease: true`). The main projects multi-target `net6.0`–`net11.0`.
- Solution file is `BlazorWasmPreRendering.Build.slnx` (slnx format, not .sln).

```powershell
# Build
dotnet build BlazorWasmPreRendering.Build.slnx

# Run all tests (NUnit on Microsoft.Testing.Platform; net11.0 only)
dotnet test BlazorWasmPreRendering.Build.Test

# Run a single test (NUnit runner filter syntax)
dotnet test BlazorWasmPreRendering.Build.Test -- --filter "FullyQualifiedName~StaticlizeCrawlerTest"

# Create the NuGet package (Release build auto-packs into _dist/)
dotnet build BlazorWasmPreRendering.Build -c Release
```

Notes on tests:
- Many tests are slow E2E tests: they `dotnet publish` the apps under `SampleApps/` and run the full pre-rendering pipeline against them.
- `SetUpFixture` publishes the WebHost project into the test output (`.webhost/`) once before any test runs.
- `BuildProgramTests` consume the packed `.nupkg` — the root `nuget.config` registers `_dist/` as a package source for that purpose.

## Architecture: two cooperating processes

Pre-rendering happens at publish time via two separate executables, both shipped inside the NuGet package (published per-TFM into `tools/`):

1. **`BlazorWasmPreRendering.Build`** (the "build program", `Program.cs`) — the entry point invoked by MSBuild. It:
   - Parses command-line options (`CommandLineOptions.cs` via CommandLineSwitchParser) into `BlazorWasmPrerenderingOptions`.
   - Resolves middleware packages (`MiddlewarePackageReferenceBuilder`) — extra ASP.NET Core middleware (e.g. HeadElement server prerendering) is downloaded via `dotnet add package` into an intermediate dir and injected into the rendering server.
   - Spawns the WebHost as a child `dotnet` process, passing options through `BWAP_`-prefixed environment variables.
   - Runs `StaticlizeCrawler` (AngleSharp-based) against the locally hosted app: starts from `/`, follows `<a>` links and `link rel=alternate`, fetches each URL per configured locale, and saves responses as static files (`index.html`-in-subfolder or `.html`-extension style, optionally gzip/brotli-compressed to match the published output).
   - Updates the PWA `service-worker-assets.js` manifest (`ServiceWorkerAssetsManifest.cs`) so the new HTML files are cached correctly.

2. **`BlazorWasmPreRendering.Build.WebHost`** — an ASP.NET Core server that server-side-renders the user's Blazor WASM app. It loads the app's assemblies from the publish output via `CustomAssemblyLoader` (handles renamed `.dll`/`.bin` extensions), locates the root component and the user's `static ConfigureServices(...)` local function by reflection, and renders pages with the configured render mode (`Static` or `WebAssemblyPrerendered`). Also emulates Azure App Service auth (`/.auth/me`).

3. **`BlazorWasmPreRendering.Build.Shared`** — types serialized between the two processes (`ServerSideRenderingOptions`, `IndexHtmlFragments`, `MiddlewarePackageReference`, env-var prefix constant).

**MSBuild integration** lives in `BlazorWasmPreRendering.Build/build/BlazorWasmPreRendering.Build.targets`: it runs after `Publish` (skipped for wasm-tools nested publish), computes all `BlazorWasmPrerendering*` property defaults, and invokes the build program. Every user-facing MSBuild property is documented in `MSBUILD-PROPERTIES.md` — keep that file in sync when adding options (an option typically threads through: .targets → `CommandLineOptions` → `BlazorWasmPrerenderingOptions` → `ServerSideRenderingOptions`/env vars → WebHost).

**Packaging** is non-standard: `dotnet pack` uses a hand-written `.nuspec`, and a `PublishForPackage` target first runs `dotnet publish` of both the build program and the WebHost for every TFM (net6.0–net11.0), pruning framework DLLs. Package release notes are extracted from the latest entry in `RELEASE-NOTES.txt`; version/author live in `VersionInfo.props`. The steps to release a new version are in `docs/release-procedure.md`.

## Prerendered HeadOutlet contents

In the `Static` render mode, the HeadOutlet contents (`<PageTitle>`, `<HeadContent>`) are prerendered between `%%-PRERENDERING-HEADOUTLET-BEGIN/END-%%` comments. At build time, `StaticTitleStash` moves the static `<title>` of `index.html` into a comment, so each served page has only one title. At run time, the JavaScript initializer `BlazorWasmPreRendering.Build/StaticWebAssets/BlazorWasmPreRendering.Build.lib.module.js` restores that title and removes each prerendered element only after Blazor renders its counterpart, so the title and stylesheets don't flicker. It depends on details of Blazor's own `HeadOutlet` behavior and has known limits.

Read `docs/head-outlet-prerendering.md` before you change any of this. It explains the problems, how the code solves them, the limits, the coding rules for the `.js` file (it is written to minify well), and how to regenerate the minified file (the bundler in the project never runs).

No automated test covers the browser side of this yet. `docs/head-outlet-browser-verification.md` describes how it was checked by hand with Playwright. It covers the test app, the probe script, the scenarios, and their expected results. Use it to verify a change, and as the specification when you turn these checks into tests.

## Directory notes

- `docs/` holds design notes for complex parts of the code.

- `SampleApps/` — Blazor WASM apps used as E2E test fixtures (plus `MiddlewarePackage1/2` test middleware packages; their versions come from `SampleApps/MiddlewarePackageVersion.props`). `BlazorWasmApp1` is also deployed to GitHub Pages by CI.
- `work/` — scratch area with ad-hoc repro projects for GitHub/Discord issues; not part of the build or tests. Don't treat it as production code.
- `_dist/` — NuGet package output, consumed as a local package source by tests.
