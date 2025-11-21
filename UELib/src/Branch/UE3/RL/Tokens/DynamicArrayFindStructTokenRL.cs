using UELib.Core;

namespace UELib.Branch.UE3.RL.Tokens;

public class DynamicArrayFindStructTokenRL : UStruct.UByteCodeDecompiler.DynamicArrayFindStructToken
{
    public override void Deserialize(IUnrealStream stream)
    {
        // Array
        DeserializeNext();
        // Size
        stream.Skip(3);
        Decompiler.AlignSize(sizeof(ushort));
        Decompiler.AlignSize(sizeof(byte));


        // Param 1
        DeserializeNext();

        // Param 2
        DeserializeNext();

        if (stream.Version >= (uint)PackageObjectLegacyVersion.EndTokenAppendedToArrayTokenIntrinsics)
        {
            // EndParms
            DeserializeNext();
        }

        DeserializeDebugToken();
    }
}
