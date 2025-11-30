using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Client;
using MCPWrapper.Lib.Config;

namespace MCPWrapper.Api.Auth;

public interface IDownstreamTokenService
{
    Task<string> GetAccessTokenAsync(string userAccessToken, CancellationToken cancellationToken = default);
}

public sealed class OnBehalfOfTokenService : IDownstreamTokenService
{
    private readonly AzureAdConfig azureAdConfig;
    private readonly SuccessFactorsConfig successFactorsConfig;
    private readonly IMemoryCache memoryCache;
    private readonly ILogger<OnBehalfOfTokenService> logger;
    private readonly IConfidentialClientApplication clientApp;

    public OnBehalfOfTokenService(
        IOptions<AzureAdConfig> azureOptions,
        IOptions<SuccessFactorsConfig> successFactorsOptions,
        IMemoryCache memoryCache,
        ILogger<OnBehalfOfTokenService> logger)
    {
        azureAdConfig = azureOptions.Value;
        successFactorsConfig = successFactorsOptions.Value;
        this.memoryCache = memoryCache;
        this.logger = logger;

        clientApp = ConfidentialClientApplicationBuilder
            .Create(azureAdConfig.ClientId)
            .WithAuthority($"https://login.microsoftonline.com/{azureAdConfig.TenantId}")
            .WithClientSecret(azureAdConfig.ClientSecret)
            .Build();
    }

    public async Task<string> GetAccessTokenAsync(string userAccessToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userAccessToken))
        {
            throw new ArgumentException("User access token is required for OBO exchange.", nameof(userAccessToken));
        }

        if (string.IsNullOrWhiteSpace(successFactorsConfig.DownstreamScope))
        {
            throw new InvalidOperationException("SuccessFactors downstream scope is not configured.");
        }

        var cacheKey = BuildCacheKey(userAccessToken, successFactorsConfig.DownstreamScope);
        if (memoryCache.TryGetValue(cacheKey, out string? cachedToken) && !string.IsNullOrWhiteSpace(cachedToken))
        {
            logger.LogInformation("Using cached SuccessFactors token for configured scope.");
            return cachedToken;
        }

        logger.LogInformation("Acquiring SuccessFactors token via OBO for scope {Scope}.", successFactorsConfig.DownstreamScope);

        var result = await clientApp
            .AcquireTokenOnBehalfOf(new[] { successFactorsConfig.DownstreamScope }, new UserAssertion(userAccessToken))
            .ExecuteAsync(cancellationToken);

        var cacheDuration = CalculateCacheDuration(result.ExpiresOn);
        memoryCache.Set(cacheKey, result.AccessToken, cacheDuration);

        logger.LogInformation("Cached SuccessFactors token for {CacheDuration}.", cacheDuration);

        return result.AccessToken;
    }

    private static string BuildCacheKey(string userAccessToken, string scope)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(userAccessToken));
        var userHash = Convert.ToHexString(hashBytes);
        return $"{userHash}:{scope}";
    }

    private static TimeSpan CalculateCacheDuration(DateTimeOffset expiresOn)
    {
        var buffer = TimeSpan.FromMinutes(1);
        var duration = expiresOn - DateTimeOffset.UtcNow - buffer;
        return duration > TimeSpan.Zero ? duration : TimeSpan.FromMinutes(5);
    }
}
