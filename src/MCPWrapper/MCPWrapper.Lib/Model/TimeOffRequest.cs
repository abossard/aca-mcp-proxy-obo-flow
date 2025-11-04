namespace MCPWrapper.Lib.Model;

public class TimeOffRequest
{
    public string ExternalCode { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string TimeType { get; set; } = string.Empty;
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string ApprovalStatus { get; set; } = string.Empty;
    public string Comment { get; set; } = string.Empty;
    public decimal? QuantityInDays { get; set; }
    public decimal? QuantityInHours { get; set; }
    public DateTime? CreatedDate { get; set; }
    public DateTime? LastModifiedDate { get; set; }
}