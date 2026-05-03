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
        // Bounds-check before NextToken — Deserialize may have stopped before reaching the
        // expected EndFunctionParms (truncated bytecode or wrong shape inference); without the
        // check, NextToken throws ArgumentOutOfRangeException and aborts the parent statement.
        while (Decompiler.CurrentTokenIndex + 1 < Decompiler.DeserializedTokens.Count)
        {
            skip = NextToken();
            if (skip is UStruct.UByteCodeDecompiler.EndFunctionParmsToken) break;
            output += $"\r\n{UDecompilingState.Tabs}{skip.Decompile()};";
        }

        // Remove last comma from output
        if (output.EndsWith(";"))
        {
            output = output.Substring(0, output.Length - 1);
        }

        return $"{DecompileNext()};{output}";
    }
}
