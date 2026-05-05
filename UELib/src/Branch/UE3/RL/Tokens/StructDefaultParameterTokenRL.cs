using System;
using UELib.Core;

namespace UELib.Branch.UE3.RL.Tokens;

/// <summary>
/// RL byte 0x5B — none-coalescing operator (<c>A ?? B</c>).
/// The binary handler at GNatives[0x5B] (sub_7FF6CD308170) does:
///   * Read 4-byte UStruct* (expanded to 8 in-memory) — the LHS-property's struct type
///   * Compute LHS-property address as <c>Locals[StructRef.offset]</c>
///   * Dispatch sub-1 — writes to the LHS-property slot ("primary" value)
///   * Read u16 SkipDistance
///   * Test the LHS slot against an all-zeros buffer of the struct's size
///   * If equal (i.e. the struct is "default"/"none"): dispatch sub-2 ("fallback")
///   * Else: skip <c>SkipDistance</c> bytes past sub-2 (use sub-1's value)
///
/// That's the runtime shape of the none-coalescing operator: <c>sub_1 ?? sub_2</c>
/// (use <c>sub_1</c> unless it's none/default, in which case use <c>sub_2</c>).
/// The cooker emits a synthetic local named <c>NoneCoalescing_0xN</c> that
/// holds the LHS slot, and the surrounding decompile typically reads
/// <c>local Foo NoneCoalescing_0x1;</c> in the function header.
///
/// Renders as <c>sub_1 ?? sub_2</c> — UnrealScript itself doesn't have a `??`
/// operator, but the form is unambiguous and matches what the user-facing
/// source likely was. Was wrongly mapped to <c>VectorConstToken</c> (12 bytes
/// — read the next 12 bytes as 3 floats), producing nonsense like
/// <c>ControllerRef = vect(0, 0, -9.52e21)</c> for a non-vector type.
/// </summary>
public class StructDefaultParameterTokenRL : UStruct.UByteCodeDecompiler.Token
{
    public UStruct StructRef;
    public ushort SkipDistance;

    public override void Deserialize(IUnrealStream stream)
    {
        // Defensive lookup: cooked RL packages can carry UStruct indices that resolve
        // to imports out-of-range or to non-UStruct objects (NTL drift / stale import
        // tables). Without try/catch, ReadObject throws BEFORE AlignObjectSize runs —
        // ScriptPosition stays uncompensated for the 4→8 expansion, the per-token
        // catch in ByteCodeDecompiler resyncs to the buffer cursor, and the outer
        // parse abandons the rest of THIS token's body (LHS sub + u16 + RHS sub),
        // leaving them as orphan top-level tokens. Visible in
        // Car_TA.GetPreviewTeamIndex as `ControllerRef = PlayerController_TA(Controller);
        // return GameEvent.LocalPlayers[0];` where the fallback became a sibling
        // statement of the assignment instead of being wrapped in `?? GameEvent.LocalPlayers[0]`.
        try
        {
            StructRef = stream.ReadObject<UStruct>();
        }
        catch (ArgumentOutOfRangeException)
        {
            StructRef = null;
        }
        catch (InvalidCastException)
        {
            StructRef = null;
        }
        Decompiler.AlignObjectSize();

        DeserializeNext();

        SkipDistance = stream.ReadUInt16();
        Decompiler.AlignSize(sizeof(ushort));

        DeserializeNext();
    }

    public override string Decompile()
    {
        string primary = DecompileNext();
        string fallback = DecompileNext();
        if (string.IsNullOrEmpty(fallback))
        {
            return primary;
        }

        if (string.IsNullOrEmpty(primary))
        {
            return fallback;
        }

        return $"{primary} ?? {fallback}";
    }
}
