using System;
using UELib.Core;

namespace UELib.Branch.UE3.RL.Tokens;
public class OrTokenRL : UStruct.UByteCodeDecompiler.NativeFunctionToken
{
    public override void Deserialize(IUnrealStream stream)
    {
        // Fill NativeItem if it is missing
        NativeItem = new NativeTableItem { Name = "||", ByteToken = 0x84, OperPrecedence = 5, Type = FunctionType.Operator };

        // See AndTokenRL for rationale on the catch.
        try
        {
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
        catch (ArgumentOutOfRangeException)
        {
        }
        catch (InvalidCastException)
        {
        }
    }
}
