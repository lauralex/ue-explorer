using System.Collections.Generic;
using UELib.Core;

namespace UELib.Branch.UE3.RL.Tokens;

/// <summary>
/// RL byte 0x37 — conditional/null-guarded call.
/// Wire format (verified against UStruct::SerializeExpr at sub_7FF6CD38C840 case 0x37):
///   * 1 sub-expression (receiver / null-tested expression)
///   * 1 sub-expression (function/method to invoke)
///   * u16 SkipDistance (bytes to skip past variadic body if receiver is null)
///   * Variadic body (function arguments, terminated by EndFunctionParms 0x3E)
///   * Optional 0x20 EX_DebugInfo trailer
///
/// Runtime handler at GNatives[0x37] (sub_7FF6CD2F5810) gates the variadic-args
/// dispatch on the receiver being non-null. If receiver is null, advances the
/// instruction pointer by SkipDistance to skip past the args entirely.
///
/// Not observed in current TAGame/Engine/ProjectX fixtures — implementation is
/// defensive, mapped here so that if 0x37 ever appears the parser doesn't desync.
/// Was wrongly FloatConstToken (4-byte literal) — would under-read by significant
/// margin and cascade into garbled output for the function containing it.
/// </summary>
public class NullConditionalCallTokenRL : UStruct.UByteCodeDecompiler.Token
{
    public ushort SkipDistance;

    public override void Deserialize(IUnrealStream stream)
    {
        DeserializeNext(); // receiver
        DeserializeNext(); // function/method

        SkipDistance = stream.ReadUInt16();
        Decompiler.AlignSize(sizeof(ushort));

        // Variadic args terminated by EndFunctionParms (0x3E)
        while (!(DeserializeNext() is UStruct.UByteCodeDecompiler.EndFunctionParmsToken)) ;

        DeserializeDebugToken();
    }

    public override string Decompile()
    {
        string receiver = DecompileNext();
        string method = DecompileNext();

        var args = new List<string>();
        while (Decompiler.CurrentTokenIndex + 1 < Decompiler.DeserializedTokens.Count)
        {
            var next = Decompiler.DeserializedTokens[Decompiler.CurrentTokenIndex + 1];
            if (next is UStruct.UByteCodeDecompiler.EndFunctionParmsToken)
            {
                Decompiler.CurrentTokenIndex++;
                break;
            }
            string arg = DecompileNext();
            if (!string.IsNullOrEmpty(arg)) args.Add(arg);
        }

        return $"{receiver}?.{method}({string.Join(", ", args)})";
    }
}
