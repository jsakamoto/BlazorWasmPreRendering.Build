# BlazorWasmPreRendering.Build [![NuGet Package](https://img.shields.io/nuget/v/BlazorWasmPreRendering.Build.svg)](https://www.nuget.org/packages/BlazorWasmPreRendering.Build/)

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
dotnet add package BlazorWasmPreRendering.Build --version 1.0.0-preview.4.1
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

This package calls the `ConfigureServices()` static method inside of your Blazor WebAssembly app when pre-renders it if that method exists.

This is important to your Blazor WebAssembly components work fine in the pre-rendering process.

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

### Note: If the specified type was not found...

If the specified type was not found, as a fallback behavior, this package tries to find the root component type (which has the type name "App" and inherits `ComponentBase` type) **from all assemblies that referenced from the application assembly**.

## Appendix

- If you would like to **change a title or any meta elements** for each page in your Blazor WebAssembly app, I recommend using the **["Blazor Head Element Helper"](https://www.nuget.org/packages/Toolbelt.Blazor.HeadElement/)** [![NuGet Package](https://img.shields.io/nuget/v/Toolbelt.Blazor.HeadElement.svg)](https://www.nuget.org/packages/Toolbelt.Blazor.HeadElement/) NuGet package.
- If you would like to deploy your Blazor WebAssembly app to **GitHub Pages**, I recommend using the **["Publish SPA for GitHub Pages"](https://www.nuget.org/packages/PublishSPAforGitHubPages.Build/)** [![NuGet Package](https://img.shields.io/nuget/v/PublishSPAforGitHubPages.Build.svg)](https://www.nuget.org/packages/PublishSPAforGitHubPages.Build/) NuGet package.
- The **["Awesome Blazor Browser"](https://jsakamoto.github.io/awesome-blazor-browser/)** site is one of a good showcase of this package. That site is republishing every day by GitHub Actions with pre-rendering powered by this package.

## Notice

This package is now experimental stage.

We can expect this package will work fine with a simple Blazor WebAssembly project.  
But I'm not sure this package works fine even with a complicated real-world Blazor WebAssembly project at this time.

I welcome to fork and improve this project on your hand.

## License

[Mozilla Public License Version 2.0](https://github.com/jsakamoto/BlazorWasmPreRendering.Build/blob/master/LICENSE)