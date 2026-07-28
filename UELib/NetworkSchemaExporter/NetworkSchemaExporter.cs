using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using UELib.Core;
using UELib.Flags;

namespace UELib.NetworkSchemaExporterTool;

/// <summary>
/// Exports the version-sensitive package map and class net-field ordering used
/// by UE3 replication. The output is consumed by NebulaProxy; it deliberately
/// contains metadata only and never modifies a package.
/// </summary>
internal static class NetworkSchemaExporter
{
    private const string FormatName = "nebula-ue3-network-schema";
    private const int FormatVersion = 1;

    public static int Run(string outputPath, IEnumerable<string> packagePaths)
    {
        var paths = packagePaths.Select(Path.GetFullPath).ToArray();
        if (paths.Length == 0)
        {
            Console.Error.WriteLine("[network-schema] at least one package is required");
            return 1;
        }

        foreach (var path in paths)
        {
            if (!File.Exists(path))
            {
                Console.Error.WriteLine("[network-schema] package not found: {0}", path);
                return 2;
            }
        }

        var loadedPackages = new List<(UnrealPackage Package, string Path)>(paths.Length);
        foreach (var path in paths)
        {
            Console.WriteLine("[network-schema] load {0}", path);
            var package = UnrealLoader.LoadPackage(path, UnrealPackage.GameBuild.BuildName.RocketLeague);
            package.InitializePackage(UnrealPackage.InitFlags.All);
            loadedPackages.Add((package, path));
        }

        // The first consumer is the external trajectory provider. Keep only the
        // Ball/Car class chains and matching actor archetypes; package generation
        // counts still preserve the exact global package-map index arithmetic.
        var selectedClassPaths = SelectTrajectoryClassPaths(loadedPackages.Select(item => item.Package));
        var packages = loadedPackages
            .Select(item => ExportPackage(item.Package, item.Path, selectedClassPaths))
            .ToList();

        var document = new NetworkSchema
        {
            Format = FormatName,
            Version = FormatVersion,
            GeneratedUtc = DateTimeOffset.UtcNow,
            Packages = packages,
        };

        outputPath = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            WriteIndented = true,
        };
        File.WriteAllText(outputPath, JsonSerializer.Serialize(document, options));

        Console.WriteLine(
            "[network-schema] wrote {0} packages, {1} net objects, {2} classes to {3}",
            packages.Count,
            packages.Sum(item => item.NetObjects.Count),
            packages.Sum(item => item.Classes.Count),
            outputPath);
        return 0;
    }

    private static HashSet<string> SelectTrajectoryClassPaths(IEnumerable<UnrealPackage> packages)
    {
        var classesByPath = packages
            .SelectMany(package => package.Objects.OfType<UClass>())
            .Where(item => item.PackageIndex.IsExport)
            .GroupBy(item => NormalizePath(item.GetPath()), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        var selected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pending = new Queue<string>(
            classesByPath
                .Where(item => item.Value.Name == "Ball_TA" || item.Value.Name == "Car_TA")
                .Select(item => item.Key));

        while (pending.TryDequeue(out var path))
        {
            if (!selected.Add(path) || !classesByPath.TryGetValue(path, out var current))
            {
                continue;
            }

            if (current.Super != null)
            {
                pending.Enqueue(NormalizePath(current.Super.GetPath()));
            }
        }
        return selected;
    }

    private static PackageSchema ExportPackage(
        UnrealPackage package,
        string sourcePath,
        HashSet<string> selectedClassPaths)
    {
        var netObjects = package.Objects
            .Where(item => item.NetIndex >= 0 && IsTrajectoryNetObject(item, selectedClassPaths))
            .OrderBy(item => item.NetIndex)
            .Select(ExportObject)
            .ToList();

        var classes = package.Objects
            .OfType<UClass>()
            .Where(item =>
                item.PackageIndex.IsExport &&
                selectedClassPaths.Contains(NormalizePath(item.GetPath())))
            .OrderBy(item => item.GetPath(), StringComparer.OrdinalIgnoreCase)
            .Select(ExportClass)
            .ToList();

        return new PackageSchema
        {
            Name = NormalizePackageName(package.PackageName),
            FileName = Path.GetFileName(sourcePath),
            Guid = package.Summary.Guid.ToString(),
            PackageVersion = package.Version,
            LicenseeVersion = package.LicenseeVersion,
            Build = package.Build.Name.ToString(),
            GenerationNetObjectCounts =
                package.Summary.Generations.Select(item => item.NetObjectCount).ToArray(),
            NetObjects = netObjects,
            Classes = classes,
        };
    }

    private static bool IsTrajectoryNetObject(UObject item, HashSet<string> selectedClassPaths)
    {
        if (item is UClass classObject)
        {
            return selectedClassPaths.Contains(NormalizePath(classObject.GetPath()));
        }

        for (var objectClass = item.Class; objectClass != null; objectClass = objectClass.Super as UClass)
        {
            if (objectClass.Name == "Ball_TA" ||
                objectClass.Name == "Car_TA" ||
                selectedClassPaths.Contains(NormalizePath(objectClass.GetPath())))
            {
                return true;
            }
        }
        return false;
    }

    private static NetObjectSchema ExportObject(UObject item)
    {
        return new NetObjectSchema
        {
            NetIndex = item.NetIndex,
            PackageIndex = item.PackageIndex.Index,
            Name = item.Name.ToString(),
            Path = NormalizePath(item.GetPath()),
            ReferencePath = item.GetReferencePath(),
            ClassName = GetObjectClassName(item),
            IsClassDefaultObject = item.ObjectFlags.HasFlag(ObjectFlag.ClassDefaultObject),
            IsArchetypeObject = item.ObjectFlags.HasFlag(ObjectFlag.ArchetypeObject),
        };
    }

    private static ClassSchema ExportClass(UClass item)
    {
        var fields = item.EnumerateFields()
            .Where(IsLocalNetField)
            .OrderBy(field => field.NetIndex)
            .Select(ExportField)
            .ToList();

        return new ClassSchema
        {
            Name = item.Name.ToString(),
            Path = NormalizePath(item.GetPath()),
            PackageIndex = item.PackageIndex.Index,
            NetIndex = item.NetIndex,
            SuperPath = item.Super == null ? null : NormalizePath(item.Super.GetPath()),
            DefaultObject = item.Default == null ? null : ExportObject(item.Default),
            LocalNetFields = fields,
        };
    }

    private static bool IsLocalNetField(UField field)
    {
        if (field is UProperty property)
        {
            return property.PropertyFlags.HasFlag(PropertyFlag.Net);
        }

        if (field is UFunction function)
        {
            return function.FunctionFlags.HasFlag(FunctionFlag.Net) && function.Super is not UFunction;
        }

        return false;
    }

    private static NetFieldSchema ExportField(UField field)
    {
        if (field is UProperty property)
        {
            return new NetFieldSchema
            {
                Name = property.Name.ToString(),
                Path = NormalizePath(property.GetPath()),
                NetIndex = property.NetIndex,
                PackageIndex = property.PackageIndex.Index,
                Kind = "property",
                Property = ExportProperty(property),
            };
        }

        var function = (UFunction)field;
        return new NetFieldSchema
        {
            Name = function.Name.ToString(),
            Path = NormalizePath(function.GetPath()),
            NetIndex = function.NetIndex,
            PackageIndex = function.PackageIndex.Index,
            Kind = "function",
            FunctionFlags = (ulong)function.FunctionFlags,
            Parameters = function.EnumerateFields<UProperty>()
                .Where(property => property.PropertyFlags.HasFlag(PropertyFlag.Parm))
                .Select(ExportProperty)
                .ToList(),
        };
    }

    private static PropertySchema ExportProperty(UProperty property)
    {
        var result = new PropertySchema
        {
            Name = property.Name.ToString(),
            Path = NormalizePath(property.GetPath()),
            Kind = GetObjectClassName(property),
            FriendlyType = property.GetFriendlyType(),
            ArrayDim = property.ArrayDim,
            ElementSize = property.ElementSize,
            PropertyFlags = (ulong)property.PropertyFlags,
            NetIndex = property.NetIndex,
            PackageIndex = property.PackageIndex.Index,
        };

        switch (property)
        {
            case UStructProperty structProperty:
                result.TypePath = structProperty.Struct == null
                    ? null
                    : NormalizePath(structProperty.Struct.GetPath());
                break;
            case UClassProperty classProperty:
                result.TypePath = classProperty.MetaClass == null
                    ? null
                    : NormalizePath(classProperty.MetaClass.GetPath());
                result.ObjectPath = classProperty.Object == null
                    ? null
                    : NormalizePath(classProperty.Object.GetPath());
                break;
            case UObjectProperty objectProperty:
                result.TypePath = objectProperty.Object == null
                    ? null
                    : NormalizePath(objectProperty.Object.GetPath());
                break;
            case UByteProperty byteProperty:
                result.TypePath = byteProperty.Enum == null
                    ? null
                    : NormalizePath(byteProperty.Enum.GetPath());
                break;
            case UArrayProperty arrayProperty:
                result.Inner = arrayProperty.InnerProperty == null
                    ? null
                    : ExportProperty(arrayProperty.InnerProperty);
                break;
            case UMapProperty mapProperty:
                result.Key = mapProperty.KeyProperty == null
                    ? null
                    : ExportProperty(mapProperty.KeyProperty);
                result.Value = mapProperty.ValueProperty == null
                    ? null
                    : ExportProperty(mapProperty.ValueProperty);
                break;
            case UDelegateProperty delegateProperty:
                result.TypePath = delegateProperty.Function == null
                    ? null
                    : NormalizePath(delegateProperty.Function.GetPath());
                break;
        }

        return result;
    }

    private static string NormalizePackageName(string name)
    {
        const string suffix = "_decrypted";
        return name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
            ? name[..^suffix.Length]
            : name;
    }

    private static string GetObjectClassName(UObject item)
    {
        return item.ImportTable != null
            ? item.ImportTable.ClassName
            : item.Class?.Name ?? "Class";
    }

    private static string NormalizePath(string path)
    {
        var separator = path.IndexOf('.');
        if (separator < 0)
        {
            return NormalizePackageName(path);
        }
        return NormalizePackageName(path[..separator]) + path[separator..];
    }

    private sealed class NetworkSchema
    {
        public string Format { get; init; } = "";
        public int Version { get; init; }
        public DateTimeOffset GeneratedUtc { get; init; }
        public List<PackageSchema> Packages { get; init; } = [];
    }

    private sealed class PackageSchema
    {
        public string Name { get; init; } = "";
        public string FileName { get; init; } = "";
        public string Guid { get; init; } = "";
        public uint PackageVersion { get; init; }
        public ushort LicenseeVersion { get; init; }
        public string Build { get; init; } = "";
        public int[] GenerationNetObjectCounts { get; init; } = [];
        public List<NetObjectSchema> NetObjects { get; init; } = [];
        public List<ClassSchema> Classes { get; init; } = [];
    }

    private sealed class NetObjectSchema
    {
        public int NetIndex { get; init; }
        public int PackageIndex { get; init; }
        public string Name { get; init; } = "";
        public string Path { get; init; } = "";
        public string ReferencePath { get; init; } = "";
        public string ClassName { get; init; } = "";
        public bool IsClassDefaultObject { get; init; }
        public bool IsArchetypeObject { get; init; }
    }

    private sealed class ClassSchema
    {
        public string Name { get; init; } = "";
        public string Path { get; init; } = "";
        public int PackageIndex { get; init; }
        public int NetIndex { get; init; }
        public string? SuperPath { get; init; }
        public NetObjectSchema? DefaultObject { get; init; }
        public List<NetFieldSchema> LocalNetFields { get; init; } = [];
    }

    private sealed class NetFieldSchema
    {
        public string Name { get; init; } = "";
        public string Path { get; init; } = "";
        public int NetIndex { get; init; }
        public int PackageIndex { get; init; }
        public string Kind { get; init; } = "";
        public ulong FunctionFlags { get; init; }
        public PropertySchema? Property { get; init; }
        public List<PropertySchema>? Parameters { get; init; }
    }

    private sealed class PropertySchema
    {
        public string Name { get; init; } = "";
        public string Path { get; init; } = "";
        public string Kind { get; init; } = "";
        public string FriendlyType { get; init; } = "";
        public int ArrayDim { get; init; }
        public ushort ElementSize { get; init; }
        public ulong PropertyFlags { get; init; }
        public int NetIndex { get; init; }
        public int PackageIndex { get; init; }
        public string? TypePath { get; set; }
        public string? ObjectPath { get; set; }
        public PropertySchema? Inner { get; set; }
        public PropertySchema? Key { get; set; }
        public PropertySchema? Value { get; set; }
    }
}
