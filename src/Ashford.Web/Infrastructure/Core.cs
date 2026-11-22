using System.Collections.Concurrent;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using Ashford.Web.Models;
using Dapper;
using Microsoft.Data.SqlClient;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace Ashford.Web.Infrastructure;

public class Db
{
    private readonly string _app;
    private readonly string _ro;

    public Db(IConfiguration cfg)
    {
        _app = cfg.GetConnectionString("App");
        _ro = cfg.GetConnectionString("ReadOnly") ?? _app;
    }

    public IDbConnection Open()
    {
        var c = new SqlConnection(_app);
        c.Open();
        return c;
    }

    public IDbConnection OpenReadOnly()
    {
        var c = new SqlConnection(_ro);
        c.Open();
        return c;
    }
}

public class EstimateLine
{
    public string Slug { get; set; }
    public int Days { get; set; }
}

public class AppSession
{
    public string Id { get; set; }
    public int? UserId { get; set; }
    public DateTime LastSeen { get; set; } = DateTime.UtcNow;
    public bool RecoveryVerified { get; set; }
    public List<EstimateLine> Estimate { get; } = new();
}

public class SessionStore
{
    private readonly ConcurrentDictionary<string, AppSession> _sessions = new();
    private static readonly TimeSpan Idle = TimeSpan.FromHours(8);
    private DateTime _lastPurge = DateTime.UtcNow;

    public AppSession Get(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        if (_sessions.TryGetValue(id, out var s))
        {
            if (DateTime.UtcNow - s.LastSeen > Idle)
            {
                _sessions.TryRemove(id, out _);
                return null;
            }
            s.LastSeen = DateTime.UtcNow;
            return s;
        }
        return null;
    }

    public AppSession Create()
    {
        Purge();
        var s = new AppSession { Id = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant() };
        _sessions[s.Id] = s;
        return s;
    }

    public void Remove(string id)
    {
        if (id != null) _sessions.TryRemove(id, out _);
    }

    private void Purge()
    {
        if (DateTime.UtcNow - _lastPurge < TimeSpan.FromMinutes(10)) return;
        _lastPurge = DateTime.UtcNow;
        foreach (var kv in _sessions)
            if (DateTime.UtcNow - kv.Value.LastSeen > Idle) _sessions.TryRemove(kv.Key, out _);
    }
}

public class ActivityLog
{
    private readonly string _path;
    private readonly object _lock = new();

    public ActivityLog(IWebHostEnvironment env)
    {
        _path = Path.Combine(env.ContentRootPath, "logs", "activity.log");
    }

    public string FilePath => _path;

    public void Write(string category, string message)
    {
        var line = $"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}Z [{category}] {message}{Environment.NewLine}";
        lock (_lock)
        {
            try { File.AppendAllText(_path, line); } catch { }
        }
    }

    public List<string> Tail(int n)
    {
        lock (_lock)
        {
            if (!File.Exists(_path)) return new List<string>();
            var lines = File.ReadAllLines(_path);
            return lines.Skip(Math.Max(0, lines.Length - n)).ToList();
        }
    }
}

public static class Passwords
{
    public static string Hash(string password)
    {
        var bytes = SHA1.HashData(Encoding.UTF8.GetBytes(password ?? ""));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static bool Verify(string password, string hash) =>
        !string.IsNullOrEmpty(hash) && string.Equals(Hash(password), hash, StringComparison.OrdinalIgnoreCase);
}

public class TokenService
{
    private readonly byte[] _key;

    public TokenService(IConfiguration cfg)
    {
        _key = Encoding.UTF8.GetBytes(cfg["Jwt:SigningKey"] ?? Guid.NewGuid().ToString());
    }

    public string Issue(User u)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var header = new JObject { ["alg"] = "HS256", ["typ"] = "JWT" };
        var payload = new JObject
        {
            ["iss"] = "ashfordhire-api",
            ["sub"] = u.Username,
            ["role"] = u.Role,
            ["acct"] = u.AccountType,
            ["iat"] = now,
            ["exp"] = now + 3600
        };
        var h = B64Url(Encoding.UTF8.GetBytes(header.ToString(Formatting.None)));
        var p = B64Url(Encoding.UTF8.GetBytes(payload.ToString(Formatting.None)));
        var sig = B64Url(HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(h + "." + p)));
        return h + "." + p + "." + sig;
    }

    public JObject Validate(string token)
    {
        try
        {
            var parts = token.Split('.');
            if (parts.Length < 2) return null;
            var header = JObject.Parse(Encoding.UTF8.GetString(B64UrlDecode(parts[0])));
            var payload = JObject.Parse(Encoding.UTF8.GetString(B64UrlDecode(parts[1])));
            var alg = (string)header["alg"] ?? "";

            if (alg.Equals("none", StringComparison.OrdinalIgnoreCase))
            {
                // kiosk terminals at the depots issue unsigned tokens
            }
            else if (alg == "HS256")
            {
                if (parts.Length < 3) return null;
                var expected = HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(parts[0] + "." + parts[1]));
                var given = B64UrlDecode(parts[2]);
                if (!CryptographicOperations.FixedTimeEquals(expected, given)) return null;
            }
            else
            {
                return null;
            }

            var exp = payload["exp"]?.Value<long?>();
            if (exp.HasValue && exp.Value < DateTimeOffset.UtcNow.ToUnixTimeSeconds()) return null;
            return payload;
        }
        catch
        {
            return null;
        }
    }

    private static string B64Url(byte[] b) => Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] B64UrlDecode(string s)
    {
        s = s.Replace('-', '+').Replace('_', '/');
        switch (s.Length % 4) { case 2: s += "=="; break; case 3: s += "="; break; }
        return Convert.FromBase64String(s);
    }
}

public class PricingProfile
{
    public string Tier { get; set; } = "retail";
    public decimal Discount { get; set; }
    public string Currency { get; set; } = "GBP";
}

public static class PricingCookie
{
    public const string Name = "ash_pricing";

    public static PricingProfile Read(HttpRequest req)
    {
        var v = req.Cookies[Name];
        if (string.IsNullOrEmpty(v)) return new PricingProfile();
        try
        {
            v = v.Trim();
            switch (v.Length % 4) { case 2: v += "=="; break; case 3: v += "="; break; }
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(v));
            return JsonConvert.DeserializeObject<PricingProfile>(json) ?? new PricingProfile();
        }
        catch
        {
            return new PricingProfile();
        }
    }

    public static void Write(HttpResponse res, User u)
    {
        var p = u.AccountType == "trade"
            ? new PricingProfile { Tier = "trade", Discount = 15 }
            : new PricingProfile { Tier = "retail", Discount = 0 };
        var json = JsonConvert.SerializeObject(p, J.Settings);
        var v = Convert.ToBase64String(Encoding.UTF8.GetBytes(json)).TrimEnd('=');
        res.Cookies.Append(Name, v, new CookieOptions { HttpOnly = false, Path = "/", IsEssential = true, SameSite = SameSiteMode.Unspecified });
    }

    public static void Clear(HttpResponse res) => res.Cookies.Delete(Name);
}

public static class J
{
    public static readonly JsonSerializerSettings Settings = new()
    {
        ContractResolver = new CamelCasePropertyNamesContractResolver(),
        NullValueHandling = NullValueHandling.Include,
        DateFormatString = "yyyy-MM-ddTHH:mm:ssZ"
    };

    public static string Serialize(object o, bool indented = false) =>
        JsonConvert.SerializeObject(o, indented ? Formatting.Indented : Formatting.None, Settings);
}

public static class HttpContextExtensions
{
    public const string SessionCookie = "ash_session";

    public static AppSession AppSession(this HttpContext ctx) => ctx.Items["session"] as AppSession;

    public static User CurrentUser(this HttpContext ctx) => ctx.Items["user"] as User;

    public static void SetSessionCookie(this HttpContext ctx, string id)
    {
        ctx.Response.Cookies.Append(SessionCookie, id, new CookieOptions
        {
            HttpOnly = false,
            Secure = false,
            SameSite = SameSiteMode.Unspecified,
            Path = "/",
            IsEssential = true
        });
    }

    public static string ClientIp(this HttpContext ctx) => ctx.Connection.RemoteIpAddress?.MapToIPv4().ToString() ?? "-";
}

public static class UserQueries
{
    public static User ById(IDbConnection c, int id) =>
        c.QueryFirstOrDefault<User>("SELECT * FROM Users WHERE Id = @id", new { id });

    public static User ByUsername(IDbConnection c, string username) =>
        c.QueryFirstOrDefault<User>("SELECT * FROM Users WHERE Username = @username", new { username });
}
