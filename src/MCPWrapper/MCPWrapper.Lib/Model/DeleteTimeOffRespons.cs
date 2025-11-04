namespace MCPWrapper.Lib.Model;

public class DeleteTimeOffResponse
{
    public int StatusCode { get; set; }
    public bool CallSuccessful { get; set; }
    public string Content { get; set; } = string.Empty;
    public string ExternalCode { get; set; } = string.Empty;
}
