using UELib.Core;

namespace UELib.Branch.UE3.RL.Tokens;
public class VariadicArrayFillOutTokenRL : UStruct.UByteCodeDecompiler.NativeFunctionToken
{
    public override void Deserialize(IUnrealStream stream)
    {
        NativeItem = new NativeTableItem { Name = "Fill", ByteToken = 0x28, OperPrecedence = 0, Type = FunctionType.Function };

        // Array
        DeserializeNext();

        // Skip 2 bytes
        stream.Skip(2);
        Decompiler.AlignSize(sizeof(ushort));

        // Variadic part
        base.Deserialize(stream);

        // Out
        DeserializeNext();
    }

    public override string Decompile()
    {
        string array = DecompileNext();

        return $"{array}.{base.Decompile()} -> {DecompileNext()}";
    }
}
