# RL GNatives Handler Catalog — Binary-Verified

Reverse-engineered from `RocketLeague_Dumped_latest.exe` GNatives table at `0x7FF6CF2AA580`.
Each handler's runtime behavior was decompiled and matched to a baseline UE3 EX_ via
unique-string fingerprints, byte-read patterns, and structural fingerprints.

## Symbols

- **GNatives base**: `funcs_7FF6CD28592F = 0x7FF6CF2AA580`
- **Default error handler**: `sub_7FF6CD31ACB0` ("Unknown code token %02X") — appears at 28 entries: 0x00, 0x02, 0x03, 0x04, 0x08, 0x0A, 0x0D, 0x14, 0x18, 0x24, 0x26, 0x34, 0x35, 0x3C, 0x3D, 0x3F, 0x42, 0x44, 0x45, 0x4B, 0x4E, 0x4F, 0x5F, 0x67, 0x68, 0x6D, 0x6E, 0x6F
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

### Investigation candidates (mapping unverified or "possibly wrong")

| Byte  | Handler                      | Notes                    |
|-------|------------------------------|--------------------------|
| 0x05  | sub_7FF6CD2F6800 (736, shared with 0x16) | 2-sub-op + byte + dispatch. Mapped to ArrayElement currently but the real ArrayElement is now at 0x1E. May be an unused alternate or a fused variant. |
| 0x09  | sub_7FF6CD2F5930 (146)       | Optional 0x20-prefix + 1-sub-op wrapper. Mapped to DefaultVariable (verified 0x58 also DefaultVariable). |
| 0x10  | sub_7FF6CD2F00C0 (324)       | Runtime "Execution beyond end of script" sentinel. Should never execute. NothingToken is correct. |
| 0x12  | sub_7FF6CD2F5740 (207)       | Variadic loop + final dispatch, terminator 0x3E. Could be FinalFunction-fused. |
| 0x16  | sub_7FF6CD2F6800 (shared 0x05) | Mapped to DynamicArrayElement (was JumpIfNot — wrong). Real array index is 0x1E. May be alt. |
| 0x20  | sub_7FF6CD3027A0 (117)       | HANDLE_OPTIONAL_DEBUG_INFO macro (peek byte 100, conditional consume 13 bytes). NOT EX_Return. |
| 0x21  | sub_7FF6CD2F5A40 (179)       | 1-sub-expr wrapper. Could be many things. Mapped to DynArrayIterator. |
| 0x32  | sub_7FF6CD2F6180 (189)       | 8-byte read + complex. Mapped to VectorConst (probably wrong). |
| 0x36  | sub_7FF6CD2F7250 (237)       | 8 bytes (skip) + 1 byte sub-op + dispatch + writes 0. Possibly EX_DynArrayLength variant. |
| 0x4D  | sub_7FF6CD2F5B80 (217)       | UStruct* + struct-buffer alloc + 1 sub-op + vtable copy. Possibly struct-init or EatReturnValue variant. |
| 0x53  | sub_7FF6CD2F6240 (414)       | Complex. Mapped to LocalVariable (was BadToken). |
| 0x69  | sub_7FF6CD2F0290 (194)       | Uses (a2+24) Object/Class info. State-related. Mapped to DelegateCmpNe. |

## Methodology Notes

- Each `execXxx` in the binary corresponds 1:1 to a baseline `case EX_Xxx:` in `SerializeExpr`.
- Runtime dispatch is via `funcs_7FF6CD28592F[opcode_byte]` (direct indexing — verified by the
  default error handler reading `Code[-1]`).
- The PARSER (UStruct::SerializeExpr-equivalent, on-disk) is in a different function but reads the
  same bytes the runtime does. So matching the runtime byte-read pattern fixes both.
- DeserializeCall in UELib's FunctionTokens.cs:38 terminates by `is EndFunctionParmsToken` token-type
  check — swapping the byte→EndFunctionParms mapping is sufficient (no string-literal byte changes
  needed elsewhere in the codebase).

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
