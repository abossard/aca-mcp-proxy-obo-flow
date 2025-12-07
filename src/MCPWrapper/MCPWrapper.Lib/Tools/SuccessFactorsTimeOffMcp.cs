using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Net.Http.Json;
using System.Text.Json;
using MCPWrapper.Lib.Model;
using MCPWrapper.Lib.Config;
using MCPWrapper.Lib.Extensions;
using MCPWrapper.Lib.Adapter;
using MCPWrapper.Lib.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace MCPWrapper.Lib.Tools;

[McpServerToolType]
public sealed class SuccessFactorsTimeOffMcp
{
    private readonly SuccessFactorsTimeOffService service;
    private readonly ITokenService tokenService;
    private readonly IHttpContextAccessor httpContextAccessor;
    private readonly ILogger<SuccessFactorsTimeOffMcp> logger;

    public SuccessFactorsTimeOffMcp(
        SuccessFactorsTimeOffService service,
        ITokenService tokenService,
        IHttpContextAccessor httpContextAccessor,
        ILogger<SuccessFactorsTimeOffMcp> logger)
    {
        this.service = service;
        this.tokenService = tokenService;
        this.httpContextAccessor = httpContextAccessor;
        this.logger = logger;
    }

    private async Task<string?> GetOboTokenAsync()
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext == null)
        {
            logger.LogWarning("No HTTP context available for token extraction");
            return null;
        }

        // Try to get the user token from the Authorization header
        var authHeader = httpContext.Request.Headers["Authorization"].FirstOrDefault();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning("No Bearer token found in Authorization header");
            return null;
        }

        var userToken = authHeader.Substring("Bearer ".Length).Trim();
        logger.LogInformation("Extracted user token from Authorization header");

        // Exchange the user token for an OBO token
        return await tokenService.GetOboTokenAsync(userToken);
    }

    [McpServerTool, Description("Book time off for an employee.")]
    public async Task<string> BookTimeOff(
        [Description("Employee ID")] string userId,
        [Description("Start date of time off")] DateTime startDate,
        [Description("End date of time off")] DateTime endDate)
    {
        var oboToken = await GetOboTokenAsync();
        return (await service.BookTimeOff(userId, startDate, endDate, oboToken)).ToMcpView();
    }



    [McpServerTool, Description("List all time off requests for an employee.")]
    public async Task<string> ListTimeOffRequests(
        [Description("Employee ID")] string userId,
        [Description("Optional: Start date filter (inclusive) - only show requests starting on or after this date")] DateTime? startDateFilter = null,
        [Description("Optional: End date filter (inclusive) - only show requests ending on or before this date")] DateTime? endDateFilter = null)
    {
        var oboToken = await GetOboTokenAsync();
        return (await service.ListTimeOffRequests(userId, startDateFilter, endDateFilter, oboToken)).ToMcpView();
    }
    

    [McpServerTool, Description("Delete a time off request by external code.")]
    public async Task<string> DeleteTimeOffRequest(
        [Description("External code of the time off request to delete")] string externalCode)
    {
        var oboToken = await GetOboTokenAsync();
        return (await service.DeleteTimeOffRequest(externalCode, oboToken)).ToMcpView();
    }
}