using UELib.Core;

namespace UELib.Branch.UE3.RL.Tokens;
public class DynamicArrayIteratorRL : UStruct.UByteCodeDecompiler.DynamicArrayIteratorToken
{
    public override void Deserialize(IUnrealStream stream)
    {
        WithIndexParam = 1;

        // Expression
        DeserializeNext();

        // Item param
        DeserializeNext();

        // Index param
        DeserializeNext();

        DeserializeBase(stream);
    }

}
