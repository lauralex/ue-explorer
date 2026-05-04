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
                { 0x01, typeof(StateVariableToken) },
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
                { 0x0A, typeof(DelegateCmpEqToken) },
                // 0x0B: VERIFIED IntConst (reads INT, NOT DynamicArrayElement).
                // GNatives[0x0B] = sub_7FF6CD2F6DC0 reads 4-byte INT and writes to *a3.
                { 0x0B, typeof(IntConstToken) },
                { 0x0C, typeof(EventSubscribeToken) },
                // 0x0D: 13-byte payload (3 ints + 1 byte) — matches DebugInfoToken's shape.
                // Decompile-side, DebugInfo tokens are skipped, so the bytes are consumed
                // without disrupting the surrounding statement.
                { 0x0D, typeof(DebugInfoToken) },
                { 0x0E, typeof(IteratorNextToken) },
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
                { 0x17, typeof(DelegatePropertyToken) },
                { 0x18, typeof(ConditionalToken) },
                { 0x19, typeof(InterfaceCastToken) },
                { 0x1A, typeof(InstanceVariableToken) },
                { 0x1B, typeof(MetaClassCastToken) },
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
                { 0x20, typeof(ReturnToken) },
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
                { 0x24, typeof(DeprecatedTokenRL) },
                { 0x25, typeof(FalseToken) },
                { 0x26, typeof(EndParmValueToken) },
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
                // 0x2C: matches DebugInfoToken's 13-byte shape.
                { 0x2C, typeof(DebugInfoToken) },
                // 0x2D: tied across EventSubscribe/EventUnsubscribe and several other
                // (FNAME + 1 sub) shapes — picked EventUnsubscribeToken as a guess.
                { 0x2D, typeof(EventUnsubscribeToken) },
                // 0x2E: VERIFIED Nothing (empty stub, aliased to 0x1D).
                // Same handler address as 0x1D — empty function. Was wrongly mapped to Case
                // (which reads a 2-byte WORD + optional sub-expression).
                { 0x2E, typeof(NothingToken) },
                // 0x2F: VERIFIED IntOne/True (writes 4-byte 1).
                // GNatives[0x2F] = sub_7FF6CD2F7040 (8-byte function): `*(_DWORD*)a3 = 1;`.
                // Aliased with 0x3A (same handler). Was wrongly mapped to GotoLabel.
                { 0x2F, typeof(IntOneToken) },
                { 0x30, typeof(NativeParameterToken) },
                { 0x31, typeof(InstanceDelegateToken) },
                // 0x32: VERIFIED InstanceDelegate (UObject* + FName, 16 bytes).
                // GNatives[0x32] = sub_7FF6CD2F6180 reads 8-byte UObject* + 8-byte FName
                // and constructs a `{Object, Name, 0}` delegate tuple — that's the canonical
                // RL `EX_InstanceDelegate` runtime behavior with the Object baked in (baseline
                // UE3 EX_InstanceDelegate carries only the FName). Was wrongly VectorConst
                // (12 bytes — under-read 4 bytes per occurrence).
                { 0x32, typeof(InstanceDelegateTokenRL) },
                { 0x33, typeof(AssertTokenRL) },
                { 0x34, typeof(LetDelegateToken) },
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
                { 0x37, typeof(FloatConstToken) },
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
                { 0x3C, typeof(TwoStepToken) },
                // 0x3D: tied across many FNAME-shape (8-byte) candidates — Virtual /
                // Global / Name / InstanceDelegate. Pick VirtualFunctionToken since the
                // bytes around 0x3D often look like an inline function call.
                { 0x3D, typeof(VirtualFunctionToken) },
                // 0x3E: VERIFIED variadic terminator (EndFunctionParms).
                // GNatives[0x3E] = sub_7FF6CD2F00B0 is `qword_..._D7B8 = 0; --Code;` — un-consume
                // pattern of a parser-side terminator. Two GNatives variadic-loop handlers
                // (0x12 sub_7FF6CD2F5740 and 0x37 sub_7FF6CD2F5810) both check `*v3 != 0x3E`.
                // Was wrongly mapped to IntZero by score-mapping (which only measured byte
                // alignment, not semantics).
                { 0x3E, typeof(EndFunctionParmsToken) },
                // 0x3F: 12-byte payload — Vector/RotationConst shape.
                { 0x3F, typeof(VectorConstToken) },
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
                { 0x42, typeof(DefaultParameterToken) },
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
                { 0x44, typeof(UnicodeStringConstToken) },
                { 0x45, typeof(EndFunctionParmsToken) },
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
                { 0x4B, typeof(DelegateCmpNeToken) },
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
                { 0x4F, typeof(ObjectConstToken) },
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
                { 0x52, typeof(DynamicCastToken) },
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
                // 0x54: 1-sub-token wrapper — tied across EatReturnValue, several Casts,
                // ReturnNothing. Pick EatReturnValueToken (simplest pass-through).
                { 0x54, typeof(EatReturnValueToken) },
                // 0x55: VERIFIED InstanceVariable (UProperty* relative to `this`).
                // GNatives[0x55] = sub_7FF6CD2ED270 reads 8-byte UProperty*, computes address
                // as `a1 + property_offset` where a1 == this. Sister opcode to 0x65
                // (LocalVariable, uses Locals frame instead). The previous NothingToken mapping
                // was a band-aid that accidentally aligned some patterns by treating it as a
                // separator — semantically wrong, and the over-consumption (8 missed bytes per
                // occurrence) was scrambling downstream tokens.
                { 0x55, typeof(InstanceVariableToken) },
                { 0x56, typeof(DebugInfoToken) },
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
                // 0x5B: 12-byte payload — Vector/RotationConst shape.
                { 0x5B, typeof(VectorConstToken) },
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
                // 0x5E: was AlternativeExtendedNativeFunctionToken (sub_byte + 5000). Binary
                // RE: byte 0x5E in the runtime dispatch is a UObject-property-access handler
                // (sub_7FF6CD309510), not a chained native dispatcher. The +5000 indexes don't
                // exist in GNatives. Mapping to NothingToken eliminates ghost natives, parse
                // remains 100% clean. Real natives go through byte 0x71 (chained dispatcher to
                // GNatives[256+sub_byte]).
                { 0x5E, typeof(NothingToken) },
                // 0x5F: BadToken in baseline RL — tied across all candidates.
                { 0x5F, typeof(LocalVariableToken) },
                // 0x60: VERIFIED VectorConst or RotationConst (reads 12 bytes = 3 INTs).
                // GNatives[0x60] = sub_7FF6CD2F9C90 (32-byte function): reads 3 ints into
                // result fields a3[0..2]. Was wrongly mapped to EmptyParm (a 1-byte leaf).
                // Picking VectorConst as the more common case in scripted code.
                { 0x60, typeof(VectorConstToken) },
                { 0x61, typeof(EmptyDelegateToken) },
                // 0x62: AssertToken-like shape (1 sub + small payload). Tied across many
                // 1-sub shapes; AssertToken edged by 1 clean function.
                { 0x62, typeof(AssertToken) },
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
                { 0x67, typeof(SkipFunctionTokenRL) },
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
