using System.Text.Json.Serialization;
namespace MCPWrapper.Lib.Model;

public class BookTimeOffRequest
{
    [JsonPropertyName("__metadata")]
    public BookTimeOffMetadata Metadata { get; set; } = new();

    [JsonPropertyName("userId")]
    public string UserId { get; set; } = string.Empty;

    [JsonPropertyName("timeType")]
    public string TimeType { get; set; } = "TT_VAC_REC";

    [JsonPropertyName("startDate")]
    public string StartDate { get; set; } = string.Empty;

    [JsonPropertyName("endDate")]
    public string EndDate { get; set; } = string.Empty;

    [JsonPropertyName("approvalStatus")]
    public string ApprovalStatus { get; set; } = "PENDING";

    [JsonPropertyName("externalCode")]
    public string ExternalCode { get; set; } = string.Empty;

    [JsonPropertyName("userIdNav")]
    public BookTimeOffNavigationProperty UserIdNav { get; set; } = new();

    [JsonPropertyName("timeTypeNav")]
    public BookTimeOffNavigationProperty TimeTypeNav { get; set; } = new();
}

public class BookTimeOffMetadata
{
    [JsonPropertyName("uri")]
    public string Uri { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;
}

public class BookTimeOffNavigationProperty
{
    [JsonPropertyName("__metadata")]
    public BookTimeOffMetadata Metadata { get; set; } = new();
}