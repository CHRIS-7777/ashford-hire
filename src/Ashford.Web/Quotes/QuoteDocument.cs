using System.Runtime.Serialization;
using System.Text;
using Newtonsoft.Json;

namespace Ashford.Web.Quotes;

public interface IQuoteAttachment
{
    string Title { get; }
    string Content { get; }
}

public class QuoteLine
{
    public string Sku { get; set; }
    public string Description { get; set; }
    public int Days { get; set; }
    public decimal DayRate { get; set; }
    public decimal LineTotal { get; set; }
}

public class QuoteDocument
{
    public string FormatVersion { get; set; } = "2.1";
    public string Reference { get; set; }
    public string PreparedFor { get; set; }
    public DateTime Created { get; set; }
    public DateTime HireStart { get; set; }
    public List<QuoteLine> Lines { get; set; } = new();
    public decimal Total { get; set; }
    public IQuoteAttachment Terms { get; set; }
}

public class TemplateDocument : IQuoteAttachment
{
    public string Title { get; set; }
    public string TemplatePath { get; set; }

    [JsonIgnore]
    public string Content { get; private set; }

    [OnDeserialized]
    internal void OnDeserialized(StreamingContext context)
    {
        Content = File.Exists(TemplatePath) ? File.ReadAllText(TemplatePath) : "(template not available)";
    }
}

public class InlineNote : IQuoteAttachment
{
    public string Title { get; set; }
    public string Text { get; set; }

    [JsonIgnore]
    public string Content => Text;
}

public static class QuoteFormat
{
    public static readonly JsonSerializerSettings Settings = new()
    {
        TypeNameHandling = TypeNameHandling.Objects,
        Formatting = Formatting.Indented,
        DateFormatString = "yyyy-MM-dd"
    };

    public static string Export(QuoteDocument doc) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(doc, Settings)));

    public static QuoteDocument Import(string payload)
    {
        var raw = (payload ?? "").Trim();
        var json = raw.StartsWith("{") ? raw : Encoding.UTF8.GetString(Convert.FromBase64String(raw));
        return JsonConvert.DeserializeObject<QuoteDocument>(json, Settings);
    }
}
