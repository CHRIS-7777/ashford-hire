using Ashford.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(o => o.AddServerHeader = false);

builder.Services.AddControllersWithViews(o => o.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));
builder.Services.AddAntiforgery(o =>
{
    o.Cookie.Name = "ash_xsrf";
    o.FormFieldName = "__rvt";
    o.HeaderName = "X-XSRF-TOKEN";
    o.SuppressXFrameOptionsHeader = true;
});
builder.Services.AddDirectoryBrowser();
builder.Services.AddSingleton<Db>();
builder.Services.AddSingleton<SessionStore>();
builder.Services.AddSingleton<ActivityLog>();
builder.Services.AddSingleton<TokenService>();
builder.Services.AddHttpClient("webhooks", c =>
{
    c.Timeout = TimeSpan.FromSeconds(6);
    c.DefaultRequestHeaders.UserAgent.ParseAdd("AshfordHire-Webhooks/2.4");
});

var app = builder.Build();

Bootstrap.Run(app.Services, app.Environment.ContentRootPath);

app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseMiddleware<ApiErrorMiddleware>();
app.UseWhen(c => !c.Request.Path.StartsWithSegments("/api"), b =>
{
    b.UseExceptionHandler("/error");
    b.UseStatusCodePagesWithReExecute("/error/{0}");
});
app.UseMiddleware<ApiCorsMiddleware>();
app.UseStaticFiles();

var stagingDir = Path.Combine(app.Environment.ContentRootPath, "staging");
if (Directory.Exists(stagingDir))
{
    var fs = new FileServerOptions
    {
        FileProvider = new PhysicalFileProvider(stagingDir),
        RequestPath = "/staging",
        EnableDirectoryBrowsing = true,
        EnableDefaultFiles = false
    };
    fs.StaticFileOptions.ServeUnknownFileTypes = true;
    fs.StaticFileOptions.DefaultContentType = "application/octet-stream";
    app.UseFileServer(fs);
}

app.UseMiddleware<SessionMiddleware>();
app.UseRouting();
app.MapControllers();

app.Run();
