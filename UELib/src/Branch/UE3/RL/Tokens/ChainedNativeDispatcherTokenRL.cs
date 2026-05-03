using System;
using UELib.Core;
using UELib.Core.Tokens;

namespace UELib.Branch.UE3.RL.Tokens;

/// <summary>
/// Handles the chained native-function dispatchers at script bytes 0x70..0x7F. Each of those
/// bytes in the binary's runtime dispatch table reads one sub-byte and indexes into
/// <c>GNatives[(byte − 0x70) × 256 + sub_byte]</c>, covering native indexes 0..4095.
///
/// Replaces the per-byte "treat as raw native index = byte" handling that produced
/// __NFUN_112__..__NFUN_127__ placeholders for these dispatchers (which were never real natives —
/// they're the dispatchers themselves).
/// </summary>
public class ChainedNativeDispatcherTokenRL : UStruct.UByteCodeDecompiler.Token
{
    public override void Deserialize(IUnrealStream stream)
    {
        var tokenFactory = Package.Branch.GetTokenFactory(Package);
        int scriptPosition = Decompiler.ScriptPosition;

        byte subOpCode = stream.ReadByte();
        Decompiler.AlignSize(sizeof(byte));

        // OpCode is the dispatcher byte (0x70..0x7F). Each one indexes a 256-entry slice of
        // GNatives. Computed: native_index = (OpCode − 0x70) × 256 + subOpCode.
        ushort nativeIndex = (ushort)(((OpCode - 0x70) << 8) | subOpCode);

        var token = tokenFactory.CreateNativeToken(nativeIndex);

        Decompiler.DeserializedTokens.Add(token);
        token.Decompiler = Decompiler;
        token.Position = scriptPosition;
        token.StoragePosition = (int)(stream.Position - Container.ScriptOffset - 1);
        token.Deserialize(stream);

        token.Size = (short)(Decompiler.ScriptPosition - scriptPosition);
        token.StorageSize = (short)(stream.Position - Container.ScriptOffset - token.StoragePosition);
        token.PostDeserialized();
    }

    public override string Decompile()
    {
        return DecompileNext();
    }
}
