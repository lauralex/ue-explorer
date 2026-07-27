using UELib.Core;

namespace UELib.Branch.UE3.RL.Tokens;

public class LetTokenRL : UStruct.UByteCodeDecompiler.LetToken
{
    public override string Decompile()
    {
        Decompiler.MarkSemicolon();
        string left = DecompileNext();
        string right = DecompileNext();

        if (string.IsNullOrWhiteSpace(left))
        {
            return right;
        }

        if (string.IsNullOrWhiteSpace(right))
        {
            return left;
        }

        return $"{left} = {right}";
    }
}

public class LetBoolTokenRL : LetTokenRL
{
}

public class LetDelegateTokenRL : LetTokenRL
{
}
