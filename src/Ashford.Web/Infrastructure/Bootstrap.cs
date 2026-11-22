using System.Text;
using Ashford.Web.Models;
using Dapper;

namespace Ashford.Web.Infrastructure;

public static class Bootstrap
{
    public static void Run(IServiceProvider services, string root)
    {
        var db = services.GetRequiredService<Db>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("Bootstrap");

        foreach (var d in new[] { "logs", "data/manuals", "data/exports", "data/service" })
            Directory.CreateDirectory(Path.Combine(root, d));

        List<Equipment> fleet = null;
        for (var attempt = 1; attempt <= 60 && fleet == null; attempt++)
        {
            try
            {
                using var c = db.Open();
                fleet = c.Query<Equipment>("SELECT * FROM Equipment ORDER BY Id").ToList();
            }
            catch (Exception ex)
            {
                logger.LogWarning("Database not ready ({Attempt}/60): {Message}", attempt, ex.Message);
                Thread.Sleep(2000);
            }
        }
        fleet ??= new List<Equipment>();

        foreach (var e in fleet)
        {
            var manual = Path.Combine(root, "data/manuals", e.ManualFile ?? (e.Sku + ".pdf"));
            if (!File.Exists(manual))
            {
                var lines = new List<string>
                {
                    $"Model reference: {e.Sku}",
                    $"Category: {e.Category}",
                    "",
                    "Specification"
                };
                lines.AddRange(e.SpecList.Select(s => $"  - {s.Key}: {s.Value}"));
                lines.AddRange(new[]
                {
                    "",
                    "Before you start",
                    "  - Walk round the machine and check for damage, leaks and loose guards.",
                    "  - Check fuel, oil and hydraulic levels. Report defects to your depot.",
                    "  - Only trained and authorised operators may use this equipment.",
                    "  - Wear the PPE listed on the hire agreement at all times.",
                    "",
                    "Breakdowns and support: 0113 496 0770 (24 hours)",
                    "Ashford Tool & Plant Hire Ltd - operator quick reference card"
                });
                File.WriteAllBytes(manual, MiniPdf.Build(e.Name + " - Operator Guide", lines));
            }

            var svc = Path.Combine(root, "data/service", e.Sku + ".log");
            if (!File.Exists(svc))
            {
                var sb = new StringBuilder();
                var rnd = new Random(e.Id * 7919);
                var date = DateTime.UtcNow.Date.AddDays(-200);
                for (var i = 0; i < 6; i++)
                {
                    date = date.AddDays(20 + rnd.Next(25));
                    var type = i % 3 == 2 ? "LOLER thorough examination" : (i % 2 == 0 ? "Service A (250h)" : "Service B (500h)");
                    sb.AppendLine($"{date:yyyy-MM-dd}  {e.Sku}  {type,-28}  tech={new[] { "RB", "JK", "AM", "DS" }[rnd.Next(4)]}  result=PASS  hours={300 + i * 140 + rnd.Next(60)}");
                }
                File.WriteAllText(svc, sb.ToString());
            }
        }

        var log = Path.Combine(root, "logs", "activity.log");
        var seed = Path.Combine(root, "data", "activity.seed");
        if (!File.Exists(log) && File.Exists(seed))
            File.Copy(seed, log);

        logger.LogInformation("Bootstrap complete: {Count} fleet items", fleet.Count);
    }
}

public static class MiniPdf
{
    public static byte[] Build(string title, IEnumerable<string> lines)
    {
        var content = new StringBuilder();
        content.Append("BT /F2 18 Tf 56 780 Td (").Append(Esc(title)).Append(") Tj ET\n");
        content.Append("0.96 0.65 0.14 RG 3 w 56 768 m 540 768 l S\n");
        var y = 744;
        foreach (var l in lines)
        {
            content.Append($"BT /F1 11 Tf 56 {y} Td (").Append(Esc(l)).Append(") Tj ET\n");
            y -= 16;
            if (y < 60) break;
        }

        var objects = new List<string>
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 4 0 R /F2 5 0 R >> >> /Contents 6 0 R >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold >>",
            $"<< /Length {Encoding.ASCII.GetByteCount(content.ToString())} >>\nstream\n{content}endstream"
        };

        var sb = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int>();
        for (var i = 0; i < objects.Count; i++)
        {
            offsets.Add(Encoding.ASCII.GetByteCount(sb.ToString()));
            sb.Append($"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }
        var xref = Encoding.ASCII.GetByteCount(sb.ToString());
        sb.Append($"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
        foreach (var o in offsets) sb.Append($"{o:D10} 00000 n \n");
        sb.Append($"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R /Info << /Producer (Ashford DocGen 1.3) >> >>\nstartxref\n{xref}\n%%EOF\n");
        return Encoding.ASCII.GetBytes(sb.ToString());
    }

    private static string Esc(string s)
    {
        var ascii = new string((s ?? "").Select(ch => ch < 128 ? ch : '-').ToArray());
        return ascii.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
    }
}

public static class Icons
{
    public static string For(string category, int size = 72)
    {
        var body = category switch
        {
            "Earthmoving" => "<rect x='8' y='40' width='34' height='14' rx='3'/><rect x='14' y='28' width='18' height='14' rx='2'/><circle cx='15' cy='58' r='5'/><circle cx='35' cy='58' r='5'/><path d='M40 34 L56 18 L62 22 L48 38' /><path d='M56 18 L64 30 L58 34 Z'/>",
            "Access Platforms" => "<rect x='10' y='54' width='44' height='8' rx='2'/><path d='M16 54 L48 22 M48 54 L16 22' stroke-width='4'/><rect x='12' y='14' width='40' height='8' rx='2'/><circle cx='16' cy='64' r='3'/><circle cx='48' cy='64' r='3'/>",
            "Power & Lighting" => "<rect x='10' y='30' width='44' height='26' rx='4'/><path d='M34 34 L26 44 H33 L29 53 L40 41 H33 Z' fill='#fff'/><rect x='18' y='22' width='10' height='8'/>",
            "Compaction" => "<rect x='12' y='44' width='40' height='14' rx='2'/><rect x='24' y='30' width='16' height='14' rx='2'/><path d='M32 30 L46 10' stroke-width='4'/><rect x='42' y='6' width='12' height='6' rx='2'/>",
            "Concrete & Cutting" => "<circle cx='26' cy='38' r='18'/><circle cx='26' cy='38' r='5' fill='#fff'/><rect x='38' y='32' width='22' height='10' rx='3'/>",
            "Lifting & Handling" => "<rect x='8' y='38' width='30' height='16' rx='3'/><path d='M30 38 L56 14' stroke-width='5'/><path d='M56 14 L56 30 L62 30' stroke-width='4'/><circle cx='15' cy='58' r='5'/><circle cx='31' cy='58' r='5'/>",
            _ => "<rect x='12' y='20' width='40' height='32' rx='4'/>"
        };
        return $"<svg class='equip-icon' viewBox='0 0 72 72' width='{size}' height='{size}' fill='currentColor' stroke='currentColor' stroke-linecap='round' aria-hidden='true'>{body}</svg>";
    }
}
