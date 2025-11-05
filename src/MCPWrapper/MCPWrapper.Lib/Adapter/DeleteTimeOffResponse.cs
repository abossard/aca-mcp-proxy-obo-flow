using MCPWrapper.Lib.Model;
namespace MCPWrapper.Lib.Adapter;
public static class DeleteTimeOffResponseAdapter
{
    public static string ToMcpView(this DeleteTimeOffResponse response)
    {
        if (!response.CallSuccessful)
        {
            return $"Failed to call the Delete Time Off endpoint, StatusCode: {response.StatusCode}";
        }

        return $"Successfully deleted Time Off with ExternalCode: {response.ExternalCode}";
    }
}