# BlazorWasmPreRendering.Build

[![NuGet Package](https://img.shields.io/nuget/v/BlazorWasmPreRendering.Build.svg)](https://www.nuget.org/packages/BlazorWasmPreRendering.Build/) [![Discord](https://img.shields.io/discord/798312431893348414?style=flat&logo=discord&logoColor=white&label=Blazor%20Community&labelColor=5865f2&color=gray)](https://discord.com/channels/798312431893348414/1202165955900473375)

## 📝Summary

When you publish your Blazor WebAssembly app, this package pre-renders and saves the app as static HTML files in your public folder.

This will help make the contents of your Blazor WebAssembly static apps findable in internet search and be visible from the OGP client.

**An output of "dotnet publish" before installing this package:**  
![fig.1 - before](https://raw.githubusercontent.com/jsakamoto/BlazorWasmPreRendering.Build/master/.assets/fig01.before.png)

**And after installing this package:**  
![fig.2 - after](https://raw.githubusercontent.com/jsakamoto/BlazorWasmPreRendering.Build/master/.assets/fig02.after.png)

## 🚀Quick Start

Install this package to your Blazor WebAssembly project.

```
dotnet add package BlazorWasmPreRendering.Build --prerelease
```

Basically, **that's all**.

**Once installing this package is done, the output of the `dotnet publish` command will include pre-rendered contents!** 🎉

## ⚙️Configurations

### Services registration

In the `Program.cs` of your Blazor WebAssembly app, you must extract the service registration part into the static local function named `static void ConfigureServices(IServiceCollection services, string baseAddress)`, like below.

```csharp
// Program.cs

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

ConfigureServices(builder.Services, builder.HostEnvironment.BaseAddress);

await builder.Build().RunAsync();

// 👇 extract the service-registration process to the static local function.
static void ConfigureServices(IServiceCollection services, string baseAddress)
{
  services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(baseAddress) });
  services.AddScoped<IFoo, MyFoo>();
}
```

This package calls the `ConfigureServices(...)` static local function inside of your Blazor WebAssembly app when the pre-rendering process starts if that function exists.

This is important for your Blazor WebAssembly components to work fine in the pre-rendering process.

#### Note: other arguments of ConfigureServices() function

The `ConfigureServices(...)` static local function can also have an `IConfiguration` argument that reflects the contents of the `wwwroot/appsetting.json` JSON file.

### Root component type and selector

In some cases, suppose the type and selector of the root component of your Blazor WebAssembly app are not `{RootNamespace}.App` and `#app` or `app`. 

In that case, you have to describe that information explicitly in the project file (.csproj) of your Blazor WebAssembly app, like this.

```xml
<!-- This is the .csproj file of your Blazor WebAssembly app -->
<Project Sdk="Microsoft.NET.Sdk.BlazorWebAssembly">
  ...
  <PropertyGroup>
    <BlazorWasmPrerenderingRootComponentType>My.Custom.RootComponentClass</BlazorWasmPrerenderingRootComponentType>
    <BlazorWasmPrerenderingRootComponentSelector>.selector-for-root</BlazorWasmPrerenderingRootComponentSelector>
  </PropertyGroup>
  ...
```

If the root component doesn't live in the application assembly, you can specify assembly name in the `<BlazorWasmPrerenderingRootComponentType>` peoperty value, like this.

```xml
<!-- This is the .csproj file of your Blazor WebAssembly app -->
<Project Sdk="Microsoft.NET.Sdk.BlazorWebAssembly">
  ...
  <PropertyGroup>
    <BlazorWasmPrerenderingRootComponentType>My.Custom.RootComponentClass, My.CustomAssembly</BlazorWasmPrerenderingRootComponentType>
    ...
```

(* See also: [_MSBuild properties reference for the "BlazorWasmPreRendering.Build"_](https://github.com/jsakamoto/BlazorWasmPreRendering.Build/blob/master/MSBUILD-PROPERTIES.md))

#### Note: If the specified type was not found...

If the specified type was not found, as a fallback behavior, this package tries to find the root component type (which has the type name "App" and inherits `ComponentBase` type) **from all assemblies that referenced from the application assembly**.

### Hosting Environment

The host environment returns the environment name **"Prerendering"** during the pre-rendering process.

```html
@inject IWebAssemblyHostEnvironment HostEnv
<p>@HostEnv.Environment</p>
<!-- 👆 This will be pre-rendered as "<p>Prerendering</p>". -->
```

If you want to customize the host environment name during the pre-rendering process, please specify the "BlazorWasmPrerenderingEnvironment" MSBuild property inside your .csproj file or inside of the "dotnet publish" command-line argument.

```xml
<!-- This is the .csproj file of your Blazor WebAssembly app -->
<Project Sdk="Microsoft.NET.Sdk.BlazorWebAssembly">
  ...
  <PropertyGroup>
    <!-- 👇 If you want to make the environment name is "Production" 
            even while pre-rendering, set the MSBuild property like this. -->
    <BlazorWasmPrerenderingEnvironment>Production</BlazorWasmPrerenderingEnvironment>
    ...
```
(* See also: [_MSBuild properties reference for the "BlazorWasmPreRendering.Build"_](https://github.com/jsakamoto/BlazorWasmPreRendering.Build/blob/master/MSBUILD-PROPERTIES.md))

### Output style

By default, all staticalized output HTML files are named "index.html" and are placed in subfolders in the same hierarchy as a request URL path.

But if you **set the `BlazorWasmPrerenderingOutputStyle` MSBuild property to `AppendHtmlExtension`** when you publish the project, the staticalized files are named with **each request URL path appended ".html" file extension.**

```xml
<!-- This is the .csproj file of your Blazor WebAssembly app -->
<Project Sdk="Microsoft.NET.Sdk.BlazorWebAssembly">
  ...
  <PropertyGroup>
    <!--
    👇 If you set this, then outut HTML files are named with
    each request URL path appended ".html" file extension.
    (ex. "http://host/foo/bar" ⇒ "./foo/bar.html")-->
    <BlazorWasmPrerenderingOutputStyle>AppendHtmlExtension</BlazorWasmPrerenderingOutputStyle>
    ...
```
(* See also: [_MSBuild properties reference for the "BlazorWasmPreRendering.Build"_](https://github.com/jsakamoto/BlazorWasmPreRendering.Build/blob/master/MSBUILD-PROPERTIES.md))

### Delete the "Loading..." contents

By default, this package **keeps the "Loading..." contents** in the original fallback page (such as an `index.html`) into prerendered output static HTML files.

And, **prerendered contents are invisible** on the browser screen.  
(Only search engine crawlers can read them.)

That is by design because even if users can see the prerendered contents immediately after initial page loading, **that page can not interact with users for a few seconds** until the Blazor WebAssembly runtime has been warmed up.

However, in some cases, developers can control the user interactions completely until the Blazor WebAssembly runtime warmed up, and they would like to make the prerendered contents are visible immediately.

For that case, set the `BlazorWasmPrerenderingDeleteLoadingContents` MSBuild property to `true`.

```xml
<!-- This is the .csproj file of your Blazor WebAssembly app -->
<Project Sdk="Microsoft.NET.Sdk.BlazorWebAssembly">
  ...
  <PropertyGroup>
    <!--
    👇 If you set this MSBuild property to true, 
       then outut HTML files do not contain the "Loading..." contents,
       and prerendered contents will be visible immediately. -->
    <BlazorWasmPrerenderingDeleteLoadingContents>true</BlazorWasmPrerenderingDeleteLoadingContents>
    ...
```

When that MSBuild property is set to `true`, this package deletes the "Loading..." contents from prerendered static HTML files and does not hide prerendered contents from users.

(* See also: [_MSBuild properties reference for the "BlazorWasmPreRendering.Build"_](https://github.com/jsakamoto/BlazorWasmPreRendering.Build/blob/master/MSBUILD-PROPERTIES.md))

### Url path to explicit fetch

By default, this package follows all of `<a>` links recursively inside the contents starting from the root index (`/`) page to save them statically.

However, in some cases, there are pages that are not linked from anywhere, such as an "Easter Egg" page.

To support that case, please **set the URL path list that you want to fetch explicitly to the `BlazorWasmPrerenderingUrlPathToExplicitFetch` MSBuild property as a semicolon-separated string**.

```xml
<Project Sdk="Microsoft.NET.Sdk.BlazorWebAssembly">
  ...
  <PropertyGroup>
    <!--
    👇 If you set this, each URL path will be fetched
       and saved as a static HTML file
       even if those URLs are not linked from anywhere.
    -->
    <BlazorWasmPrerenderingUrlPathToExplicitFetch>/unkinked/page1;/unlinked/page2</BlazorWasmPrerenderingUrlPathToExplicitFetch>
    ...
```
(* See also: [_MSBuild properties reference for the "BlazorWasmPreRendering.Build"_](https://github.com/jsakamoto/BlazorWasmPreRendering.Build/blob/master/MSBUILD-PROPERTIES.md))

### Render mode

As you may know, this package is based on the standard ASP.NET Core Blazor server-side prerendering support.

- See also: ["Prerender and integrate ASP.NET Core Razor components | Microsoft Learn"](https://learn.microsoft.com/aspnet/core/blazor/components/prerendering-and-integration?pivots=webassembly)

By default, the render mode of prerendering is `Static`.  
And, **you can specify the render mode to `WebAssemblyPrerendered` via the `BlazorWasmPrerenderingMode` MSBuild property** if you want.

```xml
<Project Sdk="Microsoft.NET.Sdk.BlazorWebAssembly">
  ...
  <PropertyGroup>
    <BlazorWasmPrerenderingMode>WebAssemblyPrerendered</BlazorWasmPrerenderingMode>
    ...
```
(* See also: [_MSBuild properties reference for the "BlazorWasmPreRendering.Build"_](https://github.com/jsakamoto/BlazorWasmPreRendering.Build/blob/master/MSBUILD-PROPERTIES.md))

As the side effect of using the `WebAssemblyPrerendered` render mode, even you specify any values to the "BlazorWasmPrerenderingDeleteLoadingContents" MSBuild property, the "Loading..." contents are always removed, and prerendered contents never are invisible.

When you use the `WebAssemblyPrerendered` render mode,  please pay attention to implementing a startup code of the Blazor WebAssembly app.

Usually, a startup code of the Blazor WebAssembly app should be like this.

```csharp
var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("app");
builder.RootComponents.Add<HeadOutlet>("head::after");
```
However, if you use the `WebAssemblyPrerendered` render mode when prerendering, you should change the startup code below.

```csharp
var builder = WebAssemblyHostBuilder.CreateDefault(args);
// 👇 Add the condition that determine root components are 
//    already registered via prerednered HTML contents.
if (!builder.RootComponents.Any())
{
    builder.RootComponents.Add<App>("app");
    builder.RootComponents.Add<HeadOutlet>("head::after");
}
```

That is because the root component types and their insertion positions are specified inside of the prerendered HTML contents, not inside of your C# code when the render mode is `WebAssemblyPrerendered`.

Moreover, you can also use the **"persisting prerendered state"** feature.

When the render mode is `WebAssemblyPrerendered`, this package injects the `<persist-component-state />` tag helper into the fallback page.

So what you should do is inject the `PersistentComponentState` service into your component and use it to store the component state at prerendering and retrieve the saved component state at the Blazor WebAssembly is started.

Using the `WebAssemblyPrerendered` render mode and the persisting component state feature has **the significant potential to far improve the perceived launch speed of Blazor WebAssembly apps**.

For more details, please see also the following link.

- ["Persist prerendered state - Prerender and integrate ASP.NET Core Razor components | Microsoft Docs"](https://docs.microsoft.com/en-us/aspnet/core/blazor/components/prerendering-and-integration?view=aspnetcore-6.0&pivots=webassembly#persist-prerendered-state).

### Locale

The pre-rendering process will default be taken under the "en" culture. If you want to pre-render that with another culture, you can do that by specifying the `BlazorWasmPrerenderingLocale` MSBuild property.
You can set a comma-separated locale list such as "en", "ja-JP,en-US", etc. those used when crawling, to the `BlazorWasmPrerenderingLocale` MSBuild property.

```xml
<Project Sdk="Microsoft.NET.Sdk.BlazorWebAssembly">
  ...
  <PropertyGroup>
    <BlazorWasmPrerenderingLocale>ja-JP,en-US</BlazorWasmPrerenderingLocale>
    ...
```

### To avoid flicker with Lazy Assembly Loading

When using this in combination with Lazy Loading assemblies on an app with the `<BlazorWasmPrerenderingMode>` MSBuild property set to `WebAssemblyPrerendered`, it is beneficial to make sure that all required assemblies are loaded before your page runs. In some hosting environments, 'OnNavigatingAsync' will be triggered on the `Router` Component in your `App.razor` page after completing prerendering, and your assemblies will load correctly. This is my experience with IIS. On other hosting services, `OnNavigatingAsync` will not be triggered, and you will have to handle assembly loading yourself. The current best solution is to abstract the lazy loading normally done in `OnNavigatingAsync` into your own `LazyLoader` service.

#### Lazy Loader service (LazyLoader.cs)

```csharp
using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Components.WebAssembly.Services;

public class LazyLoader
{
    public List<Assembly> AdditionalAssemblies { get; } = new();

    private readonly LazyAssemblyLoader _lazyAssemblyLoader;

    private readonly NavigationManager _navigationManager;

    private readonly ILogger<LazyLoader> _logger;

    public LazyLoader(
        LazyAssemblyLoader lazyAssemblyLoader,
        NavigationManager navigationManager,
        ILogger<LazyLoader> logger)
    {
        this._lazyAssemblyLoader = lazyAssemblyLoader;
        this._navigationManager = navigationManager;
        this._logger = logger;
    }

    public async Task OnNavigateAsync(NavigationContext context) =>
        await this.OnNavigateAsync(context.Path.Trim('/'));

    public async Task PreloadAsync()
    {
        var uri = new Uri(this._navigationManager.Uri);
        await this.OnNavigateAsync(uri.LocalPath.Trim('/'));
    }

    public async Task OnNavigateAsync(string path)
    {
        try
        {
            // 👇 Load lazy assemblies that are needed for the current URL path. 
            if (path == "counter")
            {
                var assemblies = await this._lazyAssemblyLoader
                    .LoadAssembliesAsync(new[] { "CounterPage.dll" });
                this.AdditionalAssemblies.AddRange(assemblies);
            }
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Error loading assemblies");
        }
    }
}
```

#### New Router (App.razor)

```html
@*
  Inject the "LazyLoader" service into the "App" component.
*@
@inject LazyLoader LazyLoader

@*
  Assign the "AdditionalAssemblies" and "OnNavigateAsync" properties of
  the "LazyLoading" service to the parameters of the "Router" component
  with the same name.
*@
<Router AppAssembly="@typeof(App).Assembly"
        AdditionalAssemblies="@LazyLoader.AdditionalAssemblies"
        OnNavigateAsync="@LazyLoader.OnNavigateAsync">
    <Found Context="routeData">
        <RouteView RouteData="@routeData" DefaultLayout="@typeof(MainLayout)" />
    </Found>
    <NotFound>
        <PageTitle>Not found</PageTitle>
        <LayoutView Layout="@typeof(MainLayout)">
            <p role="alert">Sorry, there's nothing at this address.</p>
        </LayoutView>
    </NotFound>
</Router>
```

##### New Program.cs

```csharp
using BlazorWasmApp;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
if (!builder.RootComponents.Any())
{
    builder.RootComponents.Add<App>("#app");
    builder.RootComponents.Add<HeadOutlet>("head::after");
}
ConfigureServices(builder.Services, builder.HostEnvironment.BaseAddress);
var host = builder.Build();

// 👇 Invoke the "PreloadAsync" method of the "LazyLoader" service
//    to preload lazy assemblies needed for the current URL path before running.
var lazyLoader = host.Services.GetRequiredService<LazyLoader>();
await lazyLoader.PreloadAsync();

await host.RunAsync();

static void ConfigureServices(IServiceCollection services, string baseAddress)
{
    // 👇 Register the "LazyLoader" service
    services.AddSingleton<LazyLoader>();

    services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(baseAddress) });
}
```

> You can reference the complete sample code [here](https://github.com/jsakamoto/BlazorWasmPreRendering.Build/tree/master/SampleApps/BlazorWasmLazyLoading)

Now, your pages will be prerendered and then correctly rendered without any flicker, even if it is a page with lazy-loaded assemblies. Attempting other solutions will result in a runtime exception or a flicker because the builder will strip #app, then the assemblies will begin to load, then the page will load and throw an exception or be blank while it waits for assemblies to load (flicker).

## 🛠️Troubleshooting

If any exceptions happen in the prerendering process, the exception messages and stack traces will be shown in the console output of the `dotnet publish` command.

![fig.3 - an exception messaage and a stack trace](https://raw.githubusercontent.com/jsakamoto/BlazorWasmPreRendering.Build/master/.assets/fig04.http500.png)

Those outputs should be helpful for you to investigate and resolve those exceptions.

But in some cases, developers may want to investigate the living prerendering process.

To do that, please **set the `BlazorWasmPrerenderingKeepServer` MSBuild property to `true`**.

```shell
dotnet publish -c:Release -p:BlazorWasmPrerenderingKeepServer=true
```

(* See also: [_MSBuild properties reference for the "BlazorWasmPreRendering.Build"_](https://github.com/jsakamoto/BlazorWasmPreRendering.Build/blob/master/MSBUILD-PROPERTIES.md))

When that MSBuild property is set to `true`, **the `dotnet publish` command will not be exited**, and the prerendering process is kept running until the Ctrl + C keyboard combination is pressed.

During the prerendering process is running, developers can investigate it.

![fig.4 - an exception messaage and a stack trace](https://raw.githubusercontent.com/jsakamoto/BlazorWasmPreRendering.Build/master/.assets/fig05.keeprunning.png)

## 🔗Appendix

- If you would like to **change a title or any meta elements** for each page in your Blazor WebAssembly app, I recommend using the [**"Blazor Head Element Helper"** ![NuGet Package](https://img.shields.io/nuget/v/Toolbelt.Blazor.HeadElement.svg)](https://www.nuget.org/packages/Toolbelt.Blazor.HeadElement/) NuGet package.
- **The `<PageTitle>` and `<HeadContent>` components** are also statically pre-rendered properly.
- If you would like to deploy your Blazor WebAssembly app to **GitHub Pages**, I recommend using the [**"Publish SPA for GitHub Pages"** ![NuGet Package](https://img.shields.io/nuget/v/PublishSPAforGitHubPages.Build.svg)](https://www.nuget.org/packages/PublishSPAforGitHubPages.Build/) NuGet package.
- The **["Awesome Blazor Browser"](https://jsakamoto.github.io/awesome-blazor-browser/)** site is one of a good showcase of this package. That site is republishing every day by GitHub Actions with pre-rendering powered by this package.

## 🎉Release notes

[Release notes](https://github.com/jsakamoto/BlazorWasmPreRendering.Build/blob/master/RELEASE-NOTES.txt)

## 📢License

[Mozilla Public License Version 2.0](https://github.com/jsakamoto/BlazorWasmPreRendering.Build/blob/master/LICENSE)
