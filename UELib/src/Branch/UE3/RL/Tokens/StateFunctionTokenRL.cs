using UELib.Core;

namespace UELib.Branch.UE3.RL.Tokens;
public class StateFunctionTokenRL : UStruct.UByteCodeDecompiler.Token
{
    private UName StateFunctionName;

    public override void Deserialize(IUnrealStream stream)
    {
        StateFunctionName = ReadName(stream);
    }

    public override string Decompile()
    {
        Decompiler.MarkSemicolon();
        return $"InvalidFunctionCall({StateFunctionName})";
    }
}
