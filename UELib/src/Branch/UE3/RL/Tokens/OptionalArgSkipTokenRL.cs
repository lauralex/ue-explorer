using UELib.Core;

namespace UELib.Branch.UE3.RL.Tokens;

/// <summary>
/// RL byte 0x31. Wire format: u16 + conditional sub-expression.
/// The binary handler at GNatives[0x31] (sub_7FF6CD2F0590) reads a u16;
/// if it's <c>0xFFFF</c> the handler returns without consuming further
/// bytes (the optional/skipped case), otherwise it dispatches a single
/// sub-expression. Used by the cooker to flag optional positional args
/// that have been omitted at the call site.
///
/// Renders the sub-expression's text when present, or empty for the
/// skipped case. Was wrongly mapped to the baseline <c>InstanceDelegateToken</c>
/// (which reads UObject* + FName = 16 bytes), causing every occurrence at
/// function entry to NRE during the import-table lookup.
/// </summary>
public class OptionalArgSkipTokenRL : UStruct.UByteCodeDecompiler.Token
{
    public ushort Size;

    public override void Deserialize(IUnrealStream stream)
    {
        Size = stream.ReadUInt16();
        Decompiler.AlignSize(sizeof(ushort));

        if (Size != 0xFFFF)
        {
            DeserializeNext();
        }
    }

    public override string Decompile()
    {
        if (Size == 0xFFFF)
        {
            return string.Empty;
        }

        return DecompileNext();
    }
}
