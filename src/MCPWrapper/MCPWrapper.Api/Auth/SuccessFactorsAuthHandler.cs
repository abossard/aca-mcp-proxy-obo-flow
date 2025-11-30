using System.Net.Http;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace MCPWrapper.Api.Auth;

public sealed class SuccessFactorsAuthHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor httpContextAccessor;
    private readonly IDownstreamTokenService downstreamTokenService;
    private readonly ILogger<SuccessFactorsAuthHandler> logger;

    public SuccessFactorsAuthHandler(
        IHttpContextAccessor httpContextAccessor,
        IDownstreamTokenService downstreamTokenService,
        ILogger<SuccessFactorsAuthHandler> logger)
    {
        this.httpContextAccessor = httpContextAccessor;
        this.downstreamTokenService = downstreamTokenService;
        this.logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext is null)
        {
            logger.LogWarning("No HTTP context available to attach downstream token for {Url}.", request.RequestUri);
            return await base.SendAsync(request, cancellationToken);
        }

        var userAccessToken = await httpContext.GetTokenAsync("access_token");
        if (string.IsNullOrWhiteSpace(userAccessToken))
        {
            logger.LogWarning("No incoming access token found for downstream call to {Url}.", request.RequestUri);
            return await base.SendAsync(request, cancellationToken);
        }

        try
        {
            var downstreamToken = await downstreamTokenService.GetAccessTokenAsync(userAccessToken, cancellationToken);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", downstreamToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to acquire downstream token for {Url}.", request.RequestUri);
            throw;
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
