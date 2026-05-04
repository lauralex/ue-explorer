using UELib.Core;

namespace UELib.Branch.UE3.RL.Tokens;

/// <summary>
/// RL byte 0x21. Wire format: 1 sub-expression. The binary handler at
/// GNatives[0x21] (sub_7FF6CD2F5A40) dispatches one sub-expression, then
/// calls a property-export helper using the property pointer global
/// `qword_7FF6CF27D780 + 200` (UProperty::PropertyClass) and the value
/// pointer `qword_7FF6CF27D7B0`. The output is FString-shape (8/4/4 swap
/// with `*a3` matches FString::operator=).
///
/// In source this corresponds to <c>string(propRef)</c> — a generic
/// property-to-string conversion using the property's own ExportText.
/// Distinct from <c>EX_PrimitiveCast</c> (0x6B) which dispatches into a
/// sub-table keyed by primitive type byte.
///
/// Was wrongly mapped to <c>DynamicArrayIteratorToken</c> based on output
/// rather than handler shape — the real foreach is dispatched via the
/// extended-native prefix (0x10 0x0A → DynamicArrayIteratorRL,
/// 0x71 0x39 → IteratorTokenRL), not via primary byte 0x21.
/// </summary>
public class StringCastTokenRL : UStruct.UByteCodeDecompiler.Token
{
    public override void Deserialize(IUnrealStream stream)
    {
        DeserializeNext();
    }

    public override string Decompile()
    {
        return $"string({DecompileNext()})";
    }
}
