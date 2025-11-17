using UELib.Core;

namespace UELib.Branch.UE3.RL.Tokens;

public class DynamicArrayElementTokenRL : UStruct.UByteCodeDecompiler.DynamicArrayElementToken
{
    public override void Deserialize(IUnrealStream stream)
    {
        // Key
        while (DeserializeNext() is UStruct.UByteCodeDecompiler.NothingToken)
        {
        }

        // Value
        DeserializeNext();
    }
}
