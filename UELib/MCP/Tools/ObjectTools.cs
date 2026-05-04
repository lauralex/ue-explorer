using System.ComponentModel;
using ModelContextProtocol.Server;
using UELib.Core;
using UELib.Flags;
using UELib.MCP.Errors;
using UELib.MCP.Models;
using UELib.MCP.Session;

namespace UELib.MCP.Tools;

[McpServerToolType]
public sealed class ObjectTools(PackageSessionManager sessions)
{
    [McpServerTool(Name = "find_object",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Look up an object by its full group path (e.g. 'Engine.Actor' or 'TAGame.Car_TA.OnPossessed'). " +
                 "Exact match only — returns null if the path is unknown. " +
                 "For case-insensitive substring search across all objects use `search_objects` instead.")]
    public Task<ObjectInfoDto?> FindObject(
        [Description("Handle returned by load_package.")] string handle,
        [Description("Dotted group path, e.g. 'Engine.Actor' or 'TAGame.Pawn_TA'.")] string group_path,
        CancellationToken ct = default)
    {
        return sessions.RunAsync<ObjectInfoDto?>(() =>
        {
            var pkg = sessions.Get(handle).Package;
            PackageTools.EnsureInitialized(pkg);

            var obj = pkg.FindObjectByGroup(group_path);
            return obj == null ? null : ToObjectInfo(obj);
        }, ct);
    }

    [McpServerTool(Name = "get_class_info",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Return a structured snapshot of a class: super, within, package imports, properties, functions, " +
                 "states, structs, consts, enums. Provide either a bare class name (e.g. 'Pawn_TA') or a dotted path. " +
                 "Requires full_init=true on load_package. " +
                 "Each member section is capped at `member_limit` items. When a section is truncated, its name and " +
                 "total count appear in `truncations` (e.g. 'functions:312'). " +
                 "For full enumeration of a single section, page through `list_exports` with class_filter, or call " +
                 "`get_function_info`/`decompile_function` for specific names.")]
    public Task<ClassInfoDto> GetClassInfo(
        [Description("Handle returned by load_package.")] string handle,
        [Description("Class name (e.g. 'Actor') or dotted path (e.g. 'Engine.Actor').")] string class_name,
        [Description("Per-section cap on returned member arrays (1..1000). Default 100. " +
                     "Sections that hit the cap are listed in `truncations`.")] int member_limit = 100,
        CancellationToken ct = default)
    {
        return sessions.RunAsync(() =>
        {
            var pkg = sessions.Get(handle).Package;
            PackageTools.EnsureInitialized(pkg);
            var cls = ResolveClass(pkg, class_name);
            return BuildClassInfo(cls, ClampMemberLimit(member_limit));
        }, ct);
    }

    [McpServerTool(Name = "get_function_info",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Return signature, flags, native index, parameters, and return type for a single function on a class. " +
                 "class_path may be a bare name or dotted path; function_name is the method's UnrealScript name. " +
                 "For the function body use `decompile_function`; for the raw bytecode tokens use `disassemble_function`.")]
    public Task<FuncInfoDto> GetFunctionInfo(
        [Description("Handle returned by load_package.")] string handle,
        [Description("Class name or dotted path (e.g. 'TAGame.Pawn_TA').")] string class_path,
        [Description("UnrealScript function name (case-sensitive match against UFunction.Name).")] string function_name,
        CancellationToken ct = default)
    {
        return sessions.RunAsync(() =>
        {
            var pkg = sessions.Get(handle).Package;
            PackageTools.EnsureInitialized(pkg);
            var cls = ResolveClass(pkg, class_path);
            var fn = ResolveFunction(cls, function_name);
            return BuildFunctionInfo(cls, fn);
        }, ct);
    }

    [McpServerTool(Name = "search_objects",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Case-insensitive substring search across object names in a loaded package. " +
                 "Use this when you don't know the exact class path. " +
                 "For exact lookup by full dotted path use `find_object` instead. " +
                 "Capped at max_results (default 50).")]
    public Task<IReadOnlyList<ObjectInfoDto>> SearchObjects(
        [Description("Handle returned by load_package.")] string handle,
        [Description("Substring to match against UObject.Name (case-insensitive).")] string query,
        [Description("Maximum number of results (1..500). Default 50.")] int max_results = 50,
        CancellationToken ct = default)
    {
        return sessions.RunAsync<IReadOnlyList<ObjectInfoDto>>(() =>
        {
            var pkg = sessions.Get(handle).Package;
            PackageTools.EnsureInitialized(pkg);

            if (string.IsNullOrEmpty(query))
            {
                throw McpErrors.InvalidParam("query must be a non-empty substring.");
            }

            int cap = max_results <= 0 ? 50 : Math.Min(max_results, 500);

            return pkg.Objects
                .Where(o => o is { Name: not null } &&
                            o.Name.ToString().Contains(query, StringComparison.OrdinalIgnoreCase))
                .Take(cap)
                .Select(ToObjectInfo)
                .ToList();
        }, ct);
    }

    // -------- helpers --------

    internal static UClass ResolveClass(UnrealPackage pkg, string nameOrPath)
    {
        if (string.IsNullOrWhiteSpace(nameOrPath))
        {
            throw McpErrors.InvalidParam("class name/path must be non-empty.");
        }

        if (nameOrPath.Contains('.', StringComparison.Ordinal))
        {
            if (pkg.FindObjectByGroup(nameOrPath) is UClass cls) return cls;
        }

        var match = pkg.Objects
            .OfType<UClass>()
            .FirstOrDefault(c => string.Equals(c.Name.ToString(), nameOrPath, StringComparison.Ordinal));

        return match ?? throw McpErrors.NotFound("Class", nameOrPath);
    }

    internal static UFunction ResolveFunction(UClass cls, string functionName)
    {
        if (string.IsNullOrWhiteSpace(functionName))
        {
            throw McpErrors.InvalidParam("function_name must be non-empty.");
        }

        var fn = cls.EnumerateFields<UFunction>()
            .FirstOrDefault(f => string.Equals(f.Name.ToString(), functionName, StringComparison.Ordinal));

        return fn ?? throw McpErrors.NotFound($"Method on '{cls.Name}'", functionName);
    }

    internal static ObjectInfoDto ToObjectInfo(UObject o)
    {
        return new ObjectInfoDto(
            name: o.Name?.ToString() ?? string.Empty,
            class_name: o.Class?.Name.ToString() ?? "Class",
            path: o.GetReferencePath(),
            outer: o.Outer?.GetReferencePath());
    }

    private static int ClampMemberLimit(int requested)
    {
        if (requested <= 0) return 100;
        if (requested > 1000) return 1000;
        return requested;
    }

    private static (List<T> Items, int Total, bool Truncated) Cap<T>(IEnumerable<T> source, int limit)
    {
        var all = source.ToList();
        if (all.Count <= limit) return (all, all.Count, false);
        return (all.Take(limit).ToList(), all.Count, true);
    }

    private static ClassInfoDto BuildClassInfo(UClass cls, int memberLimit)
    {
        var truncations = new List<string>();

        var (properties, propsTotal, propsTrunc) = Cap(
            cls.EnumerateFields<UProperty>().Select(BuildPropertyInfo), memberLimit);
        if (propsTrunc) truncations.Add($"properties:{propsTotal}");

        var (functions, funcsTotal, funcsTrunc) = Cap(
            cls.EnumerateFields<UFunction>().Select(MakeFuncSummary), memberLimit);
        if (funcsTrunc) truncations.Add($"functions:{funcsTotal}");

        var (states, statesTotal, statesTrunc) = Cap(
            cls.EnumerateFields<UState>()
                .Where(s => s is not UClass)
                .Select(s => new StateSummaryDto(
                    name: s.Name.ToString(),
                    state_flags: FlagsFormat.Format(s.StateFlags),
                    function_count: s.EnumerateFields<UFunction>().Count())),
            memberLimit);
        if (statesTrunc) truncations.Add($"states:{statesTotal}");

        var (structs, structsTotal, structsTrunc) = Cap(
            cls.EnumerateFields<UStruct>()
                .Where(s => s.IsPureStruct())
                .Select(s => new StructSummaryDto(s.Name.ToString(), FlagsFormat.Format(s.StructFlags))),
            memberLimit);
        if (structsTrunc) truncations.Add($"structs:{structsTotal}");

        var (consts, constsTotal, constsTrunc) = Cap(
            cls.EnumerateFields<UConst>()
                .Select(c => new ConstInfoDto(c.Name.ToString(), TryGetConstValue(c))),
            memberLimit);
        if (constsTrunc) truncations.Add($"consts:{constsTotal}");

        var (enums, enumsTotal, enumsTrunc) = Cap(
            cls.EnumerateFields<UEnum>()
                .Select(e => new EnumInfoDto(
                    e.Name.ToString(),
                    e.Names?.Select(n => n.ToString()).ToList() ?? new List<string>())),
            memberLimit);
        if (enumsTrunc) truncations.Add($"enums:{enumsTotal}");

        var packageImports = cls.PackageImportNames?.Select(n => n.ToString()).ToList()
                             ?? new List<string>();

        return new ClassInfoDto(
            name: cls.Name.ToString(),
            super_name: (cls.Super as UClass)?.Name.ToString(),
            within: cls.Within?.Name.ToString(),
            package_imports: packageImports,
            class_flags: FlagsFormat.Format(cls.ClassFlags),
            properties: properties,
            functions: functions,
            states: states,
            structs: structs,
            consts: consts,
            enums: enums,
            truncations: truncations);
    }

    private static FuncSummaryDto MakeFuncSummary(UFunction f) =>
        new(
            name: f.Name.ToString(),
            native_index: f.NativeToken,
            flags: FlagsFormat.Format(f.FunctionFlags),
            param_count: f.EnumerateFields<UProperty>().Count(p => p.IsParm()),
            script_size: f.ScriptSize);

    private static FuncInfoDto BuildFunctionInfo(UClass cls, UFunction fn)
    {
        var parms = fn.EnumerateFields<UProperty>()
            .Where(p => p.IsParm() && !p.PropertyFlags.HasFlag(PropertyFlag.ReturnParm))
            .Select(BuildPropertyInfo)
            .ToList();

        var ret = fn.EnumerateFields<UProperty>()
            .FirstOrDefault(p => p.PropertyFlags.HasFlag(PropertyFlag.ReturnParm));

        return new FuncInfoDto(
            name: fn.Name.ToString(),
            class_path: cls.GetReferencePath(),
            native_index: fn.NativeToken,
            oper_precedence: fn.OperPrecedence,
            flags: FlagsFormat.Format(fn.FunctionFlags),
            @params: parms,
            return_property: ret == null ? null : BuildPropertyInfo(ret),
            script_size: fn.ScriptSize);
    }

    internal static PropertyInfoDto BuildPropertyInfo(UProperty p)
    {
        return new PropertyInfoDto(
            name: p.Name.ToString(),
            type_class: p.Class?.Name.ToString() ?? p.GetType().Name,
            array_dim: p.ArrayDim,
            flags: FlagsFormat.Format(p.PropertyFlags));
    }

    private static string? TryGetConstValue(UConst c)
    {
        try
        {
            // UConst's literal text lives on a property whose name has shifted across forks.
            // Use reflection so we don't take a hard dep on a name that may differ by branch.
            var prop = c.GetType().GetProperty("Value");
            return prop?.GetValue(c)?.ToString();
        }
        catch
        {
            return null;
        }
    }
}
