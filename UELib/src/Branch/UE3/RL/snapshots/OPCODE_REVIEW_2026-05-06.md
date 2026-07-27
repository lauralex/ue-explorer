# Rocket League opcode review - 2026-05-06

Scope: source/binary review only. I did not use the UELib MCP package tools or
decompile package fixtures for this pass.

IDA database reviewed:

- `UStruct__SerializeExpr` at `0x7FF6CD38C840`
- `GNatives` table at `0x7FF6CF2AA580`
- Dynamic-array method sub-table at `0x7FF6CF2B2D80`
- Chained native dispatcher handlers around `0x7FF6CD30C850`
- Selected runtime handlers for rows where storage and runtime disagree

Important rule confirmed again: for UELib disk parsing, `UStruct__SerializeExpr`
is the storage authority. GNatives is still valuable for runtime semantics, but
several bytes have different runtime and storage shapes.

## Executive findings

1. The snapshot file is stale in several rows.
   - Current code maps primary `0x20` to `DebugInfoToken`; the snapshot row still
     says legacy return.
   - Current code maps primary `0x2C` to `ConditionalToken`; the snapshot row
     still says statement wrapper, even though `SerializeExpr` case `0x2C` is
     the 3-sub + 2-u16 ternary shape.
   - GNatives entry `0x63` is `0x7FF6CD2F0380`, an empty/zero-code-pointer stub.
     The snapshot row says `0x7FF6CD2F03B0` and signed-byte shape; that address is
     inside `execSwitch`, not the actual `0x63` handler.

2. Primary opcode deserialization has multiple storage-shape risks. These are
   not just rendering concerns; if the byte appears in cooked script, the current
   token can consume the wrong number of bytes. Highest risk rows: `0x03`,
   `0x05`, `0x07`, `0x12`, `0x13`, `0x14`, `0x16`, `0x17`, `0x21`, `0x2E`,
   `0x31`, `0x33`, `0x35`, `0x3B`, `0x46`, `0x48`, `0x4D`, `0x54`, `0x5A`,
   `0x5F`, `0x63`, `0x6D`.

3. The dynamic-array sub-table is the largest unresolved area. The binary table
   has many real non-error handlers that are either unmapped or mapped to tokens
   whose current `Deserialize` shape does not match `SerializeExpr`'s inner
   switch. The fallback `opCode + 5000` path in `ExtendedNativeFunctionToken` is
   structurally unsafe for a `0x19` sub-byte: this is an array sub-table, not a
   native-index namespace.

4. The chained native dispatcher design is basically right after the recent
   `CreateNativeToken` fix. IDA confirms:
   - `0x70` reads a sub-byte and dispatches `GNatives[sub]`.
   - `0x71` reads a sub-byte and dispatches the table at `GNatives + 0x100*8`.
   - The same pattern continues for `0x72..0x7F`.
   - `SerializeExpr` treats `0x70..0x7F` as sub-byte + variadic args until
     `0x3E`, which matches `ChainedNativeDispatcherTokenRL` and
     `ExAlternativeExtendedNativeFunctionTokenRL` when the computed native index
     is real.

5. Direct native bytes `0x80..0xFF` are storage-shaped as variadic native calls
   until `0x3E` plus optional debug. The special code mappings for `0x82` and
   `0x84` are fine if their token classes remain variadic-call compatible.
   `0xC8` mapped to `NothingToken` is a recovery override for a default-error
   native entry; keep it only if the GNatives row remains the default handler.

## Legend

- `OK`: current token consumes the parser shape.
- `RECOVERY`: parser/runtime default or reserved; current one-byte leaf recovery
  is acceptable if the byte is invalid/absent.
- `SEM`: storage consumption is close enough, but semantics or rendering need
  review.
- `RISK`: current token likely consumes the wrong storage shape if the byte
  occurs.

## Primary opcode table

| Byte | SerializeExpr storage shape | Current code mapping | Verdict |
|------|-----------------------------|----------------------|---------|
| `0x00` | 1 sub | `ContextAwareReturnTokenRL` | OK with padding caveat |
| `0x01` | 1 sub | `BoolVariableToken` | OK; runtime handler diverges |
| `0x02` | default bad expr | `IntConstToken` | RISK if seen; should be recovery leaf |
| `0x03` | label table entries, 12 bytes each until null FName | `NothingToken` | RISK |
| `0x04` | default bad expr | `EndOfScriptToken` | RECOVERY |
| `0x05` | variadic body until `0x3E` + optional debug | `ArrayElementToken` | RISK |
| `0x06` | 1 sub | `BoolVariableToken` | OK |
| `0x07` | UField/UObject ref + 1 sub | `ReturnNothingToken` | RISK |
| `0x08` | default bad expr | `EatReturnValueTokenRL` | RISK if seen |
| `0x09` | optional debug + 2 subs | `DiscardKeepTokenRL` | SEM; misses explicit leading debug if present |
| `0x0A` | default bad expr | `NothingToken` | RECOVERY |
| `0x0B` | dword literal | `IntConstToken` | OK |
| `0x0C` | 2 subs | `LetBoolToken` | OK |
| `0x0D` | default bad expr | `NothingToken` | RECOVERY |
| `0x0E` | UField/UObject ref + 2 subs | `StructCmpEqToken` | OK |
| `0x0F` | UField/UFunction ref + variadic body until `0x3E` + debug | `FinalFunctionTokenRL` | OK |
| `0x10` | leaf | `NothingToken` | OK |
| `0x11` | UProperty ref | `OutVariableToken` | OK |
| `0x12` | variadic body until `0x3E` + debug + 1 sub | `EatReturnValueTokenRL` | RISK |
| `0x13` | UProperty ref | `NoObjectToken` | RISK |
| `0x14` | u16 + 1 sub | `DynamicArrayLengthToken` | RISK |
| `0x15` | 1 sub | `InterfaceContextToken` | OK |
| `0x16` | variadic body until `0x3E` + optional debug | `DynamicArrayElementToken` | RISK |
| `0x17` | 1 sub | `DelegateAccessTokenRL` | RISK; GNatives runtime reads 2 subs |
| `0x18` | leaf | `NothingToken` | OK |
| `0x19` | 1 sub-byte + dynamic-array inner switch | `ExtendedNativeFunctionToken` | See dynamic-array table |
| `0x1A` | UField/UClass ref + 1 sub | `DynamicCastToken` | OK shape |
| `0x1B` | variadic body until `0x3E` + optional debug | `DynamicArrayElementToken` | RISK |
| `0x1C` | leaf | `IntZeroToken` | OK |
| `0x1D` | leaf | `NothingToken` | OK |
| `0x1E` | 2 subs | `ArrayElementToken` | OK |
| `0x1F` | leaf | `SelfToken` | OK |
| `0x20` | 3 dwords + 1 byte | `DebugInfoToken` | OK; snapshot row stale |
| `0x21` | leaf | `StringCastTokenRL` | RISK; GNatives runtime reads 1 sub |
| `0x22` | u16 | `JumpToken` | OK |
| `0x23` | leaf | `NoObjectToken` | OK |
| `0x24` | default bad expr | `NothingToken` | RECOVERY |
| `0x25` | u16, then 1 sub unless u16 is `0xFFFF` | `CaseToken` | OK |
| `0x26` | default bad expr | `NothingToken` | RECOVERY |
| `0x27` | leaf | `IntZeroToken` | OK |
| `0x28` | byte + sub + u16 + UField/UObject ref + byte + sub | `ContextTokenRL` | OK |
| `0x29` | u16 + 1 sub | `JumpIfNotToken` | OK |
| `0x2A` | byte literal | `ByteConstToken` | OK |
| `0x2B` | byte literal | `IntConstByteToken` | OK |
| `0x2C` | sub + u16 + sub + u16 + sub | `ConditionalToken` | OK; snapshot row stale |
| `0x2D` | u16 + byte + 3 subs | `AssertExpressionTokenRL` | OK |
| `0x2E` | 1 sub + u16 | `NothingToken` | RISK |
| `0x2F` | leaf | `IntOneToken` | OK |
| `0x30` | 3 dwords | `VectorConstToken` | OK |
| `0x31` | u16 + optional debug + sub + optional debug + byte | `OptionalArgSkipTokenRL` | RISK; runtime optional-skip shape also differs |
| `0x32` | FName + UProperty ref | `InstanceDelegateTokenRL` | OK |
| `0x33` | 2 subs | `EatReturnValueTokenRL` | RISK |
| `0x34` | default bad expr | `NothingToken` | RECOVERY |
| `0x35` | default bad expr | `NameConstToken` | RISK if seen |
| `0x36` | UField/UObject ref + 1 sub | `PropertySetterDiscardTokenRL` | OK shape |
| `0x37` | 2 subs + u16 + variadic body until `0x3E` + debug | `NullConditionalCallTokenRL` | OK |
| `0x38` | byte + sub + u16 + UField/UObject ref + byte + sub | `ClassContextToken` | OK shape |
| `0x39` | 8 raw bytes / FName-like | `NameConstToken` | OK |
| `0x3A` | leaf | `TrueToken` | OK |
| `0x3B` | FName | `ObjectConstToken` | RISK; storage width/type likely wrong |
| `0x3C` | default bad expr | `NothingToken` | RECOVERY |
| `0x3D` | default bad expr | `NothingToken` | RECOVERY |
| `0x3E` | leaf terminator | `EndFunctionParmsToken` | OK |
| `0x3F` | default bad expr | `NothingToken` | RECOVERY |
| `0x40` | byte + UProperty ref + FName + variadic body until `0x3E` + debug | `DelegateFunctionToken` | OK shape |
| `0x41` | FName + variadic body until `0x3E` + debug | `VirtualFunctionToken` | OK |
| `0x42` | default bad expr | `NothingToken` | RECOVERY |
| `0x43` | UObject ref | `ObjectConstToken` | OK |
| `0x44` | default bad expr | `NothingToken` | RECOVERY |
| `0x45` | default bad expr | `NothingToken` | RECOVERY |
| `0x46` | 1 sub | `LetBoolToken` | RISK; GNatives runtime reads 2 subs |
| `0x47` | leaf | `EmptyParmToken` | OK |
| `0x48` | 2 raw bytes + FName + 1 byte | `EventSubscribeToken` | RISK |
| `0x49` | 2 subs | `LetDelegateToken` | OK |
| `0x4A` | UProperty ref + UField ref + byte + byte + sub | `StructMemberToken` | OK shape |
| `0x4B` | default bad expr | `NothingToken` | RECOVERY |
| `0x4C` | 2 subs | `LetToken` | OK |
| `0x4D` | UProperty ref | `StructValueTokenRL` | RISK; GNatives runtime reads struct ref + sub |
| `0x4E` | default bad expr | `NothingToken` | RECOVERY |
| `0x4F` | leaf / optional-skip terminator marker | `NothingToken` | OK |
| `0x50` | null-terminated ANSI string | `StringConstToken` | OK |
| `0x51` | null-terminated UTF-16 string | `UnicodeStringConstToken` | OK |
| `0x52` | 1 sub | `BoolVariableToken` | OK |
| `0x53` | UField/UObject ref + 2 subs | `StructCmpEqToken` | OK |
| `0x54` | variadic body until `0x3E` + optional debug | `DynamicArrayElementToken` | RISK |
| `0x55` | UProperty ref | `InstanceVariableToken` | OK |
| `0x56` | FName | `StateFunctionTokenRL` | OK |
| `0x57` | UProperty/UField/type + switch expression + case loop | `SwitchToken` | OK shape |
| `0x58` | UProperty ref | `DefaultVariableToken` | OK |
| `0x59` | FName + variadic body until `0x3E` + debug | `GlobalFunctionToken` | OK |
| `0x5A` | 8 raw bytes / FName-like | `LocalVariableToken` | RISK |
| `0x5B` | object ref + sub + u16 + sub | `StructDefaultParameterTokenRL` | OK shape; verify ref type |
| `0x5C` | 1 sub | `GotoLabelToken` | OK |
| `0x5D` | u16 | `JumpToken` | OK |
| `0x5E` | UProperty ref | `LocalVariableToken` | OK |
| `0x5F` | default bad expr | `LocalVariableToken` | RISK if seen |
| `0x60` | 3 dwords | `VectorConstToken` | OK |
| `0x61` | 5 subs | `NewExpressionTokenRL` | OK |
| `0x62` | FName | `DelegateFunctionRefTokenRL` | OK |
| `0x63` | leaf; GNatives actual handler `0x7FF6CD2F0380` zeroes code pointer | `IntConstByteToken` | RISK; snapshot row wrong |
| `0x64` | dword literal | `FloatConstToken` | OK |
| `0x65` | UProperty ref | `LocalVariableToken` | OK |
| `0x66` | 1 sub | `BoolVariableToken` | OK |
| `0x67` | default bad expr | `NothingToken` | RECOVERY |
| `0x68` | default bad expr | `NothingToken` | RECOVERY |
| `0x69` | leaf | `IteratorPopToken` | OK shape |
| `0x6A` | leaf | `EmptyDelegateToken` | OK |
| `0x6B` | byte + 1 sub | `PrimitiveCastToken` | OK |
| `0x6C` | UProperty ref | `ReturnNothingToken` | OK shape |
| `0x6D` | default bad expr | `OutVariableToken` | RISK if seen |
| `0x6E` | default bad expr | `NothingToken` | RECOVERY |
| `0x6F` | default bad expr | `NothingToken` | RECOVERY |
| `0x70` | sub-byte + variadic native call | `ChainedNativeDispatcherTokenRL` | OK |
| `0x71` | sub-byte + variadic native call | `ExAlternativeExtendedNativeFunctionTokenRL` | OK |
| `0x72..0x7F` | sub-byte + variadic native call | `ChainedNativeDispatcherTokenRL` | OK |

## Dynamic-array method sub-table for primary `0x19`

The sub-table at `0x7FF6CF2B2D80` contains 64 entries. The first 54 entries are
not all reserved; many are real handlers. The parser's inner switch is the
storage authority.

| Sub-byte | GNatives/sub-table handler | SerializeExpr inner shape | Current mapping | Verdict |
|----------|----------------------------|---------------------------|-----------------|---------|
| `0x00` | `0x7FF6CD2ED7E0` | byte + 2 subs | `DynamicArrayElementTokenRL` | OK |
| `0x01` | `0x7FF6CD2EDE20` | 1 sub | `DynamicArrayLengthToken` | OK |
| `0x02` | `0x7FF6CD2EDE80` | 4 subs + debug | fallback `__NFUN_5002__` | RISK |
| `0x03` | `0x7FF6CD2EE230` | 4 subs + debug | `DynamicArrayRemoveToken` | RISK |
| `0x04` | `0x7FF6CD2EE930` | sub + byte + u16 + 2 subs + debug | `DynamicArrayFindContainsTokenRL` | OK shape |
| `0x05` | `0x7FF6CD2EEB10` | sub + byte + u16 + 3 subs + debug | `DynamicArrayFindStructTokenRL` | OK shape |
| `0x06` | `0x7FF6CD2EEE20` | 3 subs + debug | `DynamicArrayAddToken` | OK |
| `0x07` | `0x7FF6CD2EF0C0` | sub + u16 + 2 subs + debug | `DynamicArrayAddItemToken` | OK shape |
| `0x08` | `0x7FF6CD2EF5B0` | sub + u16 + 2 subs + debug | `DynamicArrayRemoveItemToken` | OK shape |
| `0x09` | `0x7FF6CD2EF2C0` | sub + u16 + 3 subs + debug | unmapped | RISK; likely insert-family |
| `0x0A` | `0x7FF6CD2EF860` | 3 subs + u16 | `DynamicArrayIteratorRL` | OK shape |
| `0x0B` | `0x7FF6CD2F3BE0` | sub + u16 + variadic until `0x3E` + debug | `VariadicArrayConcatTokenRL` | OK |
| `0x0C` | `0x7FF6CD2F3CD0` | sub + u16 + variadic until `0x3E` + debug | `VariadicArrayAddUniqueItemsRL` | OK |
| `0x0D` | `0x7FF6CD2F45E0` | sub + u16 + 2 subs + debug | `DynamicArrayAddUniqueItemTokenRL` | OK shape |
| `0x0E` | `0x7FF6CD2EFBA0` | sub + u16 + 2 subs + debug | unmapped | RISK |
| `0x0F` | `0x7FF6CD2EFDE0` | sub + u16 + 2 subs + debug | unmapped | RISK |
| `0x10..0x1F` | default error handler | bad array token | fallback native path | RISK if seen; fallback overconsumes |
| `0x20` | `0x7FF6CD2F4F60` | sub + u16 + sub + UProperty + sub + debug + sub | unmapped | RISK |
| `0x21` | `0x7FF6CD2F1880` | sub + u16 + sub + UProperty + sub + debug + sub | `DynamicArrayFilterOutTokenRL` | OK shape |
| `0x22` | `0x7FF6CD2F1D20` | sub + u16 + sub + UProperty + sub + debug + sub | `DynamicArrayMapTokenRL` | OK shape |
| `0x23` | `0x7FF6CD2F26D0` | sub + u16 + sub + UProperty + 2 subs + debug + sub | unmapped | RISK; reduce-family |
| `0x24` | `0x7FF6CD2F2D20` | sub + u16 + 2 subs + debug | `FindFirstWithDelegate` | RISK |
| `0x25` | `0x7FF6CD2F31C0` | sub + u16 + 2 subs + debug | `DynamicArrayEveryTokenRL` | OK shape |
| `0x26` | `0x7FF6CD2F31E0` | sub + u16 + 2 subs + debug | `DynamicArrayAnyTokenRL` | OK shape |
| `0x27` | `0x7FF6CD2F3200` | sub + u16 + sub + u16 + UProperty + sub + debug + sub | unmapped | RISK |
| `0x28` | `0x7FF6CD2F3E80` | sub + u16 + variadic until `0x3E` + debug + sub | `VariadicArrayFillOutTokenRL` | OK shape |
| `0x29` | `0x7FF6CD2F3FD0` | sub + u16 + 2 subs + debug | `DynamicArrayFilterTokenRL` | OK shape |
| `0x2A` | `0x7FF6CD2F47F0` | sub + u16 + UProperty + sub + debug + sub | unmapped | RISK |
| `0x2B` | `0x7FF6CD2F4970` | sub + u16 + 3 subs + debug | `DynamicArrayFirstTokenRL` | SEM/RISK; current token also expects an EndParms sub |
| `0x2C` | `0x7FF6CD2F4E20` | sub + u16 + sub + u16 + sub + debug | `DynArrayEqualToken` | RISK |
| `0x2D` | `0x7FF6CD2F45D0` | sub + u16 + 2 subs + debug | unmapped | RISK |
| `0x2E` | `0x7FF6CD2F51E0` | sub + u16 + sub + UProperty + sub + debug + sub | unmapped | RISK |
| `0x2F` | `0x7FF6CD2F2200` | sub + u16 + sub + UProperty + sub + debug + sub | unmapped | RISK |
| `0x30` | `0x7FF6CD2F5420` | sub + u16 + 2 subs + debug | `DynamicArrayFindTypeTokenRL` | OK shape |
| `0x31` | `0x7FF6CD2F3410` | sub + u16 + sub + u16 + UProperty + sub + debug + sub | `DynamicArrayConcatTokenRL` | OK shape |
| `0x32` | `0x7FF6CD2F3600` | sub + u16 + sub + u16 + UProperty + sub + debug + sub | unmapped | RISK |
| `0x33` | `0x7FF6CD2F38A0` | sub + u16 + sub + u16 + UProperty + sub + debug + sub | unmapped | RISK |
| `0x34` | `0x7FF6CD2F55A0` | 1 sub | unmapped | RISK |
| `0x35` | `0x7FF6CD2EDB50` | byte + 2 subs | unmapped | RISK |
| `0x36..0x3F` | default error handler | bad array token | fallback native path | RISK if seen; fallback overconsumes |

## Chained native dispatchers

IDA decompile checks:

- `0x70` handler `0x7FF6CD30C850` reads one sub-byte and dispatches
  `GNatives[sub_byte]`.
- `0x71` handler `0x7FF6CD30C870` reads one sub-byte and dispatches
  the table at `0x7FF6CF2AAD80`, which is `GNatives + 256*8`.
- The primary GNatives table entries for `0x72..0x7F` are consecutive
  dispatcher stubs, matching slices `512..4095`.
- `EndFunctionParms` runtime handler `0x7FF6CD2F00B0` backs up the runtime code
  pointer; parser still treats byte `0x3E` as the terminator.

Current source:

- `ChainedNativeDispatcherTokenRL` computes
  `((OpCode - 0x70) << 8) | subOpCode`, which matches the binary.
- `ExAlternativeExtendedNativeFunctionTokenRL` computes `256 + subOpCode`,
  which matches the `0x71` slice.
- `TokenFactory.CreateNativeToken` now avoids feeding native indexes above
  `0xFF` back into the primary opcode map. That fix is necessary for
  `0x71 0x02` = native 258 `ClassIsChildOf`.

Remaining risk:

- Unknown-native suppression assumes the following bytes do not form a real
  variadic call. That is good for rows proven to be default-error GNatives, but
  the unknown set should be regenerated if the binary changes.

## Direct native bytes `0x80..0xFF`

`SerializeExpr` handles `v10 >= 0x80` by recursively serializing args until
`0x3E`, then consuming optional debug info. This is the normal variadic native
wire format.

Current source implications:

- `AndTokenRL` at `0x82` and `OrTokenRL` at `0x84` must continue to consume
  native-call argument lists, not fixed primary-opcode operands.
- Most direct natives should resolve through `RocketLeagueNativeNames` /
  package NTL data and `NativeFunctionToken`.
- The hard override `0xC8 -> NothingToken` should be kept only while
  GNatives[200] is confirmed to be the default-error handler for this binary.

## Recommended next work

1. Update `GNATIVES_SNAPSHOT_v868.md` to separate runtime GNatives shape from
   parser storage shape for every conflicting byte. Do not keep one "wire
   format" column when the two disagree.
2. Fix high-risk primary mappings by storage shape first:
   - `0x03` should be `LabelTableToken` or an RL equivalent.
   - `0x02`, `0x08`, `0x35`, `0x5F`, `0x6D` should not be multi-byte tokens if
     they remain parser default rows.
   - Review and re-tokenize the parser/runtime conflict rows before trusting
     rendered output: `0x17`, `0x21`, `0x31`, `0x46`, `0x48`, `0x4D`, `0x5A`,
     `0x63`.
3. Replace the dynamic-array fallback path with a parser-shaped bad-array token
   for reserved/default sub-bytes, and add explicit token classes for the real
   unmapped sub-bytes before they appear in fixtures.
4. Re-run the normal sentinel package tests only after the source-level mapping
   pass. This review intentionally did not run MCP fixture tests.
