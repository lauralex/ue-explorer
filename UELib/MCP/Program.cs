using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using UELib.MCP.Session;

// stdio MCP servers use stdout for JSON-RPC. UELib (UnrealPackage.cs has many
// Console.WriteLine sites) and any other Console.Write inside the library
// would corrupt the channel. Redirect Console.Out to stderr — the SDK's stdio
// transport uses Console.OpenStandardOutput() at the OS-stream level and is
// unaffected by Console.SetOut.
Console.SetOut(Console.Error);

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.AddConsole(o =>
{
    o.LogToStandardErrorThreshold = LogLevel.Trace;
});

builder.Services.AddSingleton<PackageSessionManager>();

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();
