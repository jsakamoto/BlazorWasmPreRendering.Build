# Prerendering the HeadOutlet contents

This document explains how the contents of `<HeadOutlet>` (rendered by `<PageTitle>` and `<HeadContent>`) are prerendered and then handed over to Blazor in the browser. It covers the `Static` render mode only. The `WebAssemblyPrerendered` mode does not use the mechanism below.

The code lives in three places.

| File | Role |
| --- | --- |
| `BlazorWasmPreRendering.Build.WebHost/Pages/_Layout.cshtml` | Renders the HeadOutlet contents between two comment markers. |
| `BlazorWasmPreRendering.Build/Internal/StaticTitleStash.cs` | Moves the static `<title>` of `index.html` out of the way at build time. |
| `BlazorWasmPreRendering.Build/StaticWebAssets/BlazorWasmPreRendering.Build.lib.module.js` | A JavaScript initializer that hands the prerendered contents over to Blazor at run time. |

## The problems

### 1. Duplicated contents

The WebHost renders the HeadOutlet contents at the end of `<head>`, between these markers.

```html
<!-- %%-PRERENDERING-HEADOUTLET-BEGIN-%% -->
<title>Counter</title>
<meta name="description" content="Counter page">
<link rel="stylesheet" href="css/counter.css">
<!-- %%-PRERENDERING-HEADOUTLET-END-%% -->
```

When Blazor starts in the browser, `HeadOutlet` (attached to `head::after`) appends the same elements again. Nobody removes the prerendered ones, so they are duplicated.

### 2. Flicker

The old JavaScript initializer removed the whole range in `afterStarted`. But `afterStarted` runs before the app's `Main` finishes, and `HeadOutlet` renders the page's head contents much later. Between the two moments, the head has no title and no page stylesheet. The browser tab shows the host name for a moment, and the page loses its styles for a moment.

### 3. Two titles in the served HTML

`index.html` usually has a static title, such as `<title>MyApp</title>`, above the markers. Then the served HTML has two titles. The browser and crawlers that do not run JavaScript use the first one, so every page looked like `MyApp` to them.

### Blazor behaviors that matter here

- The browser shows the first `<title>` in document order.
- On its first render, `HeadOutlet` calls the JavaScript function `getAndRemoveExistingTitle`. It removes every title in `<head>` that Blazor did not render, and remembers the text of the last one as the default title. `HeadOutlet` shows this default title while the page has no `<PageTitle>`.
- A title counts as rendered by Blazor when its previous sibling is a comment that Blazor tracks.
- When a root component is attached with the `::after` selector, Blazor adopts all existing child nodes of that element (here, `<head>`) as its own logical nodes. So every comment that is already in `<head>`, including our markers, counts as a comment that Blazor tracks.
- `blazor.webassembly.js` calls only `beforeStart` and `afterStarted`. `blazor.web.js` calls only `beforeWebStart` and `afterWebStarted` when the module exports them.
## How it works

### At build time

`StaticlizeCrawler` passes every page through `StaticTitleStash.Stash()` before it saves the page. When the HeadOutlet range contains a title, each static title outside the range is replaced with a comment. The text is URI encoded, so a title that contains `-->` cannot break the comment.

```html
<!-- %%-PRERENDERING-DEFAULT-TITLE-%% MyApp -->
```

Now the served HTML has only one title, the page's own one. Pages whose HeadOutlet range has no title keep their static title as it is.

`IndexHtmlParser` calls `StaticTitleStash.Restore()` when it reads an `index.html` that was prerendered by an earlier publish. Without this, the static title would be lost on every publish.

### At run time

`beforeStart` (or `beforeWebStart`) runs before Blazor starts, and does the following.

1. `restoreStaticTitles()` turns the stash comment back into `<title>MyApp</title>` and appends it at the end of `<head>`, below the END marker. The tab still shows the prerendered title because it comes first. When `HeadOutlet` runs `getAndRemoveExistingTitle`, the restored title is the last one, so `MyApp` becomes the default title as it should. A text node is put just before the restored title. Without it, the previous sibling would be the END marker, and Blazor would treat the restored title as its own title.
2. `findPrerenderedRange()` finds the markers and the nodes between them.
3. `PrerenderedHeadCleaner` watches `<head>` with a `MutationObserver` and removes the prerendered elements one by one, only after Blazor renders their counterparts. The observer callback runs before the next paint, so the user never sees a moment without the element.

**Matching rules**

| Element | Counterpart when |
| --- | --- |
| `<meta>` | Same tag and the same value of `name`, `property`, `http-equiv`, or `charset`. The `content` may differ. |
| `<link>` | Same `rel` and `href`. |
| Others | Same `outerHTML`. |
| `<title>` | Handled separately. See below. |

A prerendered stylesheet is removed only after the new `<link>` fires `load` or `error`. This avoids a flash of unstyled content. The cleanup below does not remove it earlier.

**Keeping the title**

`getAndRemoveExistingTitle` removes the prerendered title. If the page renders its `<PageTitle>` late (for example, after it fetches data), `HeadOutlet` shows the default title in the meantime. Without a fix, the tab would change like `Counter`, `MyApp`, `Counter`.

So when the prerendered title is removed, the cleaner puts it back right after the BEGIN marker. This is above the titles of `HeadOutlet`, so the tab keeps the prerendered title. When a title other than the default one appears (or a title with the same text as the prerendered one), the cleaner removes the prerendered title for good.

A timeline for a page that renders its title 1.5 seconds after start.

| Step | Who | What happens | Tab |
| --- | --- | --- | --- |
| 1 | Browser | Receives the HTML. The only title is the prerendered `Slow`. | Slow |
| 2 | lib.module.js | Restores `<title>MyApp</title>` below the END marker. | Slow |
| 3 | HeadOutlet | Removes both titles, and takes `MyApp` as the default title. | (not painted) |
| 3' | lib.module.js | Puts `<title>Slow</title>` back after the BEGIN marker. | Slow |
| 4 | HeadOutlet | Appends the default `<title>MyApp</title>` at the end of `<head>`. | Slow |
| 5 | HeadOutlet | Replaces the default title with `<title>Slow</title>`, and appends the meta and link. | Slow |
| 5' | lib.module.js | Removes the prerendered title, meta, and link (the link after the new one loads). | Slow |
| 6 | lib.module.js | Three seconds after `afterStarted`, removes the markers and anything left between them. | Slow |
**Cleanup**

Some prerendered elements may never get a counterpart. This happens when the prerendered head differs from the head in the browser. Some examples are a `<HeadContent>` shown only when the environment is `Prerendering`, data that cannot be fetched in the browser, a different login state, and a `href` that changes on every render. The cleaner removes everything left in the range, including the markers, and stops watching, at whichever of these two moments comes first.

- **Three seconds after `afterStarted` (or `afterWebStarted`).** Anything Blazor has not rendered by then is treated as not for the current page.
- **On the first page navigation.** The leftovers belong to the old page, so they must go at once. `blazor.webassembly.js` has no public navigation event, and `history.pushState` fires no event. So the cleaner watches `<body>` and compares `location.href` (without the hash) with the first URL on every change. Blazor always changes the body when it moves to another page.

The head observer is created before the body observer, so it handles the head changes of a navigation first. This way, when the new page uses the same stylesheet, the prerendered one still waits for the new one to load.

## Limits

- **The three seconds are a guess.** If a page renders its head contents later than that (for example, after a slow fetch), the prerendered ones are already gone. The tab shows the default title, and the page may lose its stylesheet until Blazor renders the head.
- **Navigation is detected only through a change of `<body>`.** A URL change without a body change is noticed at the next body change or at the three second cleanup. A change of the hash only is not treated as navigation.
- **Comments must reach the browser as they are.** An HTML minifier that removes comments (for example, on a CDN) breaks this mechanism, and the older markers as well.

## Maintenance notes

- **The minified file is made by hand.** The project references `BuildBundlerMinifier2022`, but its targets file is named `BuildBundlerMinifier.targets`, so NuGet never imports it and `bundleconfig.json` is never processed. After you edit the `.js` file, regenerate the minified file and copy it to `wwwroot`.

  ```shell
  npx terser@5.39.0 StaticWebAssets/BlazorWasmPreRendering.Build.lib.module.js --module -c -m -o StaticWebAssets/BlazorWasmPreRendering.Build.lib.module.min.js
  cp StaticWebAssets/BlazorWasmPreRendering.Build.lib.module.min.js wwwroot/BlazorWasmPreRendering.Build.lib.module.js
  ```

- **No automated test covers the browser side.** The unit tests cover `StaticTitleStash` only, and the E2E tests check that each served page has a single title. To check the run time behavior, I did the following.
  1. Packed the package with a unique prerelease version (`-p:Version=... -p:PackageOutputPath=...`).
  2. Published a test app with a static title, a page with `<PageTitle>` and `<HeadContent>` (a meta and a stylesheet), a page that renders them after `await Task.Delay(1500)`, and a page without a title.
  3. Served the output and opened it with Playwright. A script added by `page.addInitScript` logged `document.title` and the computed style of an element on every animation frame.
  4. Checked direct loads, and navigation from the slow page while it was still loading and after it loaded. Waited more than three seconds before checking the final state of `<head>`.
