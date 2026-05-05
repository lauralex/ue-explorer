using UELib.Core;

namespace UELib.Branch.UE3.RL.Tokens;

public class DynamicArrayFirstTokenRL : UStruct.UByteCodeDecompiler.DynamicArrayMethodToken
{
    public override void Deserialize(IUnrealStream stream)
    {
        // Array
        DeserializeNext();

        // Skip size/range metadata.
        stream.Skip(2);
        Decompiler.AlignSize(sizeof(ushort));

        // Predicate delegate
        DeserializeNext();

        // Start index
        DeserializeNext();

        // Count
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
        Decompiler.MarkSemicolon();
        string context = DecompileNext();
        string predicate = DecompileNext();
        string startIndex = DecompileNext();
        string count = DecompileNext();

        if (Package.Version >= (uint)PackageObjectLegacyVersion.EndTokenAppendedToArrayTokenIntrinsics)
        {
            AssertSkipCurrentToken<UStruct.UByteCodeDecompiler.EndFunctionParmsToken>();
        }

        return $"{context}.First({predicate}, {startIndex}, {count})";
    }
}
