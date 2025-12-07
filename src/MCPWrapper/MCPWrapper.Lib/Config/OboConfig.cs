namespace MCPWrapper.Lib.Config;

public sealed class OboConfig
{
    public const string SectionName = "Obo";

    public string TenantId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string DownstreamApiScope { get; set; } = string.Empty;
}
