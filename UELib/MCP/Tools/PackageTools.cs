using System.ComponentModel;
using ModelContextProtocol.Server;
using UELib.Core;
using UELib.MCP.Errors;
using UELib.MCP.Models;
using UELib.MCP.Session;

namespace UELib.MCP.Tools;

[McpServerToolType]
public sealed class PackageTools(PackageSessionManager sessions)
{
    [McpServerTool(Name = "load_package")]
    [Description("Open an Unreal package (.upk/.u/.umap) and return a handle plus the package summary. " +
                 "If full_init is true (default) all objects are deserialized so list_classes / decompile_* work; " +
                 "set false to read just the summary cheaply. " +
                 "build_target overrides auto-detection (e.g. 'RocketLeague', 'UDK', 'UT2004').")]
    public Task<LoadResultDto> LoadPackage(
        [Description("Absolute path to the package file. Rocket League packages must already be decrypted by RLUPKTool.")] string path,
        [Description("Optional GameBuild.BuildName enum value to force, e.g. 'RocketLeague'. Omit to auto-detect.")] string? build_target = null,
        [Description("If true, fully deserialize all objects (required for class/function inspection). Default true.")] bool full_init = true,
        CancellationToken ct = default)
    {
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
            }

            string handle = sessions.Add(path, package);
            return new LoadResultDto(handle, BuildSummary(handle, path, package));
        }, ct);
    }

    [McpServerTool(Name = "unload_package")]
    [Description("Dispose a previously loaded package and free its file handle. Returns ok=true if a session existed.")]
    public Task<UnloadResultDto> UnloadPackage(
        [Description("Handle returned by load_package.")] string handle,
        CancellationToken ct = default)
    {
        return sessions.RunAsync(() => new UnloadResultDto(sessions.Remove(handle)), ct);
    }

    [McpServerTool(Name = "list_loaded_packages")]
    [Description("List all currently open package sessions with their handles, paths, and detected build names.")]
    public Task<IReadOnlyList<LoadedPackageDto>> ListLoadedPackages(CancellationToken ct = default)
    {
        return sessions.RunAsync<IReadOnlyList<LoadedPackageDto>>(() =>
            sessions.All()
                .Select(s => new LoadedPackageDto(
                    s.Handle,
                    s.Path,
                    s.Package.Build?.Name.ToString() ?? "Unknown"))
                .ToList(),
            ct);
    }

    [McpServerTool(Name = "get_package_summary")]
    [Description("Return summary metadata (version, GUID, build, table counts, flags) for a loaded package.")]
    public Task<PackageSummaryDto> GetPackageSummary(
        [Description("Handle returned by load_package.")] string handle,
        CancellationToken ct = default)
    {
        return sessions.RunAsync(() =>
        {
            var s = sessions.Get(handle);
            return BuildSummary(handle, s.Path, s.Package);
        }, ct);
    }

    [McpServerTool(Name = "list_names")]
    [Description("Page through the package's name table. Returns name strings (with their table index and flags). " +
                 "filter does case-insensitive substring matching.")]
    public Task<IReadOnlyList<NameEntryDto>> ListNames(
        [Description("Handle returned by load_package.")] string handle,
        [Description("Skip this many entries from the start. Default 0.")] int offset = 0,
        [Description("Maximum entries to return (1..2000). Default 200.")] int limit = 200,
        [Description("Optional case-insensitive substring filter applied to the name.")] string? filter = null,
        CancellationToken ct = default)
    {
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

    [McpServerTool(Name = "list_imports")]
    [Description("Page through the package's import table — objects this package references from other packages.")]
    public Task<IReadOnlyList<ImportEntryDto>> ListImports(
        [Description("Handle returned by load_package.")] string handle,
        [Description("Skip this many entries from the start. Default 0.")] int offset = 0,
        [Description("Maximum entries to return (1..2000). Default 200.")] int limit = 200,
        CancellationToken ct = default)
    {
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

    [McpServerTool(Name = "list_exports")]
    [Description("Page through the package's export table — objects defined inside this package. " +
                 "class_filter does case-insensitive exact match against the export's class name (e.g. 'Function', 'Class').")]
    public Task<IReadOnlyList<ExportEntryDto>> ListExports(
        [Description("Handle returned by load_package.")] string handle,
        [Description("Skip this many entries from the start. Default 0.")] int offset = 0,
        [Description("Maximum entries to return (1..2000). Default 200.")] int limit = 200,
        [Description("Optional class-name filter (case-insensitive exact match), e.g. 'Function' or 'Class'.")] string? class_filter = null,
        CancellationToken ct = default)
    {
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

    [McpServerTool(Name = "list_classes")]
    [Description("List every UClass instance in the package. Requires full_init=true on load_package. " +
                 "Returns class name, super class name (if any), and class flags.")]
    public Task<IReadOnlyList<ClassEntryDto>> ListClasses(
        [Description("Handle returned by load_package.")] string handle,
        CancellationToken ct = default)
    {
        return sessions.RunAsync<IReadOnlyList<ClassEntryDto>>(() =>
        {
            var pkg = sessions.Get(handle).Package;
            EnsureInitialized(pkg);

            return pkg.Objects
                .OfType<UClass>()
                .OrderBy(c => c.Name.ToString(), StringComparer.OrdinalIgnoreCase)
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

    internal static int ClampLimit(int requested)
    {
        if (requested <= 0) return 200;
        if (requested > 2000) return 2000;
        return requested;
    }
}
