using System.Text.Json.Serialization;

namespace MCPWrapper.Lib.Model;

public class BookTimeOffResponse
{
    public int StatusCode { get; set; }
    public bool CallSuccessful { get; set; }
    public string Content { get; set; } = string.Empty;
    public string ExternalCode { get; set; } = string.Empty;
}