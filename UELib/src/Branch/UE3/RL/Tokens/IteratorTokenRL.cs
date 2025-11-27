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

        return string.IsNullOrEmpty(optionalInterfaceClass) || optionalInterfaceClass == "," ? $"foreach AllObjects({expression}, {var})" : $"foreach AllObjects({expression}, {var}, {optionalInterfaceClass})";
    }
}
