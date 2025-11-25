using UELib.Core;

namespace UELib.Branch.UE3.RL.Tokens;
public class DynamicArrayFindTypeTokenRL : UStruct.UByteCodeDecompiler.DynamicArrayFindToken
{
    public override string Decompile()
    {
        return DecompileOneParamMethod("FindType");
    }
}
