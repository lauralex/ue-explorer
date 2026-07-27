using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using ModelContextProtocol.Server;
using UELib.Core;
using UELib.MCP.Errors;
using UELib.MCP.Models;
using UELib.MCP.Session;

namespace UELib.MCP.Tools;

[McpServerToolType]
public sealed class DecompilerTools(PackageSessionManager sessions)
{
    // Default keeps single-call output well under typical host response caps;
    // RL classes like Actor decompile to hundreds of thousands of chars.
    internal const int DefaultMaxChars = 32_000;
    internal const int MaxMaxChars = 120_000;

    [McpServerTool(Name = "decompile_object", UseStructuredContent = true,
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Decompile any IUnrealDecompilable object (class, function, struct, enum, const, state, property) " +
                 "looked up by group path. Returns the textual UnrealScript. " +
                 "Prefer the more specific siblings when you know the kind: `decompile_class` for whole classes by name, " +
                 "`decompile_function` for a single method on a class. Use this generic tool for non-class/non-function " +
                 "decompilable kinds (struct, enum, const, state, property). " +
                 "Output is capped at `max_chars`; check `truncations` for the original size when truncated.")]
    public Task<DecompileResultDto> DecompileObject(
        [Description("Handle returned by load_package.")] string handle,
        [Description("Dotted group path, e.g. 'TAGame.Car_TA.OnPossessed'.")] string group_path,
        [Range(1, MaxMaxChars), Description("Max characters of decompiled source to return (1..120000). Default 32000. " +
                     "If exceeded, the source is truncated and `truncations` lists the original length.")] int max_chars = DefaultMaxChars,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return sessions.RunAsync(() =>
        {
            var pkg = sessions.Get(handle).Package;
            PackageTools.EnsureInitialized(pkg);

            var obj = pkg.FindObjectByGroup(group_path)
                      ?? throw McpErrors.NotFound("Object", group_path);

            if (obj is not IUnrealDecompilable decompilable)
            {
                throw McpErrors.InvalidParam(
                    $"Object '{group_path}' (class {obj.Class?.Name ?? new UName("Class")}) is not decompilable.");
            }

            return Decompile(decompilable, ClampMaxChars(max_chars));
        }, ct);
    }

    [McpServerTool(Name = "decompile_class", UseStructuredContent = true,
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Decompile a UClass to full UnrealScript source (declaration, properties, functions, states, " +
                 "default-properties block). Output can be very large for RL classes like Actor — for a single function " +
                 "use `decompile_function`; for a structured (non-source) snapshot use `get_class_info`. " +
                 "Output is capped at `max_chars`; check `truncations` for the original size when truncated. " +
                 "May emit a warning if some tokens are not yet implemented for this game branch.")]
    public Task<DecompileResultDto> DecompileClass(
        [Description("Handle returned by load_package.")] string handle,
        [Description("Class name or dotted path.")] string class_name,
        [Range(1, MaxMaxChars), Description("Max characters of decompiled source to return (1..120000). Default 32000. " +
                     "RL classes like Actor produce 200k+ chars at full size — bump this up if needed, or pull individual " +
                     "functions via `decompile_function`.")] int max_chars = DefaultMaxChars,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return sessions.RunAsync(() =>
        {
            var pkg = sessions.Get(handle).Package;
            PackageTools.EnsureInitialized(pkg);
            var cls = ObjectTools.ResolveClass(pkg, class_name);
            return Decompile(cls, ClampMaxChars(max_chars));
        }, ct);
    }

    [McpServerTool(Name = "decompile_function", UseStructuredContent = true,
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Decompile a single UFunction to UnrealScript. Includes signature line, flags keywords, " +
                 "and the function body produced by walking the bytecode. " +
                 "For the entire class source use `decompile_class`. For raw bytecode tokens (offsets, opcode bytes, " +
                 "per-token text) use `disassemble_function`. " +
                 "Output is capped at `max_chars`; check `truncations` for the original size when truncated. " +
                 "Wraps the call in try/catch — if a token throws (common while RL token coverage is being expanded) " +
                 "the warning field carries the exception message and source contains whatever produced before the " +
                 "throw (may be empty).")]
    public Task<DecompileResultDto> DecompileFunction(
        [Description("Handle returned by load_package.")] string handle,
        [Description("Class name or dotted path.")] string class_path,
        [Description("UnrealScript function name.")] string function_name,
        [Range(1, MaxMaxChars), Description("Max characters of decompiled source to return (1..120000). Default 32000.")] int max_chars = DefaultMaxChars,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return sessions.RunAsync(() =>
        {
            var pkg = sessions.Get(handle).Package;
            PackageTools.EnsureInitialized(pkg);
            var cls = ObjectTools.ResolveClass(pkg, class_path);
            var fn = ObjectTools.ResolveFunction(cls, function_name);
            return Decompile(fn, ClampMaxChars(max_chars));
        }, ct);
    }

    [McpServerTool(Name = "search_function_source", UseStructuredContent = true,
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Search decompiled source across every UFunction in a loaded package. " +
                 "This finds call sites and field uses that object-name search cannot see. The query and optional " +
                 "class/name filters are case-insensitive literal substrings. Results include one bounded context " +
                 "snippet per matching function; malformed functions are counted and skipped.")]
    public Task<FunctionSourceSearchDto> SearchFunctionSource(
        [Description("Handle returned by load_package.")] string handle,
        [MinLength(1), Description("Case-insensitive literal substring to find in decompiled function source.")] string query,
        [Range(1, 500), Description("Maximum matching functions to return (1..500). Default 100.")] int max_results = 100,
        [Range(1, 100_000), Description("Maximum functions to decompile and scan (1..100000). Default 25000.")] int max_functions = 25_000,
        [Description("Optional case-insensitive substring filter applied to the owning class path.")] string? class_filter = null,
        [Description("Optional case-insensitive substring filter applied to the function name.")] string? name_filter = null,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return sessions.RunAsync(() =>
        {
            var pkg = sessions.Get(handle).Package;
            PackageTools.EnsureInitialized(pkg);

            if (string.IsNullOrEmpty(query))
            {
                throw McpErrors.InvalidParam("query must not be empty.");
            }

            max_results = Math.Clamp(max_results, 1, 500);
            max_functions = Math.Clamp(max_functions, 1, 100_000);

            IEnumerable<UFunction> source = pkg.Objects
                .OfType<UFunction>()
                .OrderBy(f => f.GetReferencePath(), StringComparer.OrdinalIgnoreCase);

            if (!string.IsNullOrEmpty(name_filter))
            {
                source = source.Where(f =>
                    f.Name.ToString().Contains(name_filter, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrEmpty(class_filter))
            {
                source = source.Where(f =>
                    GetOwningClassPath(f).Contains(class_filter, StringComparison.OrdinalIgnoreCase));
            }

            int scanned = 0;
            int errors = 0;
            var matches = new List<FunctionSourceMatchDto>();
            using var enumerator = source.GetEnumerator();

            while (scanned < max_functions && matches.Count < max_results && enumerator.MoveNext())
            {
                ct.ThrowIfCancellationRequested();
                UFunction fn = enumerator.Current;
                ++scanned;

                string decompiled;
                try
                {
                    decompiled = fn.Decompile() ?? string.Empty;
                }
                catch
                {
                    ++errors;
                    continue;
                }

                int matchIndex = decompiled.IndexOf(query, StringComparison.OrdinalIgnoreCase);
                if (matchIndex < 0)
                {
                    continue;
                }

                matches.Add(new FunctionSourceMatchDto(
                    name: fn.Name.ToString(),
                    path: fn.GetReferencePath(),
                    class_path: GetOwningClassPath(fn),
                    flags: FlagsFormat.Format(fn.FunctionFlags),
                    snippet: MakeSnippet(decompiled, matchIndex, query.Length)));
            }

            bool truncated = matches.Count >= max_results
                             || (scanned >= max_functions && enumerator.MoveNext());
            return new FunctionSourceSearchDto(
                scanned,
                matches.Count,
                errors,
                truncated,
                matches);
        }, ct);
    }

    [McpServerTool(Name = "disassemble_function", UseStructuredContent = true,
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Tokenize a function's bytecode without producing UnrealScript. Returns each token's stream offset, " +
                 "size in bytes, leading opcode byte, .NET type name, and the per-token decompiled text. " +
                 "Indispensable for diagnosing token-coverage gaps in the Rocket League bytecode. " +
                 "For the assembled UnrealScript source use `decompile_function`.")]
    public Task<DisassembleResultDto> DisassembleFunction(
        [Description("Handle returned by load_package.")] string handle,
        [Description("Class name or dotted path.")] string class_path,
        [Description("UnrealScript function name.")] string function_name,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return sessions.RunAsync(() =>
        {
            var pkg = sessions.Get(handle).Package;
            PackageTools.EnsureInitialized(pkg);
            var cls = ObjectTools.ResolveClass(pkg, class_path);
            var fn = ObjectTools.ResolveFunction(cls, function_name);

            var manager = fn.ByteCodeManager;
            if (manager == null || fn.ScriptSize <= 0)
            {
                return new DisassembleResultDto(
                    cls.GetReferencePath(), fn.Name.ToString(), 0,
                    Array.Empty<TokenDto>(), warning: "Function has no bytecode.");
            }

            string? warning = null;
            try
            {
                manager.Deserialize();
            }
            catch (Exception ex)
            {
                warning = $"Token deserialize threw {ex.GetType().Name}: {ex.Message}";
            }

            var tokens = new List<TokenDto>(manager.DeserializedTokens.Count);
            foreach (var tok in manager.DeserializedTokens)
            {
                string decompiledText;
                try { decompiledText = tok.Decompile() ?? string.Empty; }
                catch (Exception ex) { decompiledText = $"<decompile failed: {ex.GetType().Name}: {ex.Message}>"; }

                tokens.Add(new TokenDto(
                    position: tok.Position,
                    storage_position: tok.StoragePosition,
                    size: tok.Size,
                    opcode_byte: tok.OpCode,
                    token_type: tok.GetType().Name,
                    decompiled: decompiledText));
            }

            return new DisassembleResultDto(
                cls.GetReferencePath(), fn.Name.ToString(), fn.ScriptSize, tokens, warning);
        }, ct);
    }

    private static DecompileResultDto Decompile(IUnrealDecompilable target, int maxChars)
    {
        string source;
        try
        {
            source = target.Decompile() ?? string.Empty;
        }
        catch (Exception ex)
        {
            // Decompiler can throw on RL-specific tokens that aren't implemented yet.
            // Surface the exception in the warning so the agent can debug, instead of failing the whole call.
            return new DecompileResultDto(
                string.Empty,
                $"{ex.GetType().Name}: {ex.Message}",
                Array.Empty<string>());
        }

        if (source.Length <= maxChars)
        {
            return new DecompileResultDto(source, null, Array.Empty<string>());
        }

        int total = source.Length;
        var trimmed = source.Substring(0, maxChars)
                      + $"\n\n// [TRUNCATED at {maxChars}/{total} chars — bump max_chars or pull individual functions via decompile_function]";
        return new DecompileResultDto(
            trimmed,
            null,
            new[] { $"source:{total}" });
    }

    internal static int ClampMaxChars(int requested)
    {
        if (requested <= 0) return DefaultMaxChars;
        if (requested > MaxMaxChars) return MaxMaxChars;
        return requested;
    }

    private static string GetOwningClassPath(UFunction fn)
    {
        UObject? owner = fn.Outer;
        while (owner is not null and not UClass)
        {
            owner = owner.Outer;
        }

        return owner?.GetReferencePath() ?? string.Empty;
    }

    private static string MakeSnippet(string source, int matchIndex, int matchLength)
    {
        const int context = 180;
        int start = Math.Max(0, matchIndex - context);
        int end = Math.Min(source.Length, matchIndex + matchLength + context);
        string snippet = source[start..end]
            .Replace("\r", string.Empty, StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Trim();
        return (start > 0 ? "…" : string.Empty)
               + snippet
               + (end < source.Length ? "…" : string.Empty);
    }
}
