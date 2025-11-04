namespace MCPWrapper.Tests;

using MCPWrapper.Lib.Config;
using Microsoft.Extensions.Configuration;

public class IntegrationTestBase
{

    protected IConfigurationRoot GetConfigurationRoot()
    {
        return new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();
    }

    protected SuccessFactorsConfig GetSuccessFactorsConfig()
    {
        var configuration = GetConfigurationRoot();

        var config = configuration.GetSection("SuccessFactors").Get<SuccessFactorsConfig>()
            ?? throw new InvalidOperationException("SuccessFactors configuration not found in appsettings.json");

        return config;
    }

    protected IHttpClientFactory GetHttpClientFactory() => new TestHttpClientFactory();

    private sealed class TestHttpClientFactory : IHttpClientFactory, IDisposable
    {
        private readonly Lazy<HttpMessageHandler> handlerLazy = new (() => new HttpClientHandler());

        public HttpClient CreateClient(string name) => new (handlerLazy.Value, disposeHandler: false);

        public void Dispose()
        {
            if (handlerLazy.IsValueCreated)
            {
                handlerLazy.Value.Dispose();
            }
        }
    }

}
