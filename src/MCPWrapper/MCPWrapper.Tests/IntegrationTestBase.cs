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

    protected Microsoft.Extensions.Logging.ILogger<T> GetLogger<T>() => new TestLogger<T>();

    private sealed class TestLogger<T> : Microsoft.Extensions.Logging.ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;
        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Console.WriteLine($"[{logLevel}] {formatter(state, exception)}");
        }
    }

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
