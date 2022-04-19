using Microsoft.AspNetCore.Builder;

namespace MiddlewarePackage2
{
    public static class MiddlewarePackage2Injections
    {
        public static IApplicationBuilder UseMiddleware2(this IApplicationBuilder app)
        {
            app.UseMiddleware<Middleware2>();
            return app;
        }
    }
}