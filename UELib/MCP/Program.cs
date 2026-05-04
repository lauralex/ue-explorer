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
    .AddMcpServer(options =>
    {
        options.ServerInstructions =
            "Use this server to inspect and decompile Unreal Engine packages " +
            "(.upk/.u/.umap), with first-class support for Rocket League's " +
            "licensee-modified bytecode.\n\n" +
            "Workflow:\n" +
            "1. Always call `load_package` first; the returned `handle` is " +
            "required by every other tool. Pass `full_init=true` (the default) " +
            "for class/function inspection — `full_init=false` only populates " +
            "the package summary.\n" +
            "2. Use `list_loaded_packages` to recover handles after a " +
            "Claude Code restart — the MCP server outlives client sessions.\n" +
            "3. For Rocket League: the .upk MUST already be decrypted by " +
            "RLUPKTool before loading. Auto-detection picks build " +
            "RocketLeague at version 868 / licensee 32.\n" +
            "4. Decompile/disassemble calls never throw on a token-coverage " +
            "gap; the exception is captured in the `warning` field while " +
            "`source` may be empty or partial. Treat a non-null warning as a " +
            "diagnostic, not a failure.";
    })
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();
