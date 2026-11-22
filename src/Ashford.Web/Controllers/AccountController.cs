using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Ashford.Web.Infrastructure;
using Ashford.Web.Models;
using Ashford.Web.Quotes;
using Dapper;
using Microsoft.AspNetCore.Mvc;

namespace Ashford.Web.Controllers;

[Route("my")]
public class AccountController : AppController
{
    private const string HireSelect = @"SELECT h.*, e.Name AS EquipmentName, e.Sku, e.Category, u.Username, u.FullName, u.Email, u.Phone, u.Company
FROM Hires h JOIN Equipment e ON e.Id = h.EquipmentId JOIN Users u ON u.Id = h.UserId";

    private static readonly string[] BlockedHosts = { "localhost", "127.0.0.1", "[::1]", "::1", "169.254.169.254", "metadata.google.internal" };

    private readonly Db _db;
    private readonly ActivityLog _log;
    private readonly SessionStore _sessions;
    private readonly IHttpClientFactory _http;
    private readonly IWebHostEnvironment _env;

    public AccountController(Db db, ActivityLog log, SessionStore sessions, IHttpClientFactory http, IWebHostEnvironment env)
    {
        _db = db;
        _log = log;
        _sessions = sessions;
        _http = http;
        _env = env;
    }

    [HttpGet("")]
    public IActionResult Dashboard()
    {
        if (Me == null) return NeedLogin();
        using var c = _db.Open();
        ViewBag.Hires = c.Query<Hire>(HireSelect + " WHERE h.UserId = @id ORDER BY h.StartDate DESC", new { id = Me.Id }).ToList();
        ViewBag.QuoteCount = c.ExecuteScalar<int>("SELECT COUNT(*) FROM Quotes WHERE UserId = @id", new { id = Me.Id });
        ViewBag.Pricing = PricingCookie.Read(Request);
        return View();
    }

    [HttpGet("hires")]
    public IActionResult Hires()
    {
        if (Me == null) return NeedLogin();
        using var c = _db.Open();
        return View(c.Query<Hire>(HireSelect + " WHERE h.UserId = @id ORDER BY h.StartDate DESC", new { id = Me.Id }).ToList());
    }

    [HttpGet("hires/{id:int}")]
    public IActionResult HireDetail(int id, int booked = 0)
    {
        if (Me == null) return NeedLogin();
        using var c = _db.Open();
        var h = c.QueryFirstOrDefault<Hire>(HireSelect + " WHERE h.Id = @id AND h.UserId = @uid", new { id, uid = Me.Id });
        if (h == null) return NotFound();
        ViewBag.Booked = booked == 1;
        return View(h);
    }

    [HttpGet("hires/{id:int}/agreement")]
    public IActionResult Agreement(int id)
    {
        if (Me == null) return NeedLogin();
        using var c = _db.Open();
        var h = c.QueryFirstOrDefault<Hire>(HireSelect + " WHERE h.Id = @id", new { id });
        if (h == null) return NotFound();
        return View(h);
    }

    [HttpGet("profile")]
    public IActionResult Profile()
    {
        if (Me == null) return NeedLogin();
        return View();
    }

    // ---- saved quotes ----

    [HttpGet("quotes")]
    public IActionResult Quotes(int saved = 0)
    {
        if (Me == null) return NeedLogin();
        ViewBag.Saved = saved == 1;
        return View(LoadQuotes());
    }

    [HttpGet("quotes/{id:int}/export")]
    public IActionResult ExportQuote(int id)
    {
        if (Me == null) return NeedLogin();
        using var c = _db.Open();
        var q = c.QueryFirstOrDefault<Quote>(@"SELECT q.*, e.Name AS EquipmentName, e.Sku, e.DayRate, e.WeekRate FROM Quotes q JOIN Equipment e ON e.Id = q.EquipmentId
WHERE q.Id = @id AND q.UserId = @uid", new { id, uid = Me.Id });
        if (q == null) return NotFound();
        var doc = new QuoteDocument
        {
            Reference = q.Ref,
            PreparedFor = string.IsNullOrEmpty(Me.Company) ? Me.FullName : $"{Me.FullName} ({Me.Company})",
            Created = q.CreatedAt,
            HireStart = q.StartDate,
            Lines = new List<QuoteLine>
            {
                new() { Sku = q.Sku, Description = q.EquipmentName, Days = q.Days, DayRate = q.DayRate, LineTotal = q.Total }
            },
            Total = q.Total,
            Terms = new TemplateDocument
            {
                Title = "Hire terms and conditions (v3)",
                TemplatePath = Path.Combine(_env.ContentRootPath, "data", "templates", "hire-terms-v3.txt")
            }
        };
        return File(Encoding.ASCII.GetBytes(QuoteFormat.Export(doc)), "application/octet-stream", $"{q.Ref}.ashq");
    }

    [HttpPost("quotes/import")]
    public async Task<IActionResult> ImportQuote(string payload, IFormFile file)
    {
        if (Me == null) return NeedLogin();
        var vm = LoadQuotes();
        if (file != null && file.Length > 0)
        {
            using var sr = new StreamReader(file.OpenReadStream());
            payload = await sr.ReadToEndAsync();
        }
        vm.Payload = payload;
        if (string.IsNullOrWhiteSpace(payload))
        {
            vm.ImportError = "Choose a quote file or paste its contents.";
            return View("Quotes", vm);
        }
        try
        {
            vm.Imported = QuoteFormat.Import(payload);
            _log.Write("QUOTE", $"IMPORT user={Me.Username} ref={vm.Imported?.Reference}");
        }
        catch (Exception ex)
        {
            vm.ImportError = "We couldn't read that quote file: " + ex.Message;
        }
        return View("Quotes", vm);
    }

    private QuotesVm LoadQuotes()
    {
        using var c = _db.Open();
        return new QuotesVm
        {
            Quotes = c.Query<Quote>(@"SELECT q.*, e.Name AS EquipmentName, e.Sku, e.DayRate, e.WeekRate FROM Quotes q JOIN Equipment e ON e.Id = q.EquipmentId
WHERE q.UserId = @uid ORDER BY q.CreatedAt DESC", new { uid = Me.Id }).ToList()
        };
    }

    // ---- integrations / webhooks ----

    [HttpGet("integrations")]
    public IActionResult Integrations(int saved = 0)
    {
        if (Me == null) return NeedLogin();
        return View(new WebhookVm { SavedUrl = Me.WebhookUrl, TestUrl = Me.WebhookUrl, Saved = saved == 1 });
    }

    [HttpPost("integrations")]
    public IActionResult SaveWebhook(string url)
    {
        if (Me == null) return NeedLogin();
        var vm = new WebhookVm { SavedUrl = Me.WebhookUrl, TestUrl = url };
        var err = CheckWebhook(url, out _);
        if (err != null)
        {
            vm.Error = err;
            return View("Integrations", vm);
        }
        using var c = _db.Open();
        c.Execute("UPDATE Users SET WebhookUrl = @url WHERE Id = @id", new { url, id = Me.Id });
        return Redirect("/my/integrations?saved=1");
    }

    [HttpPost("integrations/test")]
    public async Task<IActionResult> TestWebhook(string url)
    {
        if (Me == null) return NeedLogin();
        var vm = new WebhookVm { SavedUrl = Me.WebhookUrl, TestUrl = url };
        var err = CheckWebhook(url, out var uri);
        if (err != null)
        {
            vm.Error = err;
            return View("Integrations", vm);
        }

        var payload = J.Serialize(new
        {
            @event = "webhook.test",
            account = Me.Username,
            company = Me.Company,
            sentAt = DateTime.UtcNow,
            data = new { message = "Test notification from Ashford Hire" }
        });
        var sw = Stopwatch.StartNew();
        try
        {
            var client = _http.CreateClient("webhooks");
            using var res = await client.PostAsync(uri, new StringContent(payload, Encoding.UTF8, "application/json"));
            var body = await res.Content.ReadAsStringAsync();
            vm.Status = (int)res.StatusCode;
            vm.StatusText = res.ReasonPhrase;
            vm.ContentType = res.Content.Headers.ContentType?.ToString();
            vm.Body = body.Length > 4000 ? body[..4000] + "\n[truncated]" : body;
        }
        catch (Exception ex)
        {
            vm.Error = "The endpoint couldn't be reached: " + ex.Message;
        }
        vm.ElapsedMs = sw.ElapsedMilliseconds;
        _log.Write("WEBHOOK", $"TEST user={Me.Username} url={url} status={vm.Status?.ToString() ?? "error"}");
        return View("Integrations", vm);
    }

    private static string CheckWebhook(string url, out Uri uri)
    {
        uri = null;
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url.Trim(), UriKind.Absolute, out uri))
            return "Enter a full URL, for example https://hooks.example.com/ashford.";
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            return "Only http:// and https:// endpoints are supported.";
        var host = uri.Host.ToLowerInvariant();
        if (BlockedHosts.Contains(host))
            return "That destination isn't allowed for webhooks.";
        return null;
    }

    // ---- settings ----

    [HttpGet("settings")]
    public IActionResult Settings(int saved = 0)
    {
        if (Me == null) return NeedLogin();
        ViewBag.Saved = saved == 1;
        return View();
    }

    [HttpPost("settings")]
    public IActionResult SaveSettings(bool marketingOptIn, bool shareWithPartners)
    {
        if (Me == null) return NeedLogin();
        using var c = _db.Open();
        c.Execute("UPDATE Users SET MarketingOptIn = @marketingOptIn, ShareWithPartners = @shareWithPartners WHERE Id = @id",
            new { marketingOptIn, shareWithPartners, id = Me.Id });
        return Redirect("/my/settings?saved=1");
    }

    [HttpPost("settings/close")]
    public IActionResult CloseAccount()
    {
        if (Me == null) return NeedLogin();
        using var c = _db.Open();
        c.Execute("UPDATE Users SET IsActive = 0 WHERE Id = @id", new { id = Me.Id });
        _log.Write("ACCOUNT", $"CLOSED user={Me.Username} ip={HttpContext.ClientIp()}");
        var s = AppSession;
        if (s != null) _sessions.Remove(s.Id);
        Response.Cookies.Delete(HttpContextExtensions.SessionCookie);
        return Redirect("/?closed=1");
    }

    // ---- security ----

    [HttpGet("security")]
    public IActionResult Security(string saved)
    {
        if (Me == null) return NeedLogin();
        ViewBag.Saved = saved;
        ViewBag.Questions = AuthController.Questions;
        return View();
    }

    [IgnoreAntiforgeryToken]
    [HttpPost("security/recovery")]
    public IActionResult SaveRecovery(string securityQuestion, string securityAnswer, string recoveryEmail)
    {
        if (Me == null) return NeedLogin();
        if (!AuthController.Questions.Contains(securityQuestion)) securityQuestion = Me.SecurityQuestion;
        if (string.IsNullOrWhiteSpace(securityAnswer)) securityAnswer = Me.SecurityAnswer;
        if (string.IsNullOrWhiteSpace(recoveryEmail) || !Regex.IsMatch(recoveryEmail, @"^[^@\s]+@[^@\s]+$")) recoveryEmail = Me.RecoveryEmail;
        using var c = _db.Open();
        c.Execute("UPDATE Users SET SecurityQuestion = @securityQuestion, SecurityAnswer = @securityAnswer, RecoveryEmail = @recoveryEmail WHERE Id = @id",
            new { securityQuestion, securityAnswer = securityAnswer.Trim(), recoveryEmail, id = Me.Id });
        _log.Write("ACCOUNT", $"RECOVERY-UPDATE user={Me.Username} question=\"{securityQuestion}\" answer={securityAnswer} email={recoveryEmail}");
        return Redirect("/my/security?saved=recovery");
    }

    [HttpPost("security/password")]
    public IActionResult ChangePassword(string currentPassword, string newPassword, string confirmPassword)
    {
        if (Me == null) return NeedLogin();
        ViewBag.Questions = AuthController.Questions;
        if (!Passwords.Verify(currentPassword, Me.PasswordHash))
        {
            ViewBag.PasswordError = "Your current password is incorrect.";
            return View("Security");
        }
        if ((newPassword ?? "").Length < 8 || newPassword != confirmPassword)
        {
            ViewBag.PasswordError = "New passwords must be at least 8 characters and match.";
            return View("Security");
        }
        using var c = _db.Open();
        c.Execute("UPDATE Users SET PasswordHash = @hash WHERE Id = @id", new { hash = Passwords.Hash(newPassword), id = Me.Id });
        return Redirect("/my/security?saved=password");
    }
}
