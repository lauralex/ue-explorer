using System;
using System.Linq;
using UELib.Services;

namespace UELib.NetworkSchemaExporterTool;

internal static class Program
{
    public static int Main(string[] args)
    {
        LibServices.LogService = new SilentLogService();

        if (args.Length < 2 || args[0] is "--help" or "-h" or "/?")
        {
            Console.Error.WriteLine(
                "Usage: NetworkSchemaExporter <output-json> <decrypted-upk> [decrypted-upk...]");
            return args.Length == 1 ? 0 : 1;
        }

        try
        {
            return NetworkSchemaExporter.Run(args[0], args.Skip(1));
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(
                "[network-schema] export failed: {0}",
                exception);
            return 3;
        }
    }

    private sealed class SilentLogService : ILogService
    {
        public void Log(string text) { }
        public void Log(string format, params object?[] arg) { }
        public void SilentException(Exception exception) { }
        public void SilentAssert(bool assert, string message) { }
    }
}
