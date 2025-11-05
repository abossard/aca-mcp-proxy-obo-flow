using MCPWrapper.Lib.Model;

namespace MCPWrapper.Lib.Adapter;

public static class ListTimeOffResponseAdapter
{
    public static string ToMcpView(this ListTimeOffResponse response)
    {
        if (!response.CallSuccessful)
        {
            return $"Failed to call the List Time Off endpoint, StatusCode: {response.StatusCode}";
        }

        if (response.TimeOffRequests.Count == 0)
        {
            return "No Time Off Requests found.";
        }
        
        return string.Join(
            Environment.NewLine,
            response.TimeOffRequests.Select(
                request => $"ExternalCode: {request.ExternalCode} / UserId: {request.UserId} / TimeType: {request.TimeType} / StartDate: {request.StartDate} / EndDate: {request.EndDate} / ApprovalStatus: {request.ApprovalStatus} / Comment: {request.Comment} / QuantityInDays: {request.QuantityInDays} / QuantityInHours: {request.QuantityInHours} "));
   }

}