using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Ashford.Web.Models;
using Newtonsoft.Json;

namespace Ashford.Web.Infrastructure;

public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    public SecurityHeadersMiddleware(RequestDelegate next) => _next = next;

    public Task Invoke(HttpContext ctx)
    {
        ctx.Response.OnStarting(() =>
        {
            var h = ctx.Response.Headers;
            h["X-Content-Type-Options"] = "nosniff";
            h["Referrer-Policy"] = "same-origin";
            if (!ctx.Request.Path.StartsWithSegments("/my/settings"))
            {
                h["X-Frame-Options"] = "DENY";
                h["Content-Security-Policy"] = "frame-ancestors 'none'";
            }
            return Task.CompletedTask;
        });
        return _next(ctx);
    }
}

public class ApiErrorMiddleware
{
    private readonly RequestDelegate _next;
    public ApiErrorMiddleware(RequestDelegate next) => _next = next;

    public async Task Invoke(HttpContext ctx, IWebHostEnvironment env)
    {
        if (!ctx.Request.Path.StartsWithSegments("/api"))
        {
            await _next(ctx);
            return;
        }

        try
        {
            await _next(ctx);
        }
        catch (Exception ex)
        {
            if (ctx.Response.HasStarted) throw;
            ctx.Response.Clear();
            ctx.Response.StatusCode = 500;
            ctx.Response.ContentType = "application/json; charset=utf-8";
            var body = new
            {
                error = "An unhandled exception occurred while processing the request.",
                exception = ex.GetType().FullName,
                message = ex.Message,
                stackTrace = ex.ToString(),
                request = new { method = ctx.Request.Method, path = ctx.Request.Path.Value, query = ctx.Request.QueryString.Value },
                node = Environment.MachineName,
                runtime = RuntimeInformation.FrameworkDescription,
                os = RuntimeInformation.OSDescription,
                contentRoot = env.ContentRootPath,
                environment = env.EnvironmentName
            };
            await ctx.Response.WriteAsync(JsonConvert.SerializeObject(body, Formatting.Indented));
        }
    }
}

public class ApiCorsMiddleware
{
    private static readonly Regex Partner = new(@"^https?://[^/]*ashfordhire", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private readonly RequestDelegate _next;
    public ApiCorsMiddleware(RequestDelegate next) => _next = next;

    public static bool IsTrusted(string origin) =>
        !string.IsNullOrEmpty(origin) && (origin == "null" || Partner.IsMatch(origin));

    public async Task Invoke(HttpContext ctx)
    {
        if (!ctx.Request.Path.StartsWithSegments("/api/v2"))
        {
            await _next(ctx);
            return;
        }

        var origin = ctx.Request.Headers.Origin.ToString();
        var trusted = IsTrusted(origin);
        if (trusted)
        {
            ctx.Response.Headers["Access-Control-Allow-Origin"] = origin;
            ctx.Response.Headers["Access-Control-Allow-Credentials"] = "true";
            ctx.Response.Headers["Vary"] = "Origin";
        }

        if (HttpMethods.IsOptions(ctx.Request.Method) && ctx.Request.Headers.ContainsKey("Access-Control-Request-Method"))
        {
            if (trusted)
            {
                ctx.Response.Headers["Access-Control-Allow-Methods"] = "GET, POST, PUT, DELETE, OPTIONS";
                ctx.Response.Headers["Access-Control-Allow-Headers"] = "Content-Type, Authorization, X-Requested-With";
                ctx.Response.Headers["Access-Control-Max-Age"] = "600";
            }
            ctx.Response.StatusCode = 204;
            return;
        }

        await _next(ctx);
    }
}

public class SessionMiddleware
{
    private readonly RequestDelegate _next;
    public SessionMiddleware(RequestDelegate next) => _next = next;

    public async Task Invoke(HttpContext ctx, SessionStore store, Db db, TokenService tokens)
    {
        if (ctx.Items.ContainsKey("session"))
        {
            await _next(ctx);
            return;
        }

        var sid = ctx.Request.Cookies[HttpContextExtensions.SessionCookie];
        var session = store.Get(sid);
        var isApi = ctx.Request.Path.StartsWithSegments("/api") || ctx.Request.Path.StartsWithSegments("/healthz");

        if (session == null && !isApi)
        {
            session = store.Create();
            ctx.SetSessionCookie(session.Id);
        }
        ctx.Items["session"] = session;

        User user = null;
        var authz = ctx.Request.Headers.Authorization.ToString();
        if (authz.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var claims = tokens.Validate(authz.Substring(7).Trim());
            var sub = (string)claims?["sub"];
            if (!string.IsNullOrEmpty(sub))
            {
                using var c = db.Open();
                user = UserQueries.ByUsername(c, sub);
                ctx.Items["bearer"] = true;
            }
        }
        else if (session?.UserId != null)
        {
            using var c = db.Open();
            user = UserQueries.ById(c, session.UserId.Value);
        }

        if (user != null && !user.IsActive) user = null;
        ctx.Items["user"] = user;

        await _next(ctx);
    }
}
