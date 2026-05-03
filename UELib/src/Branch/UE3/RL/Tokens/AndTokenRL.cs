using System;
using UELib.Core;

namespace UELib.Branch.UE3.RL.Tokens;

public class AndTokenRL : UStruct.UByteCodeDecompiler.NativeFunctionToken
{
    public override void Deserialize(IUnrealStream stream)
    {
        // Fill NativeItem if it is missing
        NativeItem = new NativeTableItem { Name = "&&", ByteToken = 0x82, OperPrecedence = 5, Type = FunctionType.Operator };

        // Each of these reads can throw on an inner ReadObject lookup if the package's import
        // table is incomplete (NTL drift). Suppress so the parser keeps walking the rest of
        // the variadic body and downstream tokens stay aligned.
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
