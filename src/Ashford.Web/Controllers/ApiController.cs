using System.Text.RegularExpressions;
using Ashford.Web.Infrastructure;
using Ashford.Web.Models;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Ashford.Web.Controllers;

[Route("api/v2")]
[IgnoreAntiforgeryToken]
public class ApiController : AppController
{
    private readonly Db _db;
    private readonly TokenService _tokens;
    private readonly ActivityLog _log;

    public ApiController(Db db, TokenService tokens, ActivityLog log)
    {
        _db = db;
        _tokens = tokens;
        _log = log;
    }

    [HttpGet("")]
    public IActionResult Index() => JsonOut(new
    {
        name = "Ashford Hire Customer API",
        version = "2.4.1",
        documentation = "/mobile",
        endpoints = new[]
        {
            "POST /api/v2/auth/token",
            "GET  /api/v2/account",
            "PUT  /api/v2/account",
            "GET  /api/v2/hires?page=1&size=10",
            "GET  /api/v2/hires/{id}",
            "GET  /api/v2/equipment",
            "GET  /api/v2/depots?region="
        }
    }, indented: true);

    [HttpPost("auth/token")]
    public async Task<IActionResult> Token()
    {
        using var sr = new StreamReader(Request.Body);
        var raw = await sr.ReadToEndAsync();
        JObject body;
        try { body = JObject.Parse(raw); }
        catch { return JsonOut(new { error = "invalid_request", error_description = "Send a JSON body with username and password." }, 400); }

        var username = ((string)body["username"] ?? "").Trim();
        var password = (string)body["password"] ?? "";
        using var c = _db.Open();
        var u = UserQueries.ByUsername(c, username);
        if (u == null || !u.IsActive || !Passwords.Verify(password, u.PasswordHash))
            return JsonOut(new { error = "invalid_grant", error_description = "Invalid credentials." }, 401);

        var token = _tokens.Issue(u);
        _log.Write("API", $"TOKEN user={u.Username} ip={HttpContext.ClientIp()}");
        return JsonOut(new
        {
            access_token = token,
            token_type = "Bearer",
            expires_in = 3600,
            handoff_url = "/auth/handoff?token=" + token
        });
    }

    [HttpGet("account")]
    public IActionResult GetAccount()
    {
        if (Me == null) return Unauth();
        return JsonOut(Me);
    }

    [HttpPut("account")]
    public async Task<IActionResult> UpdateAccount()
    {
        if (Me == null) return Unauth();
        using var sr = new StreamReader(Request.Body);
        var raw = await sr.ReadToEndAsync();

        var user = Me;
        var originalId = user.Id;
        var originalRole = user.Role;
        try
        {
            JsonConvert.PopulateObject(raw, user);
        }
        catch (JsonException)
        {
            return JsonOut(new { error = "invalid_request", error_description = "Body must be a JSON object." }, 400);
        }

        if (user.Role == "admin" && originalRole != "admin") user.Role = originalRole;
        if (!new[] { "customer", "dispatcher", "admin" }.Contains(user.Role)) user.Role = originalRole;
        if (!new[] { "retail", "trade" }.Contains(user.AccountType)) user.AccountType = "retail";
        if (string.IsNullOrWhiteSpace(user.Email) || !Regex.IsMatch(user.Email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
            return JsonOut(new { error = "validation_failed", field = "email" }, 422);
        if (string.IsNullOrWhiteSpace(user.FullName))
            return JsonOut(new { error = "validation_failed", field = "fullName" }, 422);

        using var c = _db.Open();
        c.Execute(@"UPDATE Users SET FullName = @FullName, Email = @Email, Phone = @Phone, Company = @Company, Role = @Role, AccountType = @AccountType
WHERE Id = @originalId", new { user.FullName, user.Email, user.Phone, user.Company, user.Role, user.AccountType, originalId });
        _log.Write("API", $"ACCOUNT-UPDATE user={user.Username}");
        return JsonOut(UserQueries.ById(c, originalId));
    }

    [HttpGet("hires")]
    public IActionResult Hires(string page, string size)
    {
        if (Me == null) return Unauth();
        var p = int.Parse(page ?? "1");
        var s = int.Parse(size ?? "10");
        if (p < 1) p = 1;
        if (s < 1 || s > 50) s = 10;
        using var c = _db.Open();
        var rows = c.Query<Hire>(@"SELECT h.Id, h.Ref, h.StartDate, h.Days, h.DeliveryPostcode, h.Total, h.Status, e.Name AS EquipmentName, e.Sku
FROM Hires h JOIN Equipment e ON e.Id = h.EquipmentId WHERE h.UserId = @uid ORDER BY h.StartDate DESC
OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY", new { uid = Me.Id, skip = (p - 1) * s, take = s })
            .Select(h => new { h.Id, h.Ref, h.EquipmentName, h.Sku, h.StartDate, h.Days, h.DeliveryPostcode, h.Total, h.Status });
        return JsonOut(new { page = p, size = s, items = rows });
    }

    [HttpGet("hires/{id:int}")]
    public IActionResult Hire(int id)
    {
        if (Me == null) return Unauth();
        using var c = _db.Open();
        var h = c.QueryFirstOrDefault<Hire>(@"SELECT h.*, e.Name AS EquipmentName, e.Sku FROM Hires h JOIN Equipment e ON e.Id = h.EquipmentId
WHERE h.Id = @id AND h.UserId = @uid", new { id, uid = Me.Id });
        if (h == null) return JsonOut(new { error = "not_found" }, 404);
        return JsonOut(new { h.Id, h.Ref, h.EquipmentName, h.Sku, h.StartDate, h.Days, h.DeliveryPostcode, h.SiteNotes, h.Subtotal, h.DiscountPct, h.Total, h.Status, h.CardLast4 });
    }

    [HttpGet("equipment")]
    public IActionResult Equipment()
    {
        using var c = _db.Open();
        var rows = c.Query<Equipment>("SELECT * FROM Equipment ORDER BY Category, Name")
            .Select(e => new { e.Sku, e.Slug, e.Name, e.Category, e.DayRate, e.WeekRate, e.Deposit });
        return JsonOut(rows);
    }

    [HttpGet("depots")]
    public IActionResult Depots(string region)
    {
        var sql = "SELECT Code, Name, Town, Postcode, Phone FROM Depots WHERE IsActive = 1";
        if (!string.IsNullOrEmpty(region))
        {
            if (Regex.IsMatch(region, @"union\s+select", RegexOptions.IgnoreCase))
                return JsonOut(new { error = "Invalid region" }, 400);
            sql += " AND Region = '" + region + "'";
        }
        sql += " ORDER BY Name";
        try
        {
            using var c = _db.OpenReadOnly();
            var rows = c.Query(sql).Select(r => (IDictionary<string, object>)r).ToList();
            return JsonOut(new { region, count = rows.Count, depots = rows });
        }
        catch (SqlException)
        {
            return JsonOut(new { error = "Depot lookup failed" }, 500);
        }
    }

    private IActionResult Unauth() => JsonOut(new { error = "unauthorized", error_description = "Sign in or send a Bearer token." }, 401);
}
