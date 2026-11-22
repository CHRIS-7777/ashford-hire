using System.Text.RegularExpressions;
using Ashford.Web.Infrastructure;
using Ashford.Web.Models;
using Dapper;
using Microsoft.AspNetCore.Mvc;

namespace Ashford.Web.Controllers;

public class AuthController : AppController
{
    public static readonly string[] Questions =
    {
        "What was the name of your first pet?",
        "What town were you born in?",
        "What was the make of your first van?",
        "Which football team do you support?",
        "What was your mother's maiden name?"
    };

    private readonly Db _db;
    private readonly ActivityLog _log;
    private readonly SessionStore _sessions;
    private readonly TokenService _tokens;

    public AuthController(Db db, ActivityLog log, SessionStore sessions, TokenService tokens)
    {
        _db = db;
        _log = log;
        _sessions = sessions;
        _tokens = tokens;
    }

    [HttpGet("/signin")]
    public IActionResult SignInPage(string returnUrl)
    {
        if (Me != null) return Redirect(Landing(Me));
        ViewBag.ReturnUrl = returnUrl;
        return View("SignIn");
    }

    [HttpPost("/signin")]
    public IActionResult SignInPost(string username, string password, string returnUrl)
    {
        ViewBag.ReturnUrl = returnUrl;
        ViewBag.Username = username;
        username = (username ?? "").Trim();
        var ip = HttpContext.ClientIp();

        using var c = _db.Open();
        var u = UserQueries.ByUsername(c, username);

        if (u != null && u.LockedUntil.HasValue && u.LockedUntil.Value > DateTime.UtcNow)
        {
            _log.Write("AUTH", $"LOCKED user={username} ip={ip}");
            ViewBag.Error = "This account is temporarily locked after repeated failed sign-in attempts. Try again in 10 minutes or reset your password.";
            return View("SignIn");
        }

        if (u == null || !u.IsActive || !Passwords.Verify(password, u.PasswordHash))
        {
            if (u != null)
            {
                var failed = u.FailedLogins + 1;
                if (failed >= 5)
                    c.Execute("UPDATE Users SET FailedLogins = 0, LockedUntil = DATEADD(minute, 10, SYSUTCDATETIME()) WHERE Id = @id", new { id = u.Id });
                else
                    c.Execute("UPDATE Users SET FailedLogins = @failed WHERE Id = @id", new { failed, id = u.Id });
            }
            _log.Write("AUTH", $"FAIL user={username} password={password} ip={ip}");
            ViewBag.Error = "The username or password is incorrect.";
            return View("SignIn");
        }

        c.Execute("UPDATE Users SET FailedLogins = 0, LockedUntil = NULL WHERE Id = @id", new { id = u.Id });
        SignIn(u);
        _log.Write("AUTH", $"OK user={u.Username} role={u.Role} ip={ip}");

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)) return Redirect(returnUrl);
        return Redirect(Landing(u));
    }

    [HttpGet("/signout")]
    public IActionResult SignOutPage()
    {
        var s = AppSession;
        if (s != null)
        {
            _sessions.Remove(s.Id);
            Response.Cookies.Delete(HttpContextExtensions.SessionCookie);
        }
        PricingCookie.Clear(Response);
        return Redirect("/");
    }

    [HttpGet("/register")]
    public IActionResult Register()
    {
        ViewBag.Questions = Questions;
        return View();
    }

    [HttpPost("/register")]
    public IActionResult RegisterPost(string fullName, string username, string email, string phone, string company,
        string password, string confirm, string securityQuestion, string securityAnswer)
    {
        ViewBag.Questions = Questions;
        var errors = new List<string>();
        username = (username ?? "").Trim().ToLowerInvariant();
        email = (email ?? "").Trim();
        if (string.IsNullOrWhiteSpace(fullName)) errors.Add("Enter your full name.");
        if (!Regex.IsMatch(username, @"^[a-z0-9._-]{3,30}$")) errors.Add("Usernames are 3-30 characters: letters, numbers, dots, dashes or underscores.");
        if (!Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$")) errors.Add("Enter a valid email address.");
        if ((password ?? "").Length < 8) errors.Add("Passwords must be at least 8 characters.");
        if (password != confirm) errors.Add("The passwords don't match.");
        if (!Questions.Contains(securityQuestion)) errors.Add("Choose a security question.");
        if (string.IsNullOrWhiteSpace(securityAnswer)) errors.Add("Enter an answer to your security question.");

        using var c = _db.Open();
        if (errors.Count == 0 && c.ExecuteScalar<int>("SELECT COUNT(*) FROM Users WHERE Username = @username OR Email = @email", new { username, email }) > 0)
            errors.Add("An account with that username or email already exists.");

        if (errors.Count > 0)
        {
            ViewBag.Errors = errors;
            ViewBag.Form = new { fullName, username, email, phone, company };
            return View("Register");
        }

        c.Execute(@"INSERT INTO Users (Username, Email, FullName, Phone, Company, PasswordHash, Role, AccountType, SecurityQuestion, SecurityAnswer, RecoveryEmail, IsActive, CreatedAt, FailedLogins, MarketingOptIn, ShareWithPartners)
VALUES (@username, @email, @fullName, @phone, @company, @hash, 'customer', 'retail', @securityQuestion, @securityAnswer, @email, 1, SYSUTCDATETIME(), 0, 0, 0)",
            new { username, email, fullName = fullName.Trim(), phone, company, hash = Passwords.Hash(password), securityQuestion, securityAnswer = securityAnswer.Trim() });
        _log.Write("ACCOUNT", $"REGISTER user={username} email={email} ip={HttpContext.ClientIp()}");
        return Redirect("/signin?registered=1");
    }

    [HttpGet("/account/recover")]
    public IActionResult Recover() => View(new RecoverVm());

    [HttpPost("/account/recover")]
    public IActionResult RecoverPost(int step, string username, string answer, string newPassword, string confirmPassword)
    {
        var vm = new RecoverVm { Username = username };
        var s = AppSession;
        using var c = _db.Open();

        if (step <= 1)
        {
            var u = UserQueries.ByUsername(c, (username ?? "").Trim());
            if (u == null || !u.IsActive)
            {
                vm.Error = "We couldn't find an active account with that username.";
                return View("Recover", vm);
            }
            vm.Step = 2;
            vm.Username = u.Username;
            vm.Question = u.SecurityQuestion;
            return View("Recover", vm);
        }

        if (step == 2)
        {
            var u = UserQueries.ByUsername(c, (username ?? "").Trim());
            if (u == null)
            {
                vm.Error = "We couldn't find an active account with that username.";
                return View("Recover", vm);
            }
            _log.Write("RECOVERY", $"VERIFY user={u.Username} answer={answer} ip={HttpContext.ClientIp()}");
            if (!string.Equals((answer ?? "").Trim(), (u.SecurityAnswer ?? "").Trim(), StringComparison.OrdinalIgnoreCase))
            {
                vm.Step = 2;
                vm.Question = u.SecurityQuestion;
                vm.Error = "That answer doesn't match our records.";
                return View("Recover", vm);
            }
            if (s != null) s.RecoveryVerified = true;
            vm.Step = 3;
            return View("Recover", vm);
        }

        if (s == null || !s.RecoveryVerified)
        {
            vm.Error = "Your recovery session has expired. Please start again.";
            return View("Recover", vm);
        }
        if ((newPassword ?? "").Length < 8 || newPassword != confirmPassword)
        {
            vm.Step = 3;
            vm.Error = "Passwords must be at least 8 characters and match.";
            return View("Recover", vm);
        }

        var target = UserQueries.ByUsername(c, (username ?? "").Trim());
        if (target == null)
        {
            vm.Step = 3;
            vm.Error = "Something went wrong updating your password.";
            return View("Recover", vm);
        }
        c.Execute("UPDATE Users SET PasswordHash = @hash, FailedLogins = 0, LockedUntil = NULL WHERE Id = @id", new { hash = Passwords.Hash(newPassword), id = target.Id });
        s.RecoveryVerified = false;
        _log.Write("RECOVERY", $"RESET user={target.Username} newpassword={newPassword} ip={HttpContext.ClientIp()}");
        vm.Done = true;
        vm.Step = 3;
        return View("Recover", vm);
    }

    [HttpGet("/auth/handoff")]
    public IActionResult Handoff(string token)
    {
        var claims = string.IsNullOrEmpty(token) ? null : _tokens.Validate(token);
        var sub = (string)claims?["sub"];
        if (string.IsNullOrEmpty(sub))
        {
            ViewBag.Error = "This sign-in link has expired. Open the Ashford Site app and tap 'Open in browser' again.";
            return View("SignIn");
        }
        using var c = _db.Open();
        var u = UserQueries.ByUsername(c, sub);
        if (u == null || !u.IsActive)
        {
            ViewBag.Error = "This sign-in link has expired. Open the Ashford Site app and tap 'Open in browser' again.";
            return View("SignIn");
        }
        SignIn(u);
        _log.Write("AUTH", $"HANDOFF user={u.Username} ip={HttpContext.ClientIp()}");
        return Redirect(Landing(u));
    }

    // ---- estimate basket (works before sign-in) ----

    [HttpGet("/estimate")]
    public IActionResult Estimate()
    {
        var s = AppSession;
        using var c = _db.Open();
        var lines = new List<(Equipment Item, int Days)>();
        foreach (var l in s?.Estimate ?? new List<EstimateLine>())
        {
            var eq = c.QueryFirstOrDefault<Equipment>("SELECT * FROM Equipment WHERE Slug = @slug", new { slug = l.Slug });
            if (eq != null) lines.Add((eq, l.Days));
        }
        ViewBag.Lines = lines;
        ViewBag.ShareLink = s == null ? null : $"{Request.Scheme}://{Request.Host}/estimate/resume?ref={s.Id}";
        return View();
    }

    [HttpPost("/estimate/add")]
    public IActionResult EstimateAdd(string slug, int days)
    {
        var s = AppSession;
        if (s != null && !string.IsNullOrEmpty(slug))
        {
            if (days < 1 || days > 60) days = 1;
            var existing = s.Estimate.FirstOrDefault(e => e.Slug == slug);
            if (existing != null) existing.Days = days;
            else if (s.Estimate.Count < 15) s.Estimate.Add(new EstimateLine { Slug = slug, Days = days });
        }
        return Redirect("/estimate");
    }

    [HttpPost("/estimate/clear")]
    public IActionResult EstimateClear()
    {
        AppSession?.Estimate.Clear();
        return Redirect("/estimate");
    }

    [HttpGet("/estimate/resume")]
    public IActionResult EstimateResume([FromQuery(Name = "ref")] string reference)
    {
        var shared = _sessions.Get(reference);
        if (shared == null) return Redirect("/estimate?expired=1");
        HttpContext.SetSessionCookie(shared.Id);
        return Redirect("/estimate");
    }

    private static string Landing(User u) => u.Role switch
    {
        "admin" => "/admin",
        "dispatcher" => "/yard",
        _ => "/my"
    };
}
