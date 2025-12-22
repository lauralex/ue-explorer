using UELib.Core;

namespace UELib.Branch.UE3.RL.Tokens;
public class IteratorTokenRL : UStruct.UByteCodeDecompiler.IteratorToken
{
    public override void Deserialize(IUnrealStream stream)
    {
        DeserializeNext(); // Expression
        DeserializeNext(); // Var
        DeserializeNext(); // [InterfaceClass]
        base.Deserialize(stream);

        // Deserialize until there's IteratorPopToken
        while (DeserializeNext() is not UStruct.UByteCodeDecompiler.IteratorPopToken)
        {
        }
    }

    public override string Decompile()
    {
        AddNest();
        SetEndComment();

        // foreach FunctionCall
        string expression = DecompileNext();
        RemoveSemicolon();

        string var = DecompileNext();
        string optionalInterfaceClass = DecompileNext();
        DecompileNext();

        string appendBody = string.Empty;

        UStruct.UByteCodeDecompiler.Token token;
        do
        {
            token = Decompiler.NextToken;
            if (token is UStruct.UByteCodeDecompiler.DebugInfoToken) continue;
            appendBody += token.Decompile();
        } while (token is not UStruct.UByteCodeDecompiler.IteratorPopToken);

        string finalBody = string.IsNullOrEmpty(optionalInterfaceClass) || optionalInterfaceClass == "," ? $"foreach AllObjects({expression}, {var})" : $"foreach AllObjects({expression}, {var}, {optionalInterfaceClass})";


        return finalBody + "\r\n" + appendBody;
    }
}
