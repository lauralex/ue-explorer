using System;
using System.Collections.Generic;
using UELib.Core;

namespace UELib.Branch.UE3.RL.Tokens;
public class FinalFunctionTokenRL : UStruct.UByteCodeDecompiler.FinalFunctionToken
{
    // Map of function names and respective token
    private static readonly Dictionary<string, Type> FunctionTokenMap = new()
    {
        // Add RL-specific function tokens here
        // Example:
        // { "RL_SpecificFunction", typeof(RL_SpecificFunctionToken) },
        { "AllControllers", typeof(AllControllersJumpTokenRL) },
    };

    public override void Deserialize(IUnrealStream stream)
    {
        // RL's 0x0F (and 0x38) FinalFunction shape reads UFunction* + 1 mandatory byte
        // (purpose unknown — possibly arg-count, vtable hint, or flags). Without this skip
        // byte, the next token (typically 0x3E IntZero) gets parsed as a spurious first
        // argument — visible in `super.PostBeginPlay(0)` instead of `super.PostBeginPlay()`.
        // Catching the ReadObject failure lets the parser continue reading the variadic
        // body so downstream tokens stay aligned. Decompile() below handles null Function.
        try
        {
            Function = stream.ReadObject<UFunction>();
            Decompiler.AlignObjectSize();
            stream.ReadByte();
            Decompiler.AlignSize(sizeof(byte));
            DeserializeCall(stream);
        }
        catch (ArgumentOutOfRangeException)
        {
            Function = null;
            DeserializeCall(stream);
        }
        catch (InvalidCastException)
        {
            Function = null;
            DeserializeCall(stream);
        }

        // Additional deserialization logic for RL can be added here

        if (Function != null && FunctionTokenMap.TryGetValue(Function.Name, out var tokenType))
        {
            // Instantiate and use the specific token type as needed
            var tokenInstance = (UStruct.UByteCodeDecompiler.Token)Activator.CreateInstance(tokenType)!;
            // Further processing with tokenInstance
            var scriptPosition = Decompiler.ScriptPosition;
            Decompiler.DeserializedTokens.Add(tokenInstance);
            tokenInstance.OpCode = 0;
            tokenInstance.Decompiler = Decompiler;
            tokenInstance.Position = scriptPosition;
            tokenInstance.StoragePosition = (int)(stream.Position - Container.ScriptOffset - 1);
            tokenInstance.Deserialize(stream);
            tokenInstance.Size = (short)(Decompiler.ScriptPosition - scriptPosition);
            tokenInstance.StorageSize = (short)(stream.Position - Container.ScriptOffset - tokenInstance.StoragePosition);
            tokenInstance.PostDeserialized();
        }
    }

    public override string Decompile()
    {
        if (Function == null)
        {
            // Lookup failed during Deserialize; consume the variadic body via DecompileCall and
            // emit a placeholder so output is at least balanced (`(args)`).
            return DecompileCall("/* unresolved final function */");
        }

        string output = base.Decompile();

        if (FunctionTokenMap.TryGetValue(Function.Name, out var tokenType))
        {
            if (tokenType == typeof(AllControllersJumpTokenRL))
            {
                DecompileNext();
                return $"{output} ?";
            }
        }

        return output;
    }
}
