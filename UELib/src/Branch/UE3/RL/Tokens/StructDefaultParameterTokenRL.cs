using UELib.Core;

namespace UELib.Branch.UE3.RL.Tokens;

/// <summary>
/// RL byte 0x5B — struct-typed default-parameter / ternary-result wrapper.
/// The binary handler at GNatives[0x5B] (sub_7FF6CD308170) reads:
///   * 8 bytes — UStruct* (the struct type)
///   * 1 sub-expression (the "default" or "fallback" value)
///   * u16 — byte size of the conditionally-skipped sub-expression
///   * 1 sub-expression (the "actual" or "computed" value)
///
/// At runtime the struct's properties are compared, and depending on the
/// result either the second sub-expression is dispatched OR u16 bytes are
/// skipped past it. For parsing/decompilation, both sub-expressions must
/// be consumed (the bytes are always present even when the runtime path
/// skips them).
///
/// Renders the second sub-expression (the actual computed value). Was
/// wrongly mapped to <c>VectorConstToken</c> (12 bytes — read the next
/// 12 bytes as 3 floats), producing nonsense like <c>ControllerRef =
/// vect(0, 0, -9.52e21)</c> for a non-vector type.
/// </summary>
public class StructDefaultParameterTokenRL : UStruct.UByteCodeDecompiler.Token
{
    public UStruct StructRef;
    public ushort SkipDistance;

    public override void Deserialize(IUnrealStream stream)
    {
        StructRef = stream.ReadObject<UStruct>();
        Decompiler.AlignObjectSize();

        DeserializeNext();

        SkipDistance = stream.ReadUInt16();
        Decompiler.AlignSize(sizeof(ushort));

        DeserializeNext();
    }

    public override string Decompile()
    {
        DecompileNext();
        return DecompileNext();
    }
}
