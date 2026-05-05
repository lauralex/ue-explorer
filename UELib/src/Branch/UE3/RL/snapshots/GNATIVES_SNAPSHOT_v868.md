# GNatives Snapshot — Rocket League v868 (LicenseeVersion 32)

> **CROSS-VERSION CALIBRATION DOCUMENT**
>
> RL rotates its UnrealScript opcode permutation across patches. This file
> records the **handler addresses** (which represent fixed semantics) for
> each script byte 0x00..0x6F in the current build, plus what each handler
> does. When a new game version is dumped, follow the procedure at the
> bottom to recompute the byte→token map without redoing the binary RE
> from scratch.

## Build identity

- Binary: `RocketLeague_Dumped_latest.exe`
- IDB: `C:\Users\Authority\Desktop\RE stuff\IDA Pro 8.3\RocketLeague_Dumped_latest.exe.i64`
- Package version: 868, LicenseeVersion 32, EngineVersion 10897, CookerVersion 136
- GNatives base: `funcs_7FF6CD28592F = 0x7FF6CF2AA580`
- Default error handler: `sub_7FF6CD31ACB0` ("Unknown code token %02X")

The dispatch table at GNatives is indexed by the script opcode byte. Bytes
`0x00..0x6F` are primary opcodes, `0x70..0x7F` are chained-native dispatchers,
and `0x80..0xFF` are direct native function pointers (operators, math
helpers, etc.).

## Primary opcode table (0x00..0x6F) — handler addresses

The "Handler" column is the absolute address of the dispatch function in the
current binary. Aliases (multiple opcodes that share one handler) are noted.
ERROR rows map to the default error handler — these bytes are intentionally
unmapped in RL and should never appear in valid bytecode.

| Byte | Handler              | Wire format / shape                                  | Token mapping                            |
|------|----------------------|------------------------------------------------------|------------------------------------------|
| 0x00 | `sub_7FF6CD31ACB0`   | ERROR                                                | `ContextAwareReturnTokenRL` (RL emits this byte both as `EX_Return` at top level and as alignment padding inside variadic args; the token disambiguates by VariadicCallDepth + DeserializationDepth + peek). |
| 0x01 | `sub_7FF6CD2F0FB0`   | 2 sub-exprs, returns sub-2 (state-variable lookup)   | `StateVariableToken` |
| 0x02 | `sub_7FF6CD31ACB0`   | ERROR                                                | `IntConstToken` (legacy, harmless if absent) |
| 0x03 | `sub_7FF6CD31ACB0`   | ERROR                                                | `NothingToken` |
| 0x04 | `sub_7FF6CD31ACB0`   | ERROR                                                | `EndOfScriptToken` |
| 0x05 | `sub_7FF6CD2F6800`   | 2 sub-exprs + dispatch (alias 0x16; ArrayElement)    | `ArrayElementToken` |
| 0x06 | `sub_7FF6CD2F0020`   | 1 sub + 8-byte peek (BoolVariable)                   | `BoolVariableToken` |
| 0x07 | `sub_7FF6CD2F7360`   | tail-call wrapper                                    | `ReturnNothingToken` (legacy) |
| 0x08 | `sub_7FF6CD31ACB0`   | ERROR                                                | `EatReturnValueToken` (placeholder) |
| 0x09 | `sub_7FF6CD2F5930`   | optional 0x20 prefix + 2 sub-exprs (comma operator)  | `DiscardKeepTokenRL` |
| 0x0A | `sub_7FF6CD31ACB0`   | ERROR                                                | `NothingToken` |
| 0x0B | `sub_7FF6CD2F6DC0`   | 4-byte INT (IntConst)                                | `IntConstToken` |
| 0x0C | `sub_7FF6CD2F0AA0`   | 2 sub-exprs + bit-clear (LetBool-shape)              | `EventSubscribeToken` (TBD — current best fit) |
| 0x0D | `sub_7FF6CD31ACB0`   | ERROR                                                | `NothingToken` |
| 0x0E | `sub_7FF6CD2F63E0`   | UStruct + 2 sub-exprs + struct-cmp (EQ)              | `StructCmpEqToken` |
| 0x0F | `sub_7FF6CD2F5F00`   | 8-byte UFunction* + dispatch (FinalFunction)         | `FinalFunctionTokenRL` |
| 0x10 | `sub_7FF6CD2F00C0`   | "Execution beyond end of script" sentinel            | `NothingToken` |
| 0x11 | `sub_7FF6CD2ED370`   | 8-byte qword + walk OutParms list                    | `OutVariableToken` |
| 0x12 | `sub_7FF6CD2F5740`   | variadic body until 0x3E + dispatch (FinalFunction-fused) | `EatReturnValueToken` (placeholder) |
| 0x13 | `sub_7FF6CD2ED3E0`   | NoObject-shape                                       | `NoObjectToken` |
| 0x14 | `sub_7FF6CD31ACB0`   | ERROR                                                | `DynamicArrayLengthToken` (legacy, harmless if absent) |
| 0x15 | `sub_7FF6CD2F59D0`   | 1 sub-expr + write 0 to result                       | `InterfaceContextToken` (TBD) |
| 0x16 | `sub_7FF6CD2F6800`   | alias 0x05 (ArrayElement)                            | `DynamicArrayElementToken` |
| 0x17 | `sub_7FF6CD2F1580`   | 2 sub-exprs + delegate-list walk (delegate access)   | `DelegateAccessTokenRL` |
| 0x18 | `sub_7FF6CD31ACB0`   | ERROR                                                | `NothingToken` |
| 0x19 | `sub_7FF6CD2F1750`   | 1 byte + dispatch into sub-table at `0x7FF6CF2B2D80` (dynarray methods) | `ExtendedNativeFunctionToken` |
| 0x1A | `sub_7FF6CD2F70C0`   | 8-byte UClass* + 1 sub-expr + class-flag check (DynamicCast) | `DynamicCastToken` |
| 0x1B | `sub_7FF6CD2F6AE0`   | 2 sub-exprs + 1-byte skip + optional 0x20 (alias 0x54) | `MetaClassCastToken` (wire format mismatch — TBD) |
| 0x1C | `sub_7FF6CD2F7030`   | write 0 (4-byte) (alias 0x27)                        | `IntZeroToken` |
| 0x1D | `sub_7FF6CD21D420`   | empty stub (alias 0x2E; different module)            | `NothingToken` |
| 0x1E | `sub_7FF6CD2ED550`   | 2 sub-exprs + bounds-check (ArrayElement)            | `ArrayElementToken` |
| 0x1F | `sub_7FF6CD2F5C60`   | `*a3 = a1` (Self)                                    | `SelfToken` |
| 0x20 | `sub_7FF6CD3027A0`   | HANDLE_OPTIONAL_DEBUG_INFO macro (peek-100 conditional consume) | `ReturnToken` (legacy — actual EX_Return is 0x00) |
| 0x21 | `sub_7FF6CD2F5A40`   | 1 sub-expr + property-export to FString (string cast) | `StringCastTokenRL` |
| 0x22 | `sub_7FF6CD2F0610`   | u16 + absolute jump (Jump)                           | `JumpToken` |
| 0x23 | `sub_7FF6CD2F7050`   | write 8-byte 0 (NoObject)                            | `NoObjectToken` |
| 0x24 | `sub_7FF6CD31ACB0`   | ERROR                                                | `NothingToken` |
| 0x25 | `sub_7FF6CD2F0590`   | u16 + conditional sub-expr (when u16 != 0xFFFF) — switch case marker | `CaseToken` |
| 0x26 | `sub_7FF6CD31ACB0`   | ERROR                                                | `NothingToken` |
| 0x27 | `sub_7FF6CD2F7030`   | alias 0x1C (write 4-byte 0)                          | `IntZeroToken` |
| 0x28 | `sub_7FF6CD2F5CB0`   | sub + 2 bytes + UField + type + sub (Context)        | `ContextToken` |
| 0x29 | `sub_7FF6CD2F0630`   | u16 + sub (JumpIfNot)                                | `JumpIfNotToken` |
| 0x2A | `sub_7FF6CD2F70A0`   | u8 read (ByteConst)                                  | `ByteConstToken` |
| 0x2B | `sub_7FF6CD2F7010`   | i8 read (IntConstByte)                               | `IntConstByteToken` |
| 0x2C | `sub_7FF6CD308010`   | 1 sub + 1 byte skip + optional 0x20 (statement wrapper) | `StatementWrapperTokenRL` |
| 0x2D | `sub_7FF6CD2F06A0`   | u16 + byte + 3 sub-exprs + assert log                | `AssertExpressionTokenRL` |
| 0x2E | `sub_7FF6CD21D420`   | alias 0x1D (empty stub)                              | `NothingToken` |
| 0x2F | `sub_7FF6CD2F7040`   | write 1 (4-byte) (alias 0x3A; IntOne/True)           | `IntOneToken` |
| 0x30 | `sub_7FF6CD2F9C50`   | 12 bytes (3 INTs) (VectorConst)                      | `VectorConstToken` |
| 0x31 | `sub_7FF6CD2ED4A0`   | u16 + variadic body until 0x4F (debug block)         | `OptionalArgSkipTokenRL` (works in practice via 1-sub spillover) |
| 0x32 | `sub_7FF6CD2F6180`   | 8-byte UObject* + 8-byte FName (InstanceDelegate)    | `InstanceDelegateTokenRL` |
| 0x33 | `sub_7FF6CD2F1770`   | 2 sub-exprs + dynarray-result handling               | `EatReturnValueToken` (1-sub passthrough — best effort) |
| 0x34 | `sub_7FF6CD31ACB0`   | ERROR                                                | `NothingToken` |
| 0x35 | `sub_7FF6CD31ACB0` (TBD verify) | ERROR? (was NameConst by guess)            | `NameConstToken` (legacy) |
| 0x36 | `sub_7FF6CD2F7250`   | 8-byte UProperty* + sub + zero-result (property setter discard) | `PropertySetterDiscardTokenRL` |
| 0x37 | `sub_7FF6CD2F5810`   | 2 sub + u16 + conditional variadic body (term 0x3E)  | `FloatConstToken` (legacy — under-reads if appears) |
| 0x38 | `sub_7FF6CD308710`   | sub + 2 bytes + UField + type + sub (ClassContext)   | `ClassContextToken` |
| 0x39 | `sub_7FF6CD2F6FA0`   | 8-byte qword (alias 0x3B, 0x43, 0x5A; NameConst-shape) | `NameConstToken` |
| 0x3A | `sub_7FF6CD2F7040`   | alias 0x2F (write 1, 4-byte)                         | `TrueToken` |
| 0x3B | `sub_7FF6CD2F6FA0`   | alias 0x39 (8-byte qword leaf)                       | `ObjectConstToken` |
| 0x3C | `sub_7FF6CD31ACB0`   | ERROR                                                | `NothingToken` |
| 0x3D | `sub_7FF6CD31ACB0`   | ERROR                                                | `NothingToken` |
| 0x3E | `sub_7FF6CD2F00B0`   | `qword=0; --Code` (variadic terminator)              | `EndFunctionParmsToken` |
| 0x3F | `sub_7FF6CD31ACB0`   | ERROR                                                | `NothingToken` |
| 0x40 | `sub_7FF6CD2F5F90`   | 1 byte + UProperty* + FName (DelegateFunction)       | `DelegateFunctionToken` |
| 0x41 | `sub_7FF6CD2F5E90`   | FName + state-aware lookup (VirtualFunction)         | `VirtualFunctionToken` |
| 0x42 | `sub_7FF6CD31ACB0`   | ERROR                                                | `NothingToken` |
| 0x43 | `sub_7FF6CD2F6FA0`   | alias 0x39 (8-byte qword leaf)                       | `ObjectConstToken` |
| 0x44 | `sub_7FF6CD31ACB0`   | ERROR                                                | `NothingToken` |
| 0x45 | `sub_7FF6CD31ACB0`   | ERROR                                                | `NothingToken` |
| 0x46 | `sub_7FF6CD2F12A0`   | 2 sub-exprs (LetBool-shape, no NULL cleanup)         | `LetBoolToken` |
| 0x47 | `sub_7FF6CD2F0360`   | 1 byte (EmptyParmValue)                              | `EmptyParmToken` |
| 0x48 | `sub_7FF6CD30D7C0`   | (delegate subscribe)                                 | `EventSubscribeToken` |
| 0x49 | `sub_7FF6CD2F0C60`   | 2 sub + delegate cleanup (LetDelegate)               | `LetDelegateToken` |
| 0x4A | `sub_7FF6CD2F6590`   | UProperty + UStruct + 2 bytes + sub (StructMember)   | `StructMemberToken` |
| 0x4B | `sub_7FF6CD31ACB0`   | ERROR                                                | `NothingToken` |
| 0x4C | `sub_7FF6CD2F08B0`   | 2 sub + "Attempt to assign variable through None" log (Let) | `LetToken` |
| 0x4D | `sub_7FF6CD2F5B80`   | 8-byte UStruct + 1 sub (struct value/copy)           | `StructValueTokenRL` |
| 0x4E | `sub_7FF6CD31ACB0`   | ERROR                                                | `NothingToken` |
| 0x4F | `sub_7FF6CD31ACB0`   | ERROR (used as terminator marker for 0x31's body)    | `NothingToken` |
| 0x50 | `sub_7FF6CD2F6E00`   | C-string until null (StringConst)                    | `StringConstToken` |
| 0x51 | `sub_7FF6CD2F6EA0`   | UTF-16 until null word (UnicodeStringConst)          | `UnicodeStringConstToken` |
| 0x52 | `sub_7FF6CD2ED450`   | 1 sub-expr + write 0 (passthrough)                   | `EatReturnValueToken` |
| 0x53 | `sub_7FF6CD2F6240`   | UStruct + 2 sub + struct-cmp (StructCmpEq)           | `StructCmpEqToken` |
| 0x54 | `sub_7FF6CD2F6AE0`   | alias 0x1B (2 sub + 1 byte)                          | `EatReturnValueToken` |
| 0x55 | `sub_7FF6CD2ED270`   | 8-byte UProperty* + this-relative addr (InstanceVariable) | `InstanceVariableToken` |
| 0x56 | `sub_7FF6CD2F5B00`   | 8-byte FName + state-fn-call log                     | `NameConstToken` |
| 0x57 | `sub_7FF6CD2F0390`   | 9 bytes (UField + type) + sub + case loop (Switch)   | `SwitchToken` |
| 0x58 | `sub_7FF6CD2ED2D0`   | 8-byte UProperty* + object-flag check (DefaultVar)   | `DefaultVariableToken` |
| 0x59 | `sub_7FF6CD2F5F20`   | FName + state-skip lookup (GlobalFunction)           | `GlobalFunctionToken` |
| 0x5A | `sub_7FF6CD2F6FA0`   | alias 0x39 (8-byte qword leaf)                       | `LocalVariableToken` |
| 0x5B | `sub_7FF6CD308170`   | UStruct + sub + u16 + sub — none-coalescing op (`A ?? B`)  | `StructDefaultParameterTokenRL` |
| 0x5C | `sub_7FF6CD2F07F0`   | sub-expr + FindLabel (GotoLabel)                     | `GotoLabelToken` |
| 0x5D | `sub_7FF6CD30D7B0`   | `Code += 2` (no-op jump, possibly EX_JumpIfFilterEditorOnly) | `JumpToken` |
| 0x5E | `sub_7FF6CD309510`   | 8-byte UProperty + locals/out-param accessor (gated on CPF_OutParm flag) | `LocalVariableToken` (FieldToken-shape, 5 disk + 4 align) |
| 0x5F | `sub_7FF6CD31ACB0`   | ERROR                                                | `LocalVariableToken` (legacy) |
| 0x60 | `sub_7FF6CD2F9C90`   | 12 bytes (3 INTs) (VectorConst)                      | `VectorConstToken` |
| 0x61 | `sub_7FF6CD3082F0`   | 5 sub-exprs (Outer, Name, Flags, Class, Template) + "No class passed to 'new' operator" log | `NewExpressionTokenRL` |
| 0x62 | `sub_7FF6CD2F6FC0`   | 8-byte FName leaf — compact delegate-function reference (renders bare name) | `DelegateFunctionRefTokenRL` |
| 0x63 | `sub_7FF6CD2F03B0`   | (signed byte)                                        | `IntConstByteToken` |
| 0x64 | `sub_7FF6CD2F6DE0`   | 4-byte literal (FloatConst, sister to 0x0B IntConst) | `FloatConstToken` |
| 0x65 | `sub_7FF6CD2ED210`   | 8-byte UProperty* + Locals[offset] (LocalVariable)   | `LocalVariableToken` |
| 0x66 | `sub_7FF6CD2F5C70`   | 1 sub + flag-clear (BoolVariable)                    | `BoolVariableToken` |
| 0x67 | `sub_7FF6CD31ACB0`   | ERROR                                                | `NothingToken` |
| 0x68 | `sub_7FF6CD31ACB0`   | ERROR                                                | `NothingToken` |
| 0x69 | `sub_7FF6CD2F0290`   | 1 byte + "Unexpected iterator pop command" log (IteratorPop) | `IteratorPopToken` |
| 0x6A | `sub_7FF6CD2F7060`   | zeroes 24 bytes + empty delegate (EmptyDelegate)     | `EmptyDelegateToken` |
| 0x6B | `sub_7FF6CD2F7340`   | 1 byte + dispatch into cast sub-table at `funcs_7FF6CD2F735D` (PrimitiveCast) | `PrimitiveCastToken` |
| 0x6C | `sub_7FF6CD2F0210`   | 8-byte UProperty + zero result + "Control reached..." log (ReturnNothing) | `ReturnNothingToken` |
| 0x6D | `sub_7FF6CD31ACB0`   | ERROR                                                | `OutVariableToken` (legacy) |
| 0x6E | `sub_7FF6CD31ACB0`   | ERROR                                                | `NothingToken` |
| 0x6F | `sub_7FF6CD31ACB0`   | ERROR                                                | `NothingToken` |

### Sub-tables

- **0x19's dynarray-method sub-table**: `0x7FF6CF2B2D80` (64 entries × 8 bytes = 512 bytes).
  Mapped entries (in `ExtendedNativeFunctionToken.s_extendedNativeFunctionTokenMap`):
  +0 DynamicArrayElementTokenRL, +1 DynArrayLength, +3 DynArrayRemove, +4
  DynArrayFindContains, +5 DynArrayFindStruct, +6 DynArrayAdd, +7
  DynArrayAddItem, +8 DynArrayRemoveItem, +0x0A DynArrayIterator, +0x0B
  VariadicArrayConcat, +0x0C VariadicArrayAddUniqueItems, +0x0D
  DynArrayAddUniqueItem, +0x21 DynArrayFilterOut, +0x22 DynArrayMap, +0x24
  FindFirstWithDelegate, +0x25 DynArrayEvery, +0x26 DynArrayAny, +0x28
  VariadicArrayFillOut, +0x29 DynArrayFilter, +0x2C DynArrayEqual, +0x30
  DynArrayFindType, +0x31 DynArrayConcat.

  Unmapped sub-bytes whose handlers are NOT the error stub (i.e., real
  array methods that *could* appear in cooked bytecode but currently
  fall through to the `__NFUN_5XXX__` ghost-native path):
  - +0x02 sub_7FF6CD2EDE80 — 3 sub-exprs (3-arg array method, role TBD)
  - +0x09 sub_7FF6CD2EF2C0 — DynArrayInsert (3 sub + bounds check, error
    string `"Attempt to insert an element at %i an %i-element array '%s'"`)
  - +0x0E sub_7FF6CD2EFBA0 — 2 sub-exprs + delegate cleanup (Sort variant?)
  - +0x0F sub_7FF6CD2EFDE0 — sister to 0x0E
  - +0x20 sub_7FF6CD2F4F60 — 4 sub-exprs (TBD)
  - +0x23 sub_7FF6CD2F26D0 — DynArrayReduce (4 sub + reduce-fn lookup,
    error strings `"DynArrayReduce: Failed to find 2nd parameter property"`,
    `"DynArrayReduce: Failed to find reduce function"`)
  - +0x27 sub_7FF6CD2F3200, +0x2A sub_7FF6CD2F47F0, +0x2B sub_7FF6CD2F4970,
    +0x2D sub_7FF6CD2F45D0, +0x2E sub_7FF6CD2F51E0, +0x2F sub_7FF6CD2F2200,
    +0x32 sub_7FF6CD2F3600, +0x33 sub_7FF6CD2F38A0, +0x34 sub_7FF6CD2F55A0,
    +0x35 sub_7FF6CD2EDB50 — additional unmapped real handlers, shapes TBD.

  Sub-bytes 0x10-0x1F and 0x36-0x3F all point to the error handler (intentionally
  reserved). No observed regression in any sentinel/failing function from
  the unmapped slots above; flip them speculatively only with concrete
  evidence per the tautological-mapping anti-pattern.

- **0x6B's primitive-cast sub-table**: `funcs_7FF6CD2F735D`. Despite the
  `funcs_` IDA name, the bytes at this address are inlined `jmp r9` jump-
  table machine code, not a function-pointer table — the dispatcher resolves
  cast types through a switch-jump pattern. The baseline UE3 `CastToken`
  enum (IntToFloat, ByteToInt, ObjectToBool, etc.) is consumed correctly
  by `PrimitiveCastToken`; no per-cast remapping has been needed.

### Native dispatchers (0x70..0x7F)

These are second-level dispatchers that route to GNatives entries 256..4095:
- 0x71 — chained dispatch with iterator dispatch sub-table (`ExAlternativeExtendedNativeFunctionTokenRL`)
- 0x70, 0x72..0x7F — generic chained dispatch (`ChainedNativeDispatcherTokenRL`)

Each reads a sub-byte and indexes `GNatives[(byte − 0x70) × 256 + sub_byte]`.

### Inline natives (0x80..0xFF)

Direct native function pointers. Specific bytes verified:
- 0x82 — AndToken (logical &&)
- 0x84 — OrToken (logical ||)
- Most others render as `NativeFunctionToken` via the standard-operator-symbol map.

## IDA database annotations (v868)

The RocketLeague_Dumped_latest.exe IDB has been annotated with:

- **`GNatives` symbol** at `0x7FF6CF2AA580` (renamed from `funcs_7FF6CD28592F`).
- **Per-byte comments** on every entry from `GNatives[0x00]` through `GNatives[0x7F]`,
  including the handler address, wire-format shape, current token mapping, and
  any "was wrongly X" history for entries that have been corrected.
- **`exec*` function names** for the major handlers:
  `execLet`, `execLetBool`, `execLetDelegate`, `execContext`, `execJump`,
  `execJumpIfNot`, `execCase`, `execSwitch`, `execEndFunctionParms`, `execNew`,
  `execAssertExpression`, `execGotoLabel`, `execDelegateAccess`, `execFinalFunction`,
  `execVirtualFunction`, `execGlobalFunction`, `execDynamicCast`, `execSelf`,
  `execStateVariable`, `execEventSubscribeShape`, `execHandleOptionalDebugInfo`,
  `execStatementWrapper` (0x2C), `execNoneCoalesce` (0x5B = `??`),
  `execArrayElement`, `execLocalVariable`, `execInstanceVariable`,
  `execEmptyParm`, `execEndOfScript`, `execReturnNothing`, `execIteratorPop`,
  `execScriptError_UnknownToken` (the default-error stub),
  `execDynArrayMethodDispatcher` (0x19's sub-table entry),
  `execPrimitiveCastDispatcher` (0x6B's cast jump-table).

**SerializeExpr-style functions** annotated as `SerializeExpr_*`:

| Address              | Name                          | Role                                                                    |
|----------------------|-------------------------------|-------------------------------------------------------------------------|
| `0x7FF6CD2858E0`     | `SerializeExpr_FString`       | Canonical 1-sub + 0x20-peek pattern, returns FString                    |
| `0x7FF6CD285B30`     | `SerializeExpr_FStringPtr`    | 1-sub variant returning FString via `sub_7FF6CD2809D0`                  |
| `0x7FF6CD2859D0`     | `SerializeExpr_StringConcat`  | 2-sub variant for binary string operators                               |
| `0x7FF6CD285C10`     | `SerializeExpr_3sub`          | 2-sub LHS-as-string dispatcher                                          |
| `0x7FF6CD285D60`     | `SerializeExpr_FName`         | Single-expression evaluator returning FName                             |
| `0x7FF6CD285E50`     | `SerializeExpr_NameConcat`    | 2-sub variant for FName operations                                      |
| `0x7FF6CD285FB0`     | `SerializeExpr_Object`        | Single-expression evaluator returning UObject*                          |
| `0x7FF6CD2860A0`     | `SerializeExpr_ObjectVariant` | 2-sub variant for object operations                                     |
| `0x7FF6CD2B61B0`     | `SerializeExpr_2sub_NameOp`   | 2-sub name operator                                                     |
| `0x7FF6CD2B62E0`     | `SerializeExpr_Concat`        | concat helper                                                           |
| `0x7FF6CD2B6BB0`     | `SerializeExpr_2sub_Concat`   | concat variant                                                          |
| `0x7FF6CD2A5D70`     | `SerializeExpr_2sub_StringOp` | 2-sub string operator (clears EmptyParm flag bit 1 between subs)        |
| `0x7FF6CD2B63C0`     | `execForEachIterator`         | foreach loop over class hierarchy                                       |
| `0x7FF6CD2B67F0`     | `execForEachClassIterator`    | foreach class iterator (alt)                                            |

**Important note about UStruct::SerializeExpr in v868 RL:**

The on-disk parser **DOES exist** as a global function: `UStruct::SerializeExpr` at
`0x7FF6CD38C840` (renamed from `sub_7FF6CD38C840`). It contains the giant switch
over EExprToken byte values per the UE3 stock pattern. The unique strings
`"Bad expr token %02x"` (default case) and `"Bad array token %02x"` (inner switch
for byte 0x19's sub-table) are present as UTF-16 in the binary — searchable in
IDA after enabling UTF-16 string detection.

This function has been verified to use v868 byte permutations by cross-checking
several cases against the runtime GNatives table:
- case 0x4C → 2 subs (LET) ✓
- case 0x65 → LABEL_79 (UProperty pointer expansion) = LocalVariable ✓
- case 0x55 → LABEL_79 = InstanceVariable ✓
- case 0x29 → u16 + sub = JumpIfNot ✓
- case 0x5B → 4-byte UStruct + sub + u16 + sub = StructDefaultParameter ✓
- case 0x61 → 5 subs = New ✓
- case 0x3E → leaf (terminator) = EndFunctionParms ✓

**Critical finding for case 0x2C (EX_Conditional):**

Stock UE3: `EX_Conditional = 0x45` with wire format `1 sub + u16 + 1 sub + u16 + 1 sub`.
v868 RL: `EX_Conditional` rotated to **byte 0x2C** with the same wire format.
Verified by reading `UStruct::SerializeExpr` case 44 disassembly at `0x7FF6CD38CC52`:
```
call qword ptr [rax]              ; recursive SerializeExpr (sub-1 = cond)
call FArchive_SerializeWord       ; u16 SkipTrue
call qword ptr [rax]              ; sub-2 = true-expr
call FArchive_SerializeWord       ; u16 SkipFalse
call qword ptr [rax]              ; sub-3 = false-expr
```

There is a runtime GNatives[0x2C] handler (`execStatementWrapper` at
`0x7FF6CD308010`) which reads only `1 sub + 1 byte + optional 0x20 debug info`
— a different shape. The reason for the divergence is unclear (possibly
debug-mode instrumentation). For decompiler purposes, the **parse-time wire
format from `UStruct::SerializeExpr` is authoritative** since that's what the
cooker emits and what the package loader reads.

Stock UE3 has a single recursive `UStruct::SerializeExpr` that contains a giant switch over EExprToken. RL keeps this AND adds GNatives table-driven runtime dispatch (`GNatives[byte](Object, Frame)`). Every GNatives handler that reads a sub-expression does its own `byte = *Code++; GNatives[byte](...)` inline.

The `SerializeExpr_*` functions named above are HELPERS used to evaluate one expression and capture its typed result (FString, FName, UObject*, etc.). They follow the canonical pattern:
```
v_byte = *Code++;
GNatives[v_byte](Object, Frame, &outResult);
if (qword_7FF6CF27D7B0) outPtr = qword_7FF6CF27D7B0;  // forwarded result ptr
*Code++;                                                // 1 byte trailer (was: EX_DebugInfo 'OldOpCode')
if (*Code == 0x20) HandleOptionalDebugInfo(...);       // peek for debug marker
```

The package-load deserializer already byte-walks the bytecode before any of these run. The actual byte-stream reader at load time is part of `UStruct::Serialize` (the FArchive serializer for UStruct), which calls into `GNatives[byte]` recursively to walk every expression.

Anchor strings still useful for cross-version dumps:
- `"Unknown code token %02X"` → xref → `execScriptError_UnknownToken` (default-error stub address; appears in many slots)
- `"Attempt to assign variable through None"` → `execLet`
- `"Accessed array '%s.%s' out of bounds (%i/%i)"` → `execArrayElement`
- `"Attempt to insert an element at %i an %i-element array '%s'"` → DynArrayInsert (sub +0x09 of 0x19's sub-table)
- `"DynArrayReduce: Failed to find 2nd parameter property"` → DynArrayReduce (sub +0x23)

## Cross-version comparison procedure

When a new RL build is dumped:

1. **Locate the new GNatives base.** Search for the dispatcher pattern
   `((funcs_7FF6CD28592F[v3]))(a1, a2)` in `UStruct::SerializeExpr` (or its
   equivalent) — the array address it indexes into is the new GNatives base.
   Or grep IDA strings for "Unknown code token %02X" — its xref is the
   error handler, and the error handler's address appears in many GNatives
   slots.

2. **Dump the new table.**
   ```
   mcp__ida-pro-mcp__get_bytes(addr=NEW_BASE, size=896)  # 112 entries × 8
   ```

3. **Match handler addresses.** For each handler address in the v868
   snapshot above, search the new dump for that address (or the same
   address relative to the binary's base ASLR offset). When a known
   handler appears at a different byte index, that byte has rotated.

4. **Update the byte→token map** in `EngineBranch.RL.cs` `BuildTokenMap`
   to reflect the new bytes for the same handlers.

5. **Cross-check the on-disk parser function.** GNatives is the runtime
   dispatcher; the parser (UStruct::SerializeExpr-equivalent) reads bytes
   at script-load time and applies the 4→8 object index expansion that the
   runtime never sees. For each remapped byte, verify the parser reads the
   same token-shape modulo storage-vs-memory size — a token whose
   `Deserialize` reads the wrong number of *storage* bytes silently
   desyncs `ScriptPosition` and corrupts every following token. Watch for:
   object/property/function index reads (8-byte qword in GNatives,
   4-byte index in parser), optional debug-info / alignment reads, and
   JumpIfNot CodeOffset (u16 parsed, interpreted as in-memory `Position`,
   subject to cooker undercount — recovery in `JumpTokens.cs`).
   Note: `sub_7FF6CD38C840` (referenced from the `Bad expr token %02x`
   string in `FScriptSerializer.cpp`) is **not** the real on-disk parser —
   its opcode permutation differs from real bytecode. Find the real
   parser by following `funcs_X[v3]` from step 1 into its containing
   function.

6. **Verify by output.** Decompile a few sentinel functions
   (`Pawn.SpawnDefaultController`, `PRI_TA.SetLoadouts`, `Ball_TA.PostBeginPlay`,
   `Ball_TA.EnableOwnerTranslucency`) — these should render cleanly with the
   correct map. Use the `Test workflow` section in `CLAUDE.md`.

### Anchors that are stable across versions (use these to identify rotation)

These handlers have unique runtime fingerprints — match by these first when
reverse-engineering a new version:

| Anchor | Identifier |
|--------|------------|
| Default error handler | string `"Unknown code token %02X"` |
| `EX_Let` | string `"Attempt to assign variable through None"` |
| `EX_ArrayElement` | string `"Accessed array '%s.%s' out of bounds (%i/%i)"` |
| `EX_Context` | string `"Accessed None '%s'"` |
| `EX_ClassContext` | string `"Accessed null class context '%s'"` |
| `EX_GotoLabel` | string `"GotoLabel (%s): Label not found"` |
| `EX_IteratorPop` | string `"Unexpected iterator pop command at %s:%04X"` |
| `EX_Assert` (RL variant 0x2D) | string `"Assertion failed, line %i"` |
| `EX_New` (RL 0x61) | string `"No class passed to 'new' operator"` |
| `EX_ReturnNothing` | string `"Control reached the end of non-void function"` |
| Variadic terminator | the byte the `*v3 != 0x3E` checks consume (this byte itself is the EndFunctionParms opcode) |

## Sub-tables that need their own snapshot

For full coverage when a new version drops, also dump:

- **Dynarray sub-table at `0x7FF6CF2B2D80`** — 0x19's method dispatch (and the
  twin 0x10/0x71 chained-native dispatchers). Used for
  `arr.Length`, `arr.Add()`, `foreach arr(item)`, etc.
- **Primitive-cast sub-table at `funcs_7FF6CD2F735D`** (currently used by 0x6B).
- **Native operator table** at `RocketLeagueNativeNames.Map` (binary-verified
  207 entries). For new versions, regenerate via the binary's GNatives
  entries 256+ along with their internal name-table lookups.

## Notes on tautological mapping (anti-pattern)

A token mapping is "tautological" if it was chosen because the rendered
output looked right in one example, rather than because the binary handler
actually matches the wire format. The 0x21 case (mapped to
`DynamicArrayIteratorToken` because the rendered text said `foreach`)
caused months of cascading errors. Always verify against the binary
handler, not the output.
