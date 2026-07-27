using System;
using UELib.Core;

namespace UELib.Branch.UE3.RL.Tokens;
public class EventSubscribeToken : UStruct.UByteCodeDecompiler.Token
{
    public UName EventName;

    public override void Deserialize(IUnrealStream stream)
    {
        DeserializeNext();

        var delegateToken = DeserializeNext();

        EventName = delegateToken switch
        {
            // Check if delegateToken is a InstanceDelegateToken
            UStruct.UByteCodeDecompiler.InstanceDelegateToken dit => dit.DelegateName,
            UStruct.UByteCodeDecompiler.DelegatePropertyToken dpt => dpt.PropertyName,
            _ => EventName,
        };
    }

    public override string Decompile()
    {
        Decompiler.MarkSemicolon();
        string left = DecompileNext();
        string right = DecompileNext();

        if (string.IsNullOrWhiteSpace(left))
        {
            return right;
        }

        if (string.IsNullOrWhiteSpace(right))
        {
            return left;
        }

        return $"{left} += {right}";
    }
}
