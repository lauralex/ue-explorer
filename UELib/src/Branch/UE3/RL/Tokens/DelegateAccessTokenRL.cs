using UELib.Core;

namespace UELib.Branch.UE3.RL.Tokens;

/// <summary>
/// RL byte 0x17 — delegate-property access. Wire format: 2 sub-expressions
/// (the receiver object expression and the function/name reference).
/// The binary handler at GNatives[0x17] (sub_7FF6CD2F1580) dispatches both
/// sub-expressions, then walks the receiver's delegate-list (qword
/// 0x7FF6CF27D7B0 + 16) to locate or clear matching entries — that's the
/// delegate-reference resolution path. Result is the second sub's value.
///
/// Was wrongly mapped to <c>DelegatePropertyToken</c> which reads
/// <c>FName + 1 sub-expr</c> (16 bytes for the FName + sub) instead of
/// <c>2 sub-exprs</c>. The 16-byte FName read NRE'd whenever the bytes
/// at that offset weren't a valid FName index — visible in
/// Car_TA.HandleTeamChanged where the EventSubscribe LHS NRE'd and
/// produced ` += ; self ` orphans.
///
/// Renders as <c>{Receiver}.{Function}</c>.
/// </summary>
public class DelegateAccessTokenRL : UStruct.UByteCodeDecompiler.Token
{
    public override void Deserialize(IUnrealStream stream)
    {
        DeserializeNext();
        DeserializeNext();
    }

    public override string Decompile()
    {
        string receiver = DecompileNext();
        string function = DecompileNext();
        if (string.IsNullOrEmpty(receiver))
        {
            return function;
        }
        if (string.IsNullOrEmpty(function))
        {
            return receiver;
        }
        return $"{receiver}.{function}";
    }
}
