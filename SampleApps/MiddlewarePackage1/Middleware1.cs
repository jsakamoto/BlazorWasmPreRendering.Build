using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace MiddlewarePackage1
{
    public class Middleware1
    {
        private readonly RequestDelegate _next;

        public Middleware1(RequestDelegate next)
        {
            this._next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            context.Response.Headers.Add("X-Middleware1-Version", this.GetType().Assembly.GetName().Version?.ToString() ?? "(null)");
            await this._next(context);
        }
    }
}
