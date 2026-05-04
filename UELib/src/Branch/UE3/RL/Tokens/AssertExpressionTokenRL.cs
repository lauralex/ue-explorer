using UELib.Core;

namespace UELib.Branch.UE3.RL.Tokens;

/// <summary>
/// RL byte 0x2D. Wire format: u16 (line) + 1 byte (debug flag) + 3 sub-expressions.
/// The binary handler at GNatives[0x2D] (sub_7FF6CD2F06A0) reads a u16 source line,
/// reads a 1-byte flag, then dispatches three sub-expressions in sequence. After
/// all three are evaluated it logs <c>"Assertion failed, line %i"</c> when the
/// debugger predicate is not skipping the assertion. Result is always 0.
///
/// Looks like an RL-extended <c>assert(cond, msg, ctx)</c>: the three sub-exprs
/// are presumably the condition, message string, and a context value. Was wrongly
/// mapped to <c>EventUnsubscribeToken</c> (which reads 2 sub-exprs) — under-consumed
/// and NRE'd because its DeserializeNext expected an InstanceDelegateToken shape.
///
/// Renders as <c>assert(arg0, arg1, arg2)</c> at top-level statement context.
/// </summary>
public class AssertExpressionTokenRL : UStruct.UByteCodeDecompiler.Token
{
    public ushort LineNumber;
    public byte Flag;

    public override void Deserialize(IUnrealStream stream)
    {
        LineNumber = stream.ReadUInt16();
        Decompiler.AlignSize(sizeof(ushort));

        Flag = stream.ReadByte();
        Decompiler.AlignSize(sizeof(byte));

        DeserializeNext();
        DeserializeNext();
        DeserializeNext();
    }

    public override string Decompile()
    {
        Decompiler.MarkSemicolon();
        string a = DecompileNext();
        string b = DecompileNext();
        string c = DecompileNext();
        return $"assert({a}, {b}, {c})";
    }
}
