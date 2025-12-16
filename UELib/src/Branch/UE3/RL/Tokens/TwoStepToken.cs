using UELib.Core;

namespace UELib.Branch.UE3.RL.Tokens;
public class TwoStepToken : UStruct.UByteCodeDecompiler.Token
{
    public override void Deserialize(IUnrealStream stream)
    {
        DeserializeNext();
        DeserializeNext();
    }

    public override string Decompile()
    {
        return $"{DecompileNext()} {DecompileNext()}";
    }
}
