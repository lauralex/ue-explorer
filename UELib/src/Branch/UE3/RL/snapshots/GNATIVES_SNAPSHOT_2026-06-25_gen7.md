# Rocket League opcode snapshot — 2026-06-25 generation 7

> Snapshot, not permanent truth. Re-check the corresponding executable and
> `UStruct::SerializeExpr` before reusing this map for another game update.

## Binary and package identity

- IDB: `C:\Users\Authority\Desktop\RE stuff\rldecrypted\RocketLeague_Dumped.exe.i64`
- Image base: `0x7FF6B6330000`
- Runtime primary dispatcher table: `0x7FF6B86339A0`
- Default/error handler: `0x7FF6B669C430`
- On-disk parser (`UStruct::SerializeExpr`): `0x7FF6B670E540`
- Package version/licensee version: `868/32`
- Generation count: `7`
- TAGame GUID: `8B48EC7F40EF2EBC868A1AA37AF62AE8`
- ProjectX GUID: `98AA74424590591C0839EFB66695E605`

The version/licensee pair did not change from the May packages, so
`EngineBranchRL` selects this rotation by generation count. The May fixtures
have generation count 5.

## Verification anchors

The table was dumped from the live July 23 process and matched against the
current IDB. Storage shapes were then checked in
`sub_7FF6B670E540`:

- `0x16` is the variadic terminator. Runtime handler
  `sub_7FF6B6671830` clears the result pointer and rewinds `Code` by one.
  Parser call loops terminate on decimal `22`.
- `0x45` is `EX_Conditional`. Parser case `0x45` reads
  `sub + u16 + sub + u16 + sub`; runtime handler
  `sub_7FF6B6689840` implements the matching conditional branches.
- `0x57` is the dynamic-array sub-dispatcher and has the corresponding inner
  switch in the parser.
- `0x41` has the debug-info fixed-width parser shape.

## Current primary token map

This is the map implemented by `BuildGeneration7TokenMap`. Entries mapped to
`NothingToken` are conservative/error/reserved positions unless separately
verified; their presence here is not a semantic claim.

| Byte | Token | Byte | Token |
|---:|---|---:|---|
| `00` | Nothing | `38` | PrimitiveCast |
| `01` | InstanceVariable | `39` | Nothing |
| `02` | DefaultVariable | `3A` | ReturnNothing |
| `03` | StateVariable | `3B` | ArrayElement |
| `04` | ContextAwareReturnRL | `3C` | DynamicArrayElement |
| `05` | Switch | `3D` | DynamicArrayElement |
| `06` | Jump | `3E` | DynamicArrayElement |
| `07` | JumpIfNot | `3F` | EmptyDelegate |
| `08` | Stop | `40` | Nothing |
| `09` | AssertExpressionRL | `41` | DebugInfo |
| `0A` | Case | `42` | DelegateFunction |
| `0B` | Nothing | `43` | InstanceDelegateRL |
| `0C` | LabelTable | `44` | LetDelegate |
| `0D` | GotoLabel | `45` | Conditional |
| `0E` | StructValueRL | `46` | OutVariable |
| `0F` | Let | `47` | OptionalArgSkipRL |
| `10` | Nothing | `48` | EmptyParm |
| `11` | NewExpressionRL | `49` | DelegateFunctionRefRL |
| `12` | ClassContext | `4A` | BoolVariable |
| `13` | PropertySetterDiscardRL | `4B` | FieldWrappedExpressionRL |
| `14` | LetBoolRL | `4C` | Nothing |
| `15` | EndParmValue | `4D` | Jump |
| `16` | EndFunctionParms | `4E` | Nothing |
| `17` | Self | `4F` | Nothing |
| `18` | Skip | `50` | EventSubscribe |
| `19` | Context | `51` | EventUnsubscribe |
| `1A` | ArrayElement | `52` | LetBoolRL |
| `1B` | VirtualFunction | `53` | FilterEditorOnlyRL |
| `1C` | FinalFunctionRL | `54` | VariadicReturnValueRL |
| `1D` | IntConst | `55` | StringLengthRL |
| `1E` | FloatConst | `56` | StructDefaultParameterRL |
| `1F` | StringConst | `57` | ExtendedNative/dynarray |
| `20` | ObjectConst | `58` | DynArrayResultRL |
| `21` | NameConst | `59` | NameConst |
| `22` | RotationConst | `5A` | NullConditionalCallRL |
| `23` | VectorConst | `5B` | DiscardKeepRL |
| `24` | IntConstByte | `5C` | StringCastRL |
| `25` | IntZero | `5D` | StateFunctionRL |
| `26` | IntOne | `5E` | BoolVariable |
| `27` | True | `5F` | Nothing |
| `28` | False | `60` | LocalVariable |
| `29` | LocalVariable | `61` | Nothing |
| `2A` | NoObject | `62` | Nothing |
| `2B` | LocalVariable | `63` | Nothing |
| `2C` | ByteConst | `64` | Nothing |
| `2D` | BoolVariable | `65` | Nothing |
| `2E` | DynamicCast | `66` | Nothing |
| `2F` | Iterator | `67` | Nothing |
| `30` | IteratorPop | `68` | Nothing |
| `31` | IteratorNext | `69` | Nothing |
| `32` | StructCmpEq | `6A` | Nothing |
| `33` | StructCmpNe | `6B` | Nothing |
| `34` | UnicodeStringConst | `6C` | Nothing |
| `35` | StructMember | `6D` | Nothing |
| `36` | Nothing | `6E` | Nothing |
| `37` | GlobalFunction | `6F` | Nothing |

Native prefixes remain `0x70..0x7F`; `0x71` uses
`ExAlternativeExtendedNativeFunctionTokenRL`, while the remaining prefixes use
`ChainedNativeDispatcherTokenRL`. Operator helpers remain `0x82` (`AndTokenRL`)
and `0x84` (`OrTokenRL`).

## Validation performed

The rebuilt MCP was launched separately before replacing the registered
server. It loaded both generation-7 packages and produced structurally clean,
warning-free output for:

- `PRI_TA.UpdateTitleFromLoadout`, `SetLoadouts`, `ValidateLoadout`,
  `ReplicateLoadoutToServer`, and `ServerSetLoadoutComplete`
- `Profile_TA.BuildServerSetLoadoutParams`
- `RBActor_TA.PostBeginPlay`, `Car_TA.PostBeginPlay`,
  `Ball_TA.IsGroundHit`, and `PRI_TA.HandlePlayerNameChanged`
- the title and loadout-validation functions used by the Nebula investigation

The full available TAGame sentinel subset was then run against both the
generation-7 and generation-5 TAGame fixtures: 15/15 functions in each set had
no warning, unresolved cast, truncation marker, or ghost native. The
generation-5 map remains selected for the May fixtures.
