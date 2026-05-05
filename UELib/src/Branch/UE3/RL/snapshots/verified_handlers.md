# RL GNatives Handler Catalog — Binary-Verified

> **⚠️ DEPRECATED for cross-version work.** Superseded by
> `GNATIVES_SNAPSHOT_v868.md` which has the canonical per-byte handler-address
> table plus the cross-version comparison procedure. Kept here as historical
> context only — do not update this file; update the snapshot.
>
> Also: this file documents only the **runtime GNatives** view. As of
> 2026-05-05 it's known that the runtime handler and the on-disk parser
> (`UStruct::SerializeExpr` at `sub_7FF6CD38C840`) CAN diverge for a byte
> (case 0x2C / EX_Conditional). For decompilation work, also check the
> parser case in the snapshot doc.

Reverse-engineered from `RocketLeague_Dumped_latest.exe` GNatives table at `0x7FF6CF2AA580`.
Each handler's runtime behavior was decompiled and matched to a baseline UE3 EX_ via
unique-string fingerprints, byte-read patterns, and structural fingerprints.

## Symbols

- **GNatives base**: `funcs_7FF6CD28592F = 0x7FF6CF2AA580`
- **Default error handler**: `sub_7FF6CD31ACB0` ("Unknown code token %02X") — appears at 28 entries: 0x00, 0x02, 0x03, 0x04, 0x08, 0x0A, 0x0D, 0x14, 0x18, 0x24, 0x26, 0x34, 0x35, 0x3C, 0x3D, 0x3F, 0x42, 0x44, 0x45, 0x4B, 0x4E, 0x4F, 0x5F, 0x67, 0x68, 0x6D, 0x6E, 0x6F
- **Alias handlers** (multiple bytes share one handler, runtime-equivalent leaves):
  - `0x1D, 0x2E` → `sub_7FF6CD21D420` (3-byte empty stub) = EX_Nothing
  - `0x05, 0x16` → `sub_7FF6CD2F6800` (736 bytes) = EX_ArrayElement / EX_DynArrayElement
  - `0x1B, 0x54` → `sub_7FF6CD2F6AE0` (736 bytes) — 2 bytes share this large handler (TBD)
  - `0x39, 0x3B, 0x43, 0x5A` → `sub_7FF6CD2F6FA0` (19 bytes) = 8-byte qword leaf (NameConst-shape)
  - `0x1C, 0x27` → `sub_7FF6CD2F7030` (8 bytes) = EX_IntZero / EX_False
  - `0x2F, 0x3A` → `sub_7FF6CD2F7040` (8 bytes) = EX_IntOne / EX_True
- **Property-setup helper**: `sub_7FF6CD317F00` (FFrame::ReadVariableSize equivalent — reads UField* + property-type byte = 9 bytes)

## VERIFIED — applied to EngineBranchRL.BuildTokenMap

### Control flow / dispatch

| Byte  | Handler                      | EX_ name              | Evidence |
|-------|------------------------------|-----------------------|----------|
| 0x0F  | sub_7FF6CD2F5F00 (28 bytes)  | EX_FinalFunction      | Reads 8-byte UFunction*, dispatches `vtable[76]`. NO +1 mandatory byte (was a band-aid). |
| 0x12  | sub_7FF6CD2F5740 (207)       | (FinalFunction-fused) | Variadic loop on `*v3 != 0x3E` + final dispatch. Confirms terminator. Mapped to EatReturnValue (placeholder). |
| 0x22  | sub_7FF6CD2F0610 (31)        | EX_Jump               | Reads u16, sets Code = ScriptStart + offset (absolute jump). |
| 0x29  | sub_7FF6CD2F0630 (104)       | EX_JumpIfNot          | u16 offset + 1 byte sub-opcode + jump. Was wrongly DynArraySort. |
| 0x37  | sub_7FF6CD2F5810 (285)       | (DelegateFn-fused?)   | Variadic loop on `*v13 != 0x3E` + null skip. Second confirmation of terminator. |
| 0x38  | sub_7FF6CD308710 (474)       | EX_ClassContext       | Unique runtime error string `"Accessed null class context '%s'"`. |
| 0x3E  | sub_7FF6CD2F00B0 (16)        | EX_EndFunctionParms   | `qword=0; --Code;` — un-consume terminator pattern. |
| 0x40  | sub_7FF6CD2F5F90 (484)       | EX_DelegateFunction   | 1 byte + UProperty* + FName, calls FindFunction lookup. |
| 0x41  | sub_7FF6CD2F5E90 (102)       | EX_VirtualFunction    | FName + state-aware lookup (flag = 0). |
| 0x47  | sub_7FF6CD2F0360 (31)        | EX_EmptyParmValue     | 1-byte leaf — sets a state flag. Empirical: appears 6 times in a row inside `Spawn(Class, ?, ?, ?, ?, ?, ?)` for the 6 optional args. |
| 0x49  | sub_7FF6CD2F0C60 (841)       | EX_LetDelegate        | 2 sub-opcodes + cleanup of LHS (delegate-replace pattern). |
| 0x4C  | sub_7FF6CD2F08B0 (491)       | EX_Let / LetBool / LetDelegate | Unique error `"Attempt to assign variable through None"`. Dispatches 2 sub-opcodes. |
| 0x57  | sub_7FF6CD2F0390 (497)       | EX_Switch             | 9 bytes via sub_7FF6CD317F00 + sub-expr + case-loop with 0xFFFF terminator using `wcsicmp`/`memcmp`. |
| 0x59  | sub_7FF6CD2F5F20 (105)       | EX_GlobalFunction     | FName + lookup with state-skip flag = 1. |
| 0x5C  | sub_7FF6CD2F07F0 (178)       | EX_GotoLabel          | Unique error `"GotoLabel (%s): Label not found"`. Dispatches label-name sub-expr. |
| 0x5D  | sub_7FF6CD30D7B0 (6)         | EX_Jump (no-jump?)    | Just `Code += 2`. Likely EX_JumpIfFilterEditorOnly (cooked = no-op). |

### Property / member access

| Byte  | Handler                      | EX_ name              | Evidence |
|-------|------------------------------|-----------------------|----------|
| 0x06  | sub_7FF6CD2F0020 (143)       | EX_BoolVariable       | 1-sub-expr wrapper, peeks 8 bytes for bit-mask test. |
| 0x11  | sub_7FF6CD2ED370 (100)       | EX_LocalOutVariable   | Walks `(a2+72)` = FFrame::OutParms. Empirical: out-params in PRI_TA.SetLoadouts. |
| 0x1E  | sub_7FF6CD2ED550 (656)       | **EX_ArrayElement**   | Index + Base sub-opcodes + bounds-check `"Accessed array '%s.%s' out of bounds (%i/%i)"`. |
| 0x28  | sub_7FF6CD2F5CB0 (474)       | EX_Context            | 1 byte + sub-expr + 2-byte null-skip + UField* + prop-type + sub-expr. `"Accessed None '%s'"`. |
| 0x4A  | sub_7FF6CD2F6590 (609)       | EX_StructMember       | UProperty* + UStruct* + 2 bytes + sub-expr. Allocates struct buffer. |
| 0x55  | sub_7FF6CD2ED270 (93)        | EX_InstanceVariable   | UProperty* + addr = `this + offset`. |
| 0x58  | sub_7FF6CD2ED2D0 (158)       | EX_DefaultVariable    | UProperty* + object-flag check. |
| 0x65  | sub_7FF6CD2ED210 (91)        | EX_LocalVariable      | UProperty* + addr = `Locals[offset]`. |
| 0x66  | sub_7FF6CD2F5C70 (54)        | EX_BoolVariable       | 1-sub-expr wrapper + flag clear. Aliased with 0x06 (different handler, same logical opcode). |

### Assignments

| Byte  | Handler                      | EX_ name              | Evidence |
|-------|------------------------------|-----------------------|----------|
| 0x46  | sub_7FF6CD2F12A0 (731)       | EX_LetBool            | 2 sub-opcodes (Let-shape, no NULL cleanup — bool variant). |
| 0x49  | sub_7FF6CD2F0C60 (841)       | EX_LetDelegate        | 2 sub-opcodes + cleanup of LHS (delegate-replace pattern). |
| 0x4C  | sub_7FF6CD2F08B0 (491)       | EX_Let                | (see above) |

### Constants / leaves

| Byte  | Handler                      | EX_ name              | Evidence |
|-------|------------------------------|-----------------------|----------|
| 0x0B  | sub_7FF6CD2F6DC0 (18)        | EX_IntConst           | Reads INT (4 bytes). |
| 0x1C  | sub_7FF6CD2F7030 (8)         | EX_IntZero / EX_False | `*(_DWORD*)a3 = 0`. Aliased with 0x27. |
| 0x1D  | AK::MemoryMgr::Start... (3)  | EX_Nothing            | Empty stub. Aliased with 0x2E. |
| 0x1F  | sub_7FF6CD2F5C60 (4)         | EX_Self               | `*a3 = a1` — pushes `this`. |
| 0x23  | sub_7FF6CD2F7050 (8)         | EX_NoObject           | `*(_QWORD*)a3 = 0` (8-byte zero — NULL pointer). |
| 0x27  | sub_7FF6CD2F7030 (shared 0x1C) | EX_False / EX_IntZero | (see 0x1C) |
| 0x2A  | sub_7FF6CD2F70A0 (18)        | EX_ByteConst          | Reads u8 (unsigned). |
| 0x2B  | sub_7FF6CD2F7010 (18)        | EX_IntConstByte       | Reads i8 (signed variant). |
| 0x2E  | (same as 0x1D) (3)           | EX_Nothing            | (see 0x1D) |
| 0x2F  | sub_7FF6CD2F7040 (8)         | EX_IntOne / EX_True   | `*(_DWORD*)a3 = 1`. Aliased with 0x3A. |
| 0x39  | sub_7FF6CD2F6FA0 (19)        | EX_NameConst (or Object/InstanceDelegate) | 8-byte qword reader. Aliased with 0x3B, 0x43, 0x5A. |
| 0x3A  | (same as 0x2F)               | EX_True / EX_IntOne   | (see 0x2F) |
| 0x50  | sub_7FF6CD2F6E00 (158)       | EX_StringConst        | Calls FString-from-cstring constructor. |
| 0x51  | sub_7FF6CD2F6EA0 (241)       | EX_UnicodeStringConst | Calls `wcslen` on Code. |
| 0x60  | sub_7FF6CD2F9C90 (32)        | EX_VectorConst (or RotationConst) | Reads 3 INTs (12 bytes). |
| 0x64  | sub_7FF6CD2F6DE0 (18)        | EX_FloatConst         | Same shape as 0x0B (4-byte read). |
| 0x6A  | sub_7FF6CD2F7060 (54)        | EX_EmptyDelegate      | Zeroes 24 bytes + constructs empty delegate. |
| 0x6B  | sub_7FF6CD2F7340 (32)        | EX_PrimitiveCast      | Reads byte, dispatches into sub-table `funcs_7FF6CD2F735D` (cast-type sub-table). |
| 0x6C  | sub_7FF6CD2F0210 (116)       | EX_ReturnNothing      | Unique error `"Control reached the end of non-void function"`. |

### Newly verified (later this session)

| Byte  | Handler                      | EX_ name              | Evidence |
|-------|------------------------------|-----------------------|----------|
| 0x21  | sub_7FF6CD2F5A40 (179)       | property→string cast (RL-specific, NOT DynArrayIterator) | Reads 1 sub-expression then calls `sub_7FF6CD3192F0(out_FString, prop_class=qword_7FF6CF27D780+200, value_ptr=qword_7FF6CF27D7B0, 0)`. The `+200` offset reads `UProperty::PropertyClass`. Result is FString-shape (8/4/4 swap into `*a3` matching `FString::operator=`). New token `StringCastTokenRL` renders as `string(<expr>)`. **Previous "verified DynArrayIterator" claim was tautological** — the rendered `foreach` came from the (wrong) token map, not from binary handler shape (which reads only 1 sub-expr, not 4 + byte + u16). The real foreach lives at the extended-native prefix: `0x10 0x0A` (DynamicArrayIteratorRL) and `0x71 0x39` (IteratorTokenRL). |
| 0x32  | sub_7FF6CD2F6180 (189)       | EX_InstanceDelegate (RL fork) | 16-byte payload (UObject* + FName). New token `InstanceDelegateTokenRL` renders as `Object.DelegateName`. |
| 0x36  | sub_7FF6CD2F7250 (237)       | property setter w/ discard | 8-byte UProperty* + sub-expr; result discarded. New token `PropertySetterDiscardTokenRL` renders as `Property = Expression`. |
| 0x09  | sub_7FF6CD2F5930 (146)       | comma operator (`A, B`) | Optional 0x20 prefix + 2 sub-exprs. New token `DiscardKeepTokenRL` renders just sub-A; cooker emits this around for-loop init expressions. |

### Newly verified (this session)

| Byte  | Handler                      | EX_ name              | Evidence |
|-------|------------------------------|-----------------------|----------|
| 0x09  | sub_7FF6CD2F5930 (146)       | EX_LetBool/Let-shape (RL variant) | Optional 0x20 debug-info prefix + 2 sub-exprs (no UProperty* read). Same shape as 0x46 (LetBool) but with debug-info prefix support. Was wrongly DefaultVariable (which reads 8-byte UProperty* — over-consumed). Mapped to LetToken. |
| 0x43  | sub_7FF6CD2F6FA0 (19)        | 8-byte qword leaf (NameConst-shape) | Aliased with 0x39, 0x3B, 0x5A. Was wrongly DebugInfoToken (13-byte payload). |
| 0x53  | sub_7FF6CD2F6240 (414)       | EX_StructCmpEq        | 8-byte UStruct* + 2 sub-exprs + struct comparison via sub_7FF6CD658AC0 (4th arg = 0 → EQ). Was wrongly LocalVariable. |
| 0x4D  | sub_7FF6CD2F5B80 (217)       | EX_StructConst-like   | 8-byte UStruct* + 1 sub-expr; allocates struct buffer of `struct.PropertiesSize` and copies via vtable[98]. Mapped to new RL token `StructValueTokenRL`. |
| 0x69  | sub_7FF6CD2F0290 (194)       | EX_IteratorPop        | 1-byte leaf with unique runtime error "Unexpected iterator pop command at %s:%04X". Was wrongly DelegateCmpNe. |
| 0x68  | sub_7FF6CD31ACB0             | UNMAPPED (default-error) | Was wrongly NameConst — over-consumed 8 bytes per occurrence. Now NothingToken (1-byte safe). |
| 0x6E  | sub_7FF6CD31ACB0             | UNMAPPED (default-error) | Same fix as 0x68. |
| 0x6F  | sub_7FF6CD31ACB0             | UNMAPPED (default-error) | Was wrongly Conditional (3 sub-exprs + 4 bytes — way over-consumed). Now NothingToken. |
| 0x03  | sub_7FF6CD31ACB0             | UNMAPPED (default-error) | Was wrongly StructCmpEq (8 bytes UObject* + 2 sub-exprs). Now NothingToken. Real StructCmpEq is at 0x53. |
| 0x4E  | sub_7FF6CD31ACB0             | UNMAPPED (default-error) | Was wrongly StructCmpNe. Now NothingToken. |

### Investigation candidates (mapping still uncertain)

| Byte  | Handler                      | Notes                    |
|-------|------------------------------|--------------------------|
| 0x05  | sub_7FF6CD2F6800 (736, shared with 0x16) | 2-sub-op + byte + dispatch. Mapped to ArrayElement currently but the real ArrayElement is now at 0x1E. May be an unused alternate or a fused variant. |
| 0x10  | sub_7FF6CD2F00C0 (324)       | Runtime "Execution beyond end of script" sentinel. Should never execute. NothingToken is correct. |
| 0x12  | sub_7FF6CD2F5740 (207)       | Variadic loop + final dispatch, terminator 0x3E. Could be FinalFunction-fused. |
| 0x16  | sub_7FF6CD2F6800 (shared 0x05) | Mapped to DynamicArrayElement. Real array index is 0x1E. May be alt. |
| 0x1B  | sub_7FF6CD2F6AE0 (736, shared with 0x54) | Two bytes share this large handler. TBD. |
| 0x20  | sub_7FF6CD3027A0 (117)       | HANDLE_OPTIONAL_DEBUG_INFO macro (peek byte 100, conditional consume 13 bytes). NOT EX_Return. |
| 0x21  | sub_7FF6CD2F5A40 (179)       | RESOLVED — see "Newly verified (later)" above. property→string cast. |
| 0x32  | sub_7FF6CD2F6180 (189)       | 16-byte payload (UObject* + FName + 0). Mapped to VectorConst (12 bytes — under-reads 4). Likely **EX_InstanceDelegate** or similar. |
| 0x36  | sub_7FF6CD2F7250 (237)       | 8 bytes (UProperty*) + 1 byte sub-op + sub-expr; sets *a3 = 0. Possibly EX_DynArrayLength setter or EX_DefaultParameter variant. |
| 0x37  | sub_7FF6CD2F5810 (285)       | VERIFIED 2 sub-exprs + u16 end-offset + **conditional** variadic body (terminator 0x3E) + optional 0x20 debug-info. Pattern: `if (*sub_A != 0) { run body until 0x3E } else { jump to end-offset }` — semantically a NULL-checked delegate/function call (not a foreach iterator: lacks the array, item, withindex, index sub-tokens). Currently FloatConst (under-reads — only consumes 4 bytes). Remap deferred until found in real bytecode (have not observed in TAGame/Engine fixtures so far). |
| 0x54  | sub_7FF6CD2F6AE0 (736, shared with 0x1B) | Same handler as 0x1B. TBD. |

## Methodology Notes

- Each `execXxx` in the binary corresponds 1:1 to a baseline `case EX_Xxx:` in `SerializeExpr`.
- Runtime dispatch is via `funcs_7FF6CD28592F[opcode_byte]` (direct indexing — verified by the
  default error handler reading `Code[-1]`).
- **Always verify both GNatives AND the on-disk parser** before mapping a byte. The parser
  (UStruct::SerializeExpr-equivalent) is in a different function from GNatives. For most opcodes
  the byte counts agree, but they DIVERGE in load-bearing ways:
  - 4-byte object/property/function indices in storage expand to 8-byte pointers in memory; only
    the parser sees the storage form, so the GNatives qword read tells you the runtime size, not
    the on-disk size. Tokens that mishandle this desync `ScriptPosition` against the stream and
    corrupt every following token.
  - `EX_DebugInfo`, optional alignment reads, and similar parse-time decoration usually live only
    in the parser path.
  - `JumpIfNot` / `Case` / `Jump` `CodeOffset` is parsed as a u16 then interpreted by the renderer
    as in-memory `Position`. The cooker can undercount, requiring the recovery logic in
    `JumpTokens.cs` (case A: CodeOffset inside JumpIfNot's own bytes; case B: CodeOffset
    mid-body-token).
- The function `sub_7FF6CD38C840` (referenced from the `Bad expr token %02x` string in
  `FScriptSerializer.cpp`) is **not** the on-disk parser — its opcode permutation differs from real
  bytecode (expects `0x3E` for the variadic terminator, real bytecode uses `0x4C`). Use it only as
  a shape corroborator after the on-disk byte is known by other means; do not read its case numbers
  as on-disk byte values.
- DeserializeCall in UELib's FunctionTokens.cs:38 terminates by `is EndFunctionParmsToken` token-type
  check — swapping the byte→EndFunctionParms mapping is sufficient (no string-literal byte changes
  needed elsewhere in the codebase).

## Output milestones (this session)

After the round of fixes captured above, real RL bytecode now decompiles to
recognizable UnrealScript instead of gibberish. Concrete diffs against the
two functions Daisy flagged in screenshots:

- `Car_TA.CreateRumblePickups` — went from
  `RumblePickups = (self != == ) != ++1.;`
  to
  `RumblePickups = Class'TAGame_decrypted.RumblePickups_TA'.static.CreateInstance(WorldInfo, self);`

- `Car_TA.UpdateTeamLoadout` — went from a wall of `,, 1,, Tan(,,,,)` orphans
  to a structured if/else with proper member access:
  `if(Class'TAGame_decrypted.Car_TA'.default.bUseDefaultLoadout) { ModifiedLoadout.Products = Class'...GameData_TA'.default.DefaultLoadouts[int(TeamPaint.Team)].Products; ... }`

- `Car_TA.PostBeginPlay` — trailing `67109385` orphan eliminated by clamping
  the deserialize loop on DataScriptSize (the on-disk byte count) instead of
  ByteScriptSize (the memory-layout size, which can be larger).

## Known remaining decompile artifacts

- **For-loops not folded into `for (init; test; update)` syntax.** The
  bytecode for a `for (Index = 0; Index < 2; ++Index) { body }` decomposes
  into `Index = 0; if (Index < 2) { body; ++Index; } goto J0xN;` — all
  pieces render correctly individually but NestManager doesn't recognize
  the goto-back pattern as a loop. Tracked as task #43.
- **0x37 wire format unmapped.** GNatives[0x37] handler is verified
  iterator-shape (2 sub-exprs + u16 + conditional variadic body, terminator
  0x3E) — semantically a NULL-checked function/delegate call wrapper. Not
  observed in TAGame/Engine fixtures yet, so the remap is deferred. Current
  FloatConst mapping under-reads when the byte does appear (consumes 4 bytes
  for what should be 4+sub+sub+u16+body+0). Remap when first encountered.
  (0x32 and 0x36 already remapped — see InstanceDelegateTokenRL and
  PropertySetterDiscardTokenRL.)
- **Spawn-call rendering.** Patterns like `X = Spawn(class'Y', self)`
  sometimes render with the args orphaned across separate lines:
  `X = none;` then `self` then `Class'Y'` as bare statements. Likely
  caused by 0x12 / similar variadic-fused tokens not handling the
  reciever-class arg correctly.
- **bool `+= 0` / `+= 1` for assignment.** UnrealScript's bool assignment
  through `bX = false; bX = true;` compiles to bytecode that we currently
  render as `bX += 0;` / `bX += 1;`. Valid but not idiomatic.
- **Trailing post-recovery decompile artifacts.** When a sub-expression's
  parse fails (typically an unmapped native operator), the central
  recovery loop walks past the failure point. Tokens parsed in the
  recovered region can render as garbled if-conditions or bare orphan
  expressions. Visible in `Ball_TA.PostBeginPlay`'s `if (StaticMesh != none)`
  rendering as `if(@NULL @ return StaticMesh -= )`. The function body
  itself still parses correctly, only the broken expression is affected.

## Decompile-side improvements (this session)

- **Hardcoded operator symbol map** (`StandardOperatorSymbols.cs`) — ~95 entries from baseline
  Object.uc covering Bool/Byte/Int/Float/String/Object/Name/Vector/Rotator/Quaternion stdlib +
  RL-specific extra indexes (131, 191, 192, 204, 206, 207, 240, 241, 248). Without this, every
  native operator rendered as a function call (e.g. `Multiply_FloatFloat(a, b)` instead of `a * b`)
  because cooked .upks strip the script-source FriendlyName.
- **Three-tier resolution** in `NativeFunctionToken.Decompile`:
  1. StandardOperatorSymbols (highest priority — supplies symbol + type)
  2. Loaded-UFunction lookup (via `IndexPackageNatives` cache)
  3. NativeItem fallback (NTL or placeholder)
- **Heuristic operator classifier** (`ClassifyNativeByName`) — for binary-fallback names without
  loaded UFunction, pattern-matches operator suffixes (`_IntInt`, `_FloatFloat`, `_Pre*`, etc.)
  to set FunctionType.Operator.
- **Recovery clamp** in `ByteCodeDecompiler.Deserialize` — central recovery loop now clamps at
  scriptSize to prevent over-reading sub-tokens from leaving orphan tokens past function end.
- **Informative resilience markers** — `/*<exc NRE>*/` becomes
  `/*<exc NRE 0xXX TokenName>*/` so masked bytes are greppable for follow-up.
