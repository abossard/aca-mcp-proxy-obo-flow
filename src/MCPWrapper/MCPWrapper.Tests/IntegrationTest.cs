namespace MCPWrapper.Tests;

using System.Text.Json;
using MCPWrapper.Lib.Adapter;
using MCPWrapper.Lib.Tools;
using Microsoft.Extensions.Options;

public class IntegrationTest : IntegrationTestBase
{
    [Fact]
    public async Task CanCallBookTimeOff()
    {
        var userId = "802986"; // Replace with a valid user ID for testing

        var service = new SuccessFactorsTimeOffService(
            GetHttpClientFactory(),
            Options.Create(GetSuccessFactorsConfig()));

        var result = await service.BookTimeOff(userId, DateTime.UtcNow.AddDays(5), DateTime.UtcNow.AddDays(15));

        var stringifiedResult = result.ToMcpView();
        Console.WriteLine(stringifiedResult);

        Assert.True(result.CallSuccessful, "BookTimeOff call was not successful");

        await Task.Delay(5000); // Wait a bit for the record to be available

        var timeOffs = await service.ListTimeOffRequests(userId, DateTime.UtcNow);
        Assert.True(timeOffs.CallSuccessful, "ListTimeOffRequests call was not successful");   
        Assert.True(timeOffs.RequestCount > 0, "No time off requests found for user after booking time off");

        if(timeOffs.TimeOffRequests.Count > 0)
        {
            var firstRequest = timeOffs.TimeOffRequests[0];
            var delResult = await service.DeleteTimeOffRequest(firstRequest.ExternalCode);
            Assert.True(delResult.CallSuccessful, "DeleteTimeOffRequest call was not successful");
        }   

    }

}
