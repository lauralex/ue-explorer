using UELib.Core;

namespace UELib.Branch.UE3.RL.Tokens;

public class SkipFunctionTokenRL : UStruct.UByteCodeDecompiler.Token
{
    public override void Deserialize(IUnrealStream stream)
    {
        while (DeserializeNext() is not UStruct.UByteCodeDecompiler.EndFunctionParmsToken)
        {
        }

        DeserializeDebugToken();

        DeserializeNext();
    }

    public override string Decompile()
    {
        UStruct.UByteCodeDecompiler.Token skip;
        string output = string.Empty;
        do
        {
            skip = NextToken();
            if (skip is not UStruct.UByteCodeDecompiler.EndFunctionParmsToken)
            {
                output += $"\r\n{UDecompilingState.Tabs}{skip.Decompile()};";
            }
        } while (skip is not UStruct.UByteCodeDecompiler.EndFunctionParmsToken);

        // Remove last comma from output
        if (output.EndsWith(";"))
        {
            output = output.Substring(0, output.Length - 1);
        }

        return $"{DecompileNext()};{output}";
    }
}
