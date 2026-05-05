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
/// Two patterns observed in cooked RL bytecode:
///
///   * **For-loop init**: sub-A is the side-effecting assignment
///     (e.g. <c>Index = 0</c>), sub-B is the variable read for the for-expression's
///     "value" (e.g. <c>Index</c>). Both render non-empty; A is the meaningful one.
///
///   * **Function out-param wrap**: sub-A is a placeholder Nothing (renders empty),
///     sub-B is the actual variable reference (e.g. <c>PlayerID</c> in
///     <c>GetUniquePlayerId(LocalUserNum, PlayerID)</c>). A is empty; B is the
///     meaningful one.
///
/// Render whichever sub is non-empty, preferring A (the documented side-effect
/// semantic) when both are present.
/// </summary>
public class DiscardKeepTokenRL : UStruct.UByteCodeDecompiler.Token
{
    public override void Deserialize(IUnrealStream stream)
    {
        DeserializeNext(); // sub-A (side effect)
        DeserializeNext(); // sub-B (value used in expression context)
    }

    public override string Decompile()
    {
        string a = DecompileNext();
        string b = DecompileNext();

        if (!string.IsNullOrEmpty(a))
        {
            return a;
        }

        return b;
    }
}
