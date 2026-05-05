using System;
using UELib.Core;

namespace UELib.Branch.UE3.RL.Tokens;

/// <summary>
/// RL bytes 0x08, 0x12, 0x33, 0x52 — defensive variant of
/// <see cref="UStruct.UByteCodeDecompiler.EatReturnValueToken"/>.
///
/// Wire format: 4 disk → 8 mem UProperty index (when version >= 201, which RL is).
///
/// The base reads <c>stream.ReadObject&lt;UProperty&gt;()</c> followed by
/// <see cref="UStruct.UByteCodeDecompiler.AlignObjectSize"/>. ReadObject advances the
/// disk cursor by 4 bytes BEFORE looking up the import — so when the lookup throws
/// (NTL drift / stale import indices in cooked RL packages), the disk cursor is
/// already past the index but <see cref="UStruct.UByteCodeDecompiler.ScriptPosition"/>
/// hasn't been bumped via AlignObjectSize.
///
/// Worse, the throw aborts the parent token's Deserialize (cascading size-0
/// tokens up the call stack, e.g. BoolVar → ContextTokenRL → EatReturnValue chains
/// in OnlinePlayerInterfaceEOS.RequestNativePlatformAuthTicket). Catching here
/// keeps Position/Storage in sync and lets the parent finish reading its remaining
/// sub-expressions.
/// </summary>
public class EatReturnValueTokenRL : UStruct.UByteCodeDecompiler.EatReturnValueToken
{
    public override void Deserialize(IUnrealStream stream)
    {
        if (stream.Version < 201) return;

        try
        {
            ReturnValueProperty = stream.ReadObject<UProperty>();
        }
        catch (ArgumentOutOfRangeException)
        {
            ReturnValueProperty = null;
        }
        catch (InvalidCastException)
        {
            ReturnValueProperty = null;
        }
        Decompiler.AlignObjectSize();
    }
}
