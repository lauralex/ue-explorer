using System;
using UELib.Core;
using UELib.Core.Tokens;

namespace UELib.Branch.UE3.RL.Tokens;

/// <summary>
/// RL's chained-native actor iterators under byte 0x71 (for example
/// 0x71 0x31 = ChildActors) use a native-call argument list followed by a
/// u16 foreach end offset. The normal NativeFunctionToken consumes only the
/// call and leaves the offset to orphan as primary opcodes.
/// </summary>
public class NativeIteratorFunctionTokenRL : UStruct.UByteCodeDecompiler.JumpToken
{
    public ushort NativeIndex { get; set; }

    public override void Deserialize(IUnrealStream stream)
    {
        var tokenFactory = Package.Branch.GetTokenFactory(Package);
        var nativeToken = tokenFactory.CreateNativeToken(NativeIndex);

        int scriptPosition = Decompiler.ScriptPosition;
        nativeToken.Decompiler = Decompiler;
        nativeToken.Position = scriptPosition;
        nativeToken.StoragePosition = (int)(stream.Position - Container.ScriptOffset);
        Decompiler.DeserializedTokens.Add(nativeToken);

        nativeToken.Deserialize(stream);
        nativeToken.Size = (short)(Decompiler.ScriptPosition - scriptPosition);
        nativeToken.StorageSize = (short)(stream.Position - Container.ScriptOffset - nativeToken.StoragePosition);
        nativeToken.PostDeserialized();

        CodeOffset = stream.ReadUInt16();
        Decompiler.AlignSize(sizeof(ushort));
    }

    public override string Decompile()
    {
        AddForEachNest();
        SetEndComment();

        string call;
        try
        {
            call = DecompileNext();
        }
        catch (NullReferenceException)
        {
            call = FallbackName() + "()";
        }
        catch (ArgumentOutOfRangeException)
        {
            call = FallbackName() + "()";
        }

        SuppressSemicolon();
        return $"foreach {call}";
    }

    private string FallbackName()
    {
        return RocketLeagueNativeNames.Map.TryGetValue(NativeIndex, out var name)
            ? name
            : $"/* unresolved native 0x{NativeIndex:X} */";
    }
}
