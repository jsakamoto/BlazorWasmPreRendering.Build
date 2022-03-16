# BlazorWasmPreRendering.Build [![NuGet Package](https://img.shields.io/nuget/v/BlazorWasmPreRendering.Build.svg)](https://j.mp/3meP0zP)

## Summary

When you publish your Blazor WebAssembly app, this package pre-renders and saves the app as static HTML files in your public folder.

This will help make the contents of your Blazor WebAssembly static apps findable in internet search and be visible from the OGP client.

**An output of "dotnet publish" before installing this package:**  
![fig.1 - before](https://raw.githubusercontent.com/jsakamoto/BlazorWasmPreRendering.Build/master/.assets/fig01.before.png)

**And after installing this package:**  
![fig.2 - after](https://raw.githubusercontent.com/jsakamoto/BlazorWasmPreRendering.Build/master/.assets/fig02.after.png)

## Quick Start

Install this package to your Blazor WebAssembly project.

```
dotnet add package BlazorWasmPreRendering.Build --version 1.0.0-preview.23.0
```

Basically, **that's all**.

**Once installing this package is done, the output of the `dotnet publish` command will include pre-rendered contents!** 🎉

## Configurations

### Services registration

If you are registering any services (except HttpClient that isn't specially configured) to the service provider at the startup of your Blazor WebAssembly app, please extract that process to the static method named `static void ConfigureServices(IServiceCollection services, string baseAddress)`.

```csharp
public class Program
{
  public static async Task Main(string[] args)
  {
    var builder = WebAssemblyHostBuilder.CreateDefault(args);
    builder.RootComponents.Add<App>("#app");

    // 👇 Extract service registration...
    services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(baseAddress) });
    services.AddScoped<IFoo, MyFoo>();

    await builder.Build().RunAsync();
  }
}
```

```csharp
public class Program
{
  public static async Task Main(string[] args)
  {
    var builder = WebAssemblyHostBuilder.CreateDefault(args);
    builder.RootComponents.Add<App>("#app");

    ConfigureServices(builder.Services, builder.HostEnvironment.BaseAddress);

    await builder.Build().RunAsync();
  }

  // 👇 ... to this named static method.
  private static void ConfigureServices(IServiceCollection services, string baseAddress)
  {
    services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(baseAddress) });
    services.AddScoped<IFoo, MyFoo>();
  }
}
```

And, if you implement the entry point as C# 9 top-level statement style, then you have to also extract the service-registration process to the static local function named `static void ConfigureServices(IServiceCollection services, string baseAddress)`.

> _**NOTICE:** The "ConfigureServices" local function must be **"static"** local function._

```csharp
// The "Program.cs" that is C# 9 top-level statement style entry point.

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");

ConfigureServices(builder.Services, builder.HostEnvironment.BaseAddress);

await builder.Build().RunAsync();

// 👇 extract the service-registration process to the static local function.
static void ConfigureServices(IServiceCollection services, string baseAddress)
{
  services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(baseAddress) });
  services.AddScoped<IFoo, MyFoo>();
}
```

> _Aside: C# 9 top-level statement style entry point can be used for only .NET6 or above._

This package calls the `ConfigureServices()` static method (or static local function) inside of your Blazor WebAssembly app when pre-renders it if that method exists.

This is important to your Blazor WebAssembly components work fine in the pre-rendering process.

#### Note: other arguments of ConfigureServices() method

The `ConfigureServices()` method can also have an `IConfiguration` argument reflected with the contents of the `wwwroot/appsetting.json` JSON file.

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

- See also: ["Prerender and integrate ASP.NET Core Razor components | Microsoft Docs"](https://docs.microsoft.com/en-us/aspnet/core/blazor/components/prerendering-and-integration?view=aspnetcore-6.0&pivots=webassembly)

By default, the render mode of prerendering is `Static`.  
And, **you can specify the render mode to `WebAssemblyRendered` via the `BlazorWasmPrerenderingMode` MSBuild property** if you want.

```xml
<Project Sdk="Microsoft.NET.Sdk.BlazorWebAssembly">
  ...
  <PropertyGroup>
    <BlazorWasmPrerenderingMode>WebAssemblyRendered</BlazorWasmPrerenderingMode>
    ...
```
(* See also: [_MSBuild properties reference for the "BlazorWasmPreRendering.Build"_](https://github.com/jsakamoto/BlazorWasmPreRendering.Build/blob/master/MSBUILD-PROPERTIES.md))

As the side effect of using the `WebAssemblyRendered` render mode, even you specify any values to the "BlazorWasmPrerenderingDeleteLoadingContents" MSBuild property, the "Loading..." contents are always removed, and prerendered contents never are invisible.

When you use the `WebAssemblyRendered` render mode,  please pay attention to implementing a startup code of the Blazor WebAssembly app.

Usually, a startup code of the Blazor WebAssembly app should be like this.

```csharp
var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("app");
builder.RootComponents.Add<HeadOutlet>("head::after");
```
However, if you use the `WebAssemblyRendered` render mode when prerendering, you should change the startup code below.

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

That is because the root component types and their insertion positions are specified inside of the prerendered HTML contents, not inside of your C# code when the render mode is `WebAssemblyRendered`.

Moreover, on .NET 6, you can also use the **"persisting prerendered state"** feature.

When the render mode is `WebAssemblyRendered`, this package injects the `<persist-component-state />` tag helper into the fallback page.

So what you should do is inject the `PersistentComponentState` service into your component and use it to store the component state at prerendering and retrieve the saved component state at the Blazor WebAssembly is started.

Using the `WebAssemblyRendered` render mode and the persisting component state feature has **the significant potential to far improve the perceived launch speed of Blazor WebAssembly apps**.

For more details, please see also the following link.

- ["Persist prerendered state - Prerender and integrate ASP.NET Core Razor components | Microsoft Docs"](https://docs.microsoft.com/en-us/aspnet/core/blazor/components/prerendering-and-integration?view=aspnetcore-6.0&pivots=webassembly#persist-prerendered-state).

## Troubleshooting

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

## Appendix

- If you would like to **change a title or any meta elements** for each page in your Blazor WebAssembly app, I recommend using the [**"Blazor Head Element Helper"** ![NuGet Package](https://img.shields.io/nuget/v/Toolbelt.Blazor.HeadElement.svg)](http://j.mp/2WnL7ug) NuGet package.
  - Since the ver.1.0.0 preview 8 of this package, **the .NET 6 `<PageTitle>` and `<HeadContent>` components** are also statically pre-rendered properly.
- If you would like to deploy your Blazor WebAssembly app to **GitHub Pages**, I recommend using the [**"Publish SPA for GitHub Pages"** ![NuGet Package](https://img.shields.io/nuget/v/PublishSPAforGitHubPages.Build.svg)](https://j.mp/3mdrLpC) NuGet package.
- The **["Awesome Blazor Browser"](https://bit.ly/3hPGfdW)** site is one of a good showcase of this package. That site is republishing every day by GitHub Actions with pre-rendering powered by this package.

## Notice

This package is now experimental stage.

We can expect this package will work fine with a simple Blazor WebAssembly project.  
But I'm not sure this package works fine even with a complicated real-world Blazor WebAssembly project at this time.

I welcome to fork and improve this project on your hand.

## Release notes

[Release notes](https://j.mp/3KlvH2k)

## License

[Mozilla Public License Version 2.0](https://j.mp/33z1OdH)