using UELib.Core;

namespace UELib.Branch.UE3.RL.Tokens;

/// <summary>
/// RL byte 0x62 — compact delegate-function-name reference. Wire format: 8-byte
/// FName (no UObject*, no flags). The binary handler at GNatives[0x62]
/// (sub_7FF6CD2F6FC0) reads `*(qword*)Code`, advances Code by 8 bytes, then
/// builds a `{context=this, name=qword, 0}` delegate tuple and dispatches the
/// property accessor — the runtime shape of a delegate-function bind.
///
/// Source-level it's the right-hand side of a delegate assignment:
///   <c>SomeObj.__EventFoo__Delegate = OnFoo;</c>
/// Where <c>OnFoo</c> is a function whose name is encoded as the 8-byte FName
/// payload. Renders as the bare function name (no quotes), so it slots into
/// <c>Let</c> / <c>LetBool</c> / <c>LetDelegate</c> RHS positions correctly.
///
/// Was wrongly mapped to <c>AssertToken</c> (which reads u16 + byte = 3 bytes
/// payload) — every delegate-assignment RHS rendered as <c>= assert();</c> and
/// the 5 unread payload bytes orphaned as <c>ContextAwareReturnTokenRL</c>
/// chains. Visible in AntiCheatMessenger_TA.PostBeginPlay,
/// AntiCheatManager_TA.__Construct_0x1, and many other classes that bind
/// engine-event delegates.
/// </summary>
public class DelegateFunctionRefTokenRL : UStruct.UByteCodeDecompiler.Token
{
    public UName FunctionName;

    public override void Deserialize(IUnrealStream stream)
    {
        FunctionName = ReadName(stream);
    }

    public override string Decompile()
    {
        return FunctionName?.ToString() ?? string.Empty;
    }
}
