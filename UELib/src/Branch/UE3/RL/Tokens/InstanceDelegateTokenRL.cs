using UELib.Core;

namespace UELib.Branch.UE3.RL.Tokens;

/// <summary>
/// RL-specific instance-delegate constant. Wire format: 8-byte UObject* + 8-byte
/// FName (= 16 bytes payload). The binary handler at GNatives[0x32]
/// (sub_7FF6CD2F6180) reads UObject* + FName and constructs a
/// <c>{Object, Name, 0}</c> delegate tuple to push to the result.
///
/// Standard UE3 EX_InstanceDelegate only carries the FName (8 bytes); RL bakes
/// the resolved Object reference into the bytecode too, so a separate RL token
/// is needed. Renders as <c>Object.DelegateName</c> or just <c>DelegateName</c>
/// when the Object resolves to NULL or self.
/// </summary>
public class InstanceDelegateTokenRL : UStruct.UByteCodeDecompiler.Token
{
    public UObject DelegateObject;
    public UName DelegateName;

    public override void Deserialize(IUnrealStream stream)
    {
        DelegateObject = stream.ReadObject();
        Decompiler.AlignObjectSize();

        DelegateName = ReadName(stream);
    }

    public override string Decompile()
    {
        string name = DelegateName.ToString();

        // The cooker emits FName index 0 to mean "delegate unbind"
        // (`delegateProperty = none;`). RL packages don't reserve index 0 for
        // NAME_None — names get alphabetical-sorted, so the actual entry at
        // index 0 is whatever sorts first under ASCII (Engine: `"*"`,
        // TAGame: `"*Distortion"`). Detect the resolved name starts with `*`
        // and render as `none` instead of `Object.*Garbage`. Visible across
        // many delegate-clear sites: `Pawn.PostBeginPlay`, `PRI_TA.PostBeginPlay`,
        // `Car_TA.PostBeginPlay`, `AntiCheatManager_TA.__Construct_0x1`, etc.
        if (name.Length > 0 && name[0] == '*')
        {
            return "none";
        }

        if (DelegateObject == null || DelegateObject.Name == "None")
        {
            return name;
        }

        return $"{DelegateObject.Name}.{name}";
    }
}
