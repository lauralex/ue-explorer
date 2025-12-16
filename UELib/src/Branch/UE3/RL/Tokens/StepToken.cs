using UELib.Core;

namespace UELib.Branch.UE3.RL.Tokens;

public class StepToken : UStruct.UByteCodeDecompiler.Token
{
    public override void Deserialize(IUnrealStream stream)
    {
        DeserializeNext();
    }

    public override string Decompile()
    {
        return DecompileNext();
    }
}
