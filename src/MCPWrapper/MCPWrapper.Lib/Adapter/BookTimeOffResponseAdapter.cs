using MCPWrapper.Lib.Model;
using System.Text.Json;
using MCPWrapper.Lib.Extensions;

namespace MCPWrapper.Lib.Adapter;

public static class BookTimeOffResponseAdapter
{
    public static string ToMcpView(this BookTimeOffResponse response)
    {
        if (!response.CallSuccessful)
        {
            return $"Failed to call the API, StatusCode: {response.StatusCode}";
        }
        var jsonDoc = JsonDocument.Parse(response.Content);

        if (jsonDoc.RootElement.TryGetProperty("d", out var dElement)
            && dElement.GetArrayLength() > 0)
        {
            var responseElement = dElement[0];
            var status = responseElement.GetStringProperty("status");
            var editStatus = responseElement.GetStringProperty("editStatus");
            var message = responseElement.GetStringProperty("message");
            var result = $"Status: {status} / EditStatus: {editStatus} / Message: {message}";
            return result;
        }

        return "No valid response found.";
    }
}