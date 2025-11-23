using UELib.Core;

namespace UELib.Branch.UE3.RL.Tokens;
public class DynamicArrayAnyTokenRL : UStruct.UByteCodeDecompiler.DynamicArrayMethodToken
{
    public override void Deserialize(IUnrealStream stream)
    {
        // Array
        DeserializeNext();

        // Skip 2 bytes
        stream.Skip(2);
        Decompiler.AlignSize(sizeof(ushort));

        // Param 1
        DeserializeNext();

        if (stream.Version >= (uint)PackageObjectLegacyVersion.EndTokenAppendedToArrayTokenIntrinsics)
        {
            // EndParms
            DeserializeNext();
        }

        DeserializeDebugToken();
    }

    public override string Decompile()
    {
        return $"{DecompileNext()}.Any({DecompileNext()})";
    }
}
