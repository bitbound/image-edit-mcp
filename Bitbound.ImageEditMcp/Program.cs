using Bitbound.ImageEditMcp.ImageEditor;
using Bitbound.SystemAbstractions;
using Bitbound.SystemAbstractions.FileSystem;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

// MCP stdio servers must send only JSON-RPC messages to stdout.
// Redirect all logging to stderr so it does not corrupt the protocol stream.
builder.Logging.ClearProviders();
builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);

builder.Services.AddFileSystem();
builder.Services.AddSingleton(sp =>
{
    var fileSystem = sp.GetRequiredService<IFileSystem>();
    var dataDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "image-edit-mcp");
    return new ImageDataManager(fileSystem, dataDirectory);
});

builder.Services.AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<ImageEditTools>();

await builder.Build().RunAsync();
