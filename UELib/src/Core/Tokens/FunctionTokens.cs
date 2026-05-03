using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using UELib.Branch;
using UELib.ObjectModel.Annotations;
using UELib.Tokens;

namespace UELib.Core
{
    public partial class UStruct
    {
        public partial class UByteCodeDecompiler
        {
            [ExprToken(ExprToken.EndFunctionParms)]
            public class EndFunctionParmsToken : Token
            {
            }

            public abstract class FunctionToken : Token
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                protected UName DeserializeFunctionName(IUnrealStream stream)
                {
                    return ReadName(stream);
                }

                protected virtual void DeserializeCall(IUnrealStream stream)
                {
                    DeserializeParms();
                    Decompiler.DeserializeDebugToken();
                }

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                private void DeserializeParms()
                {
#pragma warning disable 642
                    while (!(DeserializeNext() is EndFunctionParmsToken)) ;
#pragma warning restore 642
                }

                private static string SafeDecompile(Token t)
                {
                    // Mirror DecompileNext's narrow-catch policy so a leaf NRE/AOOR doesn't
                    // bubble up and abort the parent operator/call statement.
                    try { return t.Decompile(); }
                    catch (NullReferenceException) { return "/*<exc NRE>*/"; }
                    catch (ArgumentOutOfRangeException) { return "/*<exc AOOR>*/"; }
                }

                private static string PrecedenceToken(Token t)
                {
                    if (!(t is FunctionToken))
                        return SafeDecompile(t);

                    // Always add ( and ) unless the conditions below are not met, in case of a VirtualFunctionCall.
                    var addParenthesis = true;
                    switch (t)
                    {
                        case NativeFunctionToken token:
                            addParenthesis = token.NativeItem != null && token.NativeItem.Type == FunctionType.Operator;
                            break;
                        case FinalFunctionToken token:
                            addParenthesis = token.Function != null && token.Function.IsOperator();
                            break;
                    }

                    return addParenthesis
                        ? $"({SafeDecompile(t)})"
                        : SafeDecompile(t);
                }

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                private bool NeedsSpace(string operatorName)
                {
                    return char.IsUpper(operatorName[0])
                           || char.IsLower(operatorName[0]);
                }

                protected string DecompilePreOperator(string operatorName)
                {
                    string operand = DecompileNext();
                    AssertSkipCurrentToken<EndFunctionParmsToken>();

                    // Only space out if we have a non-symbol operator name.
                    return NeedsSpace(operatorName)
                        ? $"{operatorName} {operand}"
                        : $"{operatorName}{operand}";
                }

                protected string DecompileOperator(string operatorName)
                {
                    // Bounds-check NextToken so a truncated DeserializedTokens list (which can
                    // happen after central-loop deserialize recovery on a malformed function)
                    // doesn't throw ArgumentOutOfRangeException and abort the parent decompile.
                    string operand1;
                    do
                    {
                        if (Decompiler.CurrentTokenIndex + 1 >= Decompiler.DeserializedTokens.Count)
                        {
                            return $"/* truncated */ {operatorName} /* truncated */";
                        }
                        operand1 = PrecedenceToken(NextToken());
                    } while (string.IsNullOrEmpty(operand1));

                    string operand2;
                    do
                    {
                        if (Decompiler.CurrentTokenIndex + 1 >= Decompiler.DeserializedTokens.Count)
                        {
                            return $"{operand1} {operatorName} /* truncated */";
                        }
                        operand2 = PrecedenceToken(NextToken());
                    } while (string.IsNullOrEmpty(operand2));

                    var output =
                        $"{operand1} {operatorName} {operand2}";
                    AssertSkipCurrentToken<EndFunctionParmsToken>();
                    return output;
                }

                protected string DecompilePostOperator(string operatorName)
                {
                    string operand = DecompileNext();
                    AssertSkipCurrentToken<EndFunctionParmsToken>();

                    // Only space out if we have a non-symbol operator name.
                    return NeedsSpace(operatorName)
                        ? $"{operand} {operatorName}"
                        : $"{operand}{operatorName}";
                }

                protected string DecompileCall(string functionName)
                {
                    if (Decompiler._IsWithinClassContext)
                    {
                        functionName = $"static.{functionName}";

                        // Set false elsewhere as well but to be sure we set it to false here to avoid getting static calls inside the params.
                        // e.g.
                        // A1233343.DrawText(Class'BTClient_Interaction'.static.A1233332(static.Max(0, A1233328 - A1233322[A1233222].StartTime)), true);
                        Decompiler._IsWithinClassContext = false;
                    }

                    string arguments = DecompileParms();
                    var output = $"{functionName}({arguments})";
                    return output;
                }

                private string DecompileParms()
                {
                    var tokens = new List<Tuple<Token, string>>();
                    while (Decompiler.CurrentTokenIndex + 1 < Decompiler.DeserializedTokens.Count)
                    {
                        var t = NextToken();
                        // Wrap t.Decompile() so a leaf NRE/AOOR in a sub-token doesn't abort the
                        // containing call statement — matches the policy in DecompileNext and
                        // PrecedenceToken.SafeDecompile.
                        string text;
                        try { text = t.Decompile(); }
                        catch (NullReferenceException) { text = "/*<exc NRE>*/"; }
                        catch (ArgumentOutOfRangeException) { text = "/*<exc AOOR>*/"; }
                        tokens.Add(Tuple.Create(t, text));
                        if (t is EndFunctionParmsToken)
                            break;
                    }

                    var output = new StringBuilder();
                    for (var i = 0; i < tokens.Count; ++i)
                    {
                        var t = tokens[i].Item1; // Token
                        string v = tokens[i].Item2; // Value

                        switch (t)
                        {
                            // Skipped optional parameters
                            case EmptyParmToken _:
                                output.Append(v);
                                break;

                            // End ")"
                            case EndFunctionParmsToken _:
                                output = new StringBuilder(output.ToString().TrimEnd(','));
                                break;

                            // Any passed values
                            default:
                                {
                                    if (i != tokens.Count - 1 && i > 0) // Skipped optional parameters
                                    {
                                        output.Append(v == string.Empty ? "," : ", ");
                                    }

                                    output.Append(v);
                                    break;
                                }
                        }
                    }

                    return output.ToString();
                }
            }

            [ExprToken(ExprToken.FinalFunction)]
            public class FinalFunctionToken : FunctionToken
            {
                public UFunction Function;

                public override void Deserialize(IUnrealStream stream)
                {
                    Function = stream.ReadObject<UFunction>();
                    Decompiler.AlignObjectSize();

                    DeserializeCall(stream);
                }

                public override string Decompile()
                {
                    var output = string.Empty;
                    // Support for non native operators.
                    if (Function.IsPost())
                    {
                        output = DecompilePreOperator(Function.FriendlyName);
                    }
                    else if (Function.IsPre())
                    {
                        output = DecompilePostOperator(Function.FriendlyName);
                    }
                    else if (Function.IsOperator())
                    {
                        output = DecompileOperator(Function.FriendlyName);
                    }
                    else
                    {
                        // Calling Super??.
                        if (Function.Name == Decompiler._Container.Name && !Decompiler._IsWithinClassContext)
                        {
                            output = "super";

                            // Check if the super call is within the super class of this functions outer(class)
                            var container = Decompiler._Container;
                            var context = (UField)container.Outer;
                            // ReSharper disable once PossibleNullReferenceException
                            var contextFuncOuterName = context.Name;
                            // ReSharper disable once PossibleNullReferenceException
                            var callFuncOuterName = Function.Outer.Name;
                            if (context.Super == null || callFuncOuterName != context.Super.Name)
                            {
                                // If there's no super to call, then we have a recursive call.
                                if (container.Super == null)
                                {
                                    output += $"({contextFuncOuterName})";
                                }
                                else
                                {
                                    // Different owners, then it is a deep super call.
                                    if (callFuncOuterName != contextFuncOuterName)
                                    {
                                        output += $"({callFuncOuterName})";
                                    }
                                }
                            }

                            output += ".";
                        }

                        output += DecompileCall(Function.Name);
                    }

                    Decompiler._CanAddSemicolon = true;
                    return output;
                }
            }

            [ExprToken(ExprToken.VirtualFunction)]
            public class VirtualFunctionToken : FunctionToken
            {
                public UName FunctionName;

                public override void Deserialize(IUnrealStream stream)
                {
                    // FIXME: Version, seen in EndWar (222) and R6Vegas (v241), gone at least since RoboBlitz (369)
                    if (stream.Version >= (uint)PackageObjectLegacyVersion.UE3 &&
                        stream.Version <= 241)
                    {
                        byte isSuper = stream.ReadByte();
                        Decompiler.AlignSize(sizeof(byte));
                    }

                    FunctionName = DeserializeFunctionName(stream);
                    DeserializeCall(stream);
                }

                public override string Decompile()
                {
                    Decompiler._CanAddSemicolon = true;
                    return DecompileCall(FunctionName);
                }
            }

            [ExprToken(ExprToken.GlobalFunction)]
            public class GlobalFunctionToken : FunctionToken
            {
                public UName FunctionName;

                public override void Deserialize(IUnrealStream stream)
                {
                    FunctionName = DeserializeFunctionName(stream);
                    DeserializeCall(stream);
                }

                public override string Decompile()
                {
                    Decompiler._CanAddSemicolon = true;
                    return $"global.{DecompileCall(FunctionName)}";
                }
            }

            [ExprToken(ExprToken.DelegateFunction)]
            public class DelegateFunctionToken : FunctionToken
            {
                public byte? IsLocal;
                public UProperty DelegateProperty;
                public UName FunctionName;

                public override void Deserialize(IUnrealStream stream)
                {
                    // FIXME: Version
                    if (stream.Version >= (uint)PackageObjectLegacyVersion.IsLocalAddedToDelegateFunctionToken)
                    {
                        IsLocal = stream.ReadByte();
                        Decompiler.AlignSize(sizeof(byte));
                    }

                    DelegateProperty = stream.ReadObject<UProperty>();
                    Decompiler.AlignObjectSize();

                    FunctionName = DeserializeFunctionName(stream);
                    DeserializeCall(stream);
                }

                public override string Decompile()
                {
                    Decompiler._CanAddSemicolon = true;
                    return DecompileCall(FunctionName);
                }
            }

            [ExprToken(ExprToken.NativeFunction)]
            public class NativeFunctionToken : FunctionToken
            {
                public NativeTableItem NativeItem;

                // Cross-package cache of native index → UFunction name. Native indexes are global
                // across all script packages (a native at index N has the same name everywhere),
                // so when one package's NTL doesn't have the entry, we look in the merged map of
                // every UnrealPackage we've ever decompile-touched. Engine.upk declares ~44
                // natives, Core.upk ~161; together they cover most of the non-extended natives RL
                // bytecode references.
                private static readonly System.Collections.Generic.Dictionary<ushort, string> s_globalNativeNames = new();
                private static readonly System.Collections.Generic.HashSet<UnrealPackage> s_indexedPackages = new();
                private static readonly object s_nativeNamesLock = new();

                /// <summary>
                /// Index every UFunction with NativeToken != 0 from <paramref name="pkg"/> into the
                /// process-global native-name cache. Called automatically the first time a token
                /// from <paramref name="pkg"/> hits the resolution fallback, but loaders (MCP,
                /// Repro tool) can also call this eagerly after loading auxiliary packages
                /// (Core/Engine) so that native names declared there can be picked up by decompile
                /// calls on a different package.
                /// </summary>
                public static void IndexPackageNatives(UnrealPackage pkg)
                {
                    if (pkg == null) return;
                    lock (s_nativeNamesLock)
                    {
                        if (!s_indexedPackages.Add(pkg)) return;
                        foreach (var obj in pkg.Objects)
                        {
                            if (obj is UFunction fn && fn.NativeToken != 0)
                            {
                                // Don't overwrite — first registration wins. Conflicting names
                                // would be a mismatched-build symptom worth surfacing rather than
                                // silently masking.
                                if (!s_globalNativeNames.ContainsKey(fn.NativeToken))
                                    s_globalNativeNames[fn.NativeToken] = fn.Name.ToString();
                            }
                        }
                        // For RL specifically, fall back to the binary-extracted GNatives map. This
                        // catches cases where the user loaded a non-script-declaring package (e.g.
                        // TAGame) without preloading Engine/Core. Loaded UFunction names always win.
                        if (pkg.Build?.Name == UnrealPackage.GameBuild.BuildName.RocketLeague)
                        {
                            foreach (var kv in UELib.Branch.UE3.RL.RocketLeagueNativeNames.Map)
                            {
                                if (!s_globalNativeNames.ContainsKey(kv.Key))
                                    s_globalNativeNames[kv.Key] = kv.Value;
                            }
                        }
                    }
                }

                private string ResolveNameFromPackage(ushort index)
                {
                    IndexPackageNatives(Package);
                    lock (s_nativeNamesLock)
                    {
                        return s_globalNativeNames.TryGetValue(index, out var name) ? name : null;
                    }
                }

                public override void Deserialize(IUnrealStream stream)
                {
                    DeserializeCall(stream);
                }

                public override string Decompile()
                {
                    // NativeItem can be null when the native index isn't in the loaded NTL (NTL
                    // drift across game versions). Fall back to a comment + DecompileCall so the
                    // rest of the surrounding statement still has a chance to render.
                    if (NativeItem == null)
                    {
                        var fallback = DecompileCall($"/* unresolved native 0x{OpCode:X2} */");
                        Decompiler._CanAddSemicolon = true;
                        return fallback;
                    }

                    // If the NTL gave us a generated placeholder ("__NFUN_NNN__"), try to upgrade
                    // to the real name by looking it up against any UFunction in the loaded package
                    // with a matching NativeToken. Catches script-declared natives (Engine.upk
                    // declares ~44, Core.upk ~161) without needing a refreshed .NTL file.
                    string displayName = NativeItem.Name;
                    if (displayName != null && displayName.StartsWith("__NFUN_", System.StringComparison.Ordinal))
                    {
                        string resolved = ResolveNameFromPackage((ushort)NativeItem.ByteToken);
                        if (resolved != null) displayName = resolved;
                    }

                    string output;
                    switch (NativeItem.Type)
                    {
                        case FunctionType.Function:
                            output = DecompileCall(displayName);
                            break;

                        case FunctionType.Operator:
                            output = DecompileOperator(displayName);
                            break;

                        case FunctionType.PostOperator:
                            output = DecompilePostOperator(displayName);
                            break;

                        case FunctionType.PreOperator:
                            output = DecompilePreOperator(displayName);
                            break;

                        default:
                            output = DecompileCall(displayName);
                            break;
                    }

                    Decompiler._CanAddSemicolon = true;
                    return output;
                }
            }
        }
    }
}
