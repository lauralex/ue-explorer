using UELib.Core;

namespace UELib.Branch.UE3.RL.Tokens;
public class DynamicArrayFilterTokenRL : UStruct.UByteCodeDecompiler.DynamicArrayMethodToken
{
    public override void Deserialize(IUnrealStream stream)
    {
        DeserializeOneParamMethodWithSkip(stream);
    }

    public override string Decompile()
    {
        return DecompileOneParamMethod("Filter");
    }
}
