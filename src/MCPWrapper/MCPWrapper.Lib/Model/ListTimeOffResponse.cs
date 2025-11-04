namespace MCPWrapper.Lib.Model;

public class ListTimeOffResponse
{
    public int StatusCode { get; set; }
    public bool CallSuccessful { get; set; }
    public List<TimeOffRequest> TimeOffRequests { get; set; } = new();
    public int RequestCount { get; set; }
    public string? ErrorMessage { get; set; }
}