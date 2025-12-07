using Microsoft.Extensions.Options;
using Microsoft.Identity.Client;
using Microsoft.Extensions.Logging;
using MCPWrapper.Lib.Config;

namespace MCPWrapper.Lib.Services;

public interface ITokenService
{
    Task<string?> GetOboTokenAsync(string userToken);
}

public sealed class TokenService : ITokenService
{
    private readonly OboConfig _config;
    private readonly ILogger<TokenService> _logger;
    private readonly IConfidentialClientApplication _msalClient;

    public TokenService(IOptions<OboConfig> config, ILogger<TokenService> logger)
    {
        _config = config.Value;
        _logger = logger;

        // Build MSAL confidential client with managed identity
        _msalClient = ConfidentialClientApplicationBuilder
            .Create(_config.ClientId)
            .WithAuthority($"https://login.microsoftonline.com/{_config.TenantId}")
            .WithClientAssertion(GetManagedIdentityAssertion)
            .Build();
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

            var userAssertion = new UserAssertion(userToken);
            var scopes = new[] { _config.DownstreamApiScope };

            var result = await _msalClient.AcquireTokenOnBehalfOf(scopes, userAssertion)
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

    private async Task<string> GetManagedIdentityAssertion()
    {
        // Use Azure.Identity to get a token for the managed identity
        // This allows us to authenticate as the managed identity without secrets
        var credential = new Azure.Identity.ManagedIdentityCredential(_config.ClientId);
        var tokenRequestContext = new Azure.Core.TokenRequestContext(
            new[] { "https://graph.microsoft.com/.default" });
        
        var token = await credential.GetTokenAsync(tokenRequestContext, default);
        return token.Token;
    }
}
