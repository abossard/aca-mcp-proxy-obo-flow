namespace MCPWrapper.Lib.Config;

public sealed class SuccessFactorsConfig
{
    public const string SectionName = "SuccessFactors";

    public string SuccessFactorsBaseUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
}