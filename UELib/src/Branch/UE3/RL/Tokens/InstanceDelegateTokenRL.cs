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
        if (DelegateObject == null || DelegateObject.Name == "None")
        {
            return name;
        }

        return $"{DelegateObject.Name}.{name}";
    }
}
