using UELib.Core;

namespace UELib.Branch.UE3.RL.Tokens;
public class AssertTokenRL : UStruct.UByteCodeDecompiler.AssertToken
{
    public override void Deserialize(IUnrealStream stream)
    {
        Line = stream.ReadUInt16();
        Decompiler.AlignSize(sizeof(short));

        // FIXME: Version, verified against (RoboBlitz v369)
        if (stream.Version >= 200)
        {
            IsDebug = stream.ReadByte();
            Decompiler.AlignSize(sizeof(byte));
        }

        DeserializeNext();

        // Debug message 1
        DeserializeNext();
        // Debug message 2
        DeserializeNext();
    }

    public override string Decompile()
    {
        if (IsDebug.HasValue)
        {
            Decompiler.PreComment = $"// DebugMode: {IsDebug}";
        }

        string condition = DecompileNext();
        string debugMsg1 = DecompileNext();
        // Transform empty string to ""
        if (string.IsNullOrEmpty(debugMsg1))
        {
            debugMsg1 = "\"\"";
        }

        string debugMsg2 = DecompileNext();
        if (string.IsNullOrEmpty(debugMsg2))
        {
            debugMsg2 = "\"\"";
        }
        Decompiler.MarkSemicolon();

        return
            $"assert({condition}, {debugMsg1}, {debugMsg2})";
    }
}
