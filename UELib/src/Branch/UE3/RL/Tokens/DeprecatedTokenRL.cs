using UELib.Core;

namespace UELib.Branch.UE3.RL.Tokens;
public class DeprecatedTokenRL : UStruct.UByteCodeDecompiler.Token
{
    private UName _Name;

    public override void Deserialize(IUnrealStream stream)
    {
        _Name = ReadName(stream);
    }

    public override string Decompile()
    {
        return $"//Deprecated function: {_Name}";
    }
}
