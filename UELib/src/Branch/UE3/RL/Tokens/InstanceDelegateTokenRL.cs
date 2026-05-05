using UELib.Core;

namespace UELib.Branch.UE3.RL.Tokens;

/// <summary>
/// RL-specific instance-delegate constant. Wire format (verified against
/// UStruct::SerializeExpr case 0x32 in sub_7FF6CD38C840):
///   * 8-byte FName  (the function/delegate name to bind)
///   * 4-byte UProperty index  (the delegate property — expanded to 8 mem bytes)
///
/// The runtime handler at GNatives[0x32] (sub_7FF6CD2F6180) reads "UObject* +
/// FName" but the parser order is FName then UProperty — and the cooker emits
/// the parse-time format, so the parser is authoritative for decompilation
/// (per CLAUDE.md validation discipline; precedent: byte 0x2C ternary, 0x01
/// passthrough). Reading in runtime order misinterpreted the FName index as a
/// UObject index, producing nonsense like <c>Default__NavigationHandle_TA.*Distortion</c>
/// for delegate bindings to <c>__AntiCheatManager_TA__Construct_0x2</c> in
/// AntiCheatManager_TA.__Construct_0x1.
///
/// Renders the FName since it identifies the bound function. Property pointer
/// metadata is consumed but not surfaced in the rendering.
/// </summary>
public class InstanceDelegateTokenRL : UStruct.UByteCodeDecompiler.Token
{
    public UName DelegateName;
    public UProperty DelegateProperty;

    public override void Deserialize(IUnrealStream stream)
    {
        DelegateName = ReadName(stream);

        try
        {
            DelegateProperty = stream.ReadObject<UProperty>();
        }
        catch (System.ArgumentOutOfRangeException)
        {
            DelegateProperty = null;
        }
        catch (System.InvalidCastException)
        {
            DelegateProperty = null;
        }
        Decompiler.AlignObjectSize();
    }

    public override string Decompile()
    {
        string name = DelegateName.ToString();

        // The cooker still uses the alphabetically-first FName (`*` / `*Distortion`
        // depending on the package) as the "delegate-unbind" sentinel even after
        // fixing the read order. Detect and render as `none` to match the source.
        if (name.Length > 0 && name[0] == '*')
        {
            return "none";
        }

        return name;
    }
}
