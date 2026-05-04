namespace UELib.MCP.Models;

// Note: serialised to JSON by the MCP host. Property names are lowercase_snake
// because that's the convention the agent sees in tool schemas.

public sealed record LoadResultDto(string handle, PackageSummaryDto summary);

public sealed record PackageSummaryDto(
    string handle,
    string path,
    uint version,
    ushort licensee_version,
    int engine_version,
    int cooker_version,
    string guid,
    int header_size,
    string package_flags,
    string build_name,
    int generation_count,
    int name_count,
    int export_count,
    int import_count,
    string folder_name,
    bool is_console_cooked,
    uint compression_flags);

public sealed record LoadedPackageDto(string handle, string path, string build_name);

public sealed record UnloadResultDto(bool ok);

public sealed record NameEntryDto(int index, string name, string flags);

public sealed record ImportEntryDto(
    int index,
    string object_name,
    string class_name,
    string class_package,
    string? outer);

public sealed record ExportEntryDto(
    int index,
    string object_name,
    string class_name,
    string? outer,
    string? archetype,
    string object_flags,
    int serial_size,
    int serial_offset);

public sealed record ClassEntryDto(string name, string? super_name, string class_flags);

public sealed record PropertyInfoDto(
    string name,
    string type_class,
    int array_dim,
    string flags);

public sealed record FuncSummaryDto(
    string name,
    ushort native_index,
    string flags,
    int param_count,
    int script_size);

public sealed record ConstInfoDto(string name, string? value);

public sealed record EnumInfoDto(string name, IReadOnlyList<string> values);

public sealed record StructSummaryDto(string name, string struct_flags);

public sealed record StateSummaryDto(string name, string state_flags, int function_count);

public sealed record ClassInfoDto(
    string name,
    string? super_name,
    string? within,
    IReadOnlyList<string> package_imports,
    string class_flags,
    IReadOnlyList<PropertyInfoDto> properties,
    IReadOnlyList<FuncSummaryDto> functions,
    IReadOnlyList<StateSummaryDto> states,
    IReadOnlyList<StructSummaryDto> structs,
    IReadOnlyList<ConstInfoDto> consts,
    IReadOnlyList<EnumInfoDto> enums,
    // Each entry is "section_name:total_count" for sections that were
    // truncated by `member_limit`. Empty when nothing got cut.
    IReadOnlyList<string> truncations);

public sealed record FuncInfoDto(
    string name,
    string class_path,
    ushort native_index,
    byte oper_precedence,
    string flags,
    IReadOnlyList<PropertyInfoDto> @params,
    PropertyInfoDto? return_property,
    int script_size);

public sealed record ObjectInfoDto(
    string name,
    string class_name,
    string path,
    string? outer);

public sealed record DecompileResultDto(
    string source,
    string? warning,
    // Each entry is "source:total_chars" when the source was truncated by `max_chars`.
    // Empty when nothing was cut.
    IReadOnlyList<string> truncations);

public sealed record TokenDto(
    int position,
    int storage_position,
    int size,
    byte opcode_byte,
    string token_type,
    string decompiled);

public sealed record DisassembleResultDto(
    string class_path,
    string function_name,
    int script_size,
    IReadOnlyList<TokenDto> tokens,
    string? warning);
