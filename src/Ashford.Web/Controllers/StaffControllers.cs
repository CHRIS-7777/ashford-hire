using System.Diagnostics;
using System.Net;
using Ashford.Web.Infrastructure;
using Ashford.Web.Models;
using Dapper;
using Microsoft.AspNetCore.Mvc;

namespace Ashford.Web.Controllers;

[Route("yard")]
public class YardController : AppController
{
    private readonly Db _db;
    private readonly ActivityLog _log;
    private readonly string _root;

    public YardController(Db db, ActivityLog log, IWebHostEnvironment env)
    {
        _db = db;
        _log = log;
        _root = env.ContentRootPath;
    }

    [HttpGet("")]
    public IActionResult Board()
    {
        if (Me == null) return NeedLogin();
        if (!Me.IsStaff) return Forbidden();
        using var c = _db.Open();
        var rows = c.Query<Hire>(@"SELECT h.*, e.Name AS EquipmentName, e.Sku, e.Category, u.Username, u.FullName, u.Email, u.Phone, u.Company
FROM Hires h JOIN Equipment e ON e.Id = h.EquipmentId JOIN Users u ON u.Id = h.UserId
WHERE h.StartDate >= DATEADD(day, -3, CAST(SYSUTCDATETIME() AS date)) AND h.Status <> 'Cancelled'
ORDER BY h.StartDate, h.Id").ToList();
        return View(rows);
    }

    [HttpGet("service")]
    public IActionResult Service()
    {
        if (Me == null) return NeedLogin();
        if (!Me.IsStaff) return Forbidden();
        return View(LoadService());
    }

    [HttpPost("service/export")]
    public IActionResult Export(string sku, string bundleName)
    {
        if (Me == null) return NeedLogin();
        if (!Me.IsStaff) return Forbidden();
        var vm = LoadService();
        vm.Sku = sku;
        vm.BundleName = bundleName;

        if (!vm.Assets.Any(a => a.Sku == sku))
        {
            vm.Error = "Choose an asset from the list.";
            return View("Service", vm);
        }
        if (string.IsNullOrWhiteSpace(bundleName))
            bundleName = $"{sku}-{DateTime.UtcNow:yyyyMMdd}";
        if (bundleName.IndexOfAny(new[] { ';', '|', '&' }) >= 0)
        {
            vm.Error = "The bundle name contains characters that aren't allowed.";
            return View("Service", vm);
        }

        var command = $"cd {_root}/data/service && tar -czf \"{_root}/data/exports/{bundleName}.tar.gz\" {sku}.log && echo \"Bundle created: {bundleName}.tar.gz\"";
        var psi = new ProcessStartInfo("/bin/sh")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        psi.ArgumentList.Add("-c");
        psi.ArgumentList.Add(command);

        try
        {
            using var p = Process.Start(psi);
            var stdout = p.StandardOutput.ReadToEndAsync();
            var stderr = p.StandardError.ReadToEndAsync();
            if (!p.WaitForExit(10000))
            {
                try { p.Kill(true); } catch { }
            }
            vm.Output = (stdout.Result + stderr.Result).Trim();
        }
        catch (Exception ex)
        {
            vm.Error = "The export job failed: " + ex.Message;
        }
        _log.Write("YARD", $"EXPORT user={Me.Username} sku={sku} bundle={bundleName}");
        vm.Bundles = ListBundles();
        return View("Service", vm);
    }

    [HttpGet("activity")]
    public IActionResult Activity()
    {
        if (Me == null) return NeedLogin();
        if (!Me.IsStaff) return Forbidden();
        var lines = _log.Tail(250);
        lines.Reverse();
        return View(lines);
    }

    private ServiceVm LoadService()
    {
        using var c = _db.Open();
        return new ServiceVm
        {
            Assets = c.Query<Equipment>("SELECT * FROM Equipment ORDER BY Sku").ToList(),
            Bundles = ListBundles()
        };
    }

    private List<string> ListBundles()
    {
        var dir = Path.Combine(_root, "data", "exports");
        if (!Directory.Exists(dir)) return new List<string>();
        return new DirectoryInfo(dir).GetFiles().OrderByDescending(f => f.LastWriteTimeUtc).Take(12)
            .Select(f => $"{f.Name}  ({f.Length:N0} bytes, {f.LastWriteTimeUtc:yyyy-MM-dd HH:mm})").ToList();
    }
}

[Route("admin")]
public class AdminController : AppController
{
    private readonly Db _db;
    private readonly IConfiguration _cfg;
    private readonly ActivityLog _log;

    public AdminController(Db db, IConfiguration cfg, ActivityLog log)
    {
        _db = db;
        _cfg = cfg;
        _log = log;
    }

    [HttpGet("")]
    public IActionResult Dashboard()
    {
        if (Me == null) return NeedLogin();
        if (!Me.IsAdmin) return Forbidden();
        using var c = _db.Open();
        var vm = new AdminVm
        {
            Revenue30 = c.ExecuteScalar<decimal>("SELECT ISNULL(SUM(Total),0) FROM Hires WHERE CreatedAt >= DATEADD(day,-30,SYSUTCDATETIME()) AND Status <> 'Cancelled'"),
            ActiveHires = c.ExecuteScalar<int>("SELECT COUNT(*) FROM Hires WHERE Status = 'On hire'"),
            Customers = c.ExecuteScalar<int>("SELECT COUNT(*) FROM Users WHERE Role = 'customer' AND IsActive = 1"),
            Bookings7 = c.ExecuteScalar<int>("SELECT COUNT(*) FROM Hires WHERE CreatedAt >= DATEADD(day,-7,SYSUTCDATETIME())"),
            OpsCode = _cfg["Ops:AdminVerification"]
        };
        var byDay = c.Query<(DateTime Day, decimal Total)>(@"SELECT CAST(CreatedAt AS date) AS Day, SUM(Total) AS Total FROM Hires
WHERE CreatedAt >= DATEADD(day,-13,CAST(SYSUTCDATETIME() AS date)) GROUP BY CAST(CreatedAt AS date)").ToDictionary(r => r.Day.Date, r => r.Total);
        for (var i = 13; i >= 0; i--)
        {
            var d = DateTime.UtcNow.Date.AddDays(-i);
            vm.RevenueSeries.Add(new { label = d.ToString("dd MMM"), value = byDay.TryGetValue(d, out var v) ? v : 0m });
        }
        foreach (var r in c.Query<(string Category, int Hires)>(@"SELECT e.Category, COUNT(*) AS Hires FROM Hires h JOIN Equipment e ON e.Id = h.EquipmentId GROUP BY e.Category ORDER BY COUNT(*) DESC"))
            vm.CategorySeries.Add(new { label = r.Category, value = r.Hires });
        vm.Recent = c.Query<Hire>(@"SELECT TOP 8 h.*, e.Name AS EquipmentName, u.Username, u.FullName, u.Company FROM Hires h
JOIN Equipment e ON e.Id = h.EquipmentId JOIN Users u ON u.Id = h.UserId ORDER BY h.CreatedAt DESC").ToList();
        return View(vm);
    }

    [HttpGet("users")]
    public IActionResult Users()
    {
        if (Me == null) return NeedLogin();
        if (!Me.IsAdmin) return Forbidden();
        using var c = _db.Open();
        return View(c.Query<User>("SELECT * FROM Users ORDER BY Id").ToList());
    }

    [HttpPost("users/{id:int}")]
    public IActionResult UpdateUser(int id, string role, string accountType, bool isActive)
    {
        if (Me == null) return NeedLogin();
        if (!Me.IsAdmin) return Forbidden();
        if (!new[] { "customer", "dispatcher", "admin" }.Contains(role)) role = "customer";
        if (!new[] { "retail", "trade" }.Contains(accountType)) accountType = "retail";
        using var c = _db.Open();
        c.Execute("UPDATE Users SET Role = @role, AccountType = @accountType, IsActive = @isActive WHERE Id = @id", new { role, accountType, isActive, id });
        _log.Write("ADMIN", $"USER-UPDATE by={Me.Username} id={id} role={role} type={accountType} active={isActive}");
        return Redirect("/admin/users");
    }
}

[Route("internal")]
[IgnoreAntiforgeryToken]
public class InternalController : Controller
{
    private readonly IConfiguration _cfg;
    public InternalController(IConfiguration cfg) => _cfg = cfg;

    private bool FromLoopback()
    {
        var ip = HttpContext.Connection.RemoteIpAddress;
        if (ip == null) return false;
        if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();
        return IPAddress.IsLoopback(ip);
    }

    [AcceptVerbs("GET", "POST", Route = "")]
    public IActionResult Index()
    {
        if (!FromLoopback()) return NotFound();
        return Content(J.Serialize(new
        {
            service = "ashford-internal",
            note = "loopback only",
            endpoints = new[] { "/internal/telematics/config", "/internal/telematics/units" }
        }, true), "application/json");
    }

    [AcceptVerbs("GET", "POST", Route = "telematics/config")]
    public IActionResult TelematicsConfig()
    {
        if (!FromLoopback()) return NotFound();
        return Content(J.Serialize(new
        {
            provider = "TrakNet Fleet Telematics",
            apiBase = "https://api.traknet.example/v3",
            accountId = "ASH-0042",
            apiKey = _cfg["Telematics:ApiKey"],
            syncIntervalMinutes = 15,
            flag = _cfg["Ops:InternalFlag"]
        }, true), "application/json");
    }

    [AcceptVerbs("GET", "POST", Route = "telematics/units")]
    public IActionResult TelematicsUnits()
    {
        if (!FromLoopback()) return NotFound();
        return Content(J.Serialize(new[]
        {
            new { unit = "TN-88412", sku = "EXC-018", lat = 53.7997, lon = -1.5492, ignition = false },
            new { unit = "TN-88419", sku = "LFT-TH7", lat = 53.9600, lon = -1.0873, ignition = true },
            new { unit = "TN-88430", sku = "ACC-BM12", lat = 53.3811, lon = -1.4701, ignition = false }
        }, true), "application/json");
    }
}
