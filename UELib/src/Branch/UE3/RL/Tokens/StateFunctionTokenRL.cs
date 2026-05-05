using UELib.Core;

namespace UELib.Branch.UE3.RL.Tokens;

/// <summary>
/// RL byte 0x56 — state-function tombstone. Wire format: 8-byte FName.
///
/// The cooker emits this as the body of state-overridable functions declared
/// in a base class without an implementation. At runtime, GNatives[0x56]
/// (sub_7FF6CD2F5B00) reads the FName and dispatches the function via the
/// current state's vtable, logging "State function '%s' called while not in
/// declared state." if the active state lacks the override.
///
/// For decompilation, the base-class declaration should render as an empty
/// body (e.g. <c>function SendReservation();</c>). The previous mapping to
/// NameConstToken produced an orphan <c>'SendReservation'</c> literal inside
/// the function body — visually noisy and not valid UnrealScript.
///
/// Wire format matches NameConstToken (8-byte FName), but rendering is
/// suppressed so the base-class stub appears empty. State-attached overrides
/// (e.g. <c>state ReservingServer { function SendReservation() { ... } }</c>)
/// have real bytecode bodies and are unaffected.
/// </summary>
public class StateFunctionTokenRL : UStruct.UByteCodeDecompiler.Token
{
    public UName Name;

    public override void Deserialize(IUnrealStream stream)
    {
        Name = stream.ReadName();
        Decompiler.AlignNameSize();
    }

    public override string Decompile()
    {
        return string.Empty;
    }
}
