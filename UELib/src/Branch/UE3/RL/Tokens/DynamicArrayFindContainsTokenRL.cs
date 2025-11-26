using UELib.Core;

namespace UELib.Branch.UE3.RL.Tokens;
public class DynamicArrayFindContainsTokenRL : UStruct.UByteCodeDecompiler.DynamicArrayMethodToken
{
    private bool _IsContains;

    public override void Deserialize(IUnrealStream stream)
    {
        // Array
        DeserializeNext();

        // IsContains flag
        _IsContains = stream.ReadByte() != 0;
        Decompiler.AlignSize(sizeof(byte));

        if (stream.Version >= (uint)PackageObjectLegacyVersion.SkipSizeAddedToArrayTokenIntrinsics)
        {
            // Size
            stream.Skip(2);
            Decompiler.AlignSize(sizeof(ushort));
        }

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
        return _IsContains ? DecompileOneParamMethod("Contains") : DecompileOneParamMethod("Find");
    }
}
