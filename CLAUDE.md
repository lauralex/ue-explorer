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

Workflow that works:

1. Make code changes (any project — both `Eliot.UELib` and `Eliot.UELib.MCP` get bundled).
2. `dotnet publish UELib/MCP/Eliot.UELib.MCP.csproj -c Release -r win-x64 -p:PublishSingleFile=true --self-contained true -o UELib/MCP/publish_new` — publishes alongside `publish/` instead of overwriting it.
3. Ask the user to: stop Claude Code, copy `publish_new\Eliot.UELib.MCP.exe` over `publish\Eliot.UELib.MCP.exe` (the lock is released once Claude exits), restart Claude Code.
4. Reload the package in the new MCP session — the handle from before is invalid.

`publish_new/` is gitignored-by-convention (don't commit). It exists only so the active session's exe stays untouched until the user does the swap.

## Decompiler/parse-recovery invariants — pitfalls to avoid

The bytecode parse and decompile pipeline has been hardened to recover from per-token failures (NTL drift, stale import-table indices, unmapped opcode bytes). When making further changes, keep these invariants:

- **`ByteCodeDecompiler.Deserialize`'s catch branch must keep advancing `ScriptPosition`.** Originally it `break`-ed on any per-token exception, killing parsing of the rest of the function. The current behavior re-syncs `ScriptPosition` to the buffer cursor (or +1 to guarantee progress) and continues. If you reintroduce a `break`, all NTL-related cascades will once again abort entire functions.
- **Do NOT make `Token.NextToken()` return a clamped/sentinel token when out of range — IT WILL HANG.** Many callers loop with patterns like `while (NextToken() is not Foo)` or `do { t = NextToken(); } while (string.IsNullOrEmpty(...))`. If `NextToken` keeps returning the same token without advancing, those loops spin forever. The correct pattern is bounds-check at each call site (see `DecompileParms` and `DecompileOperator` in `FunctionTokens.cs` for examples). `DecompileNext` *can* safely return `string.Empty` when out of range because string-concat callers continue cleanly.
- **`DecompileNests` must check `CurrentTokenIndex` against `DeserializedTokens.Count` before reading `CurrentToken`.** After parse-recovery, the cursor can walk past the list end. Without this guard, the entire decompile fails with "Failed to format nests!" trace dumps.
- **Tokens with `ReadObject<T>()` lookups should be defensive in RL forks** (e.g. `FinalFunctionTokenRL` catches the `Imports[]` `ArgumentOutOfRangeException`). The base classes can't be made defensive without breaking other UE3 forks. If you add a new RL token that reads object/property/function references, wrap the read in try/catch and fall through to `DeserializeCall(stream)` so the variadic body still consumes its bytes.
- **`NativeFunctionToken.Decompile` falls back when `NativeItem` is null** — don't remove that guard; NTL drift produces null `NativeItem` constantly and the unconditional `NativeItem.Type` access cascades into NRE chains.

## Current opcode-mapping state

`RL_OPCODE_ANALYSIS.md` in `UELib/src/Branch/UE3/RL/` is the working note that captures (a) the IDA reverse-engineering trail (and why `sub_7FF6CD38C840` turned out to be `FScriptSerializer`, *not* the on-disk walker), (b) what's verified working on the current build, and (c) the gap list of unresolved primary opcode bytes (~14 lower-frequency bytes plus the suspect `0x45 = EndFunctionParms` map entry). Read it before adding new token mappings — it documents what was tried, what worked, and what to investigate next.

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
