using UELib.Core;

namespace UELib.Branch.UE3.RL.Tokens;

/// <summary>
/// Wire format: optional 0x20 EX_DebugInfo prefix + 2 sub-expressions. Used by GNatives[0x09]
/// (sub_7FF6CD2F5930) which evaluates sub-A for side effects, then evaluates sub-B and
/// returns its result. Effectively a comma operator: <c>(A, B)</c> with the value of <c>B</c>.
///
/// Cooker emits this around for-loop init expressions like <c>for (Index = 0; ...; ...)</c>
/// where sub-A is the assignment and sub-B is the variable being initialized (for the
/// "expression value" the for-loop notation requires). Rendering naively as a Let
/// (<c>A = B</c>) produces nonsense like <c>Index = false = Index</c>.
///
/// We render just sub-A (the side-effecting statement) and skip sub-B. For for-loop
/// init this gives the source-level <c>Index = 0;</c> back. If 0x09 ever appears in a
/// context where sub-B's value matters (rare), we lose that — accepted tradeoff.
/// </summary>
public class DiscardKeepTokenRL : UStruct.UByteCodeDecompiler.Token
{
    public override void Deserialize(IUnrealStream stream)
    {
        DeserializeNext(); // sub-A (side effect)
        DeserializeNext(); // sub-B (discarded value)
    }

    public override string Decompile()
    {
        string a = DecompileNext();
        DecompileNext(); // skip sub-B in output
        return a;
    }
}
