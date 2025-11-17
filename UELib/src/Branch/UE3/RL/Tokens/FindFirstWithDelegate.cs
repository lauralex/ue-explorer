using UELib.Core;

namespace UELib.Branch.UE3.RL.Tokens;
public class FindFirstWithDelegate : UStruct.UByteCodeDecompiler.Token
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
        Decompiler.AlignSize(sizeof(byte));
    }

    public override string Decompile()
    {
        string firstExpression = DecompileNext();

        return $"" +
               $"foreach {firstExpression}(Idx)" +
               $"{{" +
               $"   if ({DecompileNext()}({firstExpression}(Idx))) break; // ItemValue" +
               $"}}";
    }
}
