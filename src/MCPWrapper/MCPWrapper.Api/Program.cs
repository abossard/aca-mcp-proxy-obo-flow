using System.Text.Json.Serialization;
using MCPWrapper.Lib.Tools;
using MCPWrapper.Lib.Config;
using MCPWrapper.Lib.Model;
using MCPWrapper.Api.Tools;


var builder = WebApplication.CreateSlimBuilder(args);

builder.Services.AddHttpContextAccessor();

builder.Services.AddOptions<SuccessFactorsConfig>()
    .Bind(builder.Configuration.GetSection(SuccessFactorsConfig.SectionName));

builder.Services.AddHttpClient();

builder.Services.AddMcpServer()
    .WithTools<SuccessFactorsTimeOffMcp>()
    .WithHttpTransport();

var app = builder.Build();

app.MapMcp();

app.Run();