# RL Decompiler Comparison Harness

Side-by-side baseline UE3 (`UnrealEngine3\Development\Src\...\*.uc`) vs current
RL decompile output. Use to identify highest-priority remaining gaps.

Last updated: after commit `84bb895` (BoolVariable at 0x06).

---

## Pawn.SpawnDefaultController

**Baseline** (`Engine\Classes\Pawn.uc:2258`):
```uc
function SpawnDefaultController()
{
    if (Controller != None)
    {
        `log("SpawnDefaultController" @ Self @ ", Controller != None" @ Controller);
        return;
    }

    if (ControllerClass != None)
    {
        Controller = Spawn(ControllerClass);
    }

    if (Controller != None)
    {
        Controller.Possess(Self, false);
    }
}
```

**Our decompile**:
```uc
final function SpawnDefaultController()
{
    if(Controller != none)
    {
        LogInternal("SpawnDefaultController" @ string(self) @ ", Controller != None" @ string(Controller));
    }
    if(ControllerClass != none)
    {
        Controller = Spawn(ControllerClass);
    }
    if(Controller != none)
    {
        Controller.Possess(self);
    }
}
```

**Diff**: NEAR PERFECT.
- ✓ all 3 if-blocks correct
- ✓ all operators (`!=`, `@`, `=`)
- ✓ Spawn(), Possess(), LogInternal()
- ⚠ `LogInternal(...)` vs `` `log(...) `` — RL uses LogInternal name, baseline uses log macro
- ⚠ `string(self)` vs `Self` — RL emits explicit cast (compiler-inserted)
- ⚠ Missing `return;` after first log (RL bytecode may not contain this — needs check)
- ⚠ Possess(self) missing `, false` second arg — RL bytecode may not emit default values

---

## Pawn.PostBeginPlay

**Baseline** (`Engine\Classes\Pawn.uc:2220`):
```uc
event PostBeginPlay()
{
    super.PostBeginPlay();

    SplashTime = 0;
    SpawnTime = WorldInfo.TimeSeconds;
    EyeHeight = BaseEyeHeight;

    if ( WorldInfo.bStartup && (Health > 0) && !bDontPossess )
    {
        SpawnDefaultController();
    }

    if( FacialAudioComp != None )
    {
        FacialAudioComp.OnAudioFinished = FaceFXAudioFinished;
    }

    if (Role == ROLE_Authority && InvManager == None && InventoryManagerClass != None)
    {
        InvManager = Spawn(InventoryManagerClass, Self);
        if ( InvManager == None )
            `log("Warning! Couldn't spawn InventoryManager" @ InventoryManagerClass @ "for" @ Self @ GetHumanReadableName() );
        else
            InvManager.SetupFor( Self );
    }

    //debug
    ClearPathStep();
}
```

**Our decompile**:
```uc
event PostBeginPlay()
{
    super.PostBeginPlay();
    EyeHeight = BaseEyeHeight;
    if(WorldInfo.bStartup && !bDontPossess)
    {
        SpawnDefaultController();
    }
    if(FacialAudioComp != none)
    {
        FacialAudioComp.__OnAudioFinished__Delegate = vect(0.0000000, 0.0000000, 0.0000000);
        ClearPathStep();
    }
    67109384
}
```

**Diff**: PARTIAL.
- ✓ super-call, EyeHeight assignment
- ✓ if(WorldInfo.bStartup && !bDontPossess) (Health > 0 missing — possibly stripped or RL specific)
- ✓ SpawnDefaultController() call inside the if
- ✓ if(FacialAudioComp != none) block
- ✗ `SplashTime = 0;` and `SpawnTime = WorldInfo.TimeSeconds;` — TWO MISSING STATEMENTS at the top
- ✗ `FacialAudioComp.OnAudioFinished = FaceFXAudioFinished` rendered as `... = vect(0,0,0)` — wrong RHS
- ✗ Entire `if (Role == ROLE_Authority && InvManager == None ...)` block missing
- ✗ `ClearPathStep()` should be at function tail (outside the if), not inside the FacialAudioComp if
- ✗ Trailing orphan `67109384` (an int literal with no statement context)

The missing statements suggest some byte mappings are still wrong AND/OR the
parser's variadic body is consuming bytes that should be subsequent statements.

---

## Pawn.SetMovementPhysics

**Baseline** (`Engine\Classes\Pawn.uc:2361`):
```uc
function SetMovementPhysics()
{
    if (PhysicsVolume.bWaterVolume) {
        SetPhysics(PHYS_Swimming);
    }
    else if (Physics != PHYS_Falling) {
        SetPhysics(PHYS_Falling);
    }
}
```

**Our decompile**:
```uc
final function SetMovementPhysics()
{
    if(int(Physics) != int(2))
    {
    }
    ==
}
```

**Diff**: PARTIAL — structure recognized but bodies missing.
- ✓ `if(...)` recognized
- ✓ `Physics != 2` (PHYS_Falling = 2 enum, RL emits as int literal)
- ✗ `PhysicsVolume.bWaterVolume` check entirely missing (the FIRST if)
- ✗ Both `SetPhysics(...)` calls missing
- ✗ `else if` chaining not reconstructed
- ✗ Trailing `==` orphan operator

---

## Actor.Destroyed

**Baseline** (`Engine\Classes\Actor.uc`): empty event, no body.

**Our decompile**: `event Destroyed();`

**Diff**: PERFECT.

---

## RotatorConversions.GetAsDegrees (Core)

**Expected pattern** (typical):
```uc
NormalizedRotator = Normalize(InRotator);
result.Pitch = NormalizedRotator.Pitch * 0.0054932;
result.Yaw = NormalizedRotator.Yaw * 0.0054932;
result.Roll = NormalizedRotator.Roll * 0.0054932;
return result;
```

**Our decompile**:
```uc
NormalizedRotator = ;
default.@NULL += StructInitializer_0x1;
float(NormalizedRotator.Pitch)
0.0054932
StructInitializer_0x1.Yaw = float(NormalizedRotator.Yaw) * 0.0054932;
StructInitializer_0x1.Roll = float(NormalizedRotator.Roll) * 0.0054932;
StructInitializer_0x1
ReturnValue
==
```

**Diff**: PARTIAL — final 2 statements work, beginning + return broken.
- ✓ `.Yaw = ... * 0.0054932` (struct member assignment with operator)
- ✓ `.Roll = ... * 0.0054932`
- ✓ `float(...)` cast
- ✗ `NormalizedRotator = Normalize(InRotator)` — missing RHS (Normalize() call)
- ✗ `.Pitch` assignment broken (rendered as orphan tokens)
- ✗ `return result;` reconstructed as `StructInitializer_0x1` + `ReturnValue` + `==` orphans
- ⚠ `default.@NULL +=` — DefaultVariable + LetBool/Let with `+=` operator; weird first statement

---

## Patterns of remaining bugs

1. **First statement of function** sometimes wrong (NormalizedRotator =, EyeHeight before SplashTime)
   — May be a LetToken parsing initial sub-tokens incorrectly
2. **Return statement** consistently broken (orphans `Variable + ReturnValue + ==`)
   — Unmapped Return opcode; baseline EX_Return = 0x04 but RL byte 0x04 is in defaults
3. **`else if` not reconstructed** — Switch/Case nests incomplete
4. **Some assignments missing entirely** — bytes consumed by previous variadic
5. **Trailing orphan literals** (`67109384`, `==`) — function-tail debug-info or NestEnd misalignment

## Remaining bytes flagged for follow-up

| Byte | Current | Binary fingerprint | Investigation lead |
|------|---------|--------------------|--------------------|
| 0x06 | BoolVariable ✓ | wrapper + bit-mask test | done |
| 0x07 | ReturnNothing | 14b, calls fn ptr | maybe unused, fine |
| 0x09 | DefaultVariable | 1-sub-expr + 0x20-prefix | unverified |
| 0x10 | Nothing | 324b, "Execution beyond end" | sentinel, fine as Nothing |
| 0x12 | EatReturnValue | variadic + final dispatch | likely fused FinalFn variant |
| 0x20 | ReturnToken | DebugInfo handler (peek 100 marker) | should be DebugInfo? |
| 0x21 | DynArrayIterator | 1-sub-expr wrapper | unverified |
| 0x32 | VectorConst | 8-byte read + complex | property accessor variant |
| 0x36 | VectorConst | 8 bytes + sub-opcode | possibly DynArrayLength |
| 0x4D | LocalVariable | UStruct* + struct alloc | possibly EatReturnValue/StructDup |
| 0x53 | LocalVariable | 414b complex | unverified |
| 0x69 | DelegateCmpNe | uses (a2+24) Object | likely state-related |

## Suggested validation methodology

1. Pick a known-source function (from `Engine\Classes\*.uc` baseline)
2. Decompile via MCP and diff
3. Identify mismatched bytes via disassemble_function output
4. Cross-check binary handler in IDA → identify proper EX_
5. Apply mapping change → rebuild → verify the diff improves

This document should be updated each session with new test functions.
