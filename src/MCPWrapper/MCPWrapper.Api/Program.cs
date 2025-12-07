using System.Text.Json.Serialization;
using MCPWrapper.Lib.Tools;
using MCPWrapper.Lib.Config;
using MCPWrapper.Lib.Model;
using MCPWrapper.Lib.Services;
using MCPWrapper.Api.Tools;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;
using OpenTelemetry.Logs;

var builder = WebApplication.CreateSlimBuilder(args);

// Configure OpenTelemetry
var serviceName = builder.Configuration["Mcp:ServerName"] ?? "MCPWrapper.Api";
var serviceVersion = builder.Configuration["Mcp:Version"] ?? "1.0.0";

builder.Logging.AddOpenTelemetry(logging =>
{
    logging.IncludeFormattedMessage = true;
    logging.IncludeScopes = true;
});

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(serviceName: serviceName, serviceVersion: serviceVersion))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddSource(serviceName))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation());

// Add Azure Monitor exporter if connection string is available
var appInsightsConnectionString = builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
if (!string.IsNullOrEmpty(appInsightsConnectionString))
{
    builder.Services.AddOpenTelemetry()
        .WithTracing(tracing => tracing.AddOtlpExporter())
        .WithMetrics(metrics => metrics.AddOtlpExporter());
    
    // Add Azure Monitor via Application Insights SDK
    builder.Services.AddApplicationInsightsTelemetry(options =>
    {
        options.ConnectionString = appInsightsConnectionString;
    });
}
else
{
    // Fallback to console exporter for local development
    builder.Services.AddOpenTelemetry()
        .WithTracing(tracing => tracing.AddConsoleExporter())
        .WithMetrics(metrics => metrics.AddConsoleExporter());
}

builder.Services.AddHttpContextAccessor();

// Configure OBO settings
builder.Services.AddOptions<OboConfig>()
    .Bind(builder.Configuration.GetSection(OboConfig.SectionName))
    .Configure(options =>
    {
        // Allow environment variables to override configuration
        if (!string.IsNullOrEmpty(builder.Configuration["AZURE_TENANT_ID"]))
            options.TenantId = builder.Configuration["AZURE_TENANT_ID"]!;
        if (!string.IsNullOrEmpty(builder.Configuration["ENTRA_CLIENT_ID"]))
            options.ClientId = builder.Configuration["ENTRA_CLIENT_ID"]!;
        if (!string.IsNullOrEmpty(builder.Configuration["DOWNSTREAM_API_SCOPE"]))
            options.DownstreamApiScope = builder.Configuration["DOWNSTREAM_API_SCOPE"]!;
    });

builder.Services.AddOptions<SuccessFactorsConfig>()
    .Bind(builder.Configuration.GetSection(SuccessFactorsConfig.SectionName));

// Register services
builder.Services.AddSingleton<ITokenService, TokenService>();
builder.Services.AddScoped<SuccessFactorsTimeOffService>();

builder.Services.AddHttpClient();

// Configure JWT Bearer authentication
var tenantId = builder.Configuration["AZURE_TENANT_ID"];
var clientId = builder.Configuration["ENTRA_CLIENT_ID"];

if (!string.IsNullOrEmpty(tenantId) && !string.IsNullOrEmpty(clientId))
{
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.Authority = $"https://login.microsoftonline.com/{tenantId}/v2.0";
            options.Audience = $"api://{tenantId}/{clientId}";
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true
            };
            
            options.Events = new JwtBearerEvents
            {
                OnAuthenticationFailed = context =>
                {
                    var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                    logger.LogWarning("JWT authentication failed: {Exception}", context.Exception.Message);
                    return Task.CompletedTask;
                },
                OnTokenValidated = context =>
                {
                    var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                    logger.LogInformation("JWT token validated for user: {User}", context.Principal?.Identity?.Name ?? "Unknown");
                    return Task.CompletedTask;
                }
            };
        });
}

builder.Services.AddAuthorization();

builder.Services.AddMcpServer()
    .WithTools<SuccessFactorsTimeOffMcp>()
    .WithHttpTransport();

var app = builder.Build();

// Use authentication and authorization middleware
app.UseAuthentication();
app.UseAuthorization();

app.MapMcp();

var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("MCP Wrapper API started. Service: {ServiceName}, Version: {ServiceVersion}", serviceName, serviceVersion);

app.Run();