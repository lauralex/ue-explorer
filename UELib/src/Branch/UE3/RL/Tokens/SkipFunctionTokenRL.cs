using System;
using UELib.Core;

namespace UELib.Branch.UE3.RL.Tokens;

public class SkipFunctionTokenRL : UStruct.UByteCodeDecompiler.Token
{
    public override void Deserialize(IUnrealStream stream)
    {
        while (DeserializeNext() is not UStruct.UByteCodeDecompiler.EndFunctionParmsToken)
        {
        }

        if (DeserializeNext() is UStruct.UByteCodeDecompiler.DebugInfoToken)
        {
            DeserializeNext();
        }
    }

    public override string Decompile()
    {
        string output;

        do
        {
            output = DecompileNext();
        } while (string.IsNullOrEmpty(output));

        return output;
    }
}
