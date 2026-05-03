# Rocket League opcode analysis

Notes on reverse-engineering the current RL `UStruct::SerializeExpr` byte mapping. Captures findings from the
session that pulled fresh `D:\Games\rocketleague\TAGame\CookedPCConsole\*.upk` files into
`C:\Users\Authority\Desktop\RE stuff\rldecrypted\absolutelynewupks\`, decrypted them with `RLUPKTool.exe`,
and surveyed them against the current `RocketLeague_Dumped_latest.exe` opened in IDA.

This document is a working note, not a spec. Take everything below as "best evidence so far" and re-verify
before changing live code.

## TL;DR

- Two RL builds are in play. The `.upk` fixtures previously in `rldecrypted\` (and its `upkbackup\`,
  `newupks\`, `newupks2\` subfolders) are **older** than the binary open in IDA. The freshly-decrypted
  packages in `absolutelynewupks\` are version-matched to the current binary.
- The current `EngineBranchRL.BuildTokenMap` is mostly correct for **both** builds. The vast majority of
  primary-table byte→token mappings (including the variadic terminator `0x4C = EndFunctionParmsToken`)
  carry over unchanged.
- The new build adds a handful of unmapped bytes that drive most of the visible decompile failures.
  Highest priority: `0x0F` (31× in survey) and `0x1D` (33×). All other gaps have ≤9 occurrences and look
  low-impact.
- The IDA function `sub_7FF6CD38C840` (the one referenced by the RL `Bad expr token %02x` string from
  `FScriptSerializer.cpp`) **is not** the on-disk `UStruct::SerializeExpr` — it expects `0x3E` as the
  variadic terminator while real bytecode uses `0x4C`. Don't try to read its case numbers as on-disk
  byte values. Its case **shapes** can still be useful as a corroborator once we know the on-disk byte
  for an opcode by another means.
- Most cascading `<decompile failed: ArgumentOutOfRangeException>` errors are not token-map bugs. They
  come from `NativeFunctionToken` / extended-native sub-table lookups failing because the **NTL data**
  has drifted between game versions. That's a separate workstream (regenerate the `.NTL` for the new
  build); fixing the token map alone won't make decompile output read cleanly.

## How the survey was done

1. `mcp__uelib__load_package` on `absolutelynewupks\Engine_decrypted.upk` (handle `c22e7af9`),
   `TAGame_decrypted.upk`, `ProjectX_decrypted.upk`, all with `full_init=true`. Class names in these
   packages have **no** `Engine.` / `TAGame.` prefix — pass bare names like `Actor`, not `Engine.Actor`.
2. Disassembled 81 functions across 28 classes via `mcp__uelib__disassemble_function`, aggregated every
   `opcode_byte` value seen, plus every `(byte, next_byte)` pair.
3. Cross-referenced with the OLD-build `Engine.upk` (handle `b35fd4aa`, before re-decryption) for the
   same function names — looking for which bytes shifted role between versions.

## Headline empirical evidence

### `EndFunctionParmsToken` is **0x4C** in both builds

22 occurrences in the new survey, all decode cleanly (`size:1`, no exception), all immediately followed
by either `0x55` (IteratorPop) (10×) or a fresh statement-start opcode (`0x65 Let` 7×, `0x66 Self` 1×,
`0x6F Conditional` 1×, etc.). This is the canonical end-of-call shape. The existing
`{ 0x4C, typeof(EndFunctionParmsToken) }` entry is correct and load-bearing.

**Counter-example for IDA:** in `sub_7FF6CD38C840` the variadic loop terminates on byte `0x3E`
(`cmp eax, 3Eh; jnz` at `0x7ff6cd38c8ae` and `0x7ff6cd38c8ef`). Byte `0x3E` in real bytecode appears
36 times, every single time as `IntZeroToken` (size 1, value `0`). The `0F 5E … 3E` pattern that looks
like a terminator is actually `<unknown 0F> <ext-native call> <native-idx-bytes> <IntZero arg>`. So
`sub_7FF6CD38C840` is reading something else — probably an internal/canonical bytecode form used by
`FScriptSerializer.cpp` for save-game/network/post-load purposes, not the on-disk script.

### `0x45 = EndFunctionParmsToken` in the existing map is suspect

Empirically: 5 occurrences in the new survey, never as a terminator. Predecessors are mostly `0x00`
not other expressions. Two opcodes claiming the same role is implausible; one of them is a different
thing. **Do not delete the entry yet** — leave it until we see what `0x45` really is. But mark it
"speculative; may be a single-arg/optional-parm marker variant".

### Gaps that need filling for the new build

Bytes currently `UnresolvedToken` / `BadToken` in `EngineBranchRL.BuildTokenMap` that DO appear as
primary opcodes in the new build's bytecode:

| Byte | Hex  | Survey count | Where seen (one example)                | Likely shape (un-verified)                 |
|------|------|--------------|-----------------------------------------|--------------------------------------------|
| 0x0F | 15   | 31           | `Pawn.PostBeginPlay` pos 0              | wraps a function call (see analysis below) |
| 0x1D | 29   | 33           | `Actor.PostBeginPlay`-family            | wraps `0x10` (ExtNative) and itself; prefix-shaped |
| 0x08 | 8    | 9            | `Actor.PostBeginPlay`                   | unknown                                    |
| 0x0D | 13   | 2            | `Camera.PostBeginPlay`                  | unknown                                    |
| 0x21 | 33   | 3            | `Actor.FellOutOfWorld`                  | unknown (also seen as tail padding in OLD) |
| 0x2B | 43   | 5            | `Actor.ShutDown`                        | unknown                                    |
| 0x2C | 44   | 1            | `GFxData_PRI_TA.SetPRI`                 | unknown                                    |
| 0x32 | 50   | 1            | `PlayerController.EnterStartState`      | unknown                                    |
| 0x3D | 61   | 1            | `GameInfo.Logout`                       | unknown                                    |
| 0x3F | 63   | 1            | `GameInfo.PostBeginPlay`                | unknown                                    |
| 0x43 | 67   | 3            | `Camera.PostBeginPlay`                  | unknown                                    |
| 0x50 | 80   | 6            | `Camera.PostBeginPlay`                  | unknown                                    |
| 0x54 | 84   | 2            | `Camera.PostBeginPlay`                  | unknown                                    |
| 0x5A | 90   | 1            | `Pawn.Destroyed`                        | unknown                                    |
| 0x62 | 98   | 1            | `_Types_X.GenerateRandomPrivateMatchName`| unknown                                   |
| 0x68 | 104  | 1            | `_Types_X.JoinCredentialsToString`      | unknown (also tail padding in OLD)         |
| 0x6B | 107  | 7            | `Pawn.Destroyed`                        | unknown                                    |
| 0x6E | 110  | 2            | `GFxData_PRI_TA.SetPRI`                 | unknown                                    |
| 0x3A | 58   | 7            | `Pawn.Destroyed`                        | already used as **inner** byte of `0x5E`/`0x71`; new occurrences as primary need re-check |

The advisor's working budget recommendation is to resolve `0x0F` and `0x1D` first (they account for
~35× more occurrences than the rest combined), ship that, then re-survey.

## `0x0F` — what we know

It's the LEADING byte of many `*.PostBeginPlay`, `*.Destroyed`, and `ReplicatedEvent` overrides in the
new build, where the OLD build had `0x38` (`FinalFunctionTokenRL` — super-call by struct pointer).

**Byte-pair frequencies for `0F`:** followed by `5E` (AltExtNative) 9×, `00` 12×, others scattered.

**OLD `Pawn.PostBeginPlay`** (would-be analog, if same body): not in the OLD survey, but
`Engine.Controller.PostBeginPlay` (parallel role) starts with `38 …` — the standard FinalFunction
shape `0x38 + UStruct*(8) + variadic… + 0x4C + DEBUG`.

**NEW `Pawn.PostBeginPlay`** disassembly (raw byte-by-byte from UELib, with current map's interpretation):

```
pos byte  size  current_token            note
  0  0F    1    UnresolvedToken          ← gap
  1  5E    6    AltExtNative             ← reported size 6 (= 1 opcode + 5 body)
  2  00    5    NativeFunction (idx 5000)← inside the AltExtNative body
  3  00    1    Nothing
  4  00    1    Nothing
  5  3E    1    IntZero
  6  4C    1    EndFunctionParms         ← terminator of the AltExtNative call
  7  55    1    IteratorPop
  8  82    0    AndTokenRL (size 0!)     ← parser desync starts here
  9  12    0    EatReturnValue (size 0!)
 …
```

If `0x0F` has shape "1 sub-token" it would wrap the entire `5E…4C` call (positions 1–6), and position 7
(`0x55 IteratorPop`) would be the next sibling. That sequence makes sense for `PostBeginPlay()` only if
RL is now wrapping every super-call statement in an iterator-pop scope — which is unusual but not
impossible.

If `0x0F` has shape "UStruct*(8) + variadic + DEBUG" (i.e. it's a renamed/renumbered FinalFunction),
positions 1–8 would be the UStruct pointer (8 bytes of opaque ID), and the variadic body would start
at position 9. Looking at byte 9 (`0x12`, currently `EatReturnValueToken`) as a candidate first
sub-token: in baseline UE3 `EX_EatReturnValue` reads `XFER_PROP_POINTER` (8 bytes, no body), which
would carry to position 18, then the next variadic arg (or `0x4C`). Without resolving the surrounding
NTL/property indices we can't independently confirm whether positions 18+ make sense as continued
sub-tokens.

**Best guess at this point:** `0x0F` is the new build's analog of `EX_FinalFunction` (super-call by
resolved pointer). If true, the change to `EngineBranchRL.BuildTokenMap` would be:

```csharp
{ 0x0F, typeof(FinalFunctionTokenRL) },   // displaced 0x38 in the new build (HYPOTHESIS — verify)
```

Cross-check before applying: pick 5 fresh `0x0F`-leading functions, hex-dump the first ~24 bytes of
each, and verify positions 1–8 look like distinct UStruct pointer values across functions (they should
differ — each super target is a different `UFunction` object). If they cluster on small repeating values
that look more like FName indices, the shape is `XFER_FUNC_NAME + variadic + DEBUG` (a `VirtualFunction`
variant) instead.

## `0x1D` — what we know

33× in the survey. Predecessors and successors:
- `1D 10` 13× (followed by `ExtendedNativeFunctionToken`)
- `1D 1D` 10× (followed by another `0x1D`)
- `00 1D` 12× (preceded by a Nothing or operand-zero byte)
- `00 08` 7× (`0x1D` itself doesn't lead here, but it's part of the cluster)

Looks like a **prefix opcode** that wraps an inner expression (most often an extended-native call).
"Prefix that wraps the next opcode" matches several baseline shapes:
- `EX_StructMember` (`XFER_PROP_POINTER + XFERPTR + BYTE + BYTE + 1 sub`) — too heavy, would consume 18+ bytes between `1D` and the inner `10`
- `EX_BoolVariable` / `EX_InterfaceContext` (just 1 sub) — the 1-byte size aligns with what UELib parses
- A custom RL "context init" prefix similar to the existing `ContextInitTokenRL` (`0x6C`) — same shape, different role

The `1D 1D` (10×) self-pair is the strong constraint. Few baseline shapes legally allow the same opcode
to immediately follow itself. `EX_BoolVariable`-shape (1 sub) does — `1D` reads its 1 sub which itself
starts with `1D`. So the most likely shape is "1 sub-token" wrapping.

**Best guess:** `0x1D` is a single-sub-token wrapping opcode in the new build. Could be a renamed
`EX_BoolVariable`, `EX_InterfaceContext`, or a new `ContextInit`-style prefix. Mapping it to
`UStruct.UByteCodeDecompiler.BoolVariableToken` is the most conservative shape-correct guess.

```csharp
{ 0x1D, typeof(BoolVariableToken) },   // 1-sub-token shape (HYPOTHESIS — verify)
```

## What `sub_7FF6CD38C840` actually is

For the record, since the trail is laid:

- Address: `0x7ff6cd38c840`. Size 0x1036.
- References two `"Bad expr token %02x"` strings at `0x7ff6cea76208` and `0x7ff6cea76288`.
- File-path string `D:\build\Inc\Sync\Development\Src\Core\Src\FScriptSerializer.cpp` lives between
  those two strings at `0x7ff6cea76230` — same source file. So this is `FScriptSerializer::SomeMethod`.
- The wrappers `sub_7FF6CD2E81A0` / `sub_7FF6CD2E82E0` set up a critical-section-protected payload at
  `parent + 152` and pass it to `sub_7FF6CD38C840` as `a1`. Wrappers are vtable[0] entries on multiple
  UClass-family-looking vtables (e.g. one at `0x7ff6cea59440`).
- Inside `sub_7FF6CD38C840`:
  - `0x80+` opcodes loop variadic until `0x3E` then `HANDLE_OPTIONAL_DEBUG_INFO` — first-native form.
  - `0x70..0x7F` opcodes serialize one extra byte then loop variadic until `0x3E` — extended-native form.
  - `0x00..0x6C` dispatch via a 109-case jumptable.
  - Helpers used: `sub_7FF6CD38A580` = XFER 2 bytes, `sub_7FF6CD38A620` = XFER 4 bytes,
    `sub_7FF6CD38A6C0` = XFERNAME (FName via vtable[7]), `sub_7FF6CD38DC20` = XFERNAME + side effect,
    `sub_7FF6CD38B9D0` = HANDLE_OPTIONAL_DEBUG_INFO.
  - case `0x19` is a sub-switch on the next byte — that's the dynarray dispatch (RL analog of UELib's
    `ExtendedNativeFunctionToken` at primary `0x10`).

Even though the byte values inside `sub_7FF6CD38C840` don't match on-disk bytes, the **set of shapes**
it knows (1 sub-token, FName + variadic + DEBUG, UStruct* + variadic + DEBUG, INT + INT + INT + BYTE
for DebugInfo, etc.) is exhaustive of what RL's runtime understands. Each on-disk opcode must fall
into one of those shapes. Use this as a corroborator: once an on-disk byte's shape is inferred from
real bytecode, confirm that some case in `sub_7FF6CD38C840` produces the same shape — it's strong
evidence the inferred semantics is real and not a coincidence.

## Recommended next steps (in order)

1. **Apply the `0x0F` and `0x1D` hypotheses as experimental mappings.** Add
   `{ 0x0F, typeof(FinalFunctionTokenRL) }` and `{ 0x1D, typeof(BoolVariableToken) }` to
   `EngineBranchRL.BuildTokenMap`. Re-run `mcp__uelib__disassemble_function` on the same set of
   `*.PostBeginPlay` functions. The success criterion is: token sizes match `script_size`, and
   downstream tokens stop showing `size:0` cascades. Decompile **output** will still be broken
   (NTL drift), but parse structure should be repaired.
2. **If parse cascade is repaired:** survey TAGame and ProjectX more thoroughly, looking for
   additional now-resolved bytes; lock the changes in.
3. **If parse cascade is NOT repaired:** the shape guess is wrong. Try the alternate
   (`0x0F` = `XFER_FUNC_NAME + variadic + DEBUG`, i.e. inherit from `VirtualFunctionToken`) and
   re-test.
4. **Regenerate the .NTL file for the new build** (separate task — uses the existing
   `Eliot.Extensions.NTLGenerator` plugin or a dump-and-import flow). Until that's done, decompile
   output of any function with native calls will continue to show `<decompile failed>` even with a
   perfect token map.
5. **Investigate the suspect `0x45` mapping.** Hex-dump the 5 places `0x45` appears in the new
   survey, look at adjacent bytes, and propose a real shape. Could be an alternate
   `EndFunctionParms`, an `EmptyParm` variant, or something new.

## Files
- `EngineBranch.RL.cs` — primary opcode → token map
- `Tokens/ExtendedNativeFunctionToken.cs` — sub-dispatch for the `0x10` extended-native prefix
- `Tokens/AlternativeExtendedNativeFunctionToken.cs` — sub-dispatch for `0x5E`
- `Tokens/FinalFunctionTokenRL.cs` — wraps base `FinalFunctionToken`, used by `0x38` today

---

## Update — what shipped after the initial analysis

After writing this doc, several rounds of MCP-restart-verify cycles validated the
hypotheses and added defensive infrastructure. Summary of the commits on `rl-custom`:

1. **`7d865bc`** — added the experimental token-map entries:
   - `{ 0x0F, typeof(FinalFunctionTokenRL) }` (super-call shape)
   - `{ 0x1D, typeof(BoolVariableToken) }` (1-sub-token wrapper)
   - **Verified working**: `Pawn.PostBeginPlay` now decompiles `super.PostBeginPlay(0); break;`
     as the first real source output. `Volume.PostBeginPlay` parses through with `0x1D`
     correctly wrapping inner extended-native calls.

2. **`83692cb`** — defensive try/catch in `FinalFunctionTokenRL`, `AndTokenRL`, `OrTokenRL`:
   - `FinalFunctionTokenRL.Deserialize` catches `ArgumentOutOfRangeException` /
     `InvalidCastException` from the `Imports[]` lookup and continues with `Function = null`.
     Decompile emits `"/* unresolved final function */(args)"` instead of NRE-cascading.
   - `AndTokenRL` / `OrTokenRL` wrap their inner reads similarly.

3. **`b3282ac`** — central deserialize loop recovers from per-token exceptions:
   - `ByteCodeDecompiler.Deserialize`'s `catch (Exception)` branch now re-syncs
     `ScriptPosition` to the buffer cursor and continues instead of `break`-ing.
   - **This was the breakthrough**: previously one throwing token aborted parsing of
     the entire function. Now parsing continues best-effort. `Pawn.PostBeginPlay` went
     from "stops at position 14" to "parses all 114 bytes including nested extended natives".

4. **`f738d57`** — `DecompileNests` guard against `CurrentTokenIndex` past the list end:
   - Eliminates the "Failed to format nests!" stack-trace dump that was appearing in
     decompiled source after the loop recovery walked the cursor past the token list.

5. **`3c3493d` / `f9cb3b2`** — bounds-check NextToken / DecompileNext / DecompileParms /
   DecompileOperator. Initial attempt to clamp `NextToken` at the last index caused
   infinite loops in callers like `do { t = NextToken(); } while (t is not Foo);` —
   reverted. Final state: `DecompileNext` returns `string.Empty` when the cursor is at
   the end (safe — string concat continues); the explicit bounds-checks in
   `DecompileParms` / `DecompileOperator` use `/* truncated */` placeholders;
   `NextToken` itself still throws (the central loop's try/catch handles it).

6. **`5091abd`** — `NativeFunctionToken.Decompile` falls back to
   `"/* unresolved native 0xNN */(args)"` when `NativeItem` is null (NTL drift).

## Current state (after these changes)

**Decompile output now produces real UnrealScript source for the first time.** Examples
from `absolutelynewupks/Engine_decrypted.upk`:

```
event PostBeginPlay()       // Pawn
{
    super.PostBeginPlay(0);
    break;
    /* Statement decompilation error: ... */
    // UnresolvedToken (0x21)
    /*@Error*/;
    { }
}
```

```
simulated event PostBeginPlay()    // PlayerController
{
    // UnresolvedToken (0x2B)
    /* unresolved final function */(0, ., break__NFUN_181__(__NFUN_115__(,, self,
        /* unresolved final function */(...)), ...));
}
```

The structure is real, calls are nested correctly. The garbage in the output is from
two known sources, both out-of-scope for token-map work:
- **`__NFUN_NNN__` placeholders** — NTL drift; native function indices in the new
  build don't resolve in the loaded `.NTL` file. Fix: regenerate the NTL via
  `Eliot.Extensions.NTLGenerator` or a dump-and-import flow against the current
  binary.
- **`UnresolvedToken (0xNN)` markers** — ~14 lower-frequency primary opcodes whose
  shape is still unknown. See "Remaining gaps" below.

## Remaining gaps in the primary token map

After applying the 0x0F / 0x1D mappings, these primary bytes still appear in real
bytecode and trigger parse desync (each byte's Decompile throws `/*@Error*/` which
propagates through the parent expression):

| Byte | Hex | Survey count | Where seen |
|------|------|------|------|
| 0x08 | 8 | 9× | Actor.PostBeginPlay, Pawn.* |
| 0x0D | 13 | 2× | Camera.PostBeginPlay |
| 0x21 | 33 | 3× | Pawn.PostBeginPlay (real position, not tail padding) |
| 0x2B | 43 | 5× | Controller.PostBeginPlay, PlayerController.* |
| 0x2C | 44 | 1× | GFxData_PRI_TA.SetPRI |
| 0x32 | 50 | 1× | PlayerController.EnterStartState |
| 0x3D | 61 | 1× | GameInfo.Logout |
| 0x3F | 63 | 1× | GameInfo.PostBeginPlay |
| 0x43 | 67 | 3× | Camera.PostBeginPlay |
| 0x50 | 80 | 6× | Camera.PostBeginPlay (multiple positions) |
| 0x54 | 84 | 2× | Camera.PostBeginPlay (multiple positions) |
| 0x5A | 90 | 1× | Pawn.Destroyed |
| 0x68 | 104 | 1× | Tail padding only — likely safe to leave unresolved |
| 0x6B | 107 | 7× | Pawn.Destroyed, Pawn.FellOutOfWorld |
| 0x6E | 110 | 2× | GFxData_PRI_TA.SetPRI |

Plus the `0x45 = EndFunctionParmsToken` map entry that never appears in the new
survey (5× total occurrences, none as terminator) — likely a vestige from a wrong
older theory; should be `BadToken` or marked `UnresolvedToken` until shape is known.

For each of these, the next investigative step is: pick 2-3 functions where the
byte appears, hex-dump the bytes around it via the disassembled token positions,
and compare to baseline `EX_` shapes from `ScriptSerialization.h`. Since central-loop
recovery is now in place, a wrong guess only corrupts that one token's child
expressions instead of breaking the entire function — much lower-risk experimentation.

## What's NOT yet done

- **Case-0x19 sub-switch decoding** in `sub_7FF6CD38C840` (the IDA function that turned
  out to be `FScriptSerializer`, not the on-disk walker). Its byte values don't directly
  translate to on-disk bytes, but its case shapes are still useful corroboration once
  shape inference for the listed gaps is done. Deferred.
- **NTL regeneration for the current build.** Separate workstream — needs the
  `Eliot.Extensions.NTLGenerator` plugin to be re-pointed at the current binary's
  native table dump.
- **`IteratorPopToken` / NestManager state** — when the cursor walks past expected
  nest closes (because earlier tokens failed and didn't push their nest), the manager
  emits orphan `{ }` blocks. Decompile-side bookkeeping fix; lower priority than the
  token-map gaps.
