# Rocket League opcode analysis

> **HONEST STATUS NOTE (READ THIS FIRST)**
>
> The numbers further down this file ("99.83% fully clean", parse-clean rates) measure
> *absence of exceptions during decompile*, not *correctness of decompile output*. The
> token map under `EngineBranchRL.BuildTokenMap` was built via empirical
> `--score-mapping` shape inference: pick a token type whose
> Deserialize consumes the right number of bytes per opcode without throwing. That
> produces structurally-consistent parses but the byte→token *meanings* are wrong for
> many bytes.
>
> Real-world consequence: decompile output for non-trivial functions (Pawn.Destroyed,
> PlayerController.PlayerTick, CustomMatchSettingsSave_TA.GetSettings) renders as
> nested-paren gibberish that uses real native names (Add_IntInt, Cos, super.X())
> but is not valid UnrealScript. Statement boundaries collapse into single
> giant expressions; if/else and switch/case structures are scrambled; left-side of
> assignments is often empty.
>
> What works: function signatures (from UFunction metadata, not bytecode),
> native-name resolution (binary-extracted, accurate), super-call patterns when
> Function.Outer resolves, default-property blocks for simple classes, the lower-level
> resilience that prevents exceptions from killing the whole decompile.
>
> What doesn't: any expression-level token interpretation that depends on the wrong
> shape choice — which is most of bytes 0x00..0x6F. See "Path to a working
> decompiler" section below.

Notes on reverse-engineering the current RL `UStruct::SerializeExpr` byte mapping. Captures findings from the
session that pulled fresh `D:\Games\rocketleague\TAGame\CookedPCConsole\*.upk` files into
`C:\Users\Authority\Desktop\RE stuff\rldecrypted\absolutelynewupks\`, decrypted them with `RLUPKTool.exe`,
and surveyed them against the current `RocketLeague_Dumped_latest.exe` opened in IDA.

This document is a working note, not a spec. Take everything below as "best evidence so far" and re-verify
before changing live code.

## Path to a working decompiler — what's needed

The fundamental issue: UELib's RL token map matches BYTE COUNTS but not BYTE MEANINGS.
To fix it, every primary opcode (0x00..0x6F) needs to be ground-truthed against the
actual handler in the binary's runtime dispatch table. Reference points already in
this file:

- **Bytecode dispatch table**: `0x7FF6CF2AA580` in `RocketLeague_Dumped_latest.exe`
  (loaded in IDA at handle `RocketLeague_Dumped_latest.exe.i64`). Indexed by script
  byte 0..255. Entries 0..0x6F are the primary opcode handlers. Entries 0x70..0x7F
  are the chained native dispatchers (each indexes a 256-entry GNatives slice).
  Entries 0x80..0xFF are direct GNatives operator/inline natives.
- **GNatives**: same address (the dispatch table IS GNatives). Indexes 256..4415
  are the actual native function pointers.
- **`RocketLeagueNativeNames.Map`** (UELib/src/Branch/UE3/RL/): 207 binary-verified
  (index, name) pairs.
- **`RocketLeagueUnknownNatives.Set`**: 3,982 indexes whose GNatives entry is the
  default error handler (`sub_7FF6CD31ACB0`) — nothing real to look up.

The work needed:

1. **Walk dispatch table entries 0x00..0x6F.** For each entry:
   - Open the handler function in IDA (single-instruction wrappers are easy; complex
     handlers need their actual sub-byte/sub-token reads counted).
   - Determine: how many bytes does it consume from the script stream, in what order
     (`stream.ReadByte()`, `stream.ReadObject<UObject*>()` = 8 bytes, `ReadName()` = 8
     bytes, recursive sub-token dispatch via `funcs_7FF6CD28592F[v]`, etc.)?
   - Match the wire format to one of UELib's known token classes, or write a new RL-
     specific token if no baseline UE3 type matches.
   - Update `EngineBranchRL.BuildTokenMap` with the verified mapping and replace the
     "tied across many candidates" comment with "verified at handler 0x7FF6CD......".
2. **Update or replace tokens whose semantics are wrong.** Score-mapping-derived
   mappings flagged as "tied" or "picked simplest leaf" in the existing inline
   comments are the most suspicious. List in Task #31.
3. **Validate output against a reference.** Without a known-good decompile to diff
   against, semantic correctness is unprovable. Options in Task #34 — most likely
   start with UE3 SDK source for inherited functions (Object.uc, Actor.uc,
   GameInfo.uc, PlayerController.uc).
4. **Remove resilience-net masking** once the underlying parse is right (Task #37).
   The current NRE/AOOR catches mask actual semantic bugs and need to be tightened
   or removed once they're no longer needed.
5. **Statement boundary, operator precedence, switch/case rendering** (Tasks #32,
   #33, #35, #36). Each is a separate decompile-side issue that compounds on top of
   the token-map issue. Address after the token map is settled.

## What is verified correct (don't break these)

- `0x0F` = `FinalFunctionTokenRL` with the +1 mandatory skip-byte after
  `UFunction*` — verified by Pawn.PostBeginPlay's `super.PostBeginPlay()` correctly
  rendering without spurious `(0)`. See section "0x0F (and 0x38) shape".
- `0x70..0x7F` chained dispatchers + `RocketLeagueNativeNames` cover non-extended
  native indexes 0..4095. Verified by Sleep, FastTrace, Trace, MoveTo etc. resolving
  correctly in call sites.
- `0x80..0xFF` direct natives (Add_IntInt, Cos, Min, etc.) — these match the
  binary's GNatives entries directly. Verified by 127/128 of those entries having a
  real exec function pointer, none default-handler.
- `0xC8` = NothingToken (GNatives[200] is unmapped — no real native at index 200).
- `RocketLeagueUnknownNatives` set suppression — eliminates ghost native call sites
  for the 3,982 indexes whose GNatives entry is the default error handler.

## What is suspect (verify against binary before trusting)

Most of `EngineBranchRL.BuildTokenMap` for bytes 0x00..0x6F. Specifically anything
with comments like:

- "tied across many candidates"
- "picked the simplest leaf"
- "all candidates tied"
- "best fit; ... shape"
- "most common case in scripted code"
- "BadToken in baseline RL"

Each of those is a guess. The score function that picked them was measuring "no
parse error" not "matches the binary's actual handler". They probably consume the
right number of bytes — but the resulting token TYPE is often wrong, so sub-tokens
get reinterpreted and statements collapse.

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

## All primary opcodes resolved (commits `fe59443` and `308bb84`)

After building a `--score-mapping` driver inside `UELib/Repro/` that overrides the
RL `TokenMap` at runtime and re-runs the full Engine sweep per candidate, every
remaining gap was filled. Final per-package score against
`absolutelynewupks/{Engine,TAGame,ProjectX}_decrypted.upk`:

| Package  | Functions | Clean | Hung | Unresolved | Bad |
|----------|----------:|------:|-----:|-----------:|----:|
| Engine   |     4,725 | 4,725 |    0 |          0 |   0 |
| TAGame   |    16,348 |16,348 |    0 |          0 |   0 |
| ProjectX |     3,965 | 3,965 |    0 |          0 |   0 |

Final mapping additions / changes (`commit fe59443` for the 19 RL `Unresolved`
bytes, `commit 308bb84` for the 4 baseline `BadToken` bytes):

| Byte | Mapped to                  | Notes                              |
|------|----------------------------|------------------------------------|
| 0x08 | EatReturnValueToken        | 1-sub wrapper                      |
| 0x0D | DebugInfoToken             | 13-byte payload                    |
| 0x21 | DynamicArrayIteratorToken  | 1-sub + iterator tail              |
| 0x28 | LocalVariableToken         | 4-byte UProperty*; -2835 bad alone |
| 0x2B | VectorConstToken           | 12-byte payload                    |
| 0x2C | DebugInfoToken             | 13-byte payload                    |
| 0x2D | EventUnsubscribeToken      | FNAME + 1 sub                      |
| 0x32 | VectorConstToken           | 12-byte payload                    |
| 0x35 | NameConstToken             | 8-byte FNAME                       |
| 0x3A | DebugInfoToken             | 13-byte payload, +52 clean         |
| 0x3D | VirtualFunctionToken       | FNAME + variadic                   |
| 0x3F | VectorConstToken           | 12-byte payload                    |
| 0x43 | DebugInfoToken             | 13-byte payload, +69 clean         |
| 0x50 | UnicodeStringConstToken    | length-prefixed string             |
| 0x53 | LocalVariableToken         | tied across all candidates         |
| 0x54 | EatReturnValueToken        | 1-sub wrapper                      |
| 0x57 | LocalVariableToken         | tied across all candidates         |
| 0x5A | LocalVariableToken         | 4-byte UProperty*                  |
| 0x5B | VectorConstToken           | 12-byte payload                    |
| 0x5F | LocalVariableToken         | tied across all candidates         |
| 0x62 | AssertToken                | 1 sub + payload                    |
| 0x68 | NameConstToken             | 8-byte FNAME                       |
| 0x6B | LocalVariableToken         | 4-byte UProperty*; top freq        |
| 0x6E | NameConstToken             | 8-byte FNAME                       |

The `0x45 = EndFunctionParmsToken` map entry that the earlier analysis flagged as
suspicious has been left in place — it scores well in the sweep and parses cleanly
on real bytecode. Likely a duplicate dispatch (RL recognises both `0x45` and `0x4C`
as variadic terminators).

## Methodology — `--score-mapping` driver

`UELib/Repro/Program.cs` exposes three modes built specifically for this work:

- `--sweep` — iterates every UFunction in a package, runs `manager.Deserialize()`
  with a 10s watchdog, then per-token `Decompile()`, and reports
  (clean / unresolved / bad) tuples plus a histogram of remaining `UnresolvedToken`
  opcode bytes.
- `--analyze-byte 0xNN [--max N]` — for every primary occurrence of byte `NN`,
  reflect-reads the next 16 raw stream bytes via `UObject.LoadBuffer()` →
  `BaseStream`, clusters by signature, prints the top 40 with example sites.
  Useful for picking candidate shapes by eye.
- `--score-mapping 0xNN:TypeName[,...]` — uses reflection to override the active
  `EngineBranchRL.TokenMap` indexer at runtime, then re-runs the sweep. Lets you
  test arbitrary candidate type assignments without rebuilding/republishing the
  MCP. (`FindTokenType` searches every loaded assembly by simple name.)

Scoring metric: `clean` is the count of UFunctions with **no** `UnresolvedToken` /
`BadToken` instances **and** no exception during `manager.Deserialize()`. Higher
clean is better. `bad` and `unresolved` totals act as tie-breakers.

The shell driver `.opcode_survey/test_candidates.ps1` runs ~80 candidate token
types per byte and prints the top 10 sorted by clean — typically a few minutes per
byte on a warm build. **Important caveat:** the score discriminates by *shape*
(stream bytes consumed, ScriptPosition advanced), not semantic type. When two
candidates with the same shape both produce the top score, picking between them is
a guess; they have different decompile-output text but identical parse structure.
The picks above prefer the simplest-leaf or most-script-plausible candidate.

## 0x0F (and 0x38) shape — extra mandatory byte after UFunction*

After locking in the primary token map, decompile output of `Pawn.PostBeginPlay`
showed `super.PostBeginPlay(0); break;` — the spurious `(0)` is wrong (PostBeginPlay
takes no args). Stem: 0x0F was using the baseline EX_FinalFunction shape
(`op + UFunction* + variadic + EndFunctionParms`), but RL's shape is actually
`op + UFunction* + 1 mandatory byte + variadic + EndFunctionParms + DEBUG`. The
mandatory byte's purpose isn't known — likely an arg count, a flag, or a vtable
hint that the runtime consumes but isn't part of visible script semantics.

Verified by writing a `FinalFunctionTokenWithSkipRL` experimental variant that
consumed one extra byte after `ReadObject<UFunction>()`; output went from
`super.PostBeginPlay(0); break;` to `super.PostBeginPlay(); break;`. Both shapes
score 100% parse-clean across Engine/TAGame/ProjectX, so the empirical sweep
can't discriminate by score alone — the skip-variant was picked because it gives
correct decompile output. Commit `6b935e7` folds the skip into
`FinalFunctionTokenRL` itself so both 0x0F and 0x38 use the new shape.

## 0x49 — `End:0x2866` smoking gun

`0x49` is currently mapped to `FilterEditorOnlyToken` (inherited from baseline UE3
where this opcode lives at `0x5A`, but the RL position was set in commit `d48a2704`
without empirical confirmation). `FilterEditorOnlyToken : JumpToken` reads a 16-bit
`CodeOffset` immediately after the opcode byte, which it then uses to open a Scope
nest spanning `[Position, CodeOffset)`.

Symptom: every `0x49` in `SkeletalMeshComponent.PlayParticleEffect` (and many other
functions in `Engine_decrypted.upk`) decompiles with `// End:0x2866`. `0x2866` is
`66 28` little-endian — i.e. the bytes immediately after the `0x49` opcode are
`0x66 0x28`, which under the current map are `SelfToken` followed by
`LocalVariableToken`. The reader is consuming two opcode bytes as if they were a
code offset, giving a nonsense scope endpoint far past function-end. NestManager
then fails to close the scope, and we see cascading "MISMATCHING REMOVE,
tried Case got Type:Scope Position:0x..." warnings in the decompile output.

To confirm the wrong shape: `--score-mapping 0x49:NothingToken` parses 4725/4725
functions clean with 0 unresolved/0 bad — i.e. the byte stream is consistent with
`0x49` being a 1-byte leaf, not a 3-byte JumpToken-style token. Either NothingToken
(silent leaf) or some still-unidentified single-byte semantics fits the wire format.

Not fixed yet (see "What this does NOT fix" below) — the right answer needs another
look at the binary's case `0x49` body, since both candidate shapes parse cleanly and
we can't distinguish purely empirically.

## Native-name resolution (`__NFUN_NNN__` placeholders)

UE3 native function call sites (the `NativeFunctionToken` and its extended-native
dispatch via opcode `0x10`) need a `(native_index → name)` map to render readable
text. The shipped `.NTL` files in `UE Explorer/Native Tables/` are baseline
UT3/UDK and don't cover RL's full index space, so call sites used to render as
`__NFUN_5113__()`, `__NFUN_145__()`, etc.

**What we wired up.** `NativeFunctionToken.Decompile` now falls back to a
process-global cache built from every loaded package's `UFunction` objects whose
`NativeToken != 0`. `NativeFunctionToken.IndexPackageNatives(pkg)` is called
automatically by `MCP/Tools/PackageTools.LoadPackage` and the standalone Repro
tool's `--preload`, so loading e.g. Engine + Core gets you ~205 named natives.

| Source package | UFunction-declared natives |
|----------------|----------------------------|
| Core           | ~161 (Object operators / string / math)  |
| Engine         | ~44 (Actor.FastTrace, Sleep, Trace, etc.)|
| TAGame, ProjectX, IpDrv, GFxUI, … | 0 each   |

After loading Core+Engine, every call site to a native at index ≤ 3971 resolves;
Pawn.PostBeginPlay → `super.PostBeginPlay()`, FastTrace's body → `Divide_IntInt`,
`InStr`, `IsA`, `Round`, etc.

**What's still missing — extended natives (UELib's `__NFUN_5000+__`).**

Binary RE update: `GNatives` is at `0x7FF6CF2AA580` in
`RocketLeague_Dumped_latest.exe`. Indexes 0..255 are the bytecode-opcode handlers
(EX_*) and entries 256..4415 are the actual native function pointers. Bytes
`0x70..0x7F` in the script are the chained native dispatchers — each one reads a
sub-byte and dispatches to the native at index `(byte − 0x70) × 256 + sub_byte`,
covering the full 0..4095 range. (`0x80..0x8F` host individual operator/inline
natives, not chained.)

**The gotcha:** UELib's `ExtendedNativeFunctionToken` (script byte `0x10`),
`AlternativeExtendedNativeFunctionToken` (`0x5E`), and
`ExAlternativeExtendedNativeFunctionTokenRL` (`0x71`) all compute their native
index as `sub_byte + 5000` (or `+ 6000`). These constants don't correspond to
anything in the binary's `GNatives` — entries 5000+ are zero. The binary
*itself* dispatches script byte `0x10` as the "Execution beyond end of script"
error handler, not as an extended-native dispatcher. This means the
`__NFUN_5000+__` call sites our parser produces are reading bytes from positions
the binary would never execute (post-EndOfScript regions, dead code, or possibly
embedded data that our parser walked past). They're not reachable native calls;
they're parser artifacts.

So the resolution is structural, not just naming: the right fix would be to
either (a) drop these "ghost native" call sites from the decompile output, or
(b) figure out the actual encoding RL uses for genuinely-extended natives (if
any), which probably requires identifying a byte in script that the binary's
dispatch table treats as a chained native dispatcher beyond 0x7F. The binary's
0x80–0x8F handlers do reference index ranges 0+ (chained), so RL might be
piggybacking on those for additional natives — but UELib's `0x10`/`0x5E`/`0x71`
mappings clearly aren't producing real native calls.

**Binary-extracted fallback names.** What we *did* extract from the binary is a
hardcoded `RocketLeagueNativeNames.Map` of 78 (index, name) pairs covering
indexes 256–3971 — pulled from the registration tables in `.data` cross-
referenced against `GNatives`. This loads automatically for RL packages so
`Sleep`, `FastTrace`, `MoveTo`, `AllActors`, etc. resolve even when the user
loads only TAGame without preloading Engine/Core. It overlaps heavily with what
loaded `UFunction.NativeToken`s already provide; the new value is the operator
natives (`Multiply_VectorVector`, `Add_QuatQuat`, `MirrorVectorByNormal`,
`RotRand`, `LessEqual_StrStr`, etc.) that don't always appear in script
declarations.

## Decompile resilience

To get useful output even when individual sub-tokens hit edge cases (null
UObject ref, cursor walked past the deserialized list), the decompile pipeline
now:

- **`DynamicCastToken` / `MetaClassCastToken` / `InterfaceCastToken`**: guard
  `CastClass.Name` with a null check, fall back to `/* unresolved cast */`
  inline.
- **`StructMemberToken`**: guard `Property.Name` similarly.
- **`AssertSkipCurrentToken<T>`**: bounds-check before `NextToken` to survive a
  parent token whose assumed sub-token shape doesn't match the wire format.
- **`SkipFunctionTokenRL.Decompile`**: replace `do { skip = NextToken() } while`
  loop with a bounds-checked `while` so a missing trailing `EndFunctionParms`
  doesn't AOOR-abort the statement.
- **`Token.DecompileNext`**: wrap the recursed `token.Decompile()` in a narrow
  catch for `NullReferenceException` / `ArgumentOutOfRangeException` only,
  emitting an inline `/*<exc NRE>*/` or `/*<exc AOOR>*/` placeholder so the
  enclosing statement still renders. Other exception types still propagate
  to the statement-level catch (so genuine bugs aren't masked).
- **`FinalFunctionTokenRL.Decompile`**: explicit `Function.Outer == null` guard
  before `base.Decompile()`, instead of catching all NREs.

Engine package decompile-survey numbers (from `Repro --decompile-survey`):

| metric              | before today | after fixes |
|---------------------|--------------|-------------|
| fully clean fns     | 2085 (44%)   | 3222 (68%)  |
| stmt-error fns      | 1199         |   20        |
| MISMATCHING REMOVE  |   45         |   44        |
| with __NFUN_ refs   | 1842         | 1477        |
| parse clean         | 4725 (100%)  | 4725 (100%) |

TAGame: 9814/16348 (60%) fully clean, 262 stmt-errors.
ProjectX: 2812/3965 (71%) fully clean, 47 stmt-errors.

## Final state — chained dispatchers + 207-entry hardcoded fallback

The previous "ghost natives" finding was *partly* wrong. Re-examining the binary
revealed:

- Bytes `0x70..0x7F` in script are **chained native dispatchers**, NOT raw
  native indexes. Each reads one sub-byte and indexes
  `GNatives[(byte − 0x70) × 256 + sub_byte]`, covering native indexes 0..4095.
- The original UELib mapping (`byte ≥ firstNative=0x70 → NativeFunctionToken
  with index = byte`) treated each as a leaf native at index 0x70..0x7F
  (= 112..127), producing `__NFUN_112__..__NFUN_127__` placeholders that were
  never real natives — they were the dispatchers themselves.

Fix: `ChainedNativeDispatcherTokenRL` (+ keeping `ExAlternativeExtendedNativeFunctionTokenRL`
for `0x71` because it has an Iterator dispatch sub-table). Each one reads
sub_byte and computes `(OpCode − 0x70) × 256 + sub_byte` for the real native
index. The resulting `NativeFunctionToken` then resolves through the loaded
`UFunction.NativeToken` cache + the binary-extracted `RocketLeagueNativeNames`
map.

`RocketLeagueNativeNames.Map` is auto-loaded for any RocketLeague package and
contains 207 (index, name) pairs spanning 29..3971: the byte-level operator
natives (Add_IntInt, Cos, Min, FastTrace's namespace), actor/controller-class
natives (Sleep, Trace, MoveTo, AllActors), vector helpers, and physics natives.
Enough to render readable output even if only one RL package is loaded.

Final survey numbers, all standalone (no preloads, no NTL file):

|          | functions | fully clean | %     | __NFUN_ refs | stmt-error |
|----------|-----------|-------------|-------|--------------|------------|
| Engine   | 4,725     | 4,719       | 99.87% | 0           |  6         |
| TAGame   | 16,348    | 16,323      | 99.85% | 2           | 23         |
| ProjectX | 3,965     | 3,953       | 99.70% | 0           | 12         |
| total    | 25,038    | 24,995      | 99.83% | 2           | 41         |

Parse-clean: 25,038 / 25,038 (100%) across all packages.

Decompile resilience added in this pass:

- `Decompiler.PeekToken` / `PreviousToken` / `CurrentToken` return null when
  out-of-bounds (callers use them in `is X` type checks; null cleanly fails).
- `DecompileParms` wraps each sub-token's `Decompile()` in NRE/AOOR catch.
- `DecompileNext` wraps the recursed `Decompile()` (NRE/AOOR scoped).
- `PrecedenceToken` / `SafeDecompile` helper for operator paths.
- `JumpIfNotToken`'s if-else seek-loop bounds-checks before reading
  `prevToken`/`elseStartToken`.
- `DynamicArrayIteratorToken`'s "Skip Index param" `NextToken()` is bounds-checked.
- `FinalFunctionTokenRL.Decompile` wraps `base.Decompile` in NRE/AOOR catch.
- `DecompileNests` resolves tied-position Case→Case mismatches by walking the
  nest chain instead of emitting a `MISMATCHING REMOVE` warning.
- `AssertSkipCurrentToken<T>` bounds-checks before `NextToken`.
- `SkipFunctionTokenRL.Decompile` uses `while` with bounds-check instead of
  `do { skip = NextToken() } while`.
- `ChainedNativeDispatcherTokenRL` and `ExAlternativeExtendedNativeFunctionTokenRL`
  emit `NothingToken` for indexes in `RocketLeagueUnknownNatives.Set` (3,982
  GNatives entries that resolve to the binary's default error handler).

`__NFUN_NNN__` placeholders for indexes whose `GNatives[N]` resolves to the
binary's default "Unknown code token" error handler are now suppressed at parse
time (`RocketLeagueUnknownNatives` covers the 3,982 such indexes via 33
compressed ranges; both `ChainedNativeDispatcherTokenRL` and
`ExAlternativeExtendedNativeFunctionTokenRL` consult it and emit
`NothingToken` instead of a `NativeFunctionToken` for those calls). The two
residual TAGame `__NFUN_` refs are at indexes 503 and 509 — real natives in
GNatives but their function pointers don't have matching `UObjectexec*`-style
name strings in the binary's registration tables (likely class-method
registrations through a different mechanism).

## What this does NOT fix

- **Decompile output quality.** Per-function structure is sound, but specific token
  semantics may be wrong wherever multiple candidates tied. E.g. mapping 0x6B to
  `LocalVariableToken` when it's really `InstanceVariableToken` will print the
  wrong name in the rendered source.
- **NTL drift.** Native function calls still render as `__NFUN_NNN__()` because
  the loaded `.NTL` is from an older RL build. Separate workstream — re-run
  `Eliot.Extensions.NTLGenerator` against the current binary.
- **NestManager / IteratorPop bookkeeping.** "MISMATCHING REMOVE" warnings still
  appear when Switch/Case/IteratorPop combinations confuse the nest scope tracker.
  Decompile-side fix, lower priority than the token-map work.
- **Case-0x19 sub-switch decoding** in `sub_7FF6CD38C840` (the IDA function that
  turned out to be `FScriptSerializer`, not the on-disk walker) is no longer
  blocking — empirical inference filled the entire primary table without it.
