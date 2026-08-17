namespace ItCockpit.Application.Parsing;

public sealed class TicketMailParserOptions
{
    public const string SectionName = "TicketParser";

    /// <summary>Kabul edilen <b>orijinal</b> gönderenler. Forward edende değil, iç zarfta aranır.</summary>
    public string[] AllowedSenders { get; set; } = ["ticket@menarini.com"];

    /// <summary>Gövdede bulunması zorunlu imza.</summary>
    public string RequiredSignature { get; set; } = "Service Desk Menarini";

    /// <summary>Offset içermeyen forward tarihlerinin ait olduğu varsayılan saat dilimi.</summary>
    public string DefaultTimeZone { get; set; } = "Europe/Istanbul";

    /// <summary>Tixbox derin bağlantısının tanınacağı host parçası.</summary>
    public string ExternalUrlHostHint { get; set; } = "tixcore";
}
