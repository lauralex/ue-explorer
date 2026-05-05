using System;
using UELib.Core;

namespace UELib.Branch.UE3.RL.Tokens;

/// <summary>
/// RL byte 0x28 — defensive variant of <see cref="UStruct.UByteCodeDecompiler.ContextToken"/>.
///
/// Wire format (verified against UStruct::SerializeExpr case 0x28 in
/// sub_7FF6CD38C840 and the GNatives runtime handler at 0x7FF6CD2F5CB0):
///   * 1 byte (skip byte)
///   * 1 sub (receiver — the object expression before the dot)
///   * u16 SkipSize (jump-past-body offset for null-receiver short-circuit)
///   * UField* (member field — 4 disk → 8 mem)
///   * 1 byte property type
///   * 1 sub (the member access expression — the part after the dot)
///
/// Cooked RL packages can carry stale UField indices that resolve to invalid
/// imports (NTL drift, package-version skew). The base
/// <see cref="UStruct.UByteCodeDecompiler.ContextToken.Deserialize"/> propagates
/// those <see cref="ArgumentOutOfRangeException"/> / <see cref="InvalidCastException"/>
/// throws — leaving the trailing u16 + sub unread, which then orphan as
/// sibling tokens (visible in AIController_Soccar_TA.HandleNewPickup as
/// <c>NewPickup.</c> with trailing dot and a missing member name).
///
/// This RL variant catches the lookup exception, sets <c>Property = null</c>,
/// and continues consuming the bytes so the second sub-expression (member)
/// is still deserialized. <see cref="Decompile"/> renders <c>receiver.member</c>
/// when Property is missing, falling back to a placeholder name only when the
/// member sub-token is also empty.
/// </summary>
public class ContextTokenRL : UStruct.UByteCodeDecompiler.ContextToken
{
    public override void Deserialize(IUnrealStream stream)
    {
        // 1-byte preamble (matches base)
        stream.ReadByte();
        Decompiler.AlignSize(sizeof(byte));

        // Sub A: receiver expression
        DeserializeNext();

        // u16 SkipSize
        stream.ReadUInt16();
        Decompiler.AlignSize(sizeof(ushort));

        // UField property — defensive read.
        // Version 868 takes the propertyAdded path (>= 588), so we read a
        // UObject<UField> reference + AlignObjectSize. If the lookup throws,
        // we still need to advance the stream — but ReadObject<T>() advances
        // the disk cursor by the index size before doing the table lookup,
        // so the on-disk bytes are already consumed regardless.
        try
        {
            Property = stream.ReadObject<UField>();
        }
        catch (ArgumentOutOfRangeException)
        {
            Property = null;
        }
        catch (InvalidCastException)
        {
            Property = null;
        }
        Decompiler.AlignObjectSize();

        // u8 PropertyType — base ContextToken's branch logic for RL (v868,
        // propertyAdded=true) takes the else branch: `version > 512 && !propertyAdded`
        // is false because propertyAdded is true, falling through to ReadByte.
        // The parser case 0x28 in UStruct::SerializeExpr also reads exactly 1 byte
        // here (LABEL_10's serialize-1-byte + ++iCode).
        PropertyType = stream.ReadByte();
        Decompiler.AlignSize(sizeof(byte));

        // Sub B: member expression
        DeserializeNext();
    }

    public override string Decompile()
    {
        string receiver = DecompileNext();
        string member = DecompileNext();

        // Property fallback when the import lookup failed during Deserialize.
        // If we have the resolved Property we'd normally use its name, but
        // since the member sub-expression already encodes the property name
        // in its own decompilation, prefer that. Only fall back to the
        // Property name (or a placeholder) when the member sub is empty.
        if (string.IsNullOrEmpty(member))
        {
            if (Property != null)
            {
                member = Property.Name.ToString();
            }
            else
            {
                member = "/* unresolved member */";
            }
        }

        return $"{receiver}.{member}";
    }
}
