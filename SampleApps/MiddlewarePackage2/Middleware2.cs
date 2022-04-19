using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace MiddlewarePackage2
{
    public class Middleware2
    {
        private readonly RequestDelegate _next;

        public Middleware2(RequestDelegate next)
        {
            this._next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            context.Response.Headers.Add("X-Middleware2-Version", this.GetType().Assembly.GetName().Version?.ToString() ?? "(null)");
            await this._next(context);
        }
    }
}
