using System.Collections.Generic;
using UELib.Core;

namespace UELib.Branch.UE3.RL.Tokens;

/// <summary>
/// RL byte 0x12. Parser shape verified against UStruct::SerializeExpr
/// (sub_7FF6CD38C840): variadic body until EndFunctionParms (0x3E),
/// optional DebugInfo, then one trailing expression whose value is returned.
/// </summary>
public class VariadicReturnValueTokenRL : UStruct.UByteCodeDecompiler.Token
{
    public override void Deserialize(IUnrealStream stream)
    {
        Decompiler.VariadicCallDepth++;
        try
        {
#pragma warning disable 642
            while (!(DeserializeNext() is UStruct.UByteCodeDecompiler.EndFunctionParmsToken)) ;
#pragma warning restore 642
            DeserializeDebugToken();
        }
        finally
        {
            Decompiler.VariadicCallDepth--;
        }

        DeserializeNext();
    }

    public override string Decompile()
    {
        var setupExpressions = new List<string>();

        while (Decompiler.CurrentTokenIndex + 1 < Decompiler.DeserializedTokens.Count)
        {
            var next = Decompiler.DeserializedTokens[Decompiler.CurrentTokenIndex + 1];
            if (next is UStruct.UByteCodeDecompiler.EndFunctionParmsToken)
            {
                Decompiler.CurrentTokenIndex++;
                break;
            }

            string setupExpression = DecompileNext();
            if (!string.IsNullOrWhiteSpace(setupExpression))
            {
                setupExpressions.Add(setupExpression);
            }
        }

        string resultExpression = DecompileNext();
        if (setupExpressions.Count == 0)
        {
            return resultExpression;
        }

        if (string.IsNullOrWhiteSpace(resultExpression))
        {
            return string.Join(", ", setupExpressions);
        }

        setupExpressions.Add(resultExpression);
        return $"({string.Join(", ", setupExpressions)})";
    }
}
