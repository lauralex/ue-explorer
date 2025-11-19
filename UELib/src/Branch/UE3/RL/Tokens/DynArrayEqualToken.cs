using UELib.Core;

namespace UELib.Branch.UE3.RL.Tokens;
public class DynArrayEqualToken : UStruct.UByteCodeDecompiler.Token
{
    public override void Deserialize(IUnrealStream stream)
    {
        DeserializeNext();

        stream.ReadByte();
        stream.ReadByte();
        Decompiler.AlignSize(sizeof(byte));
        Decompiler.AlignSize(sizeof(byte));

        DeserializeNext();

        stream.ReadByte();
        stream.ReadByte();
        stream.ReadByte();
        Decompiler.AlignSize(sizeof(byte));
        Decompiler.AlignSize(sizeof(byte));
        Decompiler.AlignSize(sizeof(byte));
    }

    public override string Decompile()
    {
        return $"{DecompileNext()}.Equals({DecompileNext()})";
    }
}
