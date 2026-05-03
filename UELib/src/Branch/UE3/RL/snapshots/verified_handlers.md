# RL GNatives Handler Catalog — Binary-Verified

Reverse-engineered from `RocketLeague_Dumped_latest.exe` GNatives table at `0x7FF6CF2AA580`.
Each handler's runtime behavior was decompiled and matched to a baseline UE3 EX_ via
unique-string fingerprints, byte-read patterns, and structural fingerprints.

## Symbols

- **GNatives base**: `funcs_7FF6CD28592F = 0x7FF6CF2AA580`
- **Default error handler**: `sub_7FF6CD31ACB0` ("Unknown code token %02X") — appears at 28 entries (0x00, 0x02, 0x03, 0x04, 0x08, 0x0A, 0x0D, 0x14, 0x18, 0x24, 0x26, 0x34, 0x35, 0x3C, 0x3D, 0x3F, 0x42, 0x44, 0x45, 0x4B, 0x4E, 0x4F, 0x5F, 0x67, 0x68, 0x6D, 0x6E, 0x6F)
- **Property-setup helper**: `sub_7FF6CD317F00` (FFrame::ReadVariableSize equivalent — reads UField* + property-type byte = 9 bytes)

## VERIFIED — applied this session

| Byte  | Handler              | Maps to EX_              | Evidence |
|-------|----------------------|--------------------------|----------|
| 0x0F  | sub_7FF6CD2F5F00 (28 bytes) | **EX_FinalFunction**   | Reads 8 bytes UFunction*, dispatches `vtable[76]`. NO +1 mandatory byte (was a band-aid). |
| 0x12  | sub_7FF6CD2F5740 (207) | (FinalFunction-fused?)  | Variadic loop on `*v3 != 0x3E` + final dispatch. Confirms terminator. |
| 0x37  | sub_7FF6CD2F5810 (285) | (DelegateFn-fused?)    | Variadic loop on `*v13 != 0x3E` + null skip. Second confirmation of terminator. |
| 0x38  | sub_7FF6CD308710 (474) | **EX_ClassContext**     | Unique runtime error string `"Accessed null class context '%s'"`. |
| 0x3E  | sub_7FF6CD2F00B0 (16)  | **EX_EndFunctionParms** | `qword_..._D7B8 = 0; --Code;` — un-consume terminator pattern. |
| 0x4C  | sub_7FF6CD2F08B0 (491) | **EX_Let / LetBool / LetDelegate** | Unique runtime error string `"Attempt to assign variable through None"`. Dispatches 2 sub-opcodes. |

## VERIFIED — pending application (next batch)

### Trivial leaves (no Code reads, push immediate to result)

| Byte  | Handler              | Maps to EX_              | Evidence |
|-------|----------------------|--------------------------|----------|
| 0x1F  | sub_7FF6CD2F5C60 (4)  | **EX_Self**             | `*a3 = a1;` — writes `this` to result (a1 == this). |
| 0x1C  | sub_7FF6CD2F7030 (8)  | **EX_IntZero or EX_False** | `*(_DWORD*)a3 = 0;` |
| 0x27  | sub_7FF6CD2F7030 (shared with 0x1C) | (same as 0x1C) | Aliased handler — likely IntZero/False sibling. |
| 0x2F  | sub_7FF6CD2F7040 (8)  | **EX_IntOne or EX_True**  | `*(_DWORD*)a3 = 1;` |
| 0x3A  | sub_7FF6CD2F7040 (shared with 0x2F) | (same as 0x2F) | Aliased handler. |
| 0x23  | sub_7FF6CD2F7050 (8)  | **EX_NoObject**         | `*(_QWORD*)a3 = 0;` (8-byte zero — NULL pointer). |
| 0x1D  | AK::MemoryMgr::StartProfileThreadUsage (3) | (no-op) | Empty stub. IDA mis-named; likely **EX_Nothing** or unused. |
| 0x2E  | (same as 0x1D) (3)    | (same)                  | Aliased. |

### Constant readers (read immediate value from Code, push to result)

| Byte  | Handler              | Maps to EX_              | Evidence |
|-------|----------------------|--------------------------|----------|
| 0x0B  | sub_7FF6CD2F6DC0 (18) | **EX_IntConst**         | Reads INT (4 bytes), writes to `*a3`. |
| 0x64  | sub_7FF6CD2F6DE0 (18) | **EX_IntConst** (or FloatConst) | Same shape: reads u32, writes to `*a3`. (Two int-const variants?) |
| 0x2B  | sub_7FF6CD2F7010 (18) | **EX_ByteConst or IntConstByte** | Reads BYTE (1 byte), writes to `*a3`. |
| 0x2A  | sub_7FF6CD2F70A0 (18) | **EX_ByteConst or IntConstByte** | Same shape. |
| 0x39  | sub_7FF6CD2F6FA0 (19) | (8-byte read leaf)      | Reads QWORD, writes to `*a3`. **EX_NameConst or EX_ObjectConst.** |
| 0x3B  | (same as 0x39) (19)   | (same)                  | Aliased. |
| 0x43  | (same as 0x39) (19)   | (same)                  | Aliased. |
| 0x5A  | (same as 0x39) (19)   | (same)                  | Aliased. |
| 0x60  | sub_7FF6CD2F9C90 (32) | **EX_VectorConst or RotationConst** | Reads 3 INTs (12 bytes), writes 3 fields to result. |

### Other patterns (need deeper analysis)

| Byte  | Handler              | Notes                    |
|-------|----------------------|--------------------------|
| 0x5D  | sub_7FF6CD30D7B0 (6)  | Just `Code += 2` — reads 2 bytes, no result write. **EX_DebugInfo prefix or EX_JumpIfFilterEditorOnly.** |
| 0x63  | sub_7FF6CD2F0380 (9)  | `Code = 0` — sets Code to NULL. Probably **EX_EndOfScript** marker. |
| 0x07  | sub_7FF6CD2F7360 (14) | Calls fn pointer with no args. Likely **EX_Stop** or similar singleton. |
| 0x6B  | sub_7FF6CD2F7340 (32) | Reads byte, dispatches into sub-table `funcs_7FF6CD2F735D`. **EX_PrimitiveCast** (sub-table = cast type). |
| 0x19  | sub_7FF6CD2F1750 (32) | Reads byte, dispatches into sub-table `funcs_7FF6CD2F176D`. Another sub-dispatcher. |
| 0x31  | sub_7FF6CD2ED4A0 (165) | Has `for ( i = *result; *result != 0x4F; ...)` — terminates on byte 0x4F. **EX_LabelTable?** (label entries until special marker) |
| 0x57  | sub_7FF6CD2F0390 (497) | Loop with `v10 != 0xFFFF` — MAXWORD pattern from EX_Case/LabelTable. |
| 0x66  | sub_7FF6CD2F5C70 (54)  | Dispatches sub-opcode + clears flag. **NOT EX_Self** (which is at 0x1F). Park for now. |

## Methodology Notes

- Each `execXxx` in the binary corresponds 1:1 to a baseline `case EX_Xxx:` in `SerializeExpr`.
- Runtime dispatch is via `funcs_7FF6CD28592F[opcode_byte]` (direct indexing — verified by the
  default error handler reading `Code[-1]`).
- The PARSER (UStruct::SerializeExpr-equivalent, on-disk) is in a different function but reads the
  same bytes the runtime does. So matching the runtime byte-read pattern fixes both.
- DeserializeCall in UELib's FunctionTokens.cs:38 terminates by `is EndFunctionParmsToken` token-type
  check — swapping the byte→EndFunctionParms mapping is sufficient (no string-literal byte changes
  needed elsewhere in the codebase).
