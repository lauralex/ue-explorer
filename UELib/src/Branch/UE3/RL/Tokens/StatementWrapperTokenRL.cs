using UELib.Core;

namespace UELib.Branch.UE3.RL.Tokens;

/// <summary>
/// RL byte 0x2C. Wire format: 1 sub-expression + 1 trailing byte + optional
/// 0x20 debug-info follow-up. The binary handler at GNatives[0x2C]
/// (sub_7FF6CD308010) dispatches one sub-expression, advances Code by 1
/// extra byte, peeks for 0x20 (debug-info marker), and finally calls a
/// log helper printing the script File. This is a debug-aware statement
/// wrapper, NOT EX_DebugInfo (which is a fixed 12-byte payload + 1 byte
/// opcode = 13 bytes). The previous mapping happened to consume the right
/// number of bytes when the inner sub-expression was 12 bytes long, but
/// silently swallowed the sub-expression in those cases.
///
/// Renders as the sub-expression's own text (passthrough). The trailing
/// byte is consumed but ignored.
/// </summary>
public class StatementWrapperTokenRL : UStruct.UByteCodeDecompiler.Token
{
    public override void Deserialize(IUnrealStream stream)
    {
        DeserializeNext();

        stream.ReadByte();
        Decompiler.AlignSize(sizeof(byte));
    }

    public override string Decompile()
    {
        return DecompileNext();
    }
}
