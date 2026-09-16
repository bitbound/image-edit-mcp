using Bitbound.ImageEditMcp.ImageEditor;
using Bitbound.SystemAbstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

// MCP stdio servers must send only JSON-RPC messages to stdout.
// Redirect all logging to stderr so it does not corrupt the protocol stream.
builder.Logging.ClearProviders();
builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);

builder.Services.AddFileSystem();
builder.Services.AddSingleton<ImageDataManager>();

builder.Services.AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<ImageEditTools>();

await builder.Build().RunAsync();
