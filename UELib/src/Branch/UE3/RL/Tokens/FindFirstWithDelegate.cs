using UELib.Core;

namespace UELib.Branch.UE3.RL.Tokens;
public class FindFirstWithDelegate : UStruct.UByteCodeDecompiler.Token
{
    public override void Deserialize(IUnrealStream stream)
    {
        DeserializeNext();

        stream.Skip(2);
        Decompiler.AlignSize(sizeof(ushort));

        DeserializeNext();
        DeserializeNext();

        DeserializeDebugToken();
    }

    public override string Decompile()
    {
        string array = DecompileNext();
        string predicate = DecompileNext();
        AssertSkipCurrentToken<UStruct.UByteCodeDecompiler.EndFunctionParmsToken>();

        return $"{array}.First({predicate})";
    }
}
