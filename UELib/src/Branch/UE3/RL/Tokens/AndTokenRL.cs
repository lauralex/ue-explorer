using UELib.Core;

namespace UELib.Branch.UE3.RL.Tokens;

public class AndTokenRL : UStruct.UByteCodeDecompiler.NativeFunctionToken
{
    public override void Deserialize(IUnrealStream stream)
    {
        // Fill NativeItem if it is missing
        NativeItem = new NativeTableItem { Name = "&&", ByteToken = 0x82, OperPrecedence = 5, Type = FunctionType.Operator };


        while (DeserializeNext() is UStruct.UByteCodeDecompiler.NothingToken)
        {
        }

        stream.ReadByte();
        stream.ReadByte();
        stream.ReadByte();
        Decompiler.AlignSize(sizeof(byte));
        Decompiler.AlignSize(sizeof(byte));
        Decompiler.AlignSize(sizeof(byte));

        DeserializeCall(stream);
    }
}
