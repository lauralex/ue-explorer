using UELib.Core;

namespace UELib.Branch.UE3.RL.Tokens;

/// <summary>
/// RL byte 0x15. Runtime handler steps one string expression into a temporary
/// FString, returns ArrayNum - 1, then frees the temporary buffer.
/// </summary>
public class StringLengthTokenRL : UStruct.UByteCodeDecompiler.Token
{
    public override void Deserialize(IUnrealStream stream)
    {
        DeserializeNext();
    }

    public override string Decompile()
    {
        string receiver = DecompileNext();
        if (string.IsNullOrEmpty(receiver))
        {
            receiver = "/* unresolved string */";
        }

        return $"{receiver}.Length";
    }
}
