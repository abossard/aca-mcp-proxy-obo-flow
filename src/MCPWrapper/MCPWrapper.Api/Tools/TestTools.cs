using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using System.ComponentModel;
using MCPWrapper.Lib.Model;
using MCPWrapper.Lib.Config;
using MCPWrapper.Lib.Extensions;

namespace MCPWrapper.Api.Tools;

[McpServerToolType]
public sealed class TestTools
{
    private readonly IHttpClientFactory httpClientFactory;
    private readonly SuccessFactorsConfig config;

    public TestTools(IHttpClientFactory httpClientFactory, IOptions<SuccessFactorsConfig> options)
    {
        this.httpClientFactory = httpClientFactory;
        this.config = options.Value;
    }

    [McpServerTool, Description("concatenate two strings.")]
    public async Task<BookTimeOffResponse> ConcatenateStrings(
        [Description("First string")] string str1,
        [Description("Second string")] string str2)
    {
        return await Task.FromResult(
            new BookTimeOffResponse
            {
                StatusCode = 200,
                CallSuccessful = true,
                Content = $"{str1}{str2}",
                ExternalCode = string.Empty
            }

        );
    }
}