using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Options;
using MCPWrapper.Api.Auth;
using MCPWrapper.Api.Tools;
using MCPWrapper.Lib.Config;
using MCPWrapper.Lib.Tools;

var builder = WebApplication.CreateSlimBuilder(args);

var azureAdConfig = builder.Configuration
    .GetSection(AzureAdConfig.SectionName)
    .Get<AzureAdConfig>() ?? new AzureAdConfig();

builder.Services.AddHttpContextAccessor();

builder.Services.AddOptions<SuccessFactorsConfig>()
    .Bind(builder.Configuration.GetSection(SuccessFactorsConfig.SectionName));

builder.Services.AddOptions<AzureAdConfig>()
    .Bind(builder.Configuration.GetSection(AzureAdConfig.SectionName));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = $"https://login.microsoftonline.com/{azureAdConfig.TenantId}/v2.0";
        options.Audience = azureAdConfig.Audience;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidAudience = azureAdConfig.Audience,
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddMemoryCache();

builder.Services.AddHttpClient();
builder.Services.AddTransient<SuccessFactorsAuthHandler>();
builder.Services.AddSingleton<IDownstreamTokenService, OnBehalfOfTokenService>();

builder.Services.AddHttpClient("SuccessFactorsApi", (sp, client) =>
{
    var config = sp.GetRequiredService<IOptions<SuccessFactorsConfig>>().Value;
    if (!string.IsNullOrWhiteSpace(config.SuccessFactorsBaseUrl))
    {
        client.BaseAddress = new Uri(config.SuccessFactorsBaseUrl);
    }

    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
})
.AddHttpMessageHandler<SuccessFactorsAuthHandler>();

builder.Services.AddMcpServer()
    .WithTools<SuccessFactorsTimeOffMcp>()
    .WithHttpTransport();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

var mcpEndpoint = app.MapMcp();
mcpEndpoint.RequireAuthorization();

app.Run();