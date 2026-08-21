using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;

namespace Dental.Integrations.Enabiz.Schemas;

/// <summary>
/// Üretilen paket XML'ini Bakanlığın <b>resmi alan tanımına</b> göre doğrular.
///
/// <para><b>Neden XSD değil:</b> USS SOAP sözleşmesinde paket gövdesi <c>xs:string</c>'dir
/// (WSDL'den doğrulandı: <c>&lt;xs:element name="input" nillable="true" type="xs:string"/&gt;</c>),
/// yani paketler WSDL'e göre opak metindir ve Bakanlık paket başına XSD yayımlamaz.
/// Makine tarafından okunabilir tek resmi kaynak, rehber.enabiz.gov.tr'nin paket detayındaki
/// alan tablosudur (ad / nesne tipi / zorunluluk / tekrar / tip / SKRS kod sistemi GUID'i).
/// Bu tablolar <c>Schemas/paket_*_fields.json</c> olarak gömülü kaynağa alınmış ve doğrulama
/// bunlardan yürütülmüştür — yani doğrulama uydurma bir şemaya değil, resmi tanıma dayanır.</para>
///
/// <para>Denetlenen kurallar:
/// <list type="bullet">
///   <item>Zorunlu (<c>zorunlu=Evet</c>) veri seti/grup/öğelerin varlığı.</item>
///   <item>Tekrarlanamaz (<c>tekrar=Hayır</c>) öğenin birden çok kez yazılmaması.</item>
///   <item>SKRS'ye bağlı öğelerin <c>codeSystemGuid</c> + <c>code</c> nitelikleriyle yazılması
///         ve GUID'in resmi tanımdakiyle aynı olması.</item>
///   <item>Tanımda olmayan öğe adı yazılmaması (yazım hatası yakalama).</item>
/// </list></para>
/// </summary>
public static class PacketSchemaValidator
{
    private static readonly ConcurrentDictionary<short, PacketSchema?> Cache = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>İlgili paket için resmi alan tanımı gömülü mü.</summary>
    public static bool HasSchema(short packetType) => Load(packetType) is not null;

    /// <summary>
    /// Doğrular; hata varsa <see cref="EnabizPacketValidationException"/> fırlatır.
    /// Tanım gömülü değilse sessizce geçer (bilinmeyen pakete gereksiz engel koymaz).
    /// </summary>
    public static void Validate(short packetType, XElement sysMessage)
    {
        ArgumentNullException.ThrowIfNull(sysMessage);

        var schema = Load(packetType);
        if (schema is null) return;

        var recordData = sysMessage.Elements().FirstOrDefault(e => e.Name.LocalName == "recordData")
            ?? throw new EnabizPacketValidationException(packetType, ["recordData öğesi yok."]);

        var errors = new List<string>();
        ValidateLevel(schema.Root, recordData, errors, path: "");

        if (errors.Count > 0)
            throw new EnabizPacketValidationException(packetType, errors);
    }

    private static void ValidateLevel(
        IReadOnlyList<SchemaNode> expected, XElement actual, List<string> errors, string path)
    {
        var byName = expected.ToDictionary(n => n.Name, StringComparer.Ordinal);

        foreach (var child in actual.Elements())
        {
            var name = child.Name.LocalName;
            if (!byName.TryGetValue(name, out var node))
            {
                errors.Add($"{path}{name}: resmi alan tanımında böyle bir öğe yok.");
                continue;
            }

            if (!node.Repeatable && actual.Elements().Count(e => e.Name.LocalName == name) > 1)
                errors.Add($"{path}{name}: tekrarlanamaz ama birden çok kez yazılmış.");

            if (node.CodeSystemGuid is { } guid)
            {
                var actualGuid = child.Attribute("codeSystemGuid")?.Value;
                if (string.IsNullOrWhiteSpace(actualGuid))
                    errors.Add($"{path}{name}: SKRS'ye bağlı öğe codeSystemGuid niteliği olmadan yazılmış.");
                else if (!string.Equals(actualGuid, guid, StringComparison.OrdinalIgnoreCase))
                    errors.Add($"{path}{name}: codeSystemGuid beklenen {guid} yerine {actualGuid}.");

                if (string.IsNullOrWhiteSpace(child.Attribute("code")?.Value))
                    errors.Add($"{path}{name}: SKRS'ye bağlı öğede code niteliği boş.");
            }
            else if (node.Children.Count == 0 && child.Attribute("value") is null)
            {
                errors.Add($"{path}{name}: value niteliği yok.");
            }

            if (node.Children.Count > 0)
                ValidateLevel(node.Children, child, errors, $"{path}{name}/");
        }

        foreach (var node in expected.Where(n => n.Required))
        {
            if (!actual.Elements().Any(e => e.Name.LocalName == node.Name))
                errors.Add($"{path}{node.Name}: zorunlu alan eksik.");
        }
    }

    private static PacketSchema? Load(short packetType) => Cache.GetOrAdd(packetType, static type =>
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resource = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith($"paket_{type}_fields.json", StringComparison.Ordinal));
        if (resource is null) return null;

        using var stream = assembly.GetManifestResourceStream(resource);
        if (stream is null) return null;

        var document = JsonSerializer.Deserialize<FieldDocument>(stream, JsonOptions);
        if (document?.Alanlar is not { Count: > 0 }) return null;

        return new PacketSchema(BuildTree(document.Alanlar));
    });

    /// <summary>Düz (id="1.0.3" biçiminde yollu) alan listesini ağaca çevirir.</summary>
    private static List<SchemaNode> BuildTree(List<FieldRow> rows)
    {
        var nodes = new Dictionary<string, SchemaNode>(StringComparer.Ordinal);
        var root = new List<SchemaNode>();

        foreach (var row in rows.Where(r => !string.IsNullOrWhiteSpace(r.Ad) && !string.IsNullOrWhiteSpace(r.Id)))
        {
            var node = new SchemaNode(
                row.Ad!,
                Required: string.Equals(row.Zorunlu, "Evet", StringComparison.OrdinalIgnoreCase),
                Repeatable: string.Equals(row.Tekrar, "Evet", StringComparison.OrdinalIgnoreCase),
                CodeSystemGuid: string.IsNullOrWhiteSpace(row.CodeSystemGuid) ? null : row.CodeSystemGuid);

            nodes[row.Id!] = node;

            var separator = row.Id!.LastIndexOf('.');
            if (separator < 0)
            {
                root.Add(node);
            }
            else if (nodes.TryGetValue(row.Id[..separator], out var parent))
            {
                parent.Children.Add(node);
            }
            else
            {
                // Üst satırı bulunamayan alanı kökte tut — tanım eksikse doğrulama yine de çalışsın.
                root.Add(node);
            }
        }

        return root;
    }

    private sealed record PacketSchema(IReadOnlyList<SchemaNode> Root);

    private sealed record SchemaNode(string Name, bool Required, bool Repeatable, string? CodeSystemGuid)
    {
        public List<SchemaNode> Children { get; } = [];
    }

    private sealed class FieldDocument
    {
        [JsonPropertyName("paket")] public string? Paket { get; set; }
        [JsonPropertyName("alanlar")] public List<FieldRow>? Alanlar { get; set; }
    }

    private sealed class FieldRow
    {
        [JsonPropertyName("id")] public string? Id { get; set; }
        [JsonPropertyName("ad")] public string? Ad { get; set; }
        [JsonPropertyName("nesneTipi")] public string? NesneTipi { get; set; }
        [JsonPropertyName("zorunlu")] public string? Zorunlu { get; set; }
        [JsonPropertyName("tekrar")] public string? Tekrar { get; set; }
        [JsonPropertyName("tip")] public string? Tip { get; set; }
        [JsonPropertyName("codeSystemGuid")] public string? CodeSystemGuid { get; set; }
    }
}

/// <summary>Paket, resmi alan tanımına uymuyor — gönderilmeden önce durdurulur.</summary>
public sealed class EnabizPacketValidationException(short packetType, IReadOnlyList<string> errors)
    : Exception($"{packetType} paketi resmi alan tanımına uymuyor: {string.Join(" | ", errors)}")
{
    public short PacketType { get; } = packetType;
    public IReadOnlyList<string> Errors { get; } = errors;
}
