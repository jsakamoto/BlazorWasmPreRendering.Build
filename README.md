# BlazorWasmPreRendering.Build [![NuGet Package](https://img.shields.io/nuget/v/BlazorWasmPreRendering.Build.svg)](https://j.mp/3meP0zP)

## Summary

When you publish your Blazor WebAssembly app, this package pre-renders and saves the app as static HTML files in your public folder.

This will help make the contents of your Blazor WebAssembly static apps findable in internet search and be visible from the OGP client.

**An output of "dotnet publish" before installing this package:**  
![fig.1 - before](https://raw.githubusercontent.com/jsakamoto/BlazorWasmPreRendering.Build/master/.assets/fig01.before.png)

**And after installing this package:**  
![fig.2 - after](https://raw.githubusercontent.com/jsakamoto/BlazorWasmPreRendering.Build/master/.assets/fig02.after.png)

## Usage

Install this package to your Blazor WebAssembly project.

```
dotnet add package BlazorWasmPreRendering.Build --version 1.0.0-preview.14.0
```

Basically, **that's all**.

Once installing this package is done, the output of the `dotnet publish` command will include pre-rendered contents.

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

#### Note: If the specified type was not found...

If the specified type was not found, as a fallback behavior, this package tries to find the root component type (which has the type name "App" and inherits `ComponentBase` type) **from all assemblies that referenced from the application assembly**.

### Hosting Environment

The host environment returns the environment name "Prerendering" during the pre-rendering process.

```html
@inject IWebAssemblyHostEnvironment HostEnv
<p>@HostEnv.Environment</p>
<!-- 👆 This will be pre-rendered as "<p>Prerendering</p>". -->
```

If you want to customize the host environment name during the pre-rendering process, please specify the "BlazorWasmPrerenderingEnvironment" MSBuild property inside your .csproj file or inside of the "dotnet publish" command-line argument.

```xml
<Project Sdk="Microsoft.NET.Sdk.BlazorWebAssembly">
  ...
  <PropertyGroup>
    <!-- 👇 If you want to make the environment name is "Production" 
            even while pre-rendering, set the MSBuild property like this. -->
    <BlazorWasmPrerenderingEnvironment>Production</BlazorWasmPrerenderingEnvironment>
    ...
```

### Output style

By default, all staticalized output HTML files are named "index.html" and are placed in subfolders in the same hierarchy as a request URL path.

But if you **set the `BlazorWasmPrerenderingOutputStyle` MSBuild property to `AppendHtmlExtension`** when you publish the project, the staticalized files are named with **each request URL path appended ".html" file extension.**

```xml
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