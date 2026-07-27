using UELib.Core;

namespace UELib.Branch.UE3.RL.Tokens;
public class DynamicArrayMapTokenRL : UStruct.UByteCodeDecompiler.DynamicArrayMethodToken
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

        try
        {
            stream.ReadObject();
        }
        catch (System.ArgumentOutOfRangeException)
        {
        }
        catch (System.InvalidCastException)
        {
        }
        Decompiler.AlignObjectSize();

        if (stream.Version >= (uint)PackageObjectLegacyVersion.EndTokenAppendedToArrayTokenIntrinsics)
        {
            // EndParms
            DeserializeNext();
        }

        DeserializeDebugToken();

        // Output Array
        DeserializeNext();
    }

    public override string Decompile()
    {
        string context = DecompileNext();
        string mapper = DecompileNext();

        if (Package.Version >= (uint)PackageObjectLegacyVersion.EndTokenAppendedToArrayTokenIntrinsics)
        {
            AssertSkipCurrentToken<UStruct.UByteCodeDecompiler.EndFunctionParmsToken>();
        }

        string output = DecompileNext();
        return string.IsNullOrEmpty(output)
            ? $"{context}.Map({mapper})"
            : $"{context}.Map({mapper}, out {output})";
    }
}
