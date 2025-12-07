using Microsoft.Extensions.Options;
using Microsoft.Identity.Client;
using Microsoft.Extensions.Logging;
using MCPWrapper.Lib.Config;
using Azure.Identity;
using Azure.Core;

namespace MCPWrapper.Lib.Services;

public interface ITokenService
{
    Task<string?> GetOboTokenAsync(string userToken);
}

public sealed class TokenService : ITokenService
{
    private readonly OboConfig _config;
    private readonly ILogger<TokenService> _logger;

    public TokenService(IOptions<OboConfig> config, ILogger<TokenService> logger)
    {
        _config = config.Value;
        _logger = logger;
    }

    public async Task<string?> GetOboTokenAsync(string userToken)
    {
        if (string.IsNullOrEmpty(_config.DownstreamApiScope))
        {
            _logger.LogWarning("Downstream API scope is not configured. Skipping OBO token exchange.");
            return null;
        }

        try
        {
            _logger.LogInformation("Attempting OBO token exchange for scope: {Scope}", _config.DownstreamApiScope);

            // Build MSAL confidential client with managed identity assertion callback
            var msalClient = ConfidentialClientApplicationBuilder
                .Create(_config.ClientId)
                .WithAuthority($"https://login.microsoftonline.com/{_config.TenantId}")
                .WithClientAssertion(async (AssertionRequestOptions options) =>
                {
                    // Use managed identity to get a token for the client assertion
                    var managedIdentityCredential = new ManagedIdentityCredential(_config.ClientId);
                    var assertionTokenContext = new TokenRequestContext(new[] { $"api://{_config.ClientId}/.default" });
                    var assertionToken = await managedIdentityCredential.GetTokenAsync(assertionTokenContext, default);
                    return assertionToken.Token;
                })
                .Build();

            var userAssertion = new UserAssertion(userToken);
            var scopes = new[] { _config.DownstreamApiScope };

            var result = await msalClient.AcquireTokenOnBehalfOf(scopes, userAssertion)
                .ExecuteAsync();

            _logger.LogInformation("OBO token acquired successfully");
            return result.AccessToken;
        }
        catch (MsalServiceException ex)
        {
            _logger.LogError(ex, "MSAL service error during OBO token exchange: {ErrorCode}", ex.ErrorCode);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during OBO token exchange");
            return null;
        }
    }
}
