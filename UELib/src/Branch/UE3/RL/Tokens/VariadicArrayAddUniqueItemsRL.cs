using UELib.Core;

namespace UELib.Branch.UE3.RL.Tokens;

public class VariadicArrayAddUniqueItemsRL : UStruct.UByteCodeDecompiler.NativeFunctionToken
{
    public override void Deserialize(IUnrealStream stream)
    {
        NativeItem = new NativeTableItem
        {
            Name = "AddUniqueItems", ByteToken = 0x0C, OperPrecedence = 0, Type = FunctionType.Function
        };

        // Array
        DeserializeNext();

        // Skip 2 bytes
        stream.Skip(2);
        Decompiler.AlignSize(sizeof(ushort));

        // Variadic part
        base.Deserialize(stream);

    }

    public override string Decompile()
    {
        string array = DecompileNext();

        return $"{array}.{base.Decompile()}";
    }
}
