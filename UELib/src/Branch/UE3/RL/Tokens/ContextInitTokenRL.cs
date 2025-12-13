using UELib.Core;

namespace UELib.Branch.UE3.RL.Tokens;
public class ContextInitTokenRL : UStruct.UByteCodeDecompiler.ContextToken
{
    public override void Deserialize(IUnrealStream stream)
    {
        // Object initializer
        DeserializeNext();

        // Invocation
        DeserializeNext();

        // Code skip
        stream.Skip(2);
        Decompiler.AlignSize(sizeof(ushort));
    }

    public override string Decompile()
    {
        return $"{DecompileNext()}.{DecompileNext()}";
    }
}
