using System;
using UELib.Core;

namespace UELib.Branch.UE3.RL.Tokens;
public class EventUnsubscribeToken : UStruct.UByteCodeDecompiler.Token
{
    public UName EventName;

    public override void Deserialize(IUnrealStream stream)
    {
        DeserializeNext();

        var delegateToken = DeserializeNext();

        EventName = delegateToken switch
        {
            // Check if the token is of type InstanceDelegateToken
            UStruct.UByteCodeDecompiler.InstanceDelegateToken dit => dit.DelegateName,
            UStruct.UByteCodeDecompiler.DelegatePropertyToken dpt => dpt.PropertyName,
            _ => EventName
        };
    }

    public override string Decompile()
    {
        Decompiler.MarkSemicolon();
        return $"{DecompileNext()} -= {DecompileNext()}";
    }
}
