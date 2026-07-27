using System;
using UELib.Core;

namespace UELib.Branch.UE3.RL.Tokens;

/// <summary>
/// RL byte 0x07. Parser shape: UField/UObject reference plus one
/// sub-expression. The runtime tail-calls a handler that skips the reference,
/// dispatches the sub-expression, then zeroes the result slot, so for source
/// reconstruction the wrapped expression is the only useful visible operand.
/// </summary>
public class FieldWrappedExpressionTokenRL : UStruct.UByteCodeDecompiler.Token
{
    public UObject Field;

    public override void Deserialize(IUnrealStream stream)
    {
        try
        {
            Field = stream.ReadObject();
        }
        catch (ArgumentOutOfRangeException)
        {
            Field = null;
        }
        catch (InvalidCastException)
        {
            Field = null;
        }
        Decompiler.AlignObjectSize();

        DeserializeNext();
    }

    public override string Decompile()
    {
        return DecompileNext();
    }
}
