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

# Run a single test (Microsoft.Testing.Platform tree-node filter syntax)
dotnet test BlazorWasmPreRendering.Build.Test -- --treenode-filter "/*/*/StaticlizeCrawlerTest/*"

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

**Packaging** is non-standard: `dotnet pack` uses a hand-written `.nuspec`, and a `PublishForPackage` target first runs `dotnet publish` of both the build program and the WebHost for every TFM (net6.0–net11.0), pruning framework DLLs. Package release notes are extracted from the latest entry in `RELEASE-NOTES.txt`; version/author live in `VersionInfo.props`.

## Directory notes

- `SampleApps/` — Blazor WASM apps used as E2E test fixtures (plus `MiddlewarePackage1/2` test middleware packages; their versions come from `SampleApps/MiddlewarePackageVersion.props`). `BlazorWasmApp1` is also deployed to GitHub Pages by CI.
- `work/` — scratch area with ad-hoc repro projects for GitHub/Discord issues; not part of the build or tests. Don't treat it as production code.
- `_dist/` — NuGet package output, consumed as a local package source by tests.
