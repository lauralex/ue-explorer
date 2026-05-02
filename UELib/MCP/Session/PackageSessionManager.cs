using ModelContextProtocol;

namespace UELib.MCP.Session;

/// <summary>
/// Owns the lifetime of every UnrealPackage opened via the MCP and serializes
/// access to UELib (which is not thread-safe — every public tool entry must
/// run inside <see cref="RunAsync"/>).
/// </summary>
public sealed class PackageSessionManager : IAsyncDisposable
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly Dictionary<string, Session> _sessions = new(StringComparer.Ordinal);

    public sealed record Session(string Handle, string Path, UnrealPackage Package);

    public async Task<T> RunAsync<T>(Func<T> body, CancellationToken ct)
    {
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try { return body(); }
        finally { _lock.Release(); }
    }

    public string Add(string path, UnrealPackage package)
    {
        var handle = Guid.NewGuid().ToString("N").Substring(0, 8);
        _sessions[handle] = new Session(handle, path, package);
        return handle;
    }

    public Session Get(string handle)
    {
        if (!_sessions.TryGetValue(handle, out var s))
        {
            throw new McpException(
                $"Unknown package handle: '{handle}'. Call list_loaded_packages to see open handles.");
        }
        return s;
    }

    public IReadOnlyCollection<Session> All() => _sessions.Values.ToArray();

    public bool Remove(string handle)
    {
        if (!_sessions.Remove(handle, out var s)) return false;
        TryDispose(s.Package);
        return true;
    }

    public async ValueTask DisposeAsync()
    {
        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            foreach (var s in _sessions.Values) TryDispose(s.Package);
            _sessions.Clear();
        }
        finally { _lock.Release(); }
    }

    private static void TryDispose(IDisposable d)
    {
        try { d.Dispose(); }
        catch { /* best-effort: a corrupt package may throw on dispose */ }
    }
}
