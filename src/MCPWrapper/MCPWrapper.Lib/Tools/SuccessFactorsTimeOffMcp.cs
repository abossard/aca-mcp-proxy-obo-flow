using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Net.Http.Json;
using System.Text.Json;
using MCPWrapper.Lib.Model;
using MCPWrapper.Lib.Config;
using MCPWrapper.Lib.Extensions;
using MCPWrapper.Lib.Adapter;

namespace MCPWrapper.Lib.Tools;

[McpServerToolType]
public sealed class SuccessFactorsTimeOffMcp
{
    private readonly SuccessFactorsTimeOffService service;

    public SuccessFactorsTimeOffMcp(IHttpClientFactory httpClientFactory, IOptions<SuccessFactorsConfig> options)
    {
        this.service = new SuccessFactorsTimeOffService(httpClientFactory, options);
    }

    [McpServerTool, Description("Book time off for an employee.")]
    public async Task<string> BookTimeOff(
        [Description("Employee ID")] string userId,
        [Description("Start date of time off")] DateTime startDate,
        [Description("End date of time off")] DateTime endDate)
    {
        return (await service.BookTimeOff(userId, startDate, endDate)).ToMcpView();
    }



    [McpServerTool, Description("List all time off requests for an employee.")]
    public async Task<string> ListTimeOffRequests(
        [Description("Employee ID")] string userId,
        [Description("Optional: Start date filter (inclusive) - only show requests starting on or after this date")] DateTime? startDateFilter = null,
        [Description("Optional: End date filter (inclusive) - only show requests ending on or before this date")] DateTime? endDateFilter = null)
    {
        return (await service.ListTimeOffRequests(userId, startDateFilter, endDateFilter)).ToMcpView();
    }
    

    [McpServerTool, Description("Delete a time off request by external code.")]
    public async Task<string> DeleteTimeOffRequest(
        [Description("External code of the time off request to delete")] string externalCode)
    {
        return (await service.DeleteTimeOffRequest(externalCode)).ToMcpView();
    }
}