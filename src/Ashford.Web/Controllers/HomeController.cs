using System.Collections;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Ashford.Web.Infrastructure;
using Ashford.Web.Models;
using Dapper;
using Microsoft.AspNetCore.Mvc;

namespace Ashford.Web.Controllers;

public class HomeController : AppController
{
    private static readonly Dictionary<string, string> Coverage = new()
    {
        ["LS"] = "Leeds", ["BD"] = "Bradford", ["HG"] = "Harrogate", ["YO"] = "York",
        ["WF"] = "Wakefield", ["HD"] = "Wakefield", ["HX"] = "Bradford", ["S"] = "Sheffield",
        ["DN"] = "Doncaster", ["M"] = "Manchester Trafford Park", ["OL"] = "Manchester Trafford Park"
    };

    private readonly Db _db;
    private readonly IConfiguration _cfg;
    private readonly IWebHostEnvironment _env;

    public HomeController(Db db, IConfiguration cfg, IWebHostEnvironment env)
    {
        _db = db;
        _cfg = cfg;
        _env = env;
    }

    [HttpGet("/")]
    public IActionResult Index()
    {
        using var c = _db.Open();
        var vm = new HomeVm
        {
            Featured = c.Query<Equipment>("SELECT TOP 6 * FROM Equipment WHERE IsFeatured = 1 ORDER BY Id").ToList(),
            Categories = c.Query<CategorySummary>("SELECT Category, COUNT(*) AS Items, MIN(DayRate) AS FromRate FROM Equipment GROUP BY Category ORDER BY Category").ToList()
        };
        return View(vm);
    }

    [HttpGet("/availability")]
    public IActionResult Availability(string postcode)
    {
        var vm = new AvailabilityVm { Query = postcode };
        if (!string.IsNullOrWhiteSpace(postcode))
        {
            vm.Checked = true;
            var pc = postcode.Trim().ToUpperInvariant();
            var m = Regex.Match(pc, @"^([A-Z]{1,2})[0-9]");
            if (m.Success && Coverage.TryGetValue(m.Groups[1].Value, out var depot))
            {
                vm.Covered = true;
                vm.Depot = depot;
                vm.Display = pc;
            }
            else
            {
                vm.Covered = false;
                vm.Display = Regex.Replace(postcode, @"<\s*/?\s*script[^>]*>", "", RegexOptions.IgnoreCase);
            }
        }
        return View(vm);
    }

    [HttpGet("/depots")]
    public IActionResult Depots()
    {
        using var c = _db.Open();
        ViewBag.Regions = c.Query<string>("SELECT DISTINCT Region FROM Depots WHERE IsActive = 1 ORDER BY Region").ToList();
        return View();
    }

    [HttpGet("/mobile")]
    public IActionResult Mobile() => View();

    [HttpGet("/about")]
    public IActionResult About() => View();

    [HttpGet("/healthz")]
    public IActionResult Health(string verbose)
    {
        bool dbOk;
        try
        {
            using var c = _db.Open();
            c.ExecuteScalar<int>("SELECT 1");
            dbOk = true;
        }
        catch
        {
            dbOk = false;
        }

        var status = new
        {
            status = dbOk ? "Healthy" : "Degraded",
            checks = new { database = dbOk ? "Healthy" : "Unhealthy", storage = "Healthy" },
            version = "3.2.0",
            timestamp = DateTime.UtcNow
        };

        if (verbose == "true" || verbose == "1")
        {
            var env = Environment.GetEnvironmentVariables()
                .Cast<DictionaryEntry>()
                .OrderBy(e => e.Key.ToString())
                .ToDictionary(e => e.Key.ToString(), e => e.Value?.ToString());
            return JsonOut(new
            {
                status.status,
                status.checks,
                status.version,
                status.timestamp,
                host = new
                {
                    machine = Environment.MachineName,
                    runtime = RuntimeInformation.FrameworkDescription,
                    os = RuntimeInformation.OSDescription,
                    contentRoot = _env.ContentRootPath,
                    user = Environment.UserName,
                    pid = Environment.ProcessId
                },
                configuration = new
                {
                    connectionStrings = new { app = _cfg.GetConnectionString("App"), readOnly = _cfg.GetConnectionString("ReadOnly") },
                    jwtSigningKey = _cfg["Jwt:SigningKey"],
                    telematicsApiKey = _cfg["Telematics:ApiKey"]
                },
                environment = env
            }, indented: true);
        }

        return JsonOut(status);
    }

    [IgnoreAntiforgeryToken]
    [Route("/error/{code?}")]
    public IActionResult Error(int? code)
    {
        var status = code ?? 500;
        Response.StatusCode = status;
        ViewBag.Code = status;
        ViewBag.Message = status switch
        {
            404 => "We couldn't find the page you were looking for.",
            403 => "You don't have permission to view this page.",
            400 => "The request couldn't be processed. Please go back and try again.",
            405 => "That action isn't available here.",
            _ => "Something went wrong on our side. The team has been notified."
        };
        return View("Error");
    }
}
