using ModelContextProtocol;

namespace UELib.MCP.Errors;

internal static class McpErrors
{
    public static McpException InvalidParam(string message) => new(message);

    public static McpException NotFound(string what, string id) =>
        new($"{what} not found: '{id}'");

    public static McpException FileMissing(string path) =>
        new($"File does not exist: '{path}'");

    public static McpException Wrap(string action, Exception inner) =>
        new($"{action} failed: {inner.GetType().Name}: {inner.Message}", inner);
}
