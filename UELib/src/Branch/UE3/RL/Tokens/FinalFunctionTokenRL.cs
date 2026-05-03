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
        // The bytecode encodes a packed UStruct* index that can point outside the package's
        // import/export tables in the current RL build (especially after the 0x0F mapping was
        // added — see RL_OPCODE_ANALYSIS.md). Catching the lookup failure lets the parser
        // continue reading the variadic body so downstream tokens stay aligned. Decompile()
        // below handles the resulting null Function.
        try
        {
            base.Deserialize(stream);
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
