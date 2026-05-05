using System;
using UELib.Branch.UE3.RL.Tokens;
using UELib.Core.Tokens;
using UELib.Tokens;
using static UELib.Core.UStruct.UByteCodeDecompiler;

namespace UELib.Branch.UE3.RL
{
    public class EngineBranchRL : DefaultEngineBranch
    {
        [Flags]
        public enum FunctionFlagsRL : ulong
        {
            Constructor = 0x400000000,
        }
        
        public EngineBranchRL(BuildGeneration generation) : base(BuildGeneration.UE3)
        {
        }

        protected override TokenMap BuildTokenMap(UnrealPackage linker)
        {
            if (linker.LicenseeVersion < 32)
            {
                return base.BuildTokenMap(linker);
            }

            var tokenMap = new TokenMap((byte)ExprToken.ExtendedNative + 0x30)
            {
                // 0x00: context-aware EX_Return / alignment-padding (RL-specific token).
                // 0x00 binary handler = default error handler ("Unknown code token"). In real RL
                // bytecode the byte appears in two unrelated roles:
                //   (a) top-level: immediately before a return-value expression
                //       (`0x00 0x2F` = `return true;`, `0x00 0x1C` = `return 0;`).
                //       Looks like baseline EX_Return.
                //   (b) inside a variadic native-call argument list: alignment padding
                //       between the last real arg and EmptyParm/EndFunctionParms.
                //       Must NOT consume the EmptyParm or LogInternal-style calls scramble
                //       into `LogInternal(..., return return return)`.
                // ContextAwareReturnTokenRL checks Decompiler.VariadicCallDepth (set by
                // FunctionToken.DeserializeCall around its parm loop) and renders as
                // EX_Return at top level / NothingToken inside a call.
                { 0x00, typeof(ContextAwareReturnTokenRL) },
                // 0x01: VERIFIED 1-sub-expression wrapper (parser case 0x01 → LABEL_70).
                // UStruct::SerializeExpr (sub_7FF6CD38C840) groups 0x01 with 0x06 / 0x66
                // (both BoolVariableToken), 0x52 (EatReturnValue), 0x5C (GotoLabel) — all
                // 1-sub passthroughs. GNatives runtime handler at 0x7FF6CD2F0FB0 reads 2
                // sub-opcodes (Let-shape) but the parser is authoritative for decompilation
                // (per CLAUDE.md validation discipline; precedent: byte 0x2C ternary).
                // Was StateVariableToken (8-byte FName read) — over-consumed and triggered
                // the parse-recovery seek bug now fixed in ByteCodeDecompiler. Visible as
                // the orphan `@NULL` token in Ball_TA.Explode at storage 521. Mapping to
                // BoolVariableToken matches the parser grouping: read 1 sub, render the sub.
                { 0x01, typeof(BoolVariableToken) },
                { 0x02, typeof(IntConstToken) },
                // 0x03: VERIFIED unmapped (binary handler = default error). Was wrongly
                // StructCmpEqToken (which reads 8 bytes UObject* + 2 sub-exprs — way too much
                // consumption for an unmapped opcode). Real EX_StructCmpEq is at 0x53.
                { 0x03, typeof(NothingToken) },
                { 0x04, typeof(EndOfScriptToken) },
                // 0x05: VERIFIED ArrayElement (or DynamicArrayElement — shared handler with 0x16).
                // GNatives[0x05] = sub_7FF6CD2F6800 dispatches 2 sub-opcodes (Index + Base) and
                // does the optional 0x20 debug-info handling — that's the canonical EX_ArrayElement
                // pattern. 0x05 and 0x16 share this same handler in the binary; mapping 0x05 to
                // ArrayElement and 0x16 to DynamicArrayElement (which inherits from ArrayElement
                // in UELib so they decompile identically with the right semantics).
                { 0x05, typeof(ArrayElementToken) },
                // 0x06: VERIFIED 1-sub-expr wrapper (NOT NameConst — that's at 0x39).
                // GNatives[0x06] = sub_7FF6CD2F0020 reads 1 byte (sub-opcode), advances Code,
                // peeks the next 8 bytes (without advancing) into a global, then dispatches the
                // sub-opcode. The 8-byte peek captures the first qword of the sub-opcode's body
                // (typically the UProperty* read by an InstanceVariable / LocalVariable
                // sub-token), used by the runtime for null-check + flag-bit testing
                // (`(*(v7+200) & *(qword_..._D7B0)) != 0`). That's the EX_BoolVariable runtime
                // pattern — wraps a property access and tests the bool storage's bit mask.
                // Was wrongly NameConstToken (which reads 8 bytes), causing `WorldInfo.''`
                // patterns where the inner property name didn't resolve.
                { 0x06, typeof(BoolVariableToken) },
                { 0x07, typeof(ReturnNothingToken) },
                // 0x08: empirically a 1-sub-token wrapper (best fit; EatReturnValue shape).
                // Tied with InterfaceCast/DynamicCast/MetaClassCast/ObjectConst — all 1-sub
                // shapes in baseline UE3. Picked EatReturnValue as the simplest leaf-of-leaf.
                { 0x08, typeof(EatReturnValueToken) },
                // 0x09: VERIFIED comma-operator-style wrapper (NOT an assignment).
                // GNatives[0x09] = sub_7FF6CD2F5930 evaluates sub-A for side-effects then
                // evaluates sub-B and returns its value: equivalent to `(A, B)` where the
                // whole expression's value is B. Cooker emits this around for-loop init
                // expressions like `for (Index = 0; ...; ...)` — sub-A is the actual
                // assignment, sub-B is the variable being read for the "expression value"
                // the for-loop notation requires. Mapping to LetToken rendered the bytes as
                // `Index = false = Index` (nested Let). DiscardKeepTokenRL renders just sub-A,
                // recovering the source-level `Index = 0;`.
                { 0x09, typeof(DiscardKeepTokenRL) },
                // 0x0A: VERIFIED unmapped (binary handler = default error). Was wrongly
                // DelegateCmpEqToken (4-byte over-consume) which produced spurious ` == `
                // operators in expression chains. Real EX_StructCmpEq is at 0x53; the
                // delegate compares are native operators.
                { 0x0A, typeof(NothingToken) },
                // 0x0B: VERIFIED IntConst (reads INT, NOT DynamicArrayElement).
                // GNatives[0x0B] = sub_7FF6CD2F6DC0 reads 4-byte INT and writes to *a3.
                { 0x0B, typeof(IntConstToken) },
                { 0x0C, typeof(EventSubscribeToken) },
                // 0x0D: VERIFIED unmapped (binary handler = default error). Was wrongly
                // DebugInfoToken (13-byte over-consume — silently swallowed adjacent tokens
                // when this byte appeared in real bytecode).
                { 0x0D, typeof(NothingToken) },
                // 0x0E: VERIFIED EX_StructCmpEq (NOT IteratorNext).
                // GNatives[0x0E] = sub_7FF6CD2F63E0 reads 8-byte UStruct* + 2 sub-exprs and
                // calls sub_7FF6CD658AC0(struct, lhs_buf, rhs_buf, 0) — same struct-compare
                // helper as 0x53, with the 4th arg = 0 indicating EQUALITY. Was wrongly
                // IteratorNextToken (a 1-byte continue marker — under-consumed and produced
                // spurious `continue` keywords).
                { 0x0E, typeof(StructCmpEqToken) },
                { 0x0F, typeof(FinalFunctionTokenRL) }, // new-build replacement for 0x38 super-call shape; FinalFunctionTokenRL now reads the mandatory skip byte after UFunction*; see RL_OPCODE_ANALYSIS.md
                // 0x10: was ExtendedNativeFunctionToken which read sub_byte and produced
                // __NFUN_(sub+5000)__ placeholders. Binary RE: byte 0x10 dispatches to the
                // "Execution beyond end of script" warning printer, NOT a chained native
                // dispatcher. The natives at +5000 don't exist in GNatives (which spans only
                // 0..4415). Mapping to NothingToken eliminates the ghost native call sites with
                // no parse regression. Real extended natives use byte 0x71 (handled below).
                { 0x10, typeof(NothingToken) },
                // 0x11: VERIFIED LocalOutVariable (out-parameter access via OutParms list).
                // GNatives[0x11] = sub_7FF6CD2ED370 reads 8-byte qword (UProperty* or FName),
                // then loops walking `(a2+72)` (FFrame::OutParms — the linked list of out-by-ref
                // parameters) until matching entry found. That's the canonical EX_LocalOutVariable
                // runtime — out-params live in their own frame separate from Locals/Instance.
                // Empirically appears for `const out` / `out` parameter accesses in PRI_TA.SetLoadouts
                // (the `Loadouts` and `LoadoutAttributes` parameters). Originally guessed
                // StateVariable based on linked-list-walk pattern, but OutParms is a more
                // accurate match.
                { 0x11, typeof(OutVariableToken) },
                { 0x12, typeof(EatReturnValueToken) },
                { 0x13, typeof(NoObjectToken) },
                { 0x14, typeof(DynamicArrayLengthToken) },
                { 0x15, typeof(InterfaceContextToken) },
                // 0x16: VERIFIED DynamicArrayElement (sister to 0x05 ArrayElement — same handler).
                // GNatives[0x16] = sub_7FF6CD2F6800 (same address as 0x05). The two opcodes share
                // a runtime dispatch path because the array-element access logic is identical
                // for static and dynamic arrays at this level.
                { 0x16, typeof(DynamicArrayElementToken) },
                // 0x17: VERIFIED 2-sub-expr delegate-access (NOT DelegatePropertyToken).
                // GNatives[0x17] = sub_7FF6CD2F1580 dispatches 2 sub-expressions then walks
                // the receiver's delegate-list at +16 to locate matching entries. Wire
                // format = 2 sub-exprs. New token DelegateAccessTokenRL renders as
                // `{Receiver}.{Function}`. Was wrongly DelegatePropertyToken (8-byte FName +
                // 1 sub) which NRE'd in Car_TA.HandleTeamChanged producing ` += ; self`
                // orphans inside the EventSubscribe LHS.
                { 0x17, typeof(DelegateAccessTokenRL) },
                // 0x18: VERIFIED unmapped (binary handler = default error). Was wrongly
                // ConditionalToken (3 sub-exprs + 2 u16s — heavily over-consumed when this
                // byte appeared in real bytecode, cascading into garbled if/while bodies).
                { 0x18, typeof(NothingToken) },
                // 0x19: VERIFIED dynarray-method sub-dispatcher (NOT InterfaceCast).
                // GNatives[0x19] = sub_7FF6CD2F1750 reads 1 byte, dispatches into a
                // sub-table at funcs_7FF6CD2F176D (= 0x7FF6CF2B2D80). Sub-table entries
                // include DynArrayElement (sub_7FF6CD2ED7E0, "Accessed array out of bounds")
                // and DynArrayLength (sub_7FF6CD2EDE20). This is THE chained-native prefix
                // for dynamic-array methods in RL — same role as the existing
                // ExtendedNativeFunctionToken (which uses the s_extendedNativeFunctionTokenMap
                // for dispatch). Was wrongly InterfaceCastToken — every occurrence rendered
                // as `/* unresolved cast */()` and NRE'd, polluting many functions including
                // Ball_TA.EnableOwnerTranslucency, Actor.FindEventsOfClass, GameInfo.FindPlayerStart.
                { 0x19, typeof(Tokens.ExtendedNativeFunctionToken) },
                // 0x1A: VERIFIED EX_DynamicCast (NOT InstanceVariable).
                // GNatives[0x1A] = sub_7FF6CD2F70C0 reads 8-byte UClass* + 1 sub-expression
                // (the value to cast), zeroes the result slot if the target class has the
                // appropriate cast-flag (0x4000). Real EX_InstanceVariable is at 0x55.
                // Was wrongly InstanceVariableToken — every cast `Class(value)` rendered as
                // a plain class-name access, breaking patterns like `SpecialPickup_Targeted_TA(NewPickup)`
                // in AIController_Soccar_TA.HandleNewPickup which appeared as
                // `SpecialPickup_Targeted_TA != NewPickup` (just the class name compared to
                // the value, with the cast operation lost).
                { 0x1A, typeof(DynamicCastToken) },
                // 0x1B: VERIFIED variadic body until 0x3E + optional debug — same parser case
                // as 0x05/0x16/0x54 in UStruct::SerializeExpr (sub_7FF6CD38C840). Cooker emits
                // 2 sub-exprs + 0x3E terminator (matches ArrayElement layout). Runtime handler
                // sub_7FF6CD2F6AE0 dispatches both subs then writes 0 to result — RL no-op
                // / debug-discard variant. Was MetaClassCastToken (UClass + 1 sub) — wire
                // format mismatch caused 8-byte over-read whenever this byte appeared.
                { 0x1B, typeof(DynamicArrayElementToken) },
                // 0x1C: VERIFIED IntZero/False (writes 4-byte 0).
                // GNatives[0x1C] = sub_7FF6CD2F7030 (8-byte function): `*(_DWORD*)a3 = 0;`.
                // Aliased with 0x27 (same handler). Was wrongly mapped to RotationConst.
                { 0x1C, typeof(IntZeroToken) },
                // 0x1D: VERIFIED Nothing (empty stub).
                // GNatives[0x1D] = AK::MemoryMgr::StartProfileThreadUsage (3-byte empty function,
                // IDA mis-named). Aliased with 0x2E to the same empty stub. Was wrongly mapped
                // to BoolVariable (which reads a sub-expression).
                { 0x1D, typeof(NothingToken) },
                // 0x1E: VERIFIED ArrayElement (Index + Base sub-exprs + bounds-check).
                // GNatives[0x1E] = sub_7FF6CD2ED550 dispatches 2 sub-opcodes (the Index and Base
                // expressions), then bounds-checks with unique error string
                // "Accessed array '%s.%s' out of bounds (%i/%i)" — that's the canonical
                // EX_ArrayElement / EX_DynArrayElement runtime. Empirically appears for static
                // array indexing patterns like `FullLoadouts[Index] = Loadouts[Index]`.
                // Was wrongly NothingToken (1-byte leaf) — every array index was getting
                // dropped, leaving `<base> <index>` as separate tokens with no `[]` syntax.
                { 0x1E, typeof(ArrayElementToken) },
                // 0x1F: VERIFIED Self (writes `this` to result).
                // GNatives[0x1F] = sub_7FF6CD2F5C60 (4-byte function): `*a3 = a1;` — pushes
                // `this` to result, the canonical EX_Self runtime behavior. Was wrongly mapped
                // to LetBool (which reads two sub-expressions).
                { 0x1F, typeof(SelfToken) },
                // 0x20: VERIFIED DebugInfo (HANDLE_OPTIONAL_DEBUG_INFO macro).
                // GNatives[0x20] = sub_7FF6CD3027A0 reads 4-byte Version (mov eax, [r8]), and
                // if value == 100 reads 4-byte Line, 4-byte TextPos, 1-byte OpCode (13 bytes
                // payload after the op). If Version != 100 the handler backs up to before the
                // 0x20 byte (lea rax, [r8-1]) — the conditional-consume pattern of the
                // HANDLE_OPTIONAL_DEBUG_INFO macro called by other handlers (0x2C peeks 0x20
                // after dispatching its sub-expr). Treating real-bytecode 0x20 occurrences as
                // EX_Return (which reads only 1 op + sub) was leaving 11+ bytes of debug
                // payload to be re-dispatched as garbage tokens — visible in
                // Ball_TA.IsGroundHit's trailing `Class'...'.default.GroundToleranceZ` orphan
                // and `return HitNormal.Z > ToleranceZ` over-read past the function end.
                { 0x20, typeof(DebugInfoToken) },
                // 0x21: VERIFIED 1-sub-expr property-to-string cast (NOT DynArrayIterator).
                // GNatives[0x21] = sub_7FF6CD2F5A40 reads ONE sub-expression then calls a
                // property-export helper (sub_7FF6CD3192F0) with the property class pointer
                // (qword_7FF6CF27D780 + 200 = UProperty::PropertyClass) and value pointer
                // (qword_7FF6CF27D7B0). Result is FString-shape; matches `string(propRef)`.
                // The real foreach is dispatched via the extended-native prefix:
                //   0x10 0x0A → DynamicArrayIteratorRL (foreach arr(item))
                //   0x71 0x39 → IteratorTokenRL (foreach AllControllers etc.)
                // The previous "DynamicArrayIteratorToken" mapping here was tautological —
                // the rendered `foreach` came from the (wrong) token map, not from the
                // binary handler. Result: Actor.FindEventsOfClass rendered
                // `foreach @NULL(...) {}` with empty body and orphan `.Length;` after.
                { 0x21, typeof(StringCastTokenRL) },
                // 0x22: VERIFIED Jump (unconditional, reads u16 offset, runtime jumps).
                // GNatives[0x22] = sub_7FF6CD2F0610 reads 2 bytes (v3 — code offset), then sets
                // `Code = ScriptStart + v3` (absolute jump). Wire format = 2 bytes only,
                // matching baseline EX_Jump exactly. DelegateFunction is correctly at 0x40
                // (1 byte + UProperty* + FName).
                // Note: 0x5D also consumes 2 bytes but its runtime handler is `Code += 2`
                // (just advances past the bytes without jumping) — likely the
                // EX_JumpIfFilterEditorOnly opcode that the cooker emits as a no-op when
                // not in editor. Both bytes parse to a 2-byte reader; mapping both to
                // JumpToken keeps parsing aligned.
                { 0x22, typeof(JumpToken) },
                // 0x23: VERIFIED NoObject (writes 8-byte 0).
                // GNatives[0x23] = sub_7FF6CD2F7050 (8-byte function): `*(_QWORD*)a3 = 0;` —
                // 8-byte zero write, the EX_NoObject pattern. Was wrongly mapped to New
                // (which reads four sub-expressions).
                { 0x23, typeof(NoObjectToken) },
                // 0x24: VERIFIED unmapped (binary handler = default error). Was wrongly
                // DeprecatedTokenRL which over-consumed bytes per occurrence.
                { 0x24, typeof(NothingToken) },
                // 0x25: VERIFIED Case (switch case marker — u16 jump target + sub-expr value).
                // GNatives[0x25] = sub_7FF6CD2F0590 reads u16; if not 0xFFFF dispatches one
                // sub-expression (the case-value expression that the runtime compares against
                // the switch value). Wire format is identical to baseline EX_Case.
                // Distinct from 0x31 (sub_7FF6CD2ED4A0) which has a variadic-body wire format
                // gated on a runtime flag — that's the actual optional-arg / default-parameter
                // pattern and stays mapped to OptionalArgSkipTokenRL.
                // Was wrongly OptionalArgSkipTokenRL — every switch case rendered as empty,
                // collapsing the switch body into a goto/if tangle. Visible in
                // AntiCheatMessenger_TA.ReplicatedEvent where the `name VarName` switch
                // dispatched cases as 1-statement bodies that wandered into goto J0x69 chains.
                { 0x25, typeof(CaseToken) },
                // 0x26: VERIFIED unmapped (binary handler = default error). Was wrongly
                // EndParmValueToken which is a 1-byte leaf — safe but its presence in the
                // parser falsely triggers `EX_EmptyParmValue` rendering as a comma.
                { 0x26, typeof(NothingToken) },
                // 0x27: VERIFIED 4-byte-zero leaf (aliased with 0x1C — same runtime handler).
                // Picking IntZeroToken (renders "0") rather than FalseToken (renders "false")
                // because empirically the cooker emits 0x27 for int-zero contexts (for-loop
                // init `Index = 0`, etc.). Rendering as "false" produced invalid UnrealScript
                // like `Index = false`. Real bool `false` constants come through 0x1C now.
                { 0x27, typeof(IntZeroToken) },
                // 0x28: VERIFIED Context (object.member access).
                // GNatives[0x28] = sub_7FF6CD2F5CB0 reads {1 byte, sub-expr (object), 2 bytes
                // null-skip, UField* + property type via sub_7FF6CD317F00, sub-expr (context)} —
                // exactly matches ContextToken.Deserialize in UELib (which already handles RL's
                // version >= 588 path with the embedded property pointer). Has unique runtime
                // error string "Accessed None '%s'". Real LocalVariable is at 0x65 (Locals-frame
                // accessor); was wrongly mapped to LocalVariable here as a high-frequency tie.
                { 0x28, typeof(ContextToken) },
                // 0x29: VERIFIED JumpIfNot-shape (2-byte offset + 1 sub-expr).
                // GNatives[0x29] = sub_7FF6CD2F0630 reads u16 (offset), reads byte (sub-opcode),
                // dispatches sub-opcode, then `Code = ScriptStart + offset`. Wire format matches
                // baseline EX_JumpIfNot exactly. Was wrongly DynamicArraySortToken — caused
                // spurious `.Sort()` in places like Pawn.PostBeginPlay where there should
                // have been an `if (...)` block. The sub-expr is the boolean condition; the
                // 2-byte offset is the absolute target if the condition is false (jump forward
                // past the if-body). NestManager will fold this into `if (...) { ... }`.
                { 0x29, typeof(JumpIfNotToken) },
                // 0x2A: VERIFIED ByteConst (reads 1 byte).
                // GNatives[0x2A] = sub_7FF6CD2F70A0 (18-byte function): reads u8 from Code,
                // writes to *a3. Was wrongly mapped to IntOne.
                { 0x2A, typeof(ByteConstToken) },
                // 0x2B: VERIFIED IntConstByte (reads 1 byte, distinct from ByteConst on 0x2A).
                // GNatives[0x2B] = sub_7FF6CD2F7010 (18-byte function): reads i8 (signed) from
                // Code, writes to *a3 as char. The signed-vs-unsigned distinction relative to
                // 0x2A's u8 read suggests this is IntConstByte. Was wrongly mapped to
                // VectorConst (which reads 12 bytes).
                { 0x2B, typeof(IntConstByteToken) },
                // 0x2C: VERIFIED EX_Conditional (3 sub-exprs + 2 u16 skip offsets).
                // Stock UE3 had EX_Conditional at byte 0x45; v868 RL rotated it to 0x2C.
                // Verified by reading UStruct::SerializeExpr (sub_7FF6CD38C840) case 44:
                //   call qword ptr [rax]              ; recursive SerializeExpr (cond)
                //   call FArchive_SerializeWord       ; u16 SkipTrue
                //   call qword ptr [rax]              ; true-expr
                //   call FArchive_SerializeWord       ; u16 SkipFalse
                //   call qword ptr [rax]              ; false-expr
                // The runtime GNatives[0x2C] handler at sub_7FF6CD308010 reads only
                // "1 sub + 1 byte + optional 0x20" — a different shape — but the
                // parse-time wire format from UStruct::SerializeExpr is authoritative
                // for decompilation since the cooker emits the parse-time format.
                // ConditionalToken (UELib/src/Core/Tokens/LetTokens.cs) already
                // implements stock UE3's wire format and renders as "((cond) ? a : b)".
                { 0x2C, typeof(ConditionalToken) },
                // 0x2D: VERIFIED 3-sub-expr assert-shape (NOT EventUnsubscribe).
                // GNatives[0x2D] = sub_7FF6CD2F06A0 reads u16 + byte + 3 sub-exprs and
                // logs "Assertion failed, line %i" via the debugger predicate. New token
                // AssertExpressionTokenRL renders as `assert(arg0, arg1, arg2)`. The
                // previous EventUnsubscribe mapping (2 sub-exprs, no u16+byte preamble)
                // mismatched the wire format and NRE'd on every occurrence — producing
                // ` -= ` orphan operators inside if-conditions in Ball_TA.PostBeginPlay
                // and similar functions.
                { 0x2D, typeof(AssertExpressionTokenRL) },
                // 0x2E: VERIFIED Nothing (empty stub, aliased to 0x1D).
                // Same handler address as 0x1D — empty function. Was wrongly mapped to Case
                // (which reads a 2-byte WORD + optional sub-expression).
                { 0x2E, typeof(NothingToken) },
                // 0x2F: VERIFIED IntOne/True (writes 4-byte 1).
                // GNatives[0x2F] = sub_7FF6CD2F7040 (8-byte function): `*(_DWORD*)a3 = 1;`.
                // Aliased with 0x3A (same handler). Was wrongly mapped to GotoLabel.
                { 0x2F, typeof(IntOneToken) },
                // 0x30: VERIFIED VectorConst-shape (reads 12 bytes = 3 INTs).
                // GNatives[0x30] = sub_7FF6CD2F9C50 reads three consecutive 4-byte ints into
                // a3[0], a3[1], a3[2]. Was wrongly NativeParameterToken (which has different
                // wire format).
                { 0x30, typeof(VectorConstToken) },
                // 0x31: VERIFIED u16 + conditional sub-expression (NOT InstanceDelegate).
                // GNatives[0x31] = sub_7FF6CD2F0590 reads a u16; if it's 0xFFFF the handler
                // returns without further bytes (skipped/optional), otherwise dispatches a
                // single sub-expression. Pattern matches EX_Skip — used for omitted optional
                // positional args at call sites. Was wrongly InstanceDelegateToken (which
                // reads 8-byte UObject* + 8-byte FName = 16 bytes) — every occurrence NRE'd
                // during the import-table lookup. Visible in Ball_TA.OnCarTouch which starts
                // with 0x31 and would render "InstanceDelegateToken size 0".
                { 0x31, typeof(OptionalArgSkipTokenRL) },
                // 0x32: VERIFIED InstanceDelegate (UObject* + FName, 16 bytes).
                // GNatives[0x32] = sub_7FF6CD2F6180 reads 8-byte UObject* + 8-byte FName
                // and constructs a `{Object, Name, 0}` delegate tuple — that's the canonical
                // RL `EX_InstanceDelegate` runtime behavior with the Object baked in (baseline
                // UE3 EX_InstanceDelegate carries only the FName). Was wrongly VectorConst
                // (12 bytes — under-read 4 bytes per occurrence).
                { 0x32, typeof(InstanceDelegateTokenRL) },
                // 0x33: VERIFIED 1-sub-expr DynArray-result wrapper. Reads 1 sub-expr,
                // then accesses dynarray-result globals + does a vtable call. Logs
                // "Result given to DynArrayResult method". Conservative mapping —
                // EatReturnValue (1-sub passthrough) until full semantics understood.
                { 0x33, typeof(EatReturnValueToken) },
                // 0x34: VERIFIED unmapped (binary handler = default error). Was wrongly
                // LetDelegateToken (2 sub-exprs + cleanup — over-consumed).
                { 0x34, typeof(NothingToken) },
                // 0x35: FNAME (8-byte) shape — tied across NameConst / Virtual / Global
                // function. Picked NameConstToken (simplest leaf).
                { 0x35, typeof(NameConstToken) },
                // 0x36: VERIFIED property-setter-with-discard.
                // GNatives[0x36] = sub_7FF6CD2F7250 reads 8-byte UProperty*, dispatches a sub-
                // expression, then writes 0 to the result slot — i.e. the sub-expression's
                // nominal value is unused. Renders as `Property = Expression`. Was wrongly
                // VectorConstToken (12 bytes — under-read by 3 bytes per occurrence and emitted
                // a vect() literal instead of a property assignment).
                { 0x36, typeof(PropertySetterDiscardTokenRL) },
                // 0x37: VERIFIED 2 sub-exprs + u16 + variadic body + 0x3E + optional debug.
                // UStruct::SerializeExpr (sub_7FF6CD38C840) case 0x37 explicitly reads this
                // shape; runtime handler GNatives[0x37] (sub_7FF6CD2F5810) gates the variadic
                // dispatch on receiver being non-null. New token NullConditionalCallTokenRL
                // matches the parser exactly. Not observed in TAGame/Engine fixtures yet —
                // defensive remap. Was FloatConstToken (4-byte literal — would under-read
                // significantly and corrupt the rest of any function containing 0x37).
                { 0x37, typeof(NullConditionalCallTokenRL) },
                // 0x38: VERIFIED ClassContext (NOT FinalFunction).
                // GNatives[0x38] = sub_7FF6CD308710 has unique runtime error
                // "Accessed null class context '%s'" — that string is the EX_ClassContext
                // signature in baseline UE3. Wire format: 1 byte + sub-opcode (object) +
                // 2 bytes (NULL skip) + 8 bytes (UField*) + 1 byte (property type), with
                // a +1 leading byte specific to RL (similar to 0x0F). Was wrongly mapped
                // to FinalFunctionTokenRL.
                { 0x38, typeof(ClassContextToken) },
                // 0x39: VERIFIED NameConst-shape (reads 8-byte qword, writes to result).
                // GNatives[0x39] = sub_7FF6CD2F6FA0 just reads `*Code` (8 bytes), advances,
                // writes to *a3. Aliased with 0x3B, 0x43, 0x5A — four bytes share this same
                // 8-byte-leaf runtime handler. Each parses to a different baseline EX_ at
                // compile time (NameConst, ObjectConst, InstanceDelegate, ...) but the runtime
                // doesn't care since all four push 8 bytes to result. Picking NameConst here as
                // the highest-frequency 8-byte leaf in cooked code.
                // GlobalFunction is at 0x59 (state-skip variant) and 0x41 (VirtualFunction).
                { 0x39, typeof(NameConstToken) },
                // 0x3A: VERIFIED IntOne/True (aliased with 0x2F — same runtime handler).
                // Picking TrueToken so we have one each of IntOne/True available.
                // Was wrongly mapped to DebugInfo by score-mapping (which only measures
                // byte alignment, not semantics).
                { 0x3A, typeof(TrueToken) },
                { 0x3B, typeof(ObjectConstToken) },
                // 0x3C: VERIFIED unmapped (binary handler = default error). Was wrongly
                // TwoStepToken (which over-consumes).
                { 0x3C, typeof(NothingToken) },
                // 0x3D: VERIFIED unmapped (binary handler = default error). Was wrongly
                // VirtualFunctionToken (8-byte FName — over-consumed 8 bytes per occurrence).
                { 0x3D, typeof(NothingToken) },
                // 0x3E: VERIFIED variadic terminator (EndFunctionParms).
                // GNatives[0x3E] = sub_7FF6CD2F00B0 is `qword_..._D7B8 = 0; --Code;` — un-consume
                // pattern of a parser-side terminator. Two GNatives variadic-loop handlers
                // (0x12 sub_7FF6CD2F5740 and 0x37 sub_7FF6CD2F5810) both check `*v3 != 0x3E`.
                // Was wrongly mapped to IntZero by score-mapping (which only measured byte
                // alignment, not semantics).
                { 0x3E, typeof(EndFunctionParmsToken) },
                // 0x3F: VERIFIED unmapped (binary handler = default error). Was wrongly
                // VectorConstToken (12 bytes — over-consumed badly per occurrence).
                { 0x3F, typeof(NothingToken) },
                // 0x40: VERIFIED DelegateFunction (1 byte + UProperty* + FName).
                // GNatives[0x40] = sub_7FF6CD2F5F90 reads 1 byte (v8 — local-prop flag), 8 bytes
                // (v10 — UProperty*), then 8 bytes (v13 — FName for function name), and dispatches
                // a delegate call via FindFunction (sub_7FF6CD34DBB0). Wire format matches
                // baseline EX_DelegateFunction case exactly: `XFER(BYTE); XFER_PROP_POINTER;
                // XFERNAME();`. Was wrongly BoolVariable.
                { 0x40, typeof(DelegateFunctionToken) },
                // 0x41: VERIFIED VirtualFunction (8-byte FName + state-aware function lookup).
                // GNatives[0x41] = sub_7FF6CD2F5E90 reads FName, calls
                // sub_7FF6CD34DBB0(this, name, 0) — third arg 0 means "use state's version"
                // (= VirtualFunction in baseline UE3 terms). Sister opcode to 0x59 (GlobalFunction
                // = same lookup with state-skip flag set). Was wrongly mapped to ClassContext
                // (which is correctly at 0x38 — different runtime signature with the
                // "Accessed null class context" error string).
                { 0x41, typeof(VirtualFunctionToken) },
                // 0x42: VERIFIED unmapped (binary handler = default error). Was wrongly
                // DefaultParameterToken (reads u16 + 2 sub-exprs — over-consumed ~6+ bytes
                // per occurrence, cascading into Ball_TA.IsGroundHit's `default. = ` orphan).
                { 0x42, typeof(NothingToken) },
                // 0x43: VERIFIED 8-byte qword leaf (NOT DebugInfo).
                // GNatives[0x43] = sub_7FF6CD2F6FA0 — same handler as 0x39, 0x3B, 0x5A. Just reads
                // 8 bytes from Code, advances 8, writes qword to *a3. The four aliases each parse
                // to a different baseline EX_ at compile time (NameConst, ObjectConst,
                // InstanceDelegate, ...) but the runtime doesn't care since all four push 8 bytes
                // to result. Picking ObjectConst here because in real RL bytecode 0x43 typically
                // appears as ClassContext's sub-expr A — the OBJECT being accessed — which
                // semantically must be a UObject*, not an FName.
                // Was wrongly DebugInfoToken (13-byte payload — over-consumed by 5 bytes).
                { 0x43, typeof(ObjectConstToken) },
                // 0x44: VERIFIED unmapped (binary handler = default error). Was wrongly
                // UnicodeStringConstToken (consumes UTF-16 chars until null — over-consumed
                // arbitrarily many bytes per occurrence). Real EX_UnicodeStringConst is at 0x51.
                { 0x44, typeof(NothingToken) },
                // 0x45: VERIFIED unmapped (binary handler = default error). Was wrongly
                // EndFunctionParmsToken (1-byte safe, but its presence in the parser triggers
                // variadic-call loop termination via the FunctionToken.DeserializeCall
                // `is EndFunctionParmsToken` check — falsely cutting variadic args short).
                // Real EX_EndFunctionParms terminator is at 0x3E.
                { 0x45, typeof(NothingToken) },
                // 0x46: VERIFIED LetBool-shape (dispatches 2 sub-opcodes — assignment).
                // GNatives[0x46] = sub_7FF6CD2F12A0 resets the runtime globals, dispatches one
                // sub-opcode (variable expression), then dispatches a second sub-opcode (value
                // expression). Same Let-shape as 0x4C (EX_Let), 0x49 (EX_LetDelegate). The lack
                // of the "Attempt to assign variable through None" cleanup that 0x4C does
                // suggests this is the boolean variant **EX_LetBool** (no NULL check needed
                // since bool storage is always backed). Was wrongly DelegateCmpEq.
                { 0x46, typeof(LetBoolToken) },
                // 0x47: VERIFIED EmptyParmValue (skipped optional argument, NOT Stop).
                // Empirically appears 6 times in a row inside `Spawn(ControllerClass, ?, ?, ?,
                // ?, ?, ?)` — that's the 6 optional arguments of Spawn (Owner, Tag, Location,
                // Rotation, RemoveCollisionFromAdjacent, ...). Each is an EX_EmptyParmValue
                // marker. Was wrongly StopToken (which renders as `stop` and only makes sense
                // in state-code context, never as a function-call argument).
                // Runtime handler at sub_7FF6CD2F0360 sets a state flag — could plausibly be
                // either Stop or EmptyParm at runtime, but the contextual usage (always as
                // call args) confirms EmptyParm.
                { 0x47, typeof(EmptyParmToken) },
                { 0x48, typeof(EventSubscribeToken) },
                // 0x49: VERIFIED Let-shape (reads 2 sub-opcodes, like EX_Let / LetBool / LetDelegate).
                // GNatives[0x49] = sub_7FF6CD2F0C60: dispatches sub-opcode A, captures result via
                // qword_..._D7B0, dispatches sub-opcode B; if first result was non-null, calls
                // sub_7FF6CD2B0640(result) and zeroes it (delegate-cleanup pattern). The cleanup of
                // the LHS suggests this is **EX_LetDelegate** (which has to release the previous
                // delegate before assigning the new one). Was wrongly NothingToken (1-byte leaf) —
                // every occurrence in real bytecode was severely under-consuming.
                { 0x49, typeof(LetDelegateToken) },
                // 0x4A: VERIFIED StructMember (8 bytes + 8 bytes + 2 bytes + sub-expr).
                // GNatives[0x4A] = sub_7FF6CD2F6590 reads UProperty* (v8), UStruct* (v9), 1 byte
                // (v10 — local-copy flag), 1 byte (v15 — modified flag), then dispatches sub-opcode
                // for the struct-member-property expression. The local-copy flag triggers
                // allocation of a temporary struct buffer; that's the canonical EX_StructMember
                // runtime pattern from baseline UE3. Was wrongly DynamicArrayFindToken.
                { 0x4A, typeof(StructMemberToken) },
                // 0x4B: VERIFIED unmapped (binary handler = default error). Was wrongly
                // DelegateCmpNeToken (over-consumes typical delegate-comparison shape).
                { 0x4B, typeof(NothingToken) },
                // 0x4C: VERIFIED Let (assignment, NOT EndFunctionParms).
                // GNatives[0x4C] = sub_7FF6CD2F08B0 has unique runtime error
                // "Attempt to assign variable through None" — that string is the EX_Let
                // signature in baseline UE3. Handler dispatches two sub-opcodes
                // (variable expression, then assignment expression). Was wrongly mapped
                // to EndFunctionParms; the real terminator is 0x3E. This was the keystone
                // mistake: variadic loops were terminating ONE byte too early on Let
                // assignments, scrambling all token alignment downstream.
                { 0x4C, typeof(LetToken) },
                // 0x4D: VERIFIED 8-byte UStruct* + 1 sub-expr (struct construction / value).
                // GNatives[0x4D] = sub_7FF6CD2F5B80 reads 8-byte UStruct*, allocates a struct
                // buffer of size `v4[28] * v4[29]`, dispatches a sub-opcode, then calls
                // `vtable[98]` (likely the struct's UProperty::CopyCompleteValue equivalent).
                // Shape: 8 bytes (UStruct*) + 1 sub-expr. Was wrongly LocalVariable (8-byte
                // UProperty* with no sub-expr — left the sub-opcode to be parsed as a sibling).
                { 0x4D, typeof(StructValueTokenRL) },
                // 0x4E: VERIFIED unmapped (binary handler = default error). Was wrongly
                // StructCmpNeToken (which reads 8 bytes UObject* + 2 sub-exprs). Real
                // EX_StructCmpEq/Ne is at 0x53.
                { 0x4E, typeof(NothingToken) },
                // 0x4F: VERIFIED unmapped (binary handler = default error). Was wrongly
                // ObjectConstToken (reads 8-byte UObject* — over-consumed 8 bytes per
                // occurrence and NRE'd on the import-table lookup). Visible in Ball_TA.OnCarTouch
                // at position 5 — would NRE during the function preamble.
                { 0x4F, typeof(NothingToken) },
                // 0x50: VERIFIED StringConst (8-bit ASCII string until null).
                // GNatives[0x50] = sub_7FF6CD2F6E00 calls sub_7FF6CD2B7EC0(&local, Code) — that's
                // the FString constructor from a C-string. UnicodeStringConst (UTF-16) is at 0x51
                // (separate handler that calls wcslen on the Code pointer).
                { 0x50, typeof(StringConstToken) },
                // 0x51: VERIFIED UnicodeStringConst (UTF-16 string until null word).
                // GNatives[0x51] = sub_7FF6CD2F6EA0 reads `_WORD *Code`, calls `wcslen(Code)` —
                // the canonical EX_UnicodeStringConst runtime behavior.
                // Was wrongly mapped to TrueToken; True is now at 0x3A.
                { 0x51, typeof(UnicodeStringConstToken) },
                // 0x52: VERIFIED 1-sub-expr passthrough (NOT DynamicCast).
                // GNatives[0x52] = sub_7FF6CD2ED450 reads 1 sub-expr and writes 0 to result
                // slot. Same shape as 0x15 (sub_7FF6CD2F59D0 — also a 1-sub-discard wrapper).
                // DynamicCast in baseline UE3 has wire format `1 byte + UClass* + 1 sub` —
                // doesn't match. Mapping to EatReturnValue (1-sub passthrough).
                { 0x52, typeof(EatReturnValueToken) },
                // 0x53: VERIFIED StructCmpEq/Ne (8-byte UStruct* + 2 sub-exprs + struct comparison).
                // GNatives[0x53] = sub_7FF6CD2F6240 reads 8-byte UStruct*, allocates two struct
                // buffers, dispatches sub-opcode A (writes to buf1), dispatches sub-opcode B
                // (writes to buf2), then `sub_7FF6CD658AC0(struct, buf1, buf2, 0)` performs the
                // comparison and writes the bool result to *a3. The 4th arg = 0 suggests EQ.
                // ComparisonToken (StructCmpEqToken's base) reads exactly UObject* + 2 sub-exprs
                // — same wire format as 0x53. Was wrongly LocalVariableToken (8-byte UProperty*
                // with no sub-exprs — left both sub-opcodes to be parsed as siblings, scrambling
                // surrounding context).
                { 0x53, typeof(StructCmpEqToken) },
                // 0x54: VERIFIED variadic body until 0x3E + optional debug — alias of 0x1B,
                // shares same parser case as 0x05/0x16 in UStruct::SerializeExpr. See 0x1B
                // entry above. Was EatReturnValueToken (reads UProperty in version >= 201) —
                // wrong wire format, would over-read by 4-8 bytes per occurrence.
                { 0x54, typeof(DynamicArrayElementToken) },
                // 0x55: VERIFIED InstanceVariable (UProperty* relative to `this`).
                // GNatives[0x55] = sub_7FF6CD2ED270 reads 8-byte UProperty*, computes address
                // as `a1 + property_offset` where a1 == this. Sister opcode to 0x65
                // (LocalVariable, uses Locals frame instead). The previous NothingToken mapping
                // was a band-aid that accidentally aligned some patterns by treating it as a
                // separator — semantically wrong, and the over-consumption (8 missed bytes per
                // occurrence) was scrambling downstream tokens.
                { 0x55, typeof(InstanceVariableToken) },
                // 0x56: VERIFIED 8-byte FName + state-fn-call (NOT DebugInfo).
                // GNatives[0x56] = sub_7FF6CD2F5B00 reads 8-byte FName, calls a state-aware
                // logger that prints "State function '%s' called while not in declared state."
                // Shape is identical to NameConst (8-byte qword leaf) — the state check is
                // runtime-only behavior, doesn't affect parsing. Was wrongly DebugInfoToken
                // (13-byte payload — over-consumed 5 bytes per occurrence).
                { 0x56, typeof(NameConstToken) },
                // 0x57: VERIFIED Switch (UProperty* + property type + sub-expr + case loop).
                // GNatives[0x57] = sub_7FF6CD2F0390 reads 9 bytes via sub_7FF6CD317F00 (UField* +
                // property type byte), dispatches sub-opcode (the switch-value expression), then
                // loops over case entries until 0xFFFF terminator using `wcsicmp`/`memcmp` for
                // case matching. That's the canonical EX_Switch runtime behavior.
                // SwitchToken.Deserialize reads ExpressionField (8 bytes) + PropertyType (1 byte
                // for version > 587) + sub-expr — exactly matches.
                { 0x57, typeof(SwitchToken) },
                // 0x58: VERIFIED DefaultVariable (default-object property access).
                // GNatives[0x58] = sub_7FF6CD2ED2D0 reads 8-byte UProperty*, gates on
                // `*(unsigned int *)(a1 + 16) & 0x200` (an object-flags check that determines
                // whether the default object is accessible) — that's the EX_DefaultVariable
                // runtime pattern. StringConst is now correctly at 0x50.
                { 0x58, typeof(DefaultVariableToken) },
                // 0x59: VERIFIED GlobalFunction (8-byte FName + lookup with state-skip flag).
                // GNatives[0x59] = sub_7FF6CD2F5F20 reads FName, calls
                // sub_7FF6CD34DBB0(this, name, 1) — third arg 1 means "skip state lookup"
                // (= GlobalFunction in baseline UE3 terms). 0x41 (sister opcode) does the
                // same lookup with arg 0 = state-aware (= VirtualFunction).
                { 0x59, typeof(GlobalFunctionToken) },
                // 0x5A: many candidates tied at the same clean count.
                // LocalVariableToken (4-byte UProperty*) chosen by frequency.
                { 0x5A, typeof(LocalVariableToken) },
                // 0x5B: VERIFIED 8-byte UStruct + 2 sub-exprs + u16 (NOT VectorConst).
                // GNatives[0x5B] = sub_7FF6CD308170 reads UStruct* (8 bytes), dispatches
                // sub-1 (default), reads u16 (byte size of next sub), conditionally
                // dispatches sub-2 (actual value) based on a struct comparison. New token
                // StructDefaultParameterTokenRL renders sub-2 (the actual computed value).
                // Was wrongly VectorConstToken (12 bytes — produced nonsense literals like
                // `ControllerRef = vect(0, 0, -9.52e21)` in Car_TA.GetPreviewTeamIndex
                // where the LHS was a PlayerController, not a Vector).
                { 0x5B, typeof(StructDefaultParameterTokenRL) },
                // 0x5C: VERIFIED GotoLabel (1 sub-expr — label name).
                // GNatives[0x5C] = sub_7FF6CD2F07F0 dispatches a sub-opcode (the label name expr),
                // then calls vtable[73] (this->FindLabel(name)) and prints "GotoLabel (%s): Label
                // not found" if missing — that's the EX_GotoLabel signature. Was wrongly JumpToken
                // (which reads 2 bytes); the real Jump byte is 0x5D (handler is just `Code += 2`).
                { 0x5C, typeof(GotoLabelToken) },
                // 0x5D: VERIFIED Jump (unconditional, 2-byte code offset).
                // GNatives[0x5D] = sub_7FF6CD30D7B0 is exactly `*(_QWORD*)(Code) += 2;` — that's
                // baseline EX_Jump's wire format (XFER(CodeSkipSizeType) only). The runtime
                // handler is intentionally a no-op since the actual jump is performed elsewhere
                // (the parser still needs to consume the 2 bytes here so the decompile can
                // reconstruct the goto target).
                { 0x5D, typeof(JumpToken) },
                // 0x5E: VERIFIED FieldToken-shape (5 disk bytes = 1 op + 4-byte UProperty
                // index, expanded to 8 bytes in-memory via AlignObjectSize).
                // GNatives[0x5E] = sub_7FF6CD309510 reads `*(_DWORD**)Code`, advances Code
                // by 8 in-memory. Has two runtime paths gated on `v4[30] & 0x100`
                // (UProperty::PropertyFlags & CPF_OutParm):
                //   * IF flag set: walks `a2[9]` (FFrame OutParms-like list) for matching
                //     UProperty, sets the global accessor target to the matched node.
                //   * ELSE: `qword_..._D7B0 = a2[6] + v4[38]` — Locals frame + property
                //     offset, identical to 0x65 LocalVariable's runtime.
                // It's the unified locals/out-param accessor, sister to 0x65 (locals-only)
                // and 0x11 (out-param-only). Mapping to LocalVariableToken so the property
                // name renders correctly. Was wrongly NothingToken (1-byte leaf), under-
                // consuming 4 bytes per occurrence — produced garbage like the orphan
                // `1577058308` in Online_X.CreateUniqueNetID where IntConstToken was
                // mis-aligned to read 0x5E's payload.
                { 0x5E, typeof(LocalVariableToken) },
                // 0x5F: BadToken in baseline RL — tied across all candidates.
                { 0x5F, typeof(LocalVariableToken) },
                // 0x60: VERIFIED VectorConst or RotationConst (reads 12 bytes = 3 INTs).
                // GNatives[0x60] = sub_7FF6CD2F9C90 (32-byte function): reads 3 ints into
                // result fields a3[0..2]. Was wrongly mapped to EmptyParm (a 1-byte leaf).
                // Picking VectorConst as the more common case in scripted code.
                { 0x60, typeof(VectorConstToken) },
                // 0x61: VERIFIED `new` expression (NOT EmptyDelegate).
                // GNatives[0x61] = sub_7FF6CD3082F0 dispatches 5 sub-expressions
                // (Outer, Name, Flags, Class, Template) and logs
                // "No class passed to 'new' operator". New token NewExpressionTokenRL
                // renders as `new(Outer, Name, Flags, Template) Class`. Was wrongly
                // EmptyDelegateToken which read 0 args — every `new(...)` call leaked
                // its 5 sub-expressions as orphan top-level statements (visible in
                // PRI_TA.PostBeginPlay's `CarDistanceTracker = none; self Class'X'`
                // pattern; affects most class-instance construction sites).
                { 0x61, typeof(NewExpressionTokenRL) },
                // 0x62: VERIFIED 8-byte FName leaf — compact delegate-function reference.
                // GNatives[0x62] = sub_7FF6CD2F6FC0 reads `*(qword*)Code`, advances Code
                // by 8 bytes, builds `{context=this, name=qword, 0}` and dispatches a
                // property accessor (sub_7FF6CD2A9C90). The qword IS an FName (function
                // name to bind as a delegate). Wire format: 1 op + 8 raw bytes = 9 bytes
                // total, matching NameConst-shape but rendered as a bare function name
                // (no single quotes) for delegate-RHS contexts.
                // Was wrongly AssertToken (u16 + byte payload = 4 storage bytes) which
                // produced `Foo.__Event*__Delegate = assert();` everywhere a delegate
                // was bound, with the 5 unread payload bytes orphaning as
                // ContextAwareReturnTokenRL chains. Visible in
                // AntiCheatMessenger_TA.PostBeginPlay (3 occurrences),
                // AntiCheatManager_TA.__Construct_0x1 (~6 occurrences), and many other
                // classes that subscribe engine-event delegates. The legitimate
                // `assert(condition, msg1, msg2)` rendering is at byte 0x2D
                // (AssertExpressionTokenRL with 3 sub-exprs), unaffected by this remap.
                { 0x62, typeof(DelegateFunctionRefTokenRL) },
                { 0x63, typeof(IntConstByteToken) },
                // 0x64: VERIFIED 4-byte literal reader (IntConst- or FloatConst-shape).
                // GNatives[0x64] = sub_7FF6CD2F6DE0 reads `*(unsigned int*)Code` (4 bytes), writes
                // to *a3, advances Code += 4. Same shape as 0x0B (which is IntConst); these are
                // sister opcodes. Picking FloatConst for 0x64 — at runtime they behave identically
                // (push 4 bytes), but the decompile-side rendering will format as a float literal
                // (`1.0`) vs IntConst's `1`. Real Switch is at 0x57 (verified via case-loop
                // 0xFFFF terminator + UProperty* + property-type wire format).
                { 0x64, typeof(FloatConstToken) },
                // 0x65: VERIFIED LocalVariable (UProperty* relative to Locals frame).
                // GNatives[0x65] = sub_7FF6CD2ED210 reads 8-byte UProperty*, computes address
                // as `*(a2+48) + property_offset` — the +48 offset is the Locals frame pointer
                // in FFrame. That's the canonical EX_LocalVariable runtime behavior.
                // Was wrongly mapped to LetToken (now correctly at 0x4C).
                { 0x65, typeof(LocalVariableToken) },
                // 0x66: VERIFIED BoolVariable (1-sub-expr wrapper that clears a flag).
                // GNatives[0x66] = sub_7FF6CD2F5C70 reads 1 byte (sub-opcode), dispatches,
                // then `*(_DWORD*)(a2+56) = 0;` (clears a flag — likely the "boolean coercion"
                // marker used by BoolVariable's runtime). Self is now correctly at 0x1F.
                { 0x66, typeof(BoolVariableToken) },
                // 0x67: VERIFIED unmapped (binary handler = default error). Was wrongly
                // SkipFunctionTokenRL (which over-consumed bytes).
                { 0x67, typeof(NothingToken) },
                // 0x68: VERIFIED unmapped (binary handler = default error "Unknown code token").
                // GNatives[0x68] = sub_7FF6CD31ACB0 (the default error handler). Should never
                // appear in valid bytecode; if it does, treat as a 1-byte leaf (NothingToken)
                // so the parser keeps progressing.
                { 0x68, typeof(NothingToken) },
                // 0x69: VERIFIED IteratorPop (1-byte leaf, runtime emits "Unexpected iterator pop
                // command at %s:%04X" if reached outside an iterator scope). GNatives[0x69] =
                // sub_7FF6CD2F0290. Was wrongly DelegateCmpNe.
                { 0x69, typeof(IteratorPopToken) },
                // 0x6A: VERIFIED EmptyDelegate (zero-out delegate result, no Code reads).
                // GNatives[0x6A] = sub_7FF6CD2F7060 zeroes 24 bytes on stack, then constructs
                // an empty delegate via sub_7FF6CD2A9C90(result, &empty). No Code reads.
                // That is the canonical EX_EmptyDelegate runtime behavior (writes
                // {NULL UObject*, NAME_None} to result).
                { 0x6A, typeof(EmptyDelegateToken) },
                // 0x6B: VERIFIED PrimitiveCast (1-byte cast type + sub-expression).
                // GNatives[0x6B] = sub_7FF6CD2F7340 reads 1 byte, dispatches into a separate
                // sub-table at funcs_7FF6CD2F735D — that's the cast-type sub-table from
                // baseline EX_PrimitiveCast (each cast variant — IntToFloat, ByteToInt etc.
                // gets its own runtime handler). Was wrongly mapped to LocalVariableToken
                // (which is an 8-byte UProperty* read, totally different shape).
                { 0x6B, typeof(PrimitiveCastToken) },
                // 0x6C: VERIFIED ReturnNothing (8-byte UProperty* + zero-out result).
                // GNatives[0x6C] = sub_7FF6CD2F0210 reads 8-byte UProperty*, prints
                // unique runtime error string "Control reached the end of non-void function
                // (make certain that all paths through the function 'return' a value)" — that
                // is the EX_ReturnNothing signature in baseline UE3.
                // Was wrongly mapped to ContextInitTokenRL.
                { 0x6C, typeof(ReturnNothingToken) },
                { 0x6D, typeof(OutVariableToken) },
                // 0x6E: VERIFIED unmapped (binary handler = default error). Treat as 1-byte leaf.
                { 0x6E, typeof(NothingToken) },
                // 0x6F: VERIFIED unmapped (binary handler = default error). Was wrongly Conditional
                // (which reads 3 sub-exprs + 4 bytes — caused massive over-consumption when this
                // byte appeared as random padding). Treat as 1-byte leaf.
                { 0x6F, typeof(NothingToken) },

                // Special RL Native tokens
                // Bytes 0x70..0x7F are the chained native-dispatcher bytes — each reads a sub_byte
                // and indexes GNatives[(byte − 0x70) × 256 + sub_byte], covering native indexes
                // 0..4095. Without these mappings the parser would treat each as a leaf native at
                // index 0x70..0x7F and emit __NFUN_112__ etc. placeholders. 0x71 keeps its existing
                // RL-specific token (which adds an iterator dispatch sub-table); the rest use the
                // generic ChainedNativeDispatcherTokenRL.
                { 0x70, typeof(ChainedNativeDispatcherTokenRL) },
                { 0x71, typeof(ExAlternativeExtendedNativeFunctionTokenRL) },
                { 0x72, typeof(ChainedNativeDispatcherTokenRL) },
                { 0x73, typeof(ChainedNativeDispatcherTokenRL) },
                { 0x74, typeof(ChainedNativeDispatcherTokenRL) },
                { 0x75, typeof(ChainedNativeDispatcherTokenRL) },
                { 0x76, typeof(ChainedNativeDispatcherTokenRL) },
                { 0x77, typeof(ChainedNativeDispatcherTokenRL) },
                { 0x78, typeof(ChainedNativeDispatcherTokenRL) },
                { 0x79, typeof(ChainedNativeDispatcherTokenRL) },
                { 0x7A, typeof(ChainedNativeDispatcherTokenRL) },
                { 0x7B, typeof(ChainedNativeDispatcherTokenRL) },
                { 0x7C, typeof(ChainedNativeDispatcherTokenRL) },
                { 0x7D, typeof(ChainedNativeDispatcherTokenRL) },
                { 0x7E, typeof(ChainedNativeDispatcherTokenRL) },
                { 0x7F, typeof(ChainedNativeDispatcherTokenRL) },
                { 0x82, typeof(AndTokenRL) },
                { 0x84, typeof(OrTokenRL) },
                // 0xC8: GNatives[200] resolves to the "Unknown code token" default error handler
                // — i.e. there is no real native at index 200 in this RL binary. Treating byte
                // 0xC8 as a native call with variadic args generated __NFUN_200__(...) ghost
                // calls in ~46 functions in Engine alone. Mapping to NothingToken drops the
                // ghost calls without affecting parse-cleanness (4725/4725 still parse clean).
                { 0xC8, typeof(NothingToken) },
            };

            return tokenMap;
        }

        protected override void SetupTokenFactory(UnrealPackage linker)
        {
            var tokenMap = BuildTokenMap(linker);
            SetupTokenFactory<TokenFactory>(
                tokenMap,
                TokenFactory.FromPackage(linker.NTLPackage),
                0x70,
                0x70);
        }
    }
}
