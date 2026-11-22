using Newtonsoft.Json;

namespace Ashford.Web.Models;

public class User
{
    public int Id { get; set; }
    public string Username { get; set; }
    public string Email { get; set; }
    public string FullName { get; set; }
    public string Phone { get; set; }
    public string Company { get; set; }
    [JsonIgnore] public string PasswordHash { get; set; }
    public string Role { get; set; }
    public string AccountType { get; set; }
    [JsonIgnore] public string SecurityQuestion { get; set; }
    [JsonIgnore] public string SecurityAnswer { get; set; }
    public string RecoveryEmail { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    [JsonIgnore] public int FailedLogins { get; set; }
    [JsonIgnore] public DateTime? LockedUntil { get; set; }
    public string WebhookUrl { get; set; }
    public bool MarketingOptIn { get; set; }
    public bool ShareWithPartners { get; set; }

    [JsonIgnore] public bool IsStaff => Role == "dispatcher" || Role == "admin";
    [JsonIgnore] public bool IsAdmin => Role == "admin";
    [JsonIgnore] public string FirstName => string.IsNullOrEmpty(FullName) ? Username : FullName.Split(' ')[0];
}

public class Equipment
{
    public int Id { get; set; }
    public string Sku { get; set; }
    public string Slug { get; set; }
    public string Name { get; set; }
    public string Category { get; set; }
    public decimal DayRate { get; set; }
    public decimal WeekRate { get; set; }
    public decimal Deposit { get; set; }
    public string Summary { get; set; }
    public string Description { get; set; }
    public string Specs { get; set; }
    public string ManualFile { get; set; }
    public bool IsFeatured { get; set; }
    public int FleetSize { get; set; }

    public IEnumerable<KeyValuePair<string, string>> SpecList =>
        (Specs ?? "").Split('|', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Split(':', 2))
            .Select(p => new KeyValuePair<string, string>(p[0].Trim(), p.Length > 1 ? p[1].Trim() : ""));

    public decimal PriceFor(int days)
    {
        if (days < 1) days = 1;
        var weeks = days / 7;
        var rem = days % 7;
        return weeks * WeekRate + Math.Min(rem * DayRate, WeekRate);
    }
}

public class Depot
{
    public int Id { get; set; }
    public string Code { get; set; }
    public string Name { get; set; }
    public string Region { get; set; }
    public string Town { get; set; }
    public string Postcode { get; set; }
    public string Phone { get; set; }
    public bool IsActive { get; set; }
}

public class Hire
{
    public int Id { get; set; }
    public string Ref { get; set; }
    public int UserId { get; set; }
    public int EquipmentId { get; set; }
    public DateTime StartDate { get; set; }
    public int Days { get; set; }
    public string DeliveryPostcode { get; set; }
    public string SiteNotes { get; set; }
    public decimal Subtotal { get; set; }
    public decimal DiscountPct { get; set; }
    public decimal Total { get; set; }
    public string Status { get; set; }
    public string CardLast4 { get; set; }
    public DateTime CreatedAt { get; set; }

    // joined
    public string EquipmentName { get; set; }
    public string Sku { get; set; }
    public string Category { get; set; }
    public string Username { get; set; }
    public string FullName { get; set; }
    public string Email { get; set; }
    public string Phone { get; set; }
    public string Company { get; set; }

    public DateTime EndDate => StartDate.AddDays(Days - 1);
}

public class Quote
{
    public int Id { get; set; }
    public string Ref { get; set; }
    public int UserId { get; set; }
    public int EquipmentId { get; set; }
    public int Days { get; set; }
    public DateTime StartDate { get; set; }
    public decimal Total { get; set; }
    public DateTime CreatedAt { get; set; }
    public string EquipmentName { get; set; }
    public string Sku { get; set; }
    public decimal DayRate { get; set; }
    public decimal WeekRate { get; set; }
}

public class CategorySummary
{
    public string Category { get; set; }
    public int Items { get; set; }
    public decimal FromRate { get; set; }
}

public class BookingForm
{
    public string StartDate { get; set; }
    public int Days { get; set; } = 1;
    public string DeliveryPostcode { get; set; }
    public string SiteNotes { get; set; }
    public string CardNumber { get; set; }
    public string CardExpiry { get; set; }
    public string CardCvc { get; set; }
}

public class EquipmentDetailVm
{
    public Equipment Item { get; set; }
    public BookingForm Form { get; set; } = new BookingForm();
    public List<string> Errors { get; set; } = new();
    public List<Equipment> Related { get; set; } = new();
}

public class HomeVm
{
    public List<Equipment> Featured { get; set; }
    public List<CategorySummary> Categories { get; set; }
}

public class CatalogueVm
{
    public List<Equipment> Items { get; set; }
    public List<string> Categories { get; set; }
    public string Category { get; set; }
    public string Sort { get; set; }
}

public class AvailabilityVm
{
    public string Query { get; set; }
    public bool Checked { get; set; }
    public bool Covered { get; set; }
    public string Depot { get; set; }
    public string Display { get; set; }
}

public class RecoverVm
{
    public int Step { get; set; } = 1;
    public string Username { get; set; }
    public string Question { get; set; }
    public string Error { get; set; }
    public bool Done { get; set; }
}

public class QuotesVm
{
    public List<Quote> Quotes { get; set; } = new();
    public Ashford.Web.Quotes.QuoteDocument Imported { get; set; }
    public string ImportError { get; set; }
    public string Payload { get; set; }
}

public class WebhookVm
{
    public string SavedUrl { get; set; }
    public string TestUrl { get; set; }
    public string Error { get; set; }
    public bool Saved { get; set; }
    public int? Status { get; set; }
    public string StatusText { get; set; }
    public string ContentType { get; set; }
    public string Body { get; set; }
    public long ElapsedMs { get; set; }
}

public class ServiceVm
{
    public List<Equipment> Assets { get; set; } = new();
    public List<string> Bundles { get; set; } = new();
    public string Output { get; set; }
    public string Error { get; set; }
    public string Sku { get; set; }
    public string BundleName { get; set; }
}

public class AdminVm
{
    public decimal Revenue30 { get; set; }
    public int ActiveHires { get; set; }
    public int Customers { get; set; }
    public int Bookings7 { get; set; }
    public List<object> RevenueSeries { get; set; } = new();
    public List<object> CategorySeries { get; set; } = new();
    public List<Hire> Recent { get; set; } = new();
    public string OpsCode { get; set; }
}
