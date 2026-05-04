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

        // The cooker emits 0x31 with a sub-expression in the function prologue
        // to seed default values for optional parameters. These run at runtime
        // when the caller didn't provide the arg, but in the decompiled source
        // they're implicit (part of the `optional` parameter's signature). The
        // sub-expression is consumed (so the bytes are accounted for) but
        // discarded — rendering it as a top-level statement leaks orphan
        // property/object names like `Location` or `none`.
        DecompileNext();
        return string.Empty;
    }
}
