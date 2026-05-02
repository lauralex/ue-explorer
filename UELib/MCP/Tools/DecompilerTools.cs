using System.ComponentModel;
using ModelContextProtocol.Server;
using UELib.Core;
using UELib.MCP.Errors;
using UELib.MCP.Models;
using UELib.MCP.Session;

namespace UELib.MCP.Tools;

[McpServerToolType]
public sealed class DecompilerTools(PackageSessionManager sessions)
{
    [McpServerTool(Name = "decompile_object")]
    [Description("Decompile any IUnrealDecompilable object (class, function, struct, enum, const, state, property) " +
                 "looked up by group path. Returns the textual UnrealScript.")]
    public Task<DecompileResultDto> DecompileObject(
        [Description("Handle returned by load_package.")] string handle,
        [Description("Dotted group path, e.g. 'TAGame.Car_TA.OnPossessed'.")] string group_path,
        CancellationToken ct = default)
    {
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

            return Decompile(decompilable);
        }, ct);
    }

    [McpServerTool(Name = "decompile_class")]
    [Description("Decompile a UClass to full UnrealScript source (declaration, properties, functions, states, " +
                 "default-properties block). May emit a warning if some tokens are not yet implemented for this game branch.")]
    public Task<DecompileResultDto> DecompileClass(
        [Description("Handle returned by load_package.")] string handle,
        [Description("Class name or dotted path.")] string class_name,
        CancellationToken ct = default)
    {
        return sessions.RunAsync(() =>
        {
            var pkg = sessions.Get(handle).Package;
            PackageTools.EnsureInitialized(pkg);
            var cls = ObjectTools.ResolveClass(pkg, class_name);
            return Decompile(cls);
        }, ct);
    }

    [McpServerTool(Name = "decompile_function")]
    [Description("Decompile a single UFunction to UnrealScript. Includes signature line, flags keywords, " +
                 "and the function body produced by walking the bytecode. Wraps the call in try/catch — " +
                 "if a token throws (common while RL token coverage is being expanded) the warning field carries " +
                 "the exception message and source contains whatever produced before the throw (may be empty).")]
    public Task<DecompileResultDto> DecompileFunction(
        [Description("Handle returned by load_package.")] string handle,
        [Description("Class name or dotted path.")] string class_path,
        [Description("UnrealScript function name.")] string function_name,
        CancellationToken ct = default)
    {
        return sessions.RunAsync(() =>
        {
            var pkg = sessions.Get(handle).Package;
            PackageTools.EnsureInitialized(pkg);
            var cls = ObjectTools.ResolveClass(pkg, class_path);
            var fn = ObjectTools.ResolveFunction(cls, function_name);
            return Decompile(fn);
        }, ct);
    }

    [McpServerTool(Name = "disassemble_function")]
    [Description("Tokenize a function's bytecode without producing UnrealScript. Returns each token's stream offset, " +
                 "size in bytes, leading opcode byte, .NET type name, and the per-token decompiled text. " +
                 "Indispensable for diagnosing token-coverage gaps in the Rocket League bytecode.")]
    public Task<DisassembleResultDto> DisassembleFunction(
        [Description("Handle returned by load_package.")] string handle,
        [Description("Class name or dotted path.")] string class_path,
        [Description("UnrealScript function name.")] string function_name,
        CancellationToken ct = default)
    {
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

    private static DecompileResultDto Decompile(IUnrealDecompilable target)
    {
        try
        {
            return new DecompileResultDto(target.Decompile() ?? string.Empty, null);
        }
        catch (Exception ex)
        {
            // Decompiler can throw on RL-specific tokens that aren't implemented yet.
            // Surface the exception in the warning so the agent can debug, instead of failing the whole call.
            return new DecompileResultDto(string.Empty, $"{ex.GetType().Name}: {ex.Message}");
        }
    }
}
