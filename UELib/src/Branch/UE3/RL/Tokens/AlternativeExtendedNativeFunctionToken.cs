using System;
using UELib.Core;
using UELib.Core.Tokens;

namespace UELib.Branch.UE3.RL.Tokens;

public class AlternativeExtendedNativeFunctionToken : UStruct.UByteCodeDecompiler.Token
{
    // Build custom extended native function opcode map
    private static readonly TokenMap s_extendedNativeFunctionTokenMap = new()
    {
        { 0x3A, typeof(StepToken) },
        { 0x3D, typeof(StepToken) },
        { 0x52, typeof(UStruct.UByteCodeDecompiler.PrimitiveInlineCastToken) },
        { 0x53, typeof(UStruct.UByteCodeDecompiler.PrimitiveInlineCastToken) },
        { 0x54, typeof(UStruct.UByteCodeDecompiler.PrimitiveInlineCastToken) },
        { 0x55, typeof(UStruct.UByteCodeDecompiler.PrimitiveInlineCastToken) },
        { 0x56, typeof(UStruct.UByteCodeDecompiler.PrimitiveInlineCastToken) },
        { 0x57, typeof(UStruct.UByteCodeDecompiler.PrimitiveInlineCastToken) },
        { 0x60, typeof(UStruct.UByteCodeDecompiler.PrimitiveInlineCastToken) },
        { 0x65, typeof(UStruct.UByteCodeDecompiler.PrimitiveInlineCastToken) },
        { 0x67, typeof(UStruct.UByteCodeDecompiler.PrimitiveInlineCastToken) },
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

        ushort nativeIndex = (ushort)(opCode + 5000);
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
