# MSBuild properties reference for the "BlazorWasmPreRendering.Build"

## MSBuild properties list for the "BlazorWasmPreRendering.Build"

Property name                               | Ddefault value | Description
--------------------------------------------|----------------|------------
BlazorWasmPrerendering                      |                | Set the `disable` to suppress prerendering.
BlazorWasmPrerenderingRootComponentType     | `$(RootNamespace).App` | Set the full name (including namespace) of a root component class.
BlazorWasmPrerenderingRootComponentSelector | `#app,app`     | Set the DOM element selector for attaching the root component.
BlazorWasmPrerenderingHeadOutletComponentSelector| `head::after` | Set the DOM element selector for attaching the `<HeadOutlet>` component of the Blazor.
BlazorWasmPrerenderingOutputStyle           | `IndexHtmlInSubFolders` | When it is set to `AppendHtmlExtension`, the page of the URL path `foo/bar` will be saved as the `foo/bar.html` instead of the `foo/bar/index.html`.
BlazorWasmPrerenderingDeleteLoadingContents | `false`        | When it is set to `true`, the "Loading..." contents will be deleted from prerendered output HTML files, and prerendered contents to be visible immediately even before the Blazor WebAssembly runtime has warmed up.
BlazorWasmPrerenderingUrlPathToExplicitFetch|                | Set the semicolon-separated URL paths explicitly that are not linked from anywhere, such as easter-egg pages, to be prerendered.
BlazorWasmPrerenderingEnvironment           | `Prerendering` | Set a name of a host environment that can retrieve via `IWebHostEnvironment.Environment`.
BlazorWasmPrerenderingEmulateAuthMe         | `true`         | When it is set to `true`, prerendering server emulates Azure App Services Auth. That means the ULR endpoint **"/.auth/me"** will return the JSON content `{"clientPrincipal":null}`
BlazorWasmPrerenderingLocale                | `en`           | Set a comma-separated locale list such as "en", "ja-JP,en-US", etc., those used when crawling. **⚠️Attention:** when you specify this MSBuild property via "dotnet" command line, you have to replace `,` (comma) with `%2c`.
BlazorWasmPrerenderingMode                  | `Static`       | Set the render mode in which `Static` or `WebAssemblyPrerendered`.
BlazorWasmPrerenderingKeepServer            | `false`        | When it is set to `true`, the `dotnet publish` command will not be exited, and the prerendering server process will keep running until `Ctrl` + `C` is pressed.


## Appendix: How to set those MSBuild property values?

### 1. Specify it in a project file (.csproj)

You can specify MSBuild properties and their values inside of a project file (.csproj) like this:

```xml
<!-- This is the .csproj file of your Blazor WebAssembly app -->
<Project Sdk="Microsoft.NET.Sdk.BlazorWebAssembly">
  ...
  <PropertyGroup>
    <{Property name 1}>{Property value 1}</{Property name 1}>
    <{Property name 2}>{Property value 2}</{Property name 2}>
    ...
  </PropertyGroup>
  ...
```

### 2. Specify it in command-line arguments when the  `dotnet publish` command executing

When you execute the `dotnet publish` command, you can specify MSBuild properties and their values by the `-p` command-line option with `-p:{name}={value}` syntax like this: 

```shell
dotnet publish -c:Release -p:{Property name 1}={Property value 1} -p:{Property name 2}={Property value 2} ...
```

> **Warning**  
> If you want to specify a comma-separated value as a property value, you must replace `,` (comma) with `%2c`.  
> ex.) `dotnet publish -c:Release -p:BlazorWasmPrerenderingLocale=ja%2cen`


### 3. Specify it in environment variables of the OS platforms

You can specify MSBuild properties and their values via environment variables of the OS platforms like this:

```powershell
# An example for PowerShell
> $env:{Property name 1} = "{Property value 1}"
> $env:{Property name 2} = "{Property value 2}"
```

```bash
# An example for Bash
> export {Property name 1}="{Property value 1}"
> export {Property name 2}="{Property value 2}"
```

