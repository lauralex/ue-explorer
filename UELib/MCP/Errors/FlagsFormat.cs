using UELib.Flags;

namespace UELib.MCP.Errors;

/// <summary>
/// Safe formatter for <see cref="UnrealFlags{T}"/>. The library's own
/// <c>ToString()</c> trips a Debug.Assert when <c>FlagsMap</c> is null
/// (which happens for some objects that were constructed but never had
/// their flags wired through an <see cref="UELib.Branch.EngineBranch"/>).
/// We fall back to a hex dump in that case so the MCP never throws on
/// a flag-formatting concern.
/// </summary>
internal static class FlagsFormat
{
    public static string Format<T>(UnrealFlags<T> flags) where T : Enum
    {
        if (flags.FlagsMap == null)
        {
            return $"0x{(ulong)flags:X}";
        }

        try { return flags.ToString(); }
        catch { return $"0x{(ulong)flags:X}"; }
    }
}
