using UELib.Core;

namespace UELib.Branch.UE3.RL.Tokens;
public class AllControllersJumpTokenRL : UStruct.UByteCodeDecompiler.JumpToken
{
    public override string Decompile()
    {
        AddForEachNest();
        SetEndComment();
        SuppressSemicolon();
        return string.Empty;
    }
}
