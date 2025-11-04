using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Net.Http.Json;
using System.Text.Json;
using MCPWrapper.Lib.Model;
using MCPWrapper.Lib.Config;
using MCPWrapper.Lib.Extensions;

namespace MCPWrapper.Lib.Tools;

[McpServerToolType]
public sealed class SuccessFactorsTimeOffTools
{
    private readonly IHttpClientFactory httpClientFactory;
    private readonly SuccessFactorsConfig config;

    public SuccessFactorsTimeOffTools(IHttpClientFactory httpClientFactory, IOptions<SuccessFactorsConfig> options)
    {
        this.httpClientFactory = httpClientFactory;
        this.config = options.Value;
    }

    [McpServerTool, Description("Book time off for an employee.")]
    public async Task<BookTimeOffResponse> BookTimeOff(
        [Description("Employee ID")] string userId,
        [Description("Start date of time off")] DateTime startDate,
        [Description("End date of time off")] DateTime endDate)
    {
        var externalCode = $"REQ_{Guid.NewGuid():N}"[..15]; // Limit to 12 chars like Python example

        // Convert to UTC and format as SAP date format
        var startDateSap = startDate.ToSapDateFormat();
        var endDateSap = endDate.ToSapDateFormat();

        // Build SAP payload according to ECTimeOff.json schema
        var payload = new
        {
            __metadata = new
            {
                uri = $"{config.SuccessFactorsBaseUrl}/EmployeeTime('{externalCode}')",
                type = "SFOData.EmployeeTime"
            },
            userId = userId,
            timeType = "TT_VAC_REC",
            startDate = startDateSap,
            endDate = endDateSap,
            approvalStatus = "PENDING",
            externalCode = externalCode,
            userIdNav = new
            {
                __metadata = new
                {
                    uri = $"{config.SuccessFactorsBaseUrl}/User('{userId}')",
                    type = "SFOData.User"
                }
            },
            timeTypeNav = new
            {
                __metadata = new
                {
                    uri = $"{config.SuccessFactorsBaseUrl}/TimeType('TT_VAC_REC')",
                    type = "SFOData.TimeType"
                }
            }
        };

        var httpClient = httpClientFactory.CreateClient();
        httpClient.DefaultRequestHeaders.Add("apikey", config.ApiKey);
        var response = await httpClient.PostAsJsonAsync(
            $"{config.SuccessFactorsBaseUrl}/upsert?workflowConfirmed=true&$format=json",
            payload);

        var responseContent = await response.Content.ReadAsStringAsync();

        return new BookTimeOffResponse
        {
            StatusCode = (int)response.StatusCode,
            CallSuccessful = response.IsSuccessStatusCode,
            Content = responseContent,
            ExternalCode = externalCode
        };
    }



    [McpServerTool, Description("List all time off requests for an employee.")]
    public async Task<ListTimeOffResponse> ListTimeOffRequests(
        [Description("Employee ID")] string userId,
        [Description("Optional: Start date filter (inclusive) - only show requests starting on or after this date")] DateTime? startDateFilter = null,
        [Description("Optional: End date filter (inclusive) - only show requests ending on or before this date")] DateTime? endDateFilter = null)
    {

        var httpClient = httpClientFactory.CreateClient("SuccessFactorsApi");
        httpClient.DefaultRequestHeaders.Add("apikey", config.ApiKey);

        var filterParts = new List<string> { $"userId eq '{userId}'" };
        
        // Add date filters if provided (using ISO date format)
        if (startDateFilter.HasValue)
        {
            var startDateIso = startDateFilter.Value.ToString("yyyy-MM-dd");
            filterParts.Add($"startDate ge '{startDateIso}'");
        }
        
        if (endDateFilter.HasValue)
        {
            var endDateIso = endDateFilter.Value.ToString("yyyy-MM-dd");
            filterParts.Add($"endDate le '{endDateIso}'");
        }
        var filterQuery = string.Join(" and ", filterParts);
        var selectFields = "externalCode,userId,timeType,startDate,endDate,approvalStatus,comment,quantityInDays,quantityInHours,createdDate,lastModifiedDate";
        var orderBy = "startDate desc";

        var requestUrl = $"{config.SuccessFactorsBaseUrl}/EmployeeTime" +
                        $"?$filter={Uri.EscapeDataString(filterQuery)}" +
                        $"&$select={selectFields}" +
                        $"&$orderby={orderBy}" +
                        $"&$format=json";
                        
        var response = await httpClient.GetAsync(requestUrl);
        var responseContent = await response.Content.ReadAsStringAsync();

        var timeOffRequests = new List<TimeOffRequest>();

        if (response.IsSuccessStatusCode && !string.IsNullOrEmpty(responseContent))
        {
            try
            {
                var jsonDoc = JsonDocument.Parse(responseContent);
                if (jsonDoc.RootElement.TryGetProperty("d", out var dElement) &&
                    dElement.TryGetProperty("results", out var resultsElement))
                {
                    foreach (var item in resultsElement.EnumerateArray())
                    {
                        timeOffRequests.Add(new TimeOffRequest
                        {
                            ExternalCode = item.GetStringProperty("externalCode"),
                            UserId = item.GetStringProperty("userId"),
                            TimeType = item.GetStringProperty("timeType"),
                            StartDate = item.GetStringProperty("startDate").ParseSapDate(),
                            EndDate = item.GetStringProperty("endDate").ParseSapDate(),
                            ApprovalStatus = item.GetStringProperty("approvalStatus"),
                            Comment = item.GetStringProperty("comment"),
                            QuantityInDays = item.GetDecimalProperty("quantityInDays"),
                            QuantityInHours = item.GetDecimalProperty("quantityInHours"),
                            CreatedDate = item.GetStringProperty("createdDate").ParseSapDate(),
                            LastModifiedDate = item.GetStringProperty("lastModifiedDate").ParseSapDate()
                        });
                    }
                }
            }
            catch (JsonException ex)
            {
                return new ListTimeOffResponse
                {
                    StatusCode = (int)response.StatusCode,
                    CallSuccessful = false,
                    TimeOffRequests = new List<TimeOffRequest>(),
                    ErrorMessage = $"Failed to parse JSON response: {ex.Message}"
                };
            }
        }

        return new ListTimeOffResponse
        {
            StatusCode = (int)response.StatusCode,
            CallSuccessful = true,
            TimeOffRequests = timeOffRequests,
            RequestCount = timeOffRequests.Count
        };
    }
    

    [McpServerTool, Description("Delete a time off request by external code.")]
    public async Task<DeleteTimeOffResponse> DeleteTimeOffRequest(
        [Description("External code of the time off request to delete")] string externalCode)
    {
        var httpClient = httpClientFactory.CreateClient("SuccessFactorsApi");
        httpClient.DefaultRequestHeaders.Add("apikey", config.ApiKey);
        
        // Build the delete URL using the external code
        var deleteUrl = $"{config.SuccessFactorsBaseUrl}/EmployeeTime('{externalCode}')";
        httpClient.DefaultRequestHeaders.Add("apikey", config.ApiKey);

        var response = await httpClient.DeleteAsync(deleteUrl);
        var responseContent = await response.Content.ReadAsStringAsync();

        return new DeleteTimeOffResponse
        {
            StatusCode = (int)response.StatusCode,
            CallSuccessful = response.IsSuccessStatusCode,
            Content = responseContent,
            ExternalCode = externalCode
        };
    }
}