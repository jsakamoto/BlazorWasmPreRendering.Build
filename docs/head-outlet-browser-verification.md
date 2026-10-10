# Verifying the HeadOutlet handover in a real browser

This document describes how the run time behavior of `BlazorWasmPreRendering.Build.lib.module.js` was checked by hand with Playwright. Read `docs/head-outlet-prerendering.md` first. It explains what the module does and why.

No automated test covers this yet. The procedure below is meant to be turned into tests later. If you are an AI agent doing that work, use this document as the specification. The scenarios and their expected results are the parts that matter. The tools (a Node.js server, Playwright MCP) are only what was used by hand. Follow the repository rules for tests instead (NUnit, ChainingAssertion, the `Microsoft.Playwright` NuGet package, `Toolbelt.WorkDirectory`, and so on).

## What is checked

Each check looks at the browser tab title and the page styles on every animation frame, and at the final contents of `<head>`.

- The tab title never changes to another text while the page is loading.
- A page stylesheet from `<HeadContent>` is never lost for even one frame.
- In the end, `<head>` has exactly one `<title>`, one `<meta name="description">`, and one page stylesheet link, and no `%%-PRERENDERING-...-%%` marker comments.
- The leftovers are cleaned up on the first navigation, or three seconds after Blazor starts.
- The module can be imported in a Web Worker without errors.

## 1. The test app

Create a Blazor WebAssembly app from the template (`dotnet new blazorwasm`). It must have the following.

**`wwwroot/index.html`** keeps the static title of the template, `<title>App1</title>`, and registers no other title.

**`Program.cs`** registers `HeadOutlet` at `head::after`. Services go into a static local function named `ConfigureServices`, so the prerendering server can call it.

```csharp
var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");
ConfigureServices(builder.Services, builder.HostEnvironment.BaseAddress);
await builder.Build().RunAsync();

static void ConfigureServices(IServiceCollection services, string baseAddress)
{
    services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(baseAddress) });
}
```

**`wwwroot/css/counter.css`** makes the effect of the page stylesheet easy to see.

```css
h1 { color: crimson; }
```

**`Pages/Counter.razor`** renders its head contents at once.

```razor
@page "/counter"

<PageTitle>Counter</PageTitle>
<HeadContent>
    <meta name="description" content="Counter page" />
    <link rel="stylesheet" href="css/counter.css" />
</HeadContent>

<h1>Counter</h1>
```

**`Pages/Slow.razor`** renders its head contents 1.5 seconds after start. This is shorter than the three second cleanup. The prerendering server waits for `OnInitializedAsync`, so the prerendered HTML still has the title, meta, and link.

```razor
@page "/slow"

@if (loaded)
{
    <PageTitle>Slow</PageTitle>
    <HeadContent>
        <meta name="description" content="Slow page" />
        <link rel="stylesheet" href="css/counter.css" />
    </HeadContent>
}

<h1>Slow</h1>

@code {
    private bool loaded = false;
    protected override async Task OnInitializedAsync()
    {
        await Task.Delay(1500);
        loaded = true;
    }
}
```

**`Pages/Slower.razor`** is a copy of `Slow.razor` with `@page "/slower"`, `<PageTitle>Slower</PageTitle>`, `<h1>Slower</h1>`, and `Task.Delay(5000)`. This is longer than the three second cleanup.

**`Pages/NoTitle.razor`** has no `<PageTitle>` and no `<HeadContent>`.

```razor
@page "/notitle"

<h1>No Title</h1>
```

**`Layout/NavMenu.razor`** has links to all pages. The crawler of this package finds pages only through links. The scenarios also click these links.

```razor
<NavLink class="nav-link" href="slow">Slow</NavLink>
<NavLink class="nav-link" href="slower">Slower</NavLink>
<NavLink class="nav-link" href="notitle">NoTitle</NavLink>
<NavLink class="nav-link" href="counter">Counter</NavLink>
```

## 2. Building with the changed package

The app references this package with a `PackageReference`. To test local changes, pack the package with a unique prerelease version into a separate folder, and point the app to it.

```shell
dotnet build BlazorWasmPreRendering.Build -c Release -p:Version=10.0.2-headoutlet.1 -p:PackageOutputPath=<packages folder>
```

- The version must be higher than any released version. Otherwise NuGet may pick a released package.
- Use a new version number for every pack. NuGet caches packages by version in `~/.nuget/packages`, so packing the same version again has no effect.
- The packed package uses `wwwroot/BlazorWasmPreRendering.Build.lib.module.js`, which is the minified file. Regenerate it after you change the source file (see `docs/head-outlet-prerendering.md`).

Add a `nuget.config` next to the app that has the packages folder as a source, set the version in the app, and publish.

```shell
dotnet publish -c Release -o publish
```

Check the publish log for `Getting http://127.0.0.1:.../slow...` and the other pages. Then check the prerendered files. For example, `publish/wwwroot/slow/index.html` should have the stash comment `<!-- %%-PRERENDERING-DEFAULT-TITLE-%% App1 -->` where the static title was, and `<title>Slow</title>` between the HeadOutlet markers. `notitle/index.html` should keep `<title>App1</title>`.

## 3. Serving the output

The server must map `/slow` to `slow/index.html` (the default output style), fall back to `index.html` for other paths, and send `application/wasm` for `.wasm` files. A small Node.js server was used. An automated test can use an in-process ASP.NET Core server with the same rules instead.

```js
import http from "node:http";
import fs from "node:fs";
import path from "node:path";
const root = path.resolve(process.argv[2]);
const port = Number(process.argv[3] ?? 5180);
const types = { ".html": "text/html; charset=utf-8", ".js": "text/javascript", ".mjs": "text/javascript", ".css": "text/css", ".json": "application/json", ".wasm": "application/wasm", ".png": "image/png", ".dat": "application/octet-stream" };
const isFile = p => { try { return fs.statSync(p).isFile(); } catch { return false; } };
http.createServer((req, res) => {
  const urlPath = decodeURIComponent(new URL(req.url, "http://x").pathname);
  let p = path.join(root, urlPath);
  if (!p.startsWith(root)) { res.writeHead(403).end(); return; }
  if (!isFile(p)) p = isFile(path.join(p, "index.html")) ? path.join(p, "index.html") : path.join(root, "index.html");
  res.writeHead(200, { "Content-Type": types[path.extname(p)] ?? "application/octet-stream", "Cache-Control": "no-cache" });
  fs.createReadStream(p).pipe(res);
}).listen(port, "127.0.0.1");
```

## 4. The probe

Register this script with `page.addInitScript()` before the first navigation. It runs before any page script. On every animation frame, it records the tab title and the color of `<h1>`, but only when they change. A change seen here is a change the user could see.

```js
() => {
  const t0 = performance.now();
  const log = window.__probe = [];
  const ms = () => Math.round(performance.now() - t0);
  document.addEventListener("DOMContentLoaded", () => {
    let lastTitle = null, lastColor = null;
    const tick = () => {
      if (document.title !== lastTitle) { lastTitle = document.title; log.push(`${ms()} title="${lastTitle}"`); }
      const h1 = document.querySelector("h1");
      const color = h1 ? getComputedStyle(h1).color : "no-h1";
      if (color !== lastColor) { lastColor = color; log.push(`${ms()} h1=${color === "rgb(220, 20, 60)" ? "crimson" : "default"}`); }
      requestAnimationFrame(tick);
    };
    tick();
  });
}
```

After each scenario, read the log and the final state of `<head>` with `page.evaluate()`.

```js
() => ({
  url: location.pathname,
  log: window.__probe,
  titles: [...document.head.querySelectorAll("title")].map(t => t.textContent),
  descriptions: [...document.head.querySelectorAll("meta[name=description]")].map(m => m.content),
  pageStylesheets: document.head.querySelectorAll('link[href="css/counter.css"]').length,
  markers: [...document.head.childNodes].filter(n => n.nodeType === Node.COMMENT_NODE && n.textContent.includes("PRERENDERING")).length,
})
```

Also collect `pageerror` events and console messages of the type `error` or `warning`. There should be none.

**Waiting for Blazor.** A link click is handled by Blazor only after Blazor has started. Before that, it is a normal page load. To know that Blazor has started, wait until `<head>` has a comment whose text is `!`. Blazor adds such comments when `HeadOutlet` renders.

```js
() => [...document.head.childNodes].some(n => n.nodeType === Node.COMMENT_NODE && n.textContent === "!")
```

## 5. Scenarios and expected results

The "Title changes" column lists the distinct titles in the probe log, in order. The "Styles" column describes the `h1` color in the log. The final state is read after the wait in the "Steps" column. It must always have no markers.

| # | Steps | Title changes | Styles | Final `<head>` |
| --- | --- | --- | --- | --- |
| 1 | Open `/counter`. Wait 4.5 s. | Counter | crimson only | title Counter, description "Counter page", 1 page stylesheet |
| 2 | Open `/slow`. Wait 4.5 s. | Slow | crimson only | title Slow, description "Slow page", 1 page stylesheet |
| 3 | Open `/slower`. Wait 7 s. | Slower, App1 (at about 3 s), Slower (at about 5 s) | crimson, default (at about 3 s), crimson (at about 5 s) | title Slower, 1 page stylesheet |
| 4 | Open `/slow`. Wait for Blazor and 200 ms more. Click the `notitle` link. Wait 4.5 s. | Slow, App1 (right after the click) | crimson, default (right after the click) | title App1, no description, no page stylesheet |
| 5 | Open `/slow`. Wait for Blazor and 200 ms more. Click the `counter` link. Wait 4.5 s. Repeat three times. | Slow, Counter | crimson only | title Counter, description "Counter page", 1 page stylesheet |
| 6 | Open `/slow`. Wait 4.5 s. Click the `notitle` link. Wait 1.5 s. | Slow, App1 | crimson, default | title App1, no description, no page stylesheet |
| 7 | Open `/notitle`. Wait 4.5 s. | App1 | default only | title App1, no description, no page stylesheet |
| 8 | Open `/`. Wait 4.5 s. | Home | default only | title Home |

Notes on the scenarios.

- Scenario 3 shows a known limit, not a bug. The page renders its head after the three second cleanup, so the default title and the default color appear in between.
- Scenario 4 checks the cleanup on navigation. In a version that had only the three second cleanup, the title stayed `Slow` and the color stayed crimson on `/notitle` until the timer fired.
- Scenario 5 checks that the cleanup on navigation does not remove a stylesheet that the new page also uses. An earlier version lost the styles for about 7 ms here. Repeat it, because it depends on timing.
- Scenario 2 is the main reason for keeping the prerendered title on top. Without it, the titles were Slow, App1, Slow.
- Do not assert exact times. Assert the order of the distinct values, and use the times only to tell "right after the click" from "at about 3 s".

## 6. Importing in a Web Worker

The module must not throw when it is imported in a Web Worker. Run this with `page.evaluate()` on any page of the app. Replace `moduleUrl` with the URL of the module file.

```js
async moduleUrl => {
  const code = `import(${JSON.stringify(moduleUrl)}).then(async m => {
    m.beforeStart(); m.beforeWebStart(); m.afterStarted(); m.afterWebStarted();
    await new Promise(r => setTimeout(r, 3500));
    postMessage("ok: " + Object.keys(m).join(","));
  }, e => postMessage("error: " + e))`;
  return await new Promise(resolve => {
    const worker = new Worker(URL.createObjectURL(new Blob([code], { type: "text/javascript" })), { type: "module" });
    worker.onerror = e => { resolve("worker error: " + e.message); worker.terminate(); };
    worker.onmessage = e => { resolve(e.data); worker.terminate(); };
  });
}
```

The expected result is `ok: afterStarted,afterWebStarted,beforeStart,beforeWebStart`. An earlier hand edit that read `window.document` at the module level returned `error: ReferenceError: window is not defined` here.

## 7. Trying a changed module without packing again

Packing and publishing takes a few minutes. To try only a change in the `.js` file, keep the published app and replace the module with `page.route()`. The published file name has a fingerprint, such as `BlazorWasmPreRendering.Build.9jhou3vefu.lib.module.js`, so match it with a regular expression.

```js
const body = /* the minified text of the changed module */;
await page.route(/BlazorWasmPreRendering\.Build\.[a-z0-9]+\.lib\.module\.js$/, route =>
  route.fulfill({ status: 200, contentType: "text/javascript", body }));
```

Count the calls of the handler, and check that it was called once for each page load. For the Worker check, serve the changed file from the app folder under another name and pass that URL.

## Ideas for the automated tests

- The test app could be added to `SampleApps/`. `BlazorWasmApp0` has no static title, so it cannot cover the title stash. Add a static title to an existing app, or add a new app.
- The E2E tests already publish the sample apps with the packed package from `_dist/`. A browser test can reuse that output and serve it with an in-process server.
- Each scenario fits one test case. The probe script and the state reader can be shared helpers.
- The timing based scenarios (3, 4, and 5) need some tolerance. Use the order of values, not exact times.
