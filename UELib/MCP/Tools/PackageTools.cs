using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using ModelContextProtocol.Server;
using UELib.Core;
using UELib.MCP.Errors;
using UELib.MCP.Models;
using UELib.MCP.Session;

namespace UELib.MCP.Tools;

[McpServerToolType]
public sealed class PackageTools(PackageSessionManager sessions)
{
    [McpServerTool(Name = "load_package", UseStructuredContent = true,
        ReadOnly = true, Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("Open an Unreal package (.upk/.u/.umap) and return a handle plus the package summary. " +
                 "If full_init is true (default) all objects are deserialized so list_classes / decompile_* work; " +
                 "set false to read just the summary cheaply. " +
                 "build_target overrides auto-detection. " +
                 "Calling twice on the same path returns two distinct handles — both work independently. " +
                 "After load, use `list_classes` for the class catalogue or `get_class_info` for a specific class.")]
    public Task<LoadResultDto> LoadPackage(
        [Description("Absolute path to the package file. Rocket League packages must already be decrypted by RLUPKTool.")] string path,
        [Description("Optional UnrealPackage.GameBuild.BuildName enum value to force build detection. " +
                     "Common values: 'RocketLeague', 'UDK', 'UT2004', 'UT2003', 'UT', 'UT3', 'BioShock', " +
                     "'MOH', 'XCOM2', 'Borderlands', 'Borderlands2', 'MassEffect3', 'GoW2'. " +
                     "Omit (default) to auto-detect from the package version.")] string? build_target = null,
        [Description("If true, fully deserialize all objects (required for class/function inspection). Default true.")] bool full_init = true,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return sessions.RunAsync<LoadResultDto>(() =>
        {
            if (!File.Exists(path))
            {
                throw McpErrors.FileMissing(path);
            }

            UnrealPackage.GameBuild.BuildName buildName = UnrealPackage.GameBuild.BuildName.Unset;
            if (!string.IsNullOrWhiteSpace(build_target))
            {
                if (!Enum.TryParse(build_target, ignoreCase: true, out buildName))
                {
                    throw McpErrors.InvalidParam(
                        $"Unknown build_target '{build_target}'. Use a valid UnrealPackage.GameBuild.BuildName value.");
                }
            }

            UnrealPackage package;
            try
            {
                package = UnrealLoader.LoadPackage(path, buildName);
            }
            catch (Exception ex)
            {
                throw McpErrors.Wrap("LoadPackage", ex);
            }

            if (full_init)
            {
                try
                {
                    package.InitializePackage();
                }
                catch (Exception ex)
                {
                    package.Dispose();
                    throw McpErrors.Wrap("InitializePackage", ex);
                }

                // Add this package's UFunctions to the global native-name index so any subsequent
                // decompile that hits a __NFUN_NNN__ placeholder can resolve it against natives
                // declared here. RL natives are split across Engine.upk + Core.upk; loading both
                // gives full coverage of non-extended natives.
                UStruct.UByteCodeDecompiler.NativeFunctionToken.IndexPackageNatives(package);
            }

            string handle = sessions.Add(path, package);
            return new LoadResultDto(handle, BuildSummary(handle, path, package));
        }, ct);
    }

    [McpServerTool(Name = "unload_package", UseStructuredContent = true,
        ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false)]
    [Description("Dispose a previously loaded package and free its file handle. Returns ok=true if a session existed, " +
                 "ok=false if the handle was unknown. Calling twice is safe (no-op on the second call).")]
    public Task<UnloadResultDto> UnloadPackage(
        [Description("Handle returned by load_package.")] string handle,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return sessions.RunAsync(() => new UnloadResultDto(sessions.Remove(handle)), ct);
    }

    [McpServerTool(Name = "list_loaded_packages", UseStructuredContent = true,
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("List all currently open package sessions with their handles, paths, and detected build names. " +
                 "Useful for recovering handles after a Claude Code restart — the MCP server outlives client sessions.")]
    public Task<IReadOnlyList<LoadedPackageDto>> ListLoadedPackages(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return sessions.RunAsync<IReadOnlyList<LoadedPackageDto>>(() =>
            sessions.All()
                .Select(s => new LoadedPackageDto(
                    s.Handle,
                    s.Path,
                    s.Package.Build?.Name.ToString() ?? "Unknown"))
                .ToList(),
            ct);
    }

    [McpServerTool(Name = "get_package_summary", UseStructuredContent = true,
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Return summary metadata (version, GUID, build, table counts, flags) for a loaded package. " +
                 "Cheap — does not require full_init=true.")]
    public Task<PackageSummaryDto> GetPackageSummary(
        [Description("Handle returned by load_package.")] string handle,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return sessions.RunAsync(() =>
        {
            var s = sessions.Get(handle);
            return BuildSummary(handle, s.Path, s.Package);
        }, ct);
    }

    [McpServerTool(Name = "list_names", UseStructuredContent = true,
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Page through the package's name table. Returns name strings (with their table index and flags). " +
                 "filter does case-insensitive substring matching. " +
                 "For object lookup by name use `search_objects`; this tool only sees the raw name table.")]
    public Task<IReadOnlyList<NameEntryDto>> ListNames(
        [Description("Handle returned by load_package.")] string handle,
        [Range(0, int.MaxValue), Description("Skip this many entries from the start. Default 0.")] int offset = 0,
        [Range(1, 2000), Description("Maximum entries to return (1..2000). Default 200.")] int limit = 200,
        [Description("Optional case-insensitive substring filter applied to the name.")] string? filter = null,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return sessions.RunAsync<IReadOnlyList<NameEntryDto>>(() =>
        {
            var pkg = sessions.Get(handle).Package;
            limit = ClampLimit(limit);

            IEnumerable<UNameTableItem> source = pkg.Names;
            if (!string.IsNullOrEmpty(filter))
            {
                source = source.Where(n => n.Name.Contains(filter, StringComparison.OrdinalIgnoreCase));
            }

            return source
                .Skip(Math.Max(0, offset))
                .Take(limit)
                .Select(n => new NameEntryDto(n.Index, n.Name, n.Flags.ToString("X")))
                .ToList();
        }, ct);
    }

    [McpServerTool(Name = "list_imports", UseStructuredContent = true,
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Page through the package's import table — objects this package references from other packages.")]
    public Task<IReadOnlyList<ImportEntryDto>> ListImports(
        [Description("Handle returned by load_package.")] string handle,
        [Range(0, int.MaxValue), Description("Skip this many entries from the start. Default 0.")] int offset = 0,
        [Range(1, 2000), Description("Maximum entries to return (1..2000). Default 200.")] int limit = 200,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return sessions.RunAsync<IReadOnlyList<ImportEntryDto>>(() =>
        {
            var pkg = sessions.Get(handle).Package;
            limit = ClampLimit(limit);

            return pkg.Imports
                .Skip(Math.Max(0, offset))
                .Take(limit)
                .Select(i => new ImportEntryDto(
                    i.Index,
                    i.ObjectName.ToString(),
                    i.ClassName.ToString(),
                    i.ClassPackageName.ToString(),
                    i.Outer?.GetPath()))
                .ToList();
        }, ct);
    }

    [McpServerTool(Name = "list_exports", UseStructuredContent = true,
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Page through the package's export table — objects defined inside this package. " +
                 "class_filter does case-insensitive exact match against the export's class name (e.g. 'Function', 'Class'). " +
                 "Returns the raw export-table view. For an enriched class snapshot use `get_class_info`.")]
    public Task<IReadOnlyList<ExportEntryDto>> ListExports(
        [Description("Handle returned by load_package.")] string handle,
        [Range(0, int.MaxValue), Description("Skip this many entries from the start. Default 0.")] int offset = 0,
        [Range(1, 2000), Description("Maximum entries to return (1..2000). Default 200.")] int limit = 200,
        [Description("Optional class-name filter (case-insensitive exact match), e.g. 'Function' or 'Class'.")] string? class_filter = null,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return sessions.RunAsync<IReadOnlyList<ExportEntryDto>>(() =>
        {
            var pkg = sessions.Get(handle).Package;
            limit = ClampLimit(limit);

            IEnumerable<UExportTableItem> source = pkg.Exports;
            if (!string.IsNullOrEmpty(class_filter))
            {
                source = source.Where(e => string.Equals(
                    e.Class?.ObjectName.ToString() ?? "Class",
                    class_filter,
                    StringComparison.OrdinalIgnoreCase));
            }

            return source
                .Skip(Math.Max(0, offset))
                .Take(limit)
                .Select(e => new ExportEntryDto(
                    e.Index,
                    e.ObjectName.ToString(),
                    e.Class?.ObjectName.ToString() ?? "Class",
                    e.Outer?.GetPath(),
                    e.Archetype?.GetPath(),
                    e.ObjectFlags.ToString("X"),
                    e.SerialSize,
                    e.SerialOffset))
                .ToList();
        }, ct);
    }

    [McpServerTool(Name = "list_classes", UseStructuredContent = true,
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Page through every UClass instance in the package, sorted by name. " +
                 "Requires full_init=true on load_package. " +
                 "Returns class name, super class name (if any), and class flags. " +
                 "For a structured snapshot of a single class (its properties, functions, etc.) use `get_class_info`.")]
    public Task<IReadOnlyList<ClassEntryDto>> ListClasses(
        [Description("Handle returned by load_package.")] string handle,
        [Range(0, int.MaxValue), Description("Skip this many classes from the start. Default 0.")] int offset = 0,
        [Range(1, 2000), Description("Maximum classes to return (1..2000). Default 500.")] int limit = 500,
        [Description("Optional case-insensitive substring filter applied to the class name.")] string? filter = null,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return sessions.RunAsync<IReadOnlyList<ClassEntryDto>>(() =>
        {
            var pkg = sessions.Get(handle).Package;
            EnsureInitialized(pkg);
            limit = ClampLimit(limit, fallback: 500);

            IEnumerable<UClass> source = pkg.Objects.OfType<UClass>();
            if (!string.IsNullOrEmpty(filter))
            {
                source = source.Where(c => c.Name.ToString().Contains(filter, StringComparison.OrdinalIgnoreCase));
            }

            return source
                .OrderBy(c => c.Name.ToString(), StringComparer.OrdinalIgnoreCase)
                .Skip(Math.Max(0, offset))
                .Take(limit)
                .Select(c => new ClassEntryDto(
                    c.Name.ToString(),
                    (c.Super as UClass)?.Name.ToString(),
                    FlagsFormat.Format(c.ClassFlags)))
                .ToList();
        }, ct);
    }

    private static PackageSummaryDto BuildSummary(string handle, string path, UnrealPackage pkg)
    {
        var s = pkg.Summary;
        return new PackageSummaryDto(
            handle: handle,
            path: path,
            version: s.Version,
            licensee_version: s.LicenseeVersion,
            engine_version: s.EngineVersion,
            cooker_version: s.CookerVersion,
            guid: s.Guid.ToString(),
            header_size: s.HeaderSize,
            package_flags: FlagsFormat.Format(s.PackageFlags),
            build_name: pkg.Build?.Name.ToString() ?? "Unknown",
            generation_count: s.Generations?.Count ?? 0,
            name_count: pkg.Names?.Count ?? 0,
            export_count: pkg.Exports?.Count ?? 0,
            import_count: pkg.Imports?.Count ?? 0,
            folder_name: s.FolderName ?? string.Empty,
            is_console_cooked: pkg.IsConsoleCooked(),
            compression_flags: s.CompressionFlags);
    }

    internal static void EnsureInitialized(UnrealPackage pkg)
    {
        if (pkg.Objects == null || pkg.Objects.Count == 0)
        {
            throw McpErrors.InvalidParam(
                "Package was loaded with full_init=false, so its objects are not constructed. " +
                "Call load_package again with full_init=true.");
        }
    }

    internal static int ClampLimit(int requested, int fallback = 200)
    {
        if (requested <= 0) return fallback;
        if (requested > 2000) return 2000;
        return requested;
    }
}
