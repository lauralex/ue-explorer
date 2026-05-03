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
                { 0x00, typeof(NothingToken) },
                { 0x01, typeof(StateVariableToken) },
                { 0x02, typeof(IntConstToken) },
                { 0x03, typeof(StructCmpEqToken) },
                { 0x04, typeof(EndOfScriptToken) },
                { 0x05, typeof(StructMemberToken) },
                { 0x06, typeof(NameConstToken) },
                { 0x07, typeof(ReturnNothingToken) },
                // 0x08: empirically a 1-sub-token wrapper (best fit; EatReturnValue shape).
                // Tied with InterfaceCast/DynamicCast/MetaClassCast/ObjectConst — all 1-sub
                // shapes in baseline UE3. Picked EatReturnValue as the simplest leaf-of-leaf.
                { 0x08, typeof(EatReturnValueToken) },
                { 0x09, typeof(DefaultVariableToken) },
                { 0x0A, typeof(DelegateCmpEqToken) },
                { 0x0B, typeof(DynamicArrayElementToken) },
                { 0x0C, typeof(EventSubscribeToken) },
                // 0x0D: 13-byte payload (3 ints + 1 byte) — matches DebugInfoToken's shape.
                // Decompile-side, DebugInfo tokens are skipped, so the bytes are consumed
                // without disrupting the surrounding statement.
                { 0x0D, typeof(DebugInfoToken) },
                { 0x0E, typeof(IteratorNextToken) },
                { 0x0F, typeof(FinalFunctionTokenRL) }, // new-build replacement for 0x38 super-call shape; FinalFunctionTokenRL now reads the mandatory skip byte after UFunction*; see RL_OPCODE_ANALYSIS.md
                { 0x10, typeof(ExtendedNativeFunctionToken) },
                { 0x11, typeof(ContextToken) },
                { 0x12, typeof(EatReturnValueToken) },
                { 0x13, typeof(NoObjectToken) },
                { 0x14, typeof(DynamicArrayLengthToken) },
                { 0x15, typeof(InterfaceContextToken) },
                { 0x16, typeof(JumpIfNotToken) },
                { 0x17, typeof(DelegatePropertyToken) },
                { 0x18, typeof(ConditionalToken) },
                { 0x19, typeof(InterfaceCastToken) },
                { 0x1A, typeof(InstanceVariableToken) },
                { 0x1B, typeof(MetaClassCastToken) },
                { 0x1C, typeof(RotationConstToken) },
                { 0x1D, typeof(BoolVariableToken) }, // 1-sub-token wrapper inferred from 1D-1D self-pair frequency; see RL_OPCODE_ANALYSIS.md
                { 0x1E, typeof(NothingToken) },
                { 0x1F, typeof(LetBoolToken) },
                { 0x20, typeof(ReturnToken) },
                // 0x21: tied across many candidates at +43 clean. DynamicArrayIterator
                // shape (one extra trailing sub) edged out simpler shapes by 1 — pick it
                // because the structure aligns with iterator-style usage seen in surrounding
                // bytecode (Actor.PlayParticleEffect / Pawn.PostBeginPlay).
                { 0x21, typeof(DynamicArrayIteratorToken) },
                { 0x22, typeof(DelegateFunctionToken) },
                { 0x23, typeof(NewToken) },
                { 0x24, typeof(DeprecatedTokenRL) },
                { 0x25, typeof(FalseToken) },
                { 0x26, typeof(EndParmValueToken) },
                { 0x27, typeof(ByteConstToken) },
                // 0x28: highest-frequency BadToken in baseline RL — score-mapping showed
                // LocalVariableToken (4-byte UProperty*) gives +750 clean / -2835 bad. All
                // tested 4/8-byte shapes scored identically; LocalVariable picked as the
                // simplest variable-style leaf.
                { 0x28, typeof(LocalVariableToken) },
                { 0x29, typeof(DynamicArraySortToken) },
                { 0x2A, typeof(IntOneToken) },
                // 0x2B: 12-byte payload — VectorConst/RotationConst share this shape.
                // VectorConst chosen as the more common case in scripted code.
                { 0x2B, typeof(VectorConstToken) },
                // 0x2C: matches DebugInfoToken's 13-byte shape.
                { 0x2C, typeof(DebugInfoToken) },
                // 0x2D: tied across EventSubscribe/EventUnsubscribe and several other
                // (FNAME + 1 sub) shapes — picked EventUnsubscribeToken as a guess.
                { 0x2D, typeof(EventUnsubscribeToken) },
                { 0x2E, typeof(CaseToken) },
                { 0x2F, typeof(GotoLabelToken) },
                { 0x30, typeof(NativeParameterToken) },
                { 0x31, typeof(InstanceDelegateToken) },
                // 0x32: 12-byte payload — Vector/RotationConst shape.
                { 0x32, typeof(VectorConstToken) },
                { 0x33, typeof(AssertTokenRL) },
                { 0x34, typeof(LetDelegateToken) },
                // 0x35: FNAME (8-byte) shape — tied across NameConst / Virtual / Global
                // function. Picked NameConstToken (simplest leaf).
                { 0x35, typeof(NameConstToken) },
                { 0x36, typeof(VectorConstToken) },
                { 0x37, typeof(FloatConstToken) },
                { 0x38, typeof(FinalFunctionTokenRL) },
                { 0x39, typeof(GlobalFunctionToken) },
                // 0x3A: matches DebugInfoToken's 13-byte shape — second-largest win
                // (+52 clean) of all the experimental mappings.
                { 0x3A, typeof(DebugInfoToken) },
                { 0x3B, typeof(ObjectConstToken) },
                { 0x3C, typeof(TwoStepToken) },
                // 0x3D: tied across many FNAME-shape (8-byte) candidates — Virtual /
                // Global / Name / InstanceDelegate. Pick VirtualFunctionToken since the
                // bytes around 0x3D often look like an inline function call.
                { 0x3D, typeof(VirtualFunctionToken) },
                { 0x3E, typeof(IntZeroToken) },
                // 0x3F: 12-byte payload — Vector/RotationConst shape.
                { 0x3F, typeof(VectorConstToken) },
                { 0x40, typeof(BoolVariableToken) },
                { 0x41, typeof(ClassContextToken) },
                { 0x42, typeof(DefaultParameterToken) },
                // 0x43: matches DebugInfoToken's 13-byte shape — strong win (+69 clean).
                { 0x43, typeof(DebugInfoToken) },
                { 0x44, typeof(UnicodeStringConstToken) },
                { 0x45, typeof(EndFunctionParmsToken) },
                { 0x46, typeof(DelegateCmpEqToken) },
                { 0x47, typeof(StopToken) },
                { 0x48, typeof(EventSubscribeToken) },
                { 0x49, typeof(FilterEditorOnlyToken) },
                { 0x4A, typeof(DynamicArrayFindToken) },
                { 0x4B, typeof(DelegateCmpNeToken) },
                { 0x4C, typeof(EndFunctionParmsToken) },
                { 0x4D, typeof(LocalVariableToken) },
                { 0x4E, typeof(StructCmpNeToken) },
                { 0x4F, typeof(ObjectConstToken) },
                // 0x50: very common (1081× before resolution). UnicodeStringConstToken
                // wins clearly — variable-length-prefixed string. Empirical winner by
                // a wide margin over leaf/Const shapes.
                { 0x50, typeof(UnicodeStringConstToken) },
                { 0x51, typeof(TrueToken) },
                { 0x52, typeof(DynamicCastToken) },
                // 0x53: BadToken in baseline RL — score-mapping showed +112 clean / -230
                // bad. Tied across all candidates (the byte appears in patterns where any
                // shape parses cleanly). LocalVariableToken picked for consistency.
                { 0x53, typeof(LocalVariableToken) },
                // 0x54: 1-sub-token wrapper — tied across EatReturnValue, several Casts,
                // ReturnNothing. Pick EatReturnValueToken (simplest pass-through).
                { 0x54, typeof(EatReturnValueToken) },
                // 0x55: was IteratorPopToken (which renders as "break"), but the byte
                // appears throughout non-iterator contexts (right after super-calls etc.)
                // and was injecting spurious "break;" statements. Mapping to NothingToken
                // (1-byte silent leaf) eliminates the noise. RL appears to use this byte
                // as a no-op statement separator. All 25,038 functions still parse-clean.
                { 0x55, typeof(NothingToken) },
                { 0x56, typeof(DebugInfoToken) },
                // 0x57: BadToken in baseline RL — tied across all candidates.
                { 0x57, typeof(LocalVariableToken) },
                { 0x58, typeof(StringConstToken) },
                { 0x59, typeof(VirtualFunctionToken) },
                // 0x5A: many candidates tied at the same clean count.
                // LocalVariableToken (4-byte UProperty*) chosen by frequency.
                { 0x5A, typeof(LocalVariableToken) },
                // 0x5B: 12-byte payload — Vector/RotationConst shape.
                { 0x5B, typeof(VectorConstToken) },
                { 0x5C, typeof(JumpToken) },
                { 0x5D, typeof(StepToken) },
                { 0x5E, typeof(AlternativeExtendedNativeFunctionToken) },
                // 0x5F: BadToken in baseline RL — tied across all candidates.
                { 0x5F, typeof(LocalVariableToken) },
                { 0x60, typeof(EmptyParmToken) },
                { 0x61, typeof(EmptyDelegateToken) },
                // 0x62: AssertToken-like shape (1 sub + small payload). Tied across many
                // 1-sub shapes; AssertToken edged by 1 clean function.
                { 0x62, typeof(AssertToken) },
                { 0x63, typeof(IntConstByteToken) },
                { 0x64, typeof(SwitchToken) },
                { 0x65, typeof(LetToken) },
                { 0x66, typeof(SelfToken) },
                { 0x67, typeof(SkipFunctionTokenRL) },
                // 0x68: FNAME (8-byte) shape — tied across many (NameConst / Virtual /
                // Global / Int64Const / Vector / DebugInfo). Picked NameConstToken.
                { 0x68, typeof(NameConstToken) },
                { 0x69, typeof(DelegateCmpNeToken) },
                { 0x6A, typeof(EventUnsubscribeToken) },
                // 0x6B: top frequency (1114× originally). 4-byte UProperty* shape —
                // tied across LocalVariable / InstanceVariable / DefaultVariable. Picked
                // LocalVariableToken (most common in cooked code).
                { 0x6B, typeof(LocalVariableToken) },
                { 0x6C, typeof(ContextInitTokenRL) },
                { 0x6D, typeof(OutVariableToken) },
                // 0x6E: FNAME (8-byte) shape — tied across NameConst / Virtual / Global /
                // InstanceDelegate / DelegateProperty. Picked NameConstToken (simplest).
                { 0x6E, typeof(NameConstToken) },
                { 0x6F, typeof(ConditionalToken) },

                // Special RL Native tokens
                { 0x71, typeof(ExAlternativeExtendedNativeFunctionTokenRL) },
                { 0x82, typeof(AndTokenRL) },
                { 0x84, typeof(OrTokenRL) },
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
