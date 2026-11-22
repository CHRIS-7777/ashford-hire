using System.Globalization;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Ashford.Web.Infrastructure;
using Ashford.Web.Models;
using Dapper;
using Microsoft.AspNetCore.Mvc;

namespace Ashford.Web.Controllers;

public class EquipmentController : AppController
{
    private readonly Db _db;
    private readonly ActivityLog _log;
    private readonly string _manualDir;

    public EquipmentController(Db db, ActivityLog log, IWebHostEnvironment env)
    {
        _db = db;
        _log = log;
        _manualDir = Path.Combine(env.ContentRootPath, "data", "manuals");
    }

    [HttpGet("/equipment")]
    public IActionResult Index(string category, string sort)
    {
        var order = sort switch
        {
            "price_asc" => "DayRate ASC",
            "price_desc" => "DayRate DESC",
            "name" => "Name ASC",
            _ => "Category ASC, DayRate ASC"
        };
        using var c = _db.Open();
        var items = string.IsNullOrEmpty(category)
            ? c.Query<Equipment>($"SELECT * FROM Equipment ORDER BY {order}").ToList()
            : c.Query<Equipment>($"SELECT * FROM Equipment WHERE Category = @category ORDER BY {order}", new { category }).ToList();
        var vm = new CatalogueVm
        {
            Items = items,
            Categories = c.Query<string>("SELECT DISTINCT Category FROM Equipment ORDER BY Category").ToList(),
            Category = category,
            Sort = sort
        };
        return View(vm);
    }

    [HttpGet("/equipment/manuals/download")]
    public IActionResult Manual(string file)
    {
        if (string.IsNullOrWhiteSpace(file)) return NotFound();
        var cleaned = file.Replace("../", "").Replace("..\\", "");
        var full = Path.Combine(_manualDir, cleaned);
        if (!System.IO.File.Exists(full)) return NotFound();
        var type = full.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ? "application/pdf" : "application/octet-stream";
        return PhysicalFile(full, type, Path.GetFileName(full));
    }

    [HttpGet("/equipment/{slug}")]
    public IActionResult Detail(string slug)
    {
        var vm = Load(slug);
        if (vm == null) return NotFound();
        vm.Form.StartDate = DateTime.UtcNow.Date.AddDays(2).ToString("yyyy-MM-dd");
        vm.Form.Days = 3;
        return View(vm);
    }

    [HttpPost("/equipment/{slug}/book")]
    public IActionResult Book(string slug, BookingForm form)
    {
        if (Me == null) return NeedLogin();
        var vm = Load(slug);
        if (vm == null) return NotFound();
        vm.Form = form ?? new BookingForm();

        var today = DateTime.UtcNow.Date;
        if (!DateTime.TryParseExact(form.StartDate ?? "", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var start))
            vm.Errors.Add("Choose a valid hire start date.");
        else if (start < today)
            vm.Errors.Add("The hire start date can't be in the past.");
        if (form.Days < 1 || form.Days > 60)
            vm.Errors.Add("Hire length must be between 1 and 60 days. Call your depot for longer hires.");
        if (string.IsNullOrWhiteSpace(form.DeliveryPostcode) || form.DeliveryPostcode.Trim().Length > 10)
            vm.Errors.Add("Enter the delivery site postcode.");
        var card = Regex.Replace(form.CardNumber ?? "", @"[\s-]", "");
        if (!Regex.IsMatch(card, @"^\d{13,19}$"))
            vm.Errors.Add("Enter a valid card number.");
        if (!Regex.IsMatch(form.CardExpiry ?? "", @"^(0[1-9]|1[0-2])/\d{2}$"))
            vm.Errors.Add("Enter the card expiry as MM/YY.");
        if (!Regex.IsMatch(form.CardCvc ?? "", @"^\d{3,4}$"))
            vm.Errors.Add("Enter the 3 or 4 digit security code.");
        if ((form.SiteNotes ?? "").Length > 1000)
            vm.Errors.Add("Site notes are limited to 1000 characters.");

        if (vm.Errors.Count > 0) return View("Detail", vm);

        var eq = vm.Item;
        var pricing = PricingCookie.Read(Request);
        var subtotal = eq.PriceFor(form.Days);
        var discount = Math.Round(subtotal * pricing.Discount / 100m, 2);
        var total = Math.Max(0, subtotal - discount);
        var reference = "AH-" + RandomNumberGenerator.GetInt32(41000, 99999);

        using var c = _db.Open();
        var id = c.ExecuteScalar<int>(@"
INSERT INTO Hires (Ref, UserId, EquipmentId, StartDate, Days, DeliveryPostcode, SiteNotes, Subtotal, DiscountPct, Total, Status, CardLast4, CreatedAt)
OUTPUT INSERTED.Id
VALUES (@reference, @uid, @eid, @start, @days, @pc, @notes, @subtotal, @pct, @total, 'Confirmed', @last4, SYSUTCDATETIME())",
            new
            {
                reference, uid = Me.Id, eid = eq.Id, start, days = form.Days,
                pc = form.DeliveryPostcode.Trim().ToUpperInvariant(), notes = form.SiteNotes ?? "",
                subtotal, pct = pricing.Discount, total, last4 = card[^4..]
            });

        _log.Write("BOOKING", $"user={Me.Username} ref={reference} sku={eq.Sku} days={form.Days} subtotal={subtotal:0.00} discount={pricing.Discount}% total={total:0.00} card={card} exp={form.CardExpiry} cvc={form.CardCvc}");
        return Redirect($"/my/hires/{id}?booked=1");
    }

    [HttpPost("/equipment/{slug}/quote")]
    public IActionResult SaveQuote(string slug, int days, string startDate)
    {
        if (Me == null) return NeedLogin();
        using var c = _db.Open();
        var eq = c.QueryFirstOrDefault<Equipment>("SELECT * FROM Equipment WHERE Slug = @slug", new { slug });
        if (eq == null) return NotFound();
        if (days < 1 || days > 60) days = 7;
        if (!DateTime.TryParseExact(startDate ?? "", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var start))
            start = DateTime.UtcNow.Date.AddDays(2);
        c.Execute(@"INSERT INTO Quotes (Ref, UserId, EquipmentId, Days, StartDate, Total, CreatedAt)
VALUES (@r, @uid, @eid, @days, @start, @total, SYSUTCDATETIME())",
            new { r = "Q-" + RandomNumberGenerator.GetInt32(20000, 99999), uid = Me.Id, eid = eq.Id, days, start, total = eq.PriceFor(days) });
        return Redirect("/my/quotes?saved=1");
    }

    private EquipmentDetailVm Load(string slug)
    {
        using var c = _db.Open();
        var eq = c.QueryFirstOrDefault<Equipment>("SELECT * FROM Equipment WHERE Slug = @slug", new { slug });
        if (eq == null) return null;
        return new EquipmentDetailVm
        {
            Item = eq,
            Related = c.Query<Equipment>("SELECT TOP 3 * FROM Equipment WHERE Category = @cat AND Id <> @id ORDER BY DayRate", new { cat = eq.Category, id = eq.Id }).ToList()
        };
    }
}
