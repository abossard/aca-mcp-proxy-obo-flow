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

public sealed class SuccessFactorsTimeOffService
{
    private readonly IHttpClientFactory httpClientFactory;
    private readonly SuccessFactorsConfig config;

    public SuccessFactorsTimeOffService(IHttpClientFactory httpClientFactory, IOptions<SuccessFactorsConfig> options)
    {
        this.httpClientFactory = httpClientFactory;
        this.config = options.Value;
    }

    public async Task<BookTimeOffResponse> BookTimeOff(
        string userId,
        DateTime startDate,
        DateTime endDate)
    {
        var externalCode = $"REQ_{Guid.NewGuid():N}"[..15]; // Limit to 12 chars like Python example

        // Convert to UTC and format as SAP date format
        var startDateSap = startDate.ToSapDateFormat();
        var endDateSap = endDate.ToSapDateFormat();

        // Build SAP payload according to ECTimeOff.json schema
        var payload = new BookTimeOffRequest
        {
            Metadata = new BookTimeOffMetadata
            {
            Uri = $"{config.SuccessFactorsBaseUrl}/EmployeeTime('{externalCode}')",
            Type = "SFOData.EmployeeTime"
            },
            UserId = userId,
            TimeType = "TT_VAC_REC",
            StartDate = startDateSap,
            EndDate = endDateSap,
            ApprovalStatus = "PENDING",
            ExternalCode = externalCode,
            UserIdNav = new BookTimeOffNavigationProperty
            {
            Metadata = new BookTimeOffMetadata
            {
                Uri = $"{config.SuccessFactorsBaseUrl}/User('{userId}')",
                Type = "SFOData.User"
            }
            },
            TimeTypeNav = new BookTimeOffNavigationProperty
            {
            Metadata = new BookTimeOffMetadata
            {
                Uri = $"{config.SuccessFactorsBaseUrl}/TimeType('TT_VAC_REC')",
                Type = "SFOData.TimeType"
            }
            }
        };

        //Console.WriteLine($"Payload for BookTimeOff: {JsonSerializer.Serialize(payload)}");

        var httpClient = httpClientFactory.CreateClient();
        httpClient.DefaultRequestHeaders.Add("apikey", config.ApiKey);
        var response = await httpClient.PostAsJsonAsync<BookTimeOffRequest>(
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

    public async Task<ListTimeOffResponse> ListTimeOffRequests(
        string userId,
        DateTime? startDateFilter = null,
        DateTime? endDateFilter = null)
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
    

    public async Task<DeleteTimeOffResponse> DeleteTimeOffRequest(
        string externalCode)
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