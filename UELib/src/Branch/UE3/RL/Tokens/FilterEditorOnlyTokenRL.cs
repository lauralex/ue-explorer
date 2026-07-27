using UELib.Core;

namespace UELib.Branch.UE3.RL.Tokens;

/// <summary>
/// RL byte 0x48. Runtime reads a u16 code offset, an 8-byte FName-like
/// discriminator, and a trailing byte before conditionally jumping to the
/// offset. This is closest to stock EX_JumpIfFilterEditorOnly, with extra RL
/// metadata in the serialized stream.
/// </summary>
public class FilterEditorOnlyTokenRL : UStruct.UByteCodeDecompiler.FilterEditorOnlyToken
{
    public UName Discriminator;
    public byte ExpectedValue;

    public override void Deserialize(IUnrealStream stream)
    {
        CodeOffset = stream.ReadUInt16();
        Decompiler.AlignSize(sizeof(ushort));

        Discriminator = ReadName(stream);

        ExpectedValue = stream.ReadByte();
        Decompiler.AlignSize(sizeof(byte));
    }
}
