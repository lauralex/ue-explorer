using UELib.Core;

namespace UELib.Branch.UE3.RL.Tokens;

/// <summary>
/// Wire format: 8-byte UStruct* + 1 sub-expression. Used by GNatives[0x4D]
/// (sub_7FF6CD2F5B80) which allocates a struct buffer of size <c>struct.PropertiesSize</c>,
/// dispatches the sub-expression to populate it, then copies the value via the struct's
/// CopyCompleteValue vtable entry. Likely RL's variant of <c>EX_StructConst</c>-style
/// "build a struct value from a sub-expression" — not the baseline EX_StructConst (which
/// inlines the literal field values), but the runtime-evaluated form used for struct copies
/// produced by the cooker.
/// </summary>
public class StructValueTokenRL : UStruct.UByteCodeDecompiler.Token
{
    public UStruct StructRef;

    public override void Deserialize(IUnrealStream stream)
    {
        StructRef = stream.ReadObject<UStruct>();
        Decompiler.AlignObjectSize();

        DeserializeNext();
    }

    public override string Decompile()
    {
        string structName = StructRef != null ? StructRef.Name.ToString() : "/* unresolved */";
        return $"{structName}({DecompileNext()})";
    }
}
