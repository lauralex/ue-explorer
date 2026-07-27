using UELib.Core;

namespace UELib.Branch.UE3.RL.Tokens;

public class DynArrayResultTokenRL : UStruct.UByteCodeDecompiler.Token
{
    public override void Deserialize(IUnrealStream stream)
    {
        DeserializeNext();
        DeserializeNext();
    }

    public override string Decompile()
    {
        string source = DecompileNext();
        string projection = DecompileNext();

        if (string.IsNullOrEmpty(source))
        {
            return projection;
        }

        if (string.IsNullOrEmpty(projection))
        {
            return source;
        }

        return projection.Contains(source)
            ? projection
            : $"{source}.{projection}";
    }
}
