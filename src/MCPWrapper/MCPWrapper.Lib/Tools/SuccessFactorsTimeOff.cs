using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
    private readonly ILogger<SuccessFactorsTimeOffService> logger;

    public SuccessFactorsTimeOffService(
        IHttpClientFactory httpClientFactory,
        IOptions<SuccessFactorsConfig> options,
        ILogger<SuccessFactorsTimeOffService> logger)
    {
        this.httpClientFactory = httpClientFactory;
        this.config = options.Value;
        this.logger = logger;
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

        logger.LogInformation("Booking time off for {UserId} from {Start} to {End} (ExternalCode: {ExternalCode})", userId, startDateSap, endDateSap, externalCode);

        var httpClient = httpClientFactory.CreateClient("SuccessFactorsApi");
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

    public async Task<ListTimeOffResponse> ListTimeOffRequests(
        string userId,
        DateTime? startDateFilter = null,
        DateTime? endDateFilter = null)
    {

        var httpClient = httpClientFactory.CreateClient("SuccessFactorsApi");

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
                        
        logger.LogInformation("Listing time off requests for {UserId} with filters start={Start} end={End}", userId, startDateFilter, endDateFilter);

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
                logger.LogWarning(ex, "Failed to parse SuccessFactors response body");

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

        // Build the delete URL using the external code
        var deleteUrl = $"{config.SuccessFactorsBaseUrl}/EmployeeTime('{externalCode}')";

        logger.LogInformation("Deleting time off request {ExternalCode}", externalCode);

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