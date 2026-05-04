using UELib.Core;

namespace UELib.Branch.UE3.RL.Tokens;

/// <summary>
/// RL-specific property setter that evaluates a sub-expression and discards
/// the result. Wire format: 8-byte UProperty* + 1 sub-expression. The binary
/// handler at GNatives[0x36] (sub_7FF6CD2F7250) reads UProperty* (8 bytes),
/// dispatches the sub-expression, then writes 0 to the result slot — i.e.
/// the sub-expression's value is computed for side effects and discarded.
///
/// Likely an RL fusion of EX_DefaultParameter or a property setter where the
/// computed value is stored elsewhere (via the UProperty*) and the
/// expression's nominal value is unused.
///
/// Renders as <c>Property = Expression</c> with the property name from the
/// UProperty* and the sub-expression decompiled normally.
/// </summary>
public class PropertySetterDiscardTokenRL : UStruct.UByteCodeDecompiler.Token
{
    public UField Property;

    public override void Deserialize(IUnrealStream stream)
    {
        Property = stream.ReadObject<UField>();
        Decompiler.AlignObjectSize();

        DeserializeNext();
    }

    public override string Decompile()
    {
        string propertyName = Property != null ? Property.Name.ToString() : "/* unresolved */";
        return $"{propertyName} = {DecompileNext()}";
    }
}
