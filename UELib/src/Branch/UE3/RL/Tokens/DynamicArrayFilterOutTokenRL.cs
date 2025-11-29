using UELib.Core;

namespace UELib.Branch.UE3.RL.Tokens;
internal class DynamicArrayFilterOutTokenRL : UStruct.UByteCodeDecompiler.DynamicArrayMethodToken
{
    public override void Deserialize(IUnrealStream stream)
    {
        // Array
        DeserializeNext();

        if (stream.Version >= (uint)PackageObjectLegacyVersion.SkipSizeAddedToArrayTokenIntrinsics)
        {
            // Size
            stream.Skip(2);
            Decompiler.AlignSize(sizeof(ushort));
        }

        // Param 1
        DeserializeNext();


        // Read Object
        stream.ReadObject();
        Decompiler.AlignObjectSize();

        if (stream.Version >= (uint)PackageObjectLegacyVersion.EndTokenAppendedToArrayTokenIntrinsics)
        {
            // EndParms
            DeserializeNext();
        }

        DeserializeDebugToken();

        // Out Param
        DeserializeNext();
    }

    public override string Decompile()
    {
        Decompiler.MarkSemicolon();
        string context = DecompileNext();
        string param1 = DecompileNext();

        if (Package.Version >= (uint)PackageObjectLegacyVersion.EndTokenAppendedToArrayTokenIntrinsics)
        {
            // EndParms
            AssertSkipCurrentToken<UStruct.UByteCodeDecompiler.EndFunctionParmsToken>();
        }

        string param2 = DecompileNext();

        return $"{context}.Filter({param1}, out {param2})";
    }
}
