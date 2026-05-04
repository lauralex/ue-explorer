using UELib.Core;

namespace UELib.Branch.UE3.RL.Tokens;

/// <summary>
/// Context-aware handler for byte 0x00 in RL bytecode.
/// <para>
/// In real RL bytecode, 0x00 appears in three unrelated roles:
/// <list type="bullet">
///   <item>At top level immediately before a return-value expression
///         (<c>0x00 0x2F</c> = `return true;`, <c>0x00 0x1C</c> = `return 0;`).
///         Looks like baseline EX_Return — needs to consume the next sub-expression.</item>
///   <item>Inside a variadic native-call argument list, between the last real
///         argument and the EmptyParm/EndFunctionParms terminator. Looks like
///         alignment / padding — must NOT consume the EmptyParm or it scrambles
///         the call's rendering into <c>LogInternal(..., return return ...)</c>.</item>
///   <item>Between top-level statements as multi-byte alignment padding (often
///         3–5 consecutive 0x00 bytes). Same constraint as the variadic case —
///         must not consume neighbouring tokens.</item>
/// </list>
/// </para>
/// <para>
/// Disambiguation:
/// <list type="number">
///   <item>If <see cref="UStruct.UByteCodeDecompiler.VariadicCallDepth"/> &gt; 0
///         we're inside a call's arg list — always padding.</item>
///   <item>If the immediate next byte in the stream is also 0x00, this is part of
///         a multi-byte padding run between statements — padding.</item>
///   <item>Otherwise, treat as EX_Return and consume the next sub-expression.</item>
/// </list>
/// </para>
/// </summary>
public class ContextAwareReturnTokenRL : UStruct.UByteCodeDecompiler.Token
{
    private bool _IsReturnContext;

    public override void Deserialize(IUnrealStream stream)
    {
        if (Decompiler.VariadicCallDepth > 0)
        {
            _IsReturnContext = false;
            return;
        }

        // Only treat 0x00 as EX_Return when it's a top-level statement
        // (DeserializeNext called from the central function-body loop, depth 1).
        // Recursive calls — sub-expressions of JumpIfNot conditions, Let RHS,
        // call argument lists not yet bumping VariadicCallDepth, etc. — should
        // always treat 0x00 as padding so we never accidentally consume an
        // operand mid-expression.
        if (Decompiler.DeserializationDepth > 1)
        {
            _IsReturnContext = false;
            return;
        }

        // After a per-token recovery in the central deserialize loop, the next
        // byte we read is whatever was left over in the failed scope's
        // bytecode — usually unparseable garbage from a token whose Deserialize
        // threw mid-stream. Treating it as a fresh top-level EX_Return would
        // consume the next real token as a bogus return value (regression
        // pattern: `if(@NULL @ return StaticMesh -= )` in Ball_TA.PostBeginPlay).
        if (Decompiler.LastIterationRecovered)
        {
            _IsReturnContext = false;
            return;
        }

        // If the IMMEDIATELY-PREVIOUS-IN-STREAM token was a 0x00 in padding
        // mode at the same depth, we're inside the tail of that padding run.
        // The check is restricted to stream-consecutive tokens (previous
        // StoragePosition + 1 == this StoragePosition) so it doesn't
        // over-trigger on padding 0x00s that lived inside an earlier
        // variadic call.
        // NOTE: DeserializeNext adds *this* token to DeserializedTokens
        // BEFORE calling its Deserialize, so tokens[Count-1] is *this*; the
        // genuinely-previous token is at Count-2.
        var tokens = Decompiler.DeserializedTokens;
        if (tokens.Count >= 2)
        {
            var prev = tokens[tokens.Count - 2];
            if (prev is ContextAwareReturnTokenRL prevContextAware
                && !prevContextAware._IsReturnContext
                && prev.StoragePosition + 1 == StoragePosition)
            {
                _IsReturnContext = false;
                return;
            }
        }

        // Peek one byte without advancing the stream. If it's also 0x00, we're
        // sitting at the head of a multi-byte padding run — don't consume a
        // sub-expression.
        long savedPosition = stream.Position;
        try
        {
            byte next = stream.ReadByte();
            stream.Position = savedPosition;
            if (next == 0x00)
            {
                _IsReturnContext = false;
                return;
            }
        }
        catch
        {
            stream.Position = savedPosition;
            _IsReturnContext = false;
            return;
        }

        _IsReturnContext = true;
        DeserializeNext(); // return-value expression
    }

    public override string Decompile()
    {
        if (!_IsReturnContext)
        {
            return string.Empty;
        }

        Decompiler.MarkSemicolon();
        string sub = DecompileNext();
        // If the consumed sub-expression renders to empty (e.g. EX_ReturnNothing
        // safety-net at end of function), emit `return;` without trailing space.
        return string.IsNullOrEmpty(sub) ? "return" : $"return {sub}";
    }
}
