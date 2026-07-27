using System;
using System.Collections.Generic;
using UELib.Core;

namespace UELib.Branch.UE3.RL.Tokens;
public class FinalFunctionTokenRL : UStruct.UByteCodeDecompiler.FinalFunctionToken
{
    // Map of function names and respective token
    private static readonly Dictionary<string, Type> FunctionTokenMap = new()
    {
        { "AllControllers", typeof(AllControllersJumpTokenRL) },
        { "AllAttachments", typeof(AllControllersJumpTokenRL) },
        { "AllNavigationPoints", typeof(AllControllersJumpTokenRL) },
        { "AllObjects", typeof(AllControllersJumpTokenRL) },
        { "AllObjectsOfType", typeof(AllControllersJumpTokenRL) },
        { "AllProductsBySlot", typeof(AllControllersJumpTokenRL) },
        { "AllSequenceObjects", typeof(AllControllersJumpTokenRL) },
        { "AllSkelControlsNamed", typeof(AllControllersJumpTokenRL) },
        { "AllValues", typeof(AllControllersJumpTokenRL) },
        { "LocalPlayerControllers", typeof(AllControllersJumpTokenRL) },
    };

    public override void Deserialize(IUnrealStream stream)
    {
        // RL's 0x0F FinalFunction wire format: 8-byte UFunction* + variadic args until
        // EX_EndFunctionParms (now correctly mapped to byte 0x3E, was wrongly 0x4C).
        // The runtime handler at GNatives[0x0F] (sub_7FF6CD2F5F00) reads exactly 8 bytes
        // and dispatches vtable[76] which loops the variadic body. No "+1 mandatory byte"
        // exists in the binary — the previous +1 was a band-aid for the wrong terminator.
        try
        {
            Function = stream.ReadObject<UFunction>();
            Decompiler.AlignObjectSize();
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

        // base.Decompile dereferences Function.Outer.Name in its super-call branch.
        // Cooked RL packages can leave Function.Outer null when the resolved object isn't
        // fully backed by import/export metadata, so guard explicitly instead of catching
        // any NRE — other NREs out of base.Decompile likely indicate real bugs and should
        // surface, not be swallowed.
        if (Function.Outer == null)
        {
            return DecompileCall($"/* unresolved final function: {Function.Name} */");
        }

        if (TryResolveOperatorSymbol(Function, out var operatorEntry))
        {
            string operatorOutput = operatorEntry.Type switch
            {
                FunctionType.PreOperator => DecompilePreOperator(operatorEntry.Symbol),
                FunctionType.PostOperator => DecompilePostOperator(operatorEntry.Symbol),
                FunctionType.Operator => DecompileOperator(operatorEntry.Symbol),
                _ => DecompileCall(Function.Name),
            };
            Decompiler.MarkSemicolon();
            return operatorOutput;
        }

        // Wrap base.Decompile in narrow NRE/AOOR catch — inside its body, the super-call branch
        // dereferences `Decompiler._Container.Outer` (cast to UField) which can be null in cooked
        // packages even when our explicit Function.Outer guard above passes; the AOOR path comes
        // from sub-token recursion that escapes DecompileNext/DecompileParms.
        string output;
        try
        {
            output = base.Decompile();
        }
        catch (NullReferenceException)
        {
            return DecompileCall($"/* base-decomp NRE: {Function.Name} */");
        }
        catch (ArgumentOutOfRangeException)
        {
            return DecompileCall($"/* base-decomp AOOR: {Function.Name} */");
        }

        if (FunctionTokenMap.TryGetValue(Function.Name, out var tokenType))
        {
            if (tokenType == typeof(AllControllersJumpTokenRL))
            {
                DecompileNext();
                return $"foreach {output}";
            }
        }

        return output;
    }

    private static bool TryResolveOperatorSymbol(
        UFunction function,
        out StandardOperatorSymbols.OperatorEntry entry)
    {
        entry = default;

        if (function.NativeToken != 0 && StandardOperatorSymbols.Map.TryGetValue(function.NativeToken, out entry))
        {
            return true;
        }

        if (!StandardOperatorSymbols.TryResolveByName(function.Name, out entry))
        {
            return false;
        }

        if (function.IsPre())
        {
            entry = new StandardOperatorSymbols.OperatorEntry(entry.Symbol, FunctionType.PreOperator, entry.Precedence);
        }
        else if (function.IsPost())
        {
            entry = new StandardOperatorSymbols.OperatorEntry(entry.Symbol, FunctionType.PostOperator, entry.Precedence);
        }

        return true;
    }
}
