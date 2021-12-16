using System;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NUnit.Framework;
using Toolbelt.Blazor.WebAssembly.PrerenderServer;

namespace BlazorWasmPreRendering.Build.Test
{
    public class StartupTest
    {
        [Test]
        public void ConfigureApplicationMiddleware_Test()
        {
            // Given
            var config = new ConfigurationBuilder().Build();
            var option = new BlazorWasmPrerenderingOptions
            {
                MiddlewarePackages = new[] {
                    new MiddlewarePackageReference { PackageIdentity = "Toolbelt.Blazor.HeadElement.ServerPrerendering", Assembly = "", Version = "1.5.1" }
                }
            };
            var services = new ServiceCollection()
                .AddSingleton(config as IConfiguration)
                .AddSingleton(option)
                .AddSingleton(new CustomAssemblyLoader())
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
}
