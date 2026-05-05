# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Repository purpose

This is the **Rocket League fork** of [UE Explorer](https://github.com/UE-Explorer/UE-Explorer) — a Windows GUI for browsing and decompiling Unreal Engine 1/2/3 packages (`.upk`, `.u`). The fork's reason to exist is that Rocket League ships heavily customized UnrealScript bytecode: licensee-modified opcode table, custom extended-native opcodes, custom variadic-array natives, etc. Most ongoing work in this repo is **adding/fixing tokens under `UELib/src/Branch/UE3/RL/`** so that RL packages decompile to readable script.

RL packages are encrypted; UELib does not decrypt them. They must first be processed with [RLUPKTool](https://github.com/AltimorTASDK/RLUPKTool) before being opened.

## Test packages — version pitfall

When verifying token-map changes against actual bytecode, the `.upk` files **must come from the same RL build** as whatever binary is open in IDA / being reverse-engineered. RL rotates its opcode permutation across patches, so a SerializeExpr in the current binary can dispatch byte `0x3E` as EndFunctionParms while older `.upk` files on disk still emit `0x4C` for the same role. A version mismatch silently invalidates every shape inference made from the binary.

To produce a fresh, version-matched fixture set:

```pwsh
# 1. Copy the canonical script packages (TAGame, Core, ProjectX, Engine, IpDrv, GFxUI,
#    GuidCache, Startup, WinDrv) from the live install:
Copy-Item "D:\Games\rocketleague\TAGame\CookedPCConsole\TAGame.upk" `
          "C:\Users\Authority\Desktop\RE stuff\rldecrypted\absolutelynewupks\"
# (repeat for each package)

# 2. Decrypt each in place — RLUPKTool writes a new decrypted file alongside the input:
& "C:\Users\Authority\Desktop\RE stuff\rldecrypted\RLUPKTool.exe" `
  "C:\Users\Authority\Desktop\RE stuff\rldecrypted\absolutelynewupks\TAGame.upk"
```

Then load the **decrypted** output via the uelib MCP (`mcp__uelib__load_package` with `build_target: "RocketLeague"`) and disassemble. Older fixtures under `rldecrypted\`, `rldecrypted\upkbackup\`, `rldecrypted\newupks\`, `rldecrypted\newupks2\` are from earlier RL versions and should not be used to validate work derived from a newer binary.

After decryption, class names in the loaded package have **no namespace prefix**: pass `Actor` to `mcp__uelib__disassemble_function` / `decompile_function`, not `Engine.Actor`. (The package summary's `path` shows `Engine_decrypted` so the qualified path would be `Engine_decrypted.Actor`, but the bare name resolves correctly.)

## Iterating on UELib code with the MCP server attached

The Claude Code MCP launches `UELib/MCP/publish/Eliot.UELib.MCP.exe` once per session and holds an exclusive lock on it. So the normal `dotnet publish ... -o UELib/MCP/publish` rebuild fails with "process cannot access the file" while a session is live.

Workflow that works (PowerShell automation — much faster than the manual stop/copy/restart):

1. Make code changes (any project — both `Eliot.UELib` and `Eliot.UELib.MCP` get bundled).
2. Build into a sibling directory:
   ```pwsh
   dotnet publish UELib/MCP/Eliot.UELib.MCP.csproj `
       -c Release -r win-x64 -p:PublishSingleFile=true --self-contained true `
       -o UELib/MCP/publish_new
   ```
3. Hot-swap: kill the live MCP process, atomically replace the exe, the user reconnects via `/mcp`:
   ```pwsh
   Get-Process -Name "Eliot.UELib.MCP" -ErrorAction SilentlyContinue | Stop-Process -Force
   Start-Sleep -Milliseconds 500
   Move-Item "UELib\MCP\publish_new\Eliot.UELib.MCP.exe" "UELib\MCP\publish\Eliot.UELib.MCP.exe" -Force
   ```
4. Ask the user to run `/mcp` to reconnect. The user's existing handle from before is invalid; reload packages.

`publish_new/` is gitignored-by-convention (don't commit). It exists only as the build target while `publish/` holds the locked-by-MCP active exe.

**Cost-saving rule.** Each rebuild+reconnect cycle costs ~30s + the user's attention. Do as much investigation/multi-byte-fixing as possible in one batch before requesting the rebuild — see "Testing workflow" below for the prescribed loop.

## Testing workflow — wrong outputs MUST be caught from your own testing

The decompiler can silently produce structurally-broken output (orphan tokens, wrong assignments, empty if-bodies, mangled cast patterns) without throwing. Don't rely on the user to flag bad outputs. **Decompile a sentinel sample after every batch of token-map changes and compare against the prior baseline before declaring done.**

### Sentinel function set (covers most opcode shapes)

These functions span the common bytecode patterns. After each rebuild, decompile each and confirm no regression:

| Function                                       | Tests                                                              |
|------------------------------------------------|--------------------------------------------------------------------|
| `Pawn.SpawnDefaultController` (Engine_decrypted) | Baseline UE3 — if-then-return + Spawn() + super calls             |
| `Pawn.PostBeginPlay` (Engine_decrypted)        | super(...).PostBeginPlay() + nested if + delegate-property access  |
| `Controller.PostBeginPlay`                     | nested if + simple assignments                                      |
| `PlayerController.PlayerTick`                  | multi-level nested if + native operator chains                     |
| `RBActor_TA.PostBeginPlay`                     | super(SpecificClass).PostBeginPlay() + simple if                   |
| `Car_TA.CreateRumblePickups`                   | static-method call with multi-arg pattern                           |
| `Car_TA.PostBeginPlay`                         | NameplateComponent + AttachComponent + multi-arg calls             |
| `Ball_TA.PostBeginPlay`                        | `assert(cond)` (0x2D), `if(...) {...}`                              |
| `Ball_TA.OnCarTouch`                           | optional-arg-skip (0x31) + super-call                              |
| `Ball_TA.EnableOwnerTranslucency`              | foreach + `new(...)` + multi-statement body                        |
| `Ball_TA.Explode`                              | `new(...)`, dynamic-cast `Class(value)`, multi-arg Spawn variant   |
| `Ball_TA.IsGroundHit`                          | ternary `? :` (currently broken — sentinel for that work)          |
| `PRI_TA.SetLoadouts`                           | `while(...)` loops with `++Index` updates                           |
| `PRI_TA.PostBeginPlay`                         | many `new(...)` patterns + optional default args                   |
| `PRI_TA.HandlePlayerNameChanged`               | nested if + GetSingleton + comparison                              |
| `Car_TA.HandleTeamChanged`                     | EventSubscribe + delegate-access + multi-arg call                  |
| `Car_TA.GetPreviewTeamIndex`                   | StructDefaultParameter (0x5B) + null-check + cast                  |
| `Car_TA.UpdateTeamLoadout`                     | early-return-from-if + complex-cond + while-loop body              |
| `AIController_Soccar_TA.HandleNewPickup`       | nested cast + delegate-property assignment + `if` chain            |
| `Actor.FindEventsOfClass` (Engine_decrypted)   | foreach (extended-native, not byte 0x21!)                          |

### Loop

```
while not done:
  edit token map / token classes
  dotnet publish ... -o publish_new
  swap exe + reconnect /mcp
  for each sentinel function:
    decompile_function(class, function)
    eyeball for: orphan tokens, "/* truncated */", "/* unresolved cast */",
                 empty if/while bodies, vect() with absurd values,
                 ".*" inside operator slots, multiple consecutive `return`,
                 `__NFUN_NNN__` ghost natives, bare names with no operator
  if any regression: revert, retry differently
```

### Output smells (red flags)

- **`/* unresolved cast */()`** — current 0x19 (now dynarray dispatcher) was producing this; if it reappears, that byte was remapped wrong.
- **`vect(0, 0, -9.52e21)`** literal where the LHS isn't a vector — a multi-byte-read token (VectorConst, RotationConst) was placed at a byte that doesn't actually emit 12 bytes of payload.
- **`obj.*Name`** with a star — `DelegatePropertyToken` rendering with a synthesized FName from the import table; sometimes legitimate, sometimes a name-resolution artifact.
- **Empty `{}` followed by an orphan `return X;`** — the JumpIfNot's CodeOffset placed the body bound BEFORE its actual content (not a token-map issue, a position/storage scaling thing — see "Position vs Storage" below).
- **`,,,,,` with leading/trailing commas inside a call** — usually `EmptyParmToken` chain for omitted optional args; legitimate but verbose.
- **`__NFUN_<index>__(...)`** — a chained-native dispatcher emitted a placeholder for a native at an index that has no real entry in GNatives. Typically means an over-consuming primary opcode mapping is reading bytes that the parser then dispatches as natives.
- **Bare `.` `=` `==` operators dangling** — a 2-or-more-sub Let/Comparison consumed only one sub successfully and the operator orphans.

### When testing reveals a bad mapping

Always verify against the **binary handler**, not the rendered output. The 0x21 case is the canonical lesson: it was mapped to `DynamicArrayIteratorToken` because the rendered text said `foreach`, but the binary handler at `GNatives[0x21]` reads only 1 sub-expression — the `foreach` was a *consequence* of the wrong mapping, not the evidence for it. Use `mcp__ida-pro-mcp__decompile` on `GNatives[byte * 8] -> handler_addr` and read the actual sub-expression count + payload reads.

## Pitfalls — read before mapping a new byte

### Tautological mapping (the 0x21 trap)
A byte mapping is *tautological* when it's chosen because the rendered output looks plausible, not because the binary handler actually has that wire format. The 0x21 case: rendered as `foreach` because mapped to `DynamicArrayIteratorToken`, which made the rendered text say `foreach`, which "verified" the mapping. The binary handler at `GNatives[0x21]` reads only **one** sub-expression — incompatible with DynArrayIterator's 4-sub + byte + u16 layout. Always derive the wire format from `mcp__ida-pro-mcp__decompile` of the handler, never from the rendered output.

### Position vs Storage scaling
Cooked RL bytecode stores object pointers as 4-byte indices on disk but expands them to 8-byte pointers in memory. The parser tracks both `Position` (in-memory, after expansion) and `StoragePosition` (on-disk, before expansion). They diverge as the parser encounters object/property reads.

JumpIfNot's u16 `CodeOffset` is read from the on-disk u16 but interpreted as an in-memory `Position`. For functions with deep object-laden conditions, this can place the if-body's `NestEnd` offset *before* the body's actual content (visible as `if(...) {} return X;` empty-body patterns where the `return` is the actual if-body). Don't try to fix this with token-map remaps — the bytes are parsed correctly; the issue is the offset interpretation.

### NRE recovery is load-bearing
The central deserialize loop in `ByteCodeDecompiler.cs` catches per-token exceptions and resyncs `ScriptPosition` to the buffer cursor (or +1 to guarantee progress). Many existing tests assume this catches drift from NTL mismatches, stale import-table indices, etc. **Don't add a `break` in the catch branch** — that was the original behavior and it killed parsing of every function that hit a recoverable error.

### Byte counts, not byte values
When picking a token class for a newly-RE'd byte, the binary's wire format defines the byte count and structure. UE token classes are interchangeable as long as their `Deserialize` methods consume the same bytes. Two different bytes with identical wire formats can map to the same token class — the dispatch lookup is by byte, the `Deserialize` is by class.

### Variadic terminator (0x3E)
`EndFunctionParmsToken` at 0x3E is the variadic-call argument-list terminator. `FunctionToken.DeserializeCall` does an `is EndFunctionParmsToken` type check to end the parm loop. **Don't map any other byte to `EndFunctionParmsToken`** — doing so causes function calls to terminate early when that byte appears in the args. (Historical bug: 0x45 was wrongly mapped to `EndFunctionParmsToken`, and any function with a 0x45 byte in its args got truncated.)

### Decompiler/parse-recovery invariants

The bytecode parse and decompile pipeline has been hardened to recover from per-token failures (NTL drift, stale import-table indices, unmapped opcode bytes). When making further changes, keep these invariants:

- **`ByteCodeDecompiler.Deserialize`'s catch branch must keep advancing `ScriptPosition`.** Originally it `break`-ed on any per-token exception, killing parsing of the rest of the function. The current behavior re-syncs `ScriptPosition` to the buffer cursor (or +1 to guarantee progress) and continues. If you reintroduce a `break`, all NTL-related cascades will once again abort entire functions.
- **Do NOT make `Token.NextToken()` return a clamped/sentinel token when out of range — IT WILL HANG.** Many callers loop with patterns like `while (NextToken() is not Foo)` or `do { t = NextToken(); } while (string.IsNullOrEmpty(...))`. If `NextToken` keeps returning the same token without advancing, those loops spin forever. The correct pattern is bounds-check at each call site (see `DecompileParms` and `DecompileOperator` in `FunctionTokens.cs` for examples). `DecompileNext` *can* safely return `string.Empty` when out of range because string-concat callers continue cleanly.
- **`DecompileNests` must check `CurrentTokenIndex` against `DeserializedTokens.Count` before reading `CurrentToken`.** After parse-recovery, the cursor can walk past the list end. Without this guard, the entire decompile fails with "Failed to format nests!" trace dumps.
- **Tokens with `ReadObject<T>()` lookups should be defensive in RL forks** (e.g. `FinalFunctionTokenRL` catches the `Imports[]` `ArgumentOutOfRangeException`). The base classes can't be made defensive without breaking other UE3 forks. If you add a new RL token that reads object/property/function references, wrap the read in try/catch and fall through to `DeserializeCall(stream)` so the variadic body still consumes its bytes.
- **`NativeFunctionToken.Decompile` falls back when `NativeItem` is null** — don't remove that guard; NTL drift produces null `NativeItem` constantly and the unconditional `NativeItem.Type` access cascades into NRE chains.

## Current opcode-mapping state

Three documents under `UELib/src/Branch/UE3/RL/` capture the state and methodology:

- **`snapshots/GNATIVES_SNAPSHOT_v868.md`** — full per-byte handler-address table for the current build. **Read this first when porting to a new RL version.** It lists every primary opcode 0x00..0x6F with its binary handler address, observed wire format, and current token mapping. The "Cross-version comparison procedure" section at the bottom describes how to recompute the byte→token map for a new build by matching handler addresses (which represent fixed semantics) rather than re-reverse-engineering each handler.
- **`RL_OPCODE_ANALYSIS.md`** — working note with session-by-session changelog, what was tried, what worked, decompile-side improvements, and remaining issues. Read this for context on *why* a mapping is what it is.
- **`snapshots/verified_handlers.md`** — historical per-byte verification trail (older format; the snapshot file supersedes it for cross-version use).

Read these before adding new token mappings.

## Opcode rotation across game versions

RL rotates its UnrealScript opcode permutation across patches — the same `EX_Let` semantics that lived at byte `0x4C` in v868 might live at `0x37` in v900. The token map in `EngineBranchRL.BuildTokenMap` is build-specific. When the user upgrades to a newer RL build:

1. **Dump the new GNatives table.** Find the new dispatcher base by searching for the `funcs_X[v3]` indexing pattern in `UStruct::SerializeExpr` (or grep for the "Unknown code token %02X" string and follow xrefs to the error handler — the error handler appears in many GNatives slots and anchors the table).
2. **Compare against the v868 snapshot** in `GNATIVES_SNAPSHOT_v868.md`. For each handler address from v868, find which byte in the new table points to that same handler (modulo ASLR). When a known handler appears at a new byte index, that byte rotated.
3. **Update `BuildTokenMap`** to the new bytes for the same handler→token mapping.
4. **Cross-check the parser function.** GNatives gives runtime semantics; the on-disk parser gives storage-side wire format (4-byte index reads, optional debug-info skips, JumpIfNot CodeOffset interpretation). For most rotated bytes the parser's structure follows GNatives, but verify byte-by-byte that the parser reads the same token-shape modulo expansion — a token whose `Deserialize` reads the wrong number of storage bytes will silently desync `ScriptPosition` and corrupt every subsequent token. See "Binary RE methodology" step 4 for what to watch for.
5. **Verify with the sentinel function set** (see "Testing workflow" above). If any baseline function regresses, the mapping is wrong somewhere.

The handler-address table in `GNATIVES_SNAPSHOT_v868.md` is the **stable** view — the byte values are not. Anchors (unique runtime fingerprint strings) listed at the bottom of the snapshot file are the most reliable way to identify a specific handler in a fresh dump.

## Binary RE methodology

When investigating a byte that the decompiler outputs garbage for:

1. **Disassemble the function** showing the broken output via `mcp__uelib__disassemble_function`. Identify the suspect token's `opcode_byte` value.
2. **Look up the handler.** `addr = 0x7FF6CF2AA580 + opcode_byte * 8` in IDA. Read the qword there to get the handler function address.
3. **Decompile the handler.** `mcp__ida-pro-mcp__decompile(addr=handler)` shows what it reads from the script stream:
   - `funcs_X[v]` calls = sub-expression dispatches (each = 1 byte sub-opcode + variable payload).
   - `*Code` / `++Code` / `Code += N` patterns = direct byte reads (counted into the wire format).
   - 8-byte qword reads = either UObject*, UProperty*, UClass*, FName, or a raw 8-byte literal.
   - Calls to `sub_7FF6CD317F00` (FFrame::ReadVariableSize) = UField* + 1 byte type.
   - Sub-table dispatchers (`funcs_Y[v]` where Y != GNatives) = secondary lookups for primitive cast / dynarray method / etc.
4. **Cross-check the parse-time wire format.** GNatives is the *runtime* dispatcher — it operates on the in-memory bytecode after the parser already applied 4→8 object/property/function index expansion, optional debug-info skips, and other on-disk decoration. For most opcodes the two agree, but **they diverge for any byte that touches storage representation**, and getting that wrong silently breaks parsing. Always confirm against the on-disk parser for:
   - **Object/property/function index reads** — 8-byte qword in GNatives = 4-byte index + 4-byte expansion in storage. The token's `Deserialize` must read 4 from the stream and call `AlignObjectSize()` (which advances `Position` by 8 to track in-memory size).
   - **`EX_DebugInfo` / optional padding / alignment reads** — these usually live in the parser path only.
   - **JumpIfNot / Case / Jump `CodeOffset`** — read by the parser as u16, interpreted by the renderer as in-memory `Position`. The cooker may undercount, which manifests as `if(...)` braces in wrong places (recovery lives in `JumpTokens.cs` — case A: CodeOffset inside JumpIfNot's own bytes, case B: CodeOffset mid-body-token).

   Finding the real parser: it's a *separate* function from GNatives, with its own dispatch table whose indices line up with the on-disk byte values. **Don't trust the function referenced from the `Bad expr token %02x` string in `FScriptSerializer.cpp`** (`sub_7FF6CD38C840` in v868) — its opcode permutation does NOT match real bytecode (it expects `0x3E` for the variadic terminator while real bytecode uses `0x4C`), so its case numbers are misleading. Find the actual parser by following the `funcs_X[v3]` indexing pattern from "Opcode rotation" step 1 above into its containing function, then verify it reads from the script stream for the byte you're investigating.
5. **Match against baseline EX_** — compare to stock UE3 `SerializeExpr` cases (or to other RL handlers with the same shape).
6. **Pick / write a token class** that matches the wire format exactly — including `AlignObjectSize` / `AlignSize(N)` calls so `ScriptPosition` stays in lock-step with the stream. Map the byte in `BuildTokenMap`.
7. **Test.** Decompile a function that contains the byte and check the output. If the output is broken, **don't tweak the rendering to make it look right** — re-verify both the GNatives handler AND the parser. The 0x21 lesson applies: tautological mapping causes cascading errors.

## Solution layout

`UE Explorer.sln` contains:

| Project                                | Purpose                                                                           | TFM                                  |
| -------------------------------------- | --------------------------------------------------------------------------------- | ------------------------------------ |
| `UEExplorer` (`UE Explorer/`)          | WinForms GUI (the app the user runs). Uses AvalonEdit + WebView2.                 | `net48`, x86/x64                     |
| `Eliot.UELib` (`UELib/src/`)           | The actual deserializer/decompiler library. **Most work happens here.**           | `net48;netstandard2.0/2.1;net8;net9` |
| `Eliot.UELib.Test` (`UELib/Test/`)     | MSTest unit tests for the library.                                                | `net9.0`                             |
| `Eliot.UELib.Benchmark`                | BenchmarkDotNet perf tests for library.                                           | `net8/9`                             |
| `Eliot.UELib.MCP` (`UELib/MCP/`)       | Model Context Protocol stdio server wrapping the library — exposes 16 tools (load_package, list_classes, decompile_*, disassemble_function, etc.) so AI agents (Claude Code, Cursor, …) can inspect/decompile RL packages programmatically. See `UELib/MCP/README.md`. | `net9.0` |
| `Eliot.UELib.MCP.Test` (`UELib/MCP.Test/`) | MSTest tests for the MCP project. Reuses TestUC2/TestUC3 fixtures from `UELib/Test/upk/` (no RL fixture in-tree).      | `net9.0`                             |
| `Eliot.Extensions.ExecGenerator`       | Optional plugin that generates UnrealScript `exec` glue. Loaded by the GUI.       | `net48`                              |
| `Eliot.Extensions.NTLGenerator`        | Plugin that generates Native Tables List (`.NTL`) files.                          | `net48`                              |
| `Eliot.Utilities`                      | Small shared helpers (string ext, log manager) used by the GUI.                   | classlib                             |
| `Setup`                                | Visual Studio installer project (`.vdproj`) — only buildable in Visual Studio.    | —                                    |

`UEExplorer.csproj` and `Eliot.UELib.csproj` both unconditionally define the `ROCKETLEAGUE` preprocessor symbol in every Configuration/Platform combo, so RL-specific `#if ROCKETLEAGUE` blocks are always live in this fork.

## Common commands

The CI workflow (`.github/workflows/build.yml`) installs .NET 9 SDK on Windows and runs:

```pwsh
dotnet restore
dotnet build "UE Explorer" --no-restore -c Release -f net48
```

Other useful invocations (run from the repo root):

```pwsh
# Build just the library (multi-targets — pick one with -f if needed)
dotnet build UELib/src/Eliot.UELib.csproj -c Debug -f net48

# Run the unit tests (net9.0)
dotnet test UELib/Test/Eliot.UELib.Test.csproj -c Debug

# Run a single test
dotnet test UELib/Test/Eliot.UELib.Test.csproj --filter "FullyQualifiedName~UnrealPackageTests.TestClassTypeOverride"

# Launch the GUI after a Debug build
& "UE Explorer\bin\Debug\net48\UEExplorer.exe"

# Run the MCP test suite
dotnet test UELib/MCP.Test/Eliot.UELib.MCP.Test.csproj -c Debug

# Publish the MCP server as a self-contained single-file Windows binary (~71 MB)
# The output exe gets registered in Claude Code's settings.json — see UELib/MCP/README.md
dotnet publish UELib/MCP/Eliot.UELib.MCP.csproj `
    -c Release -r win-x64 `
    -p:PublishSingleFile=true --self-contained true `
    -o UELib/MCP/publish
```

The `Setup` installer project requires Visual Studio with the "Microsoft Visual Studio Installer Projects" extension and is **not** built by `dotnet build` — skip it for everyday work.

The GUI persists user state (config, recent files, logs) under `%AppData%\EliotVU\UE Explorer`; delete this folder if settings get corrupted while testing.

## Architecture

### Loading flow

`UnrealLoader.LoadPackage(path)` → constructs `UPackageStream` and `UnrealPackage` → `UnrealPackage.Deserialize` reads the summary, auto-detects the build (or honors `BuildTarget`), instantiates the matching `EngineBranch`, sets up the serializer + token factory, then reads the name/import/export tables. `UnrealPackage.InitializePackage()` then materializes `UObject` instances. Decompiled UnrealScript is produced lazily by `UStruct.UByteCodeDecompiler` walking the `DeserializedTokens` list.

### Engine branches (the extension point for per-game behavior)

Every Unreal-derived game family that diverges from stock UE3 gets a class deriving from `UELib.Branch.EngineBranch` (typically `DefaultEngineBranch` for UE1–3 games). The branch owns:

- **`BuildTokenMap`** — maps opcode bytes (`0x00`..`0x6F`) to `Token` types. RL completely rewrites this table (see `EngineBranchRL.BuildTokenMap`); base UE3 opcodes are *not* a valid assumption inside RL packages with `LicenseeVersion >= 32`.
- **`SetupTokenFactory`** — picks the cutoff between extended-native and first-native opcodes. RL passes `0x70`/`0x70` so anything ≥ `0x70` is treated as a native function index.
- A `Decoder` (`IBufferDecoder`) for at-rest encryption (e.g., Huxley).
- A `Serializer` (`IPackageSerializer`) for header/object-table serialization quirks.

A branch is bound to a game by tagging the `UnrealPackage.GameBuild.BuildName` enum entry (in `UELib/src/UnrealPackage.cs`) with `[Build(...)]` (version range) and `[BuildEngineBranch(typeof(EngineBranchXxx))]`. RL is registered as `BuildName.RocketLeague` with `[Build(867, 868, 9u, 32u)]`. Other UE3 forks live as siblings under `UELib/src/Branch/UE3/` (APB, Borderlands `Willow`, DD2, GIGANTIC, HUXLEY, MOH, R6, RSS, SA2, SFX).

### Tokens (the bytecode → text translation layer)

Every UnrealScript opcode is a class deriving from `UStruct.UByteCodeDecompiler.Token`. A token has two responsibilities:

1. **`Deserialize(IUnrealStream)`** — read the token's payload from the script stream. After every raw stream read, call `Decompiler.AlignSize(N)` so `Decompiler.ScriptPosition` stays in lock-step with the stream — getting this wrong shifts every subsequent token's reported size and breaks the hex viewer.
2. **`Decompile()`** — emit the textual UnrealScript. Most tokens recurse into their operands via `DecompileNext()` / `NextToken<T>()`, which walks the flattened token list left-to-right. Skip a sub-token deliberately with `SkipCurrentToken()`; insert a trailing `;` with `Decompiler.MarkSemicolon()`.

Patterns to follow when adding RL tokens (`UELib/src/Branch/UE3/RL/Tokens/`):

- For an opcode in the **primary** opcode table, derive from `Token` (or a closer base like `JumpIfNotToken`, `ContextToken`, `FinalFunctionToken`) and register it in `EngineBranchRL.BuildTokenMap`.
- For an opcode under the **extended-native** prefix (`0x10` and `0x5E`/`0x71` in RL), register it in `ExtendedNativeFunctionToken.s_extendedNativeFunctionTokenMap` instead — that token reads the second opcode byte and dispatches. Anything not in that map falls through to a native call with index `opCode + 5000`.
- For a **specific named native** that needs a follow-up token (e.g. `AllControllers`), add the function name + token type to `FinalFunctionTokenRL.FunctionTokenMap`. The base token deserializes normally, then the mapped token is deserialized in-line and inserted into `DeserializedTokens`.
- For a **variadic native call** that takes an array as receiver plus a varargs tail, derive from `NativeFunctionToken`, deserialize the array via `DeserializeNext()`, skip any inline padding (commonly 2 bytes — remember the matching `AlignSize`), then call `base.Deserialize(stream)` for the variadic tail.

When stuck, the closest analogues live in `UELib/src/Core/Tokens/` (the stock UE token implementations) and the other UE3 branch directories.

## Conventions

- `.editorconfig` is authoritative: 4-space indent, CRLF for generated files, `dotnet_style_namespace_match_folder = true` (so a token in `UELib/src/Branch/UE3/RL/Tokens/` belongs in namespace `UELib.Branch.UE3.RL.Tokens`).
- `Eliot.UELib` enables `Nullable` and `EnforceCodeStyleInBuild` — null annotations matter, and analyzer warnings will surface in PR builds.
- The codebase relies heavily on `partial class UStruct { partial class UByteCodeDecompiler { ... } }` nesting; tokens reference siblings by their full nested name (e.g. `UStruct.UByteCodeDecompiler.JumpIfNotToken`).
- Game-conditional code uses `#if <SYMBOL>` (e.g. `#if ROCKETLEAGUE`, `#if BIOSHOCK`). The full symbol set lives in `Eliot.UELib.csproj`'s root `<DefineConstants>`.
- Native Tables (`*.NTL`) under `UE Explorer/Native Tables/` are loaded at runtime by the GUI; new ones must be added to `UEExplorer.csproj` with `CopyToOutputDirectory=PreserveNewest`.
