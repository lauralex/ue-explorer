using UELib.Core;

namespace UELib.Branch.UE3.RL.Tokens;

/// <summary>
/// RL byte 0x61 — UnrealScript <c>new (Outer, Name, Flags, Class, Template)</c>
/// constructor expression. The binary handler at GNatives[0x61]
/// (sub_7FF6CD3082F0) dispatches **five** sub-expressions in sequence and
/// logs <c>"No class passed to 'new' operator"</c> if no class arg was
/// provided.
///
/// Wire format:
///   0x61 + 5 sub-exprs (Outer, Name, Flags, Class, Template)
///
/// Renders as <c>new(Outer, Name, Flags, Template) Class</c> matching
/// UnrealScript syntax. Was wrongly mapped to <c>EmptyDelegateToken</c>
/// which consumes 0 bytes after the opcode — every <c>new(...)</c>
/// occurrence rendered as <c>none</c> with the 5 args orphaned as
/// top-level statements (visible in PRI_TA.PostBeginPlay's
/// <c>CarDistanceTracker = none; self Class'X'</c> pattern).
/// </summary>
public class NewExpressionTokenRL : UStruct.UByteCodeDecompiler.Token
{
    public override void Deserialize(IUnrealStream stream)
    {
        DeserializeNext();
        DeserializeNext();
        DeserializeNext();
        DeserializeNext();
        DeserializeNext();
    }

    public override string Decompile()
    {
        string outer = DecompileNext();
        string name = DecompileNext();
        string flags = DecompileNext();
        string @class = DecompileNext();
        string template = DecompileNext();

        // UnrealScript syntax: `new(Outer, Name, Flags, Template) Class`.
        // Args after Outer are typically empty / "none" when not specified.
        return $"new({outer}, {name}, {flags}, {template}) {@class}";
    }
}
