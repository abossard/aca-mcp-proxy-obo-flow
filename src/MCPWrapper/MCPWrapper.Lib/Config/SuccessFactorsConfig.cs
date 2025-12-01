namespace MCPWrapper.Lib.Config;

public sealed class SuccessFactorsConfig
{
    public const string SectionName = "SuccessFactors";

    public string SuccessFactorsBaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Scope (audience) to request during the On-Behalf-Of exchange for the SuccessFactors downstream API
    /// (e.g. "api://<successfactors-api-client-id>/.default").
    /// </summary>
    public string DownstreamScope { get; set; } = string.Empty;
}