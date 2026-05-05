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

    public override string Decompile()
    {
        AddForEachNest();

        SetEndComment();

        string expression = DecompileNext();
        string item = DecompileIteratorParameter();
        string index = DecompileIteratorParameter();
        string output = string.IsNullOrWhiteSpace(index) || index == ","
            ? $"foreach {expression}({item})"
            : $"foreach {expression}({item}, {index})";

        SuppressSemicolon();
        return output;
    }

    private string DecompileIteratorParameter()
    {
        if (Decompiler.CurrentTokenIndex + 1 >= Decompiler.DeserializedTokens.Count)
        {
            return string.Empty;
        }

        var token = NextToken();
        if (token is DiscardKeepTokenRL)
        {
            string first = DecompileNext();
            string second = DecompileNext();
            return string.IsNullOrWhiteSpace(first) || first == "," ? second : first;
        }

        return token is UStruct.UByteCodeDecompiler.EmptyParmToken ? string.Empty : token.Decompile();
    }
}
