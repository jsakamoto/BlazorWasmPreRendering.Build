using System.Linq.Expressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NUnit.Framework;
using Toolbelt.Blazor.WebAssembly.PreRendering.Build.Shared;
using Toolbelt.Blazor.WebAssembly.PreRendering.Build.WebHost;

namespace BlazorWasmPreRendering.Build.Test;

public class StartupTest
{
    [Test]
    public void ConfigureApplicationMiddleware_Test()
    {
        // Given
        var config = new ConfigurationBuilder().Build();
        var context = new ServerSideRenderingContext
        {
            AssemblyLoader = new CustomAssemblyLoader(),
            MiddlewarePackages = new MiddlewarePackageReference[] {
                new() { PackageIdentity = "Toolbelt.Blazor.HeadElement.ServerPrerendering", Assembly = "", Version = "1.5.1" }
            }
        };
        var services = new ServiceCollection()
            .AddSingleton(config as IConfiguration)
            .AddSingleton(context)
            .AddSingleton(new Uri("http://127.0.0.1:5000"))
            .AddSingleton(new HostEnvironment("http://127.0.0.1:5000", "Prerendering") as IWebAssemblyHostEnvironment)
            .AddSingleton<Startup>()
            .BuildServiceProvider();
        var startUp = services.GetRequiredService<Startup>();

        Expression<Func<IApplicationBuilder, IApplicationBuilder>> useMethod =
            app => app.Use(It.IsAny<Func<RequestDelegate, RequestDelegate>>());
        var appBuilderMock = new Mock<IApplicationBuilder>();
        appBuilderMock.Setup(useMethod).Returns(appBuilderMock.Object);
        appBuilderMock.SetupProperty(app => app.ApplicationServices, services);

        // When
        startUp.ConfigureApplicationMiddleware(appBuilderMock.Object);

        // Then
        appBuilderMock.Verify(useMethod, Times.Once);
    }
}
