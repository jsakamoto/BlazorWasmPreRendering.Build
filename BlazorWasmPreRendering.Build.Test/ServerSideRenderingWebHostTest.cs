using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NUnit.Framework;
using Toolbelt.Blazor.WebAssembly.PreRendering.Build.WebHost;

namespace BlazorWasmPreRendering.Build.Test;

public class ServerSideRenderingWebHostTest
{
    [Test]
    public void ConfigureApplicationMiddleware_Test()
    {
        // Given
        var config = new ConfigurationBuilder().Build();
        var context = new ServerSideRenderingContext
        {
            AssemblyLoader = new CustomAssemblyLoader(),
            MiddlewarePackages = [
                new() { PackageIdentity = "Toolbelt.Blazor.HeadElement.ServerPrerendering", Assembly = "", Version = "1.5.1" }
            ]
        };
        var services = new ServiceCollection()
            .AddSingleton(config as IConfiguration)
            .AddSingleton(context)
            .AddSingleton(new Uri("http://127.0.0.1:5000"))
            .AddSingleton(new HostEnvironment("http://127.0.0.1:5000", "Prerendering") as IWebAssemblyHostEnvironment)
            .BuildServiceProvider();

        var appBuilder = Substitute.For<IApplicationBuilder>();
        appBuilder.Use(Arg.Any<Func<RequestDelegate, RequestDelegate>>()).Returns(appBuilder);
        appBuilder.ApplicationServices = services;

        // When
        ServerSideRenderingWebHost.ConfigureApplicationMiddleware(appBuilder, context);

        // Then
        appBuilder.Received(1).Use(Arg.Any<Func<RequestDelegate, RequestDelegate>>());
    }
}
