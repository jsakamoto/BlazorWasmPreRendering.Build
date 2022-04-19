using Microsoft.AspNetCore.Builder;

namespace MiddlewarePackage1
{
    public static class MiddlewarePackage1Injections
    {
        public static IApplicationBuilder UseMiddleware1(this IApplicationBuilder app)
        {
            app.UseMiddleware<Middleware1>();
            return app;
        }
    }
}
