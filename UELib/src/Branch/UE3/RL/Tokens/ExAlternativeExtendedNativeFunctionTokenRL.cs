using System;
using UELib.Core;
using UELib.Core.Tokens;

namespace UELib.Branch.UE3.RL.Tokens;
public class ExAlternativeExtendedNativeFunctionTokenRL : UStruct.UByteCodeDecompiler.NativeFunctionToken
{
    // Build custom extended native function opcode map
    private static readonly TokenMap s_extendedNativeFunctionTokenMap = new()
    {
        { 0x39, typeof(IteratorTokenRL) },
    };

    public override void Deserialize(IUnrealStream stream)
    {
        var tokenFactory = Package.Branch.GetTokenFactory(Package);
        int scriptPosition = Decompiler.ScriptPosition;

        byte opCode = stream.ReadByte();
        Decompiler.AlignSize(sizeof(byte));

        // Check if opcode is present in extended native function token map
        if (s_extendedNativeFunctionTokenMap.TryGetValue(opCode, out var tokenType))
        {
            var extendedNativeToken = (UStruct.UByteCodeDecompiler.Token)Activator.CreateInstance(tokenType)!;
            Decompiler.DeserializedTokens.Add(extendedNativeToken);
            extendedNativeToken.OpCode = opCode;
            extendedNativeToken.Decompiler = Decompiler;
            extendedNativeToken.Position = scriptPosition;
            extendedNativeToken.StoragePosition = (int)(stream.Position - Container.ScriptOffset - 1);
            extendedNativeToken.Deserialize(stream);
            extendedNativeToken.Size = (short)(Decompiler.ScriptPosition - scriptPosition);
            extendedNativeToken.StorageSize = (short)(stream.Position - Container.ScriptOffset - extendedNativeToken.StoragePosition);
            extendedNativeToken.PostDeserialized();
            return;
        }

        // Binary RE: script byte 0x71 in RL is the chained native dispatcher for indexes 256-511
        // (binary handler at 0x7FF6CD30C870 looks up GNatives[256 + sub_byte]). The previous
        // "+6000" labeling produced names that didn't exist in the binary's GNatives. See
        // RL_OPCODE_ANALYSIS.md "Native-name resolution" + "ghost natives" sections.
        ushort nativeIndex = (ushort)(opCode + 256);

        // If GNatives[index] is the binary's "Unknown code token" default handler, emit a
        // NothingToken instead of a NativeFunctionToken — otherwise we'd render
        // __NFUN_NNN__(...) ghost calls for indexes the binary itself would error on at runtime.
        if (UELib.Branch.UE3.RL.RocketLeagueUnknownNatives.Set.Contains(nativeIndex))
        {
            var nop = new UStruct.UByteCodeDecompiler.NothingToken();
            Decompiler.DeserializedTokens.Add(nop);
            nop.OpCode = OpCode;
            nop.Decompiler = Decompiler;
            nop.Position = scriptPosition;
            nop.StoragePosition = (int)(stream.Position - Container.ScriptOffset - 1);
            nop.Size = (short)(Decompiler.ScriptPosition - scriptPosition);
            nop.StorageSize = (short)(stream.Position - Container.ScriptOffset - nop.StoragePosition);
            nop.PostDeserialized();
            return;
        }

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
