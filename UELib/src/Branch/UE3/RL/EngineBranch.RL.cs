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
                { 0x08, typeof(UnresolvedToken) },
                { 0x09, typeof(DefaultVariableToken) },
                { 0x0A, typeof(DelegateCmpEqToken) },
                { 0x0B, typeof(DynamicArrayElementToken) },
                { 0x0C, typeof(EventSubscribeToken) },
                { 0x0D, typeof(UnresolvedToken) },
                { 0x0E, typeof(IteratorNextToken) },
                { 0x0F, typeof(UnresolvedToken) },
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
                { 0x1D, typeof(UnresolvedToken) },
                { 0x1E, typeof(NothingToken) },
                { 0x1F, typeof(LetBoolToken) },
                { 0x20, typeof(ReturnToken) },
                { 0x21, typeof(UnresolvedToken) },
                { 0x22, typeof(DelegateFunctionToken) },
                { 0x23, typeof(NewToken) },
                { 0x24, typeof(DeprecatedTokenRL) },
                { 0x25, typeof(FalseToken) },
                { 0x26, typeof(EndParmValueToken) },
                { 0x27, typeof(ByteConstToken) },
                { 0x28, typeof(BadToken) },
                { 0x29, typeof(DynamicArraySortToken) },
                { 0x2A, typeof(IntOneToken) },
                { 0x2B, typeof(UnresolvedToken) },
                { 0x2C, typeof(UnresolvedToken) },
                { 0x2D, typeof(UnresolvedToken) },
                { 0x2E, typeof(CaseToken) },
                { 0x2F, typeof(GotoLabelToken) },
                { 0x30, typeof(NativeParameterToken) },
                { 0x31, typeof(InstanceDelegateToken) },
                { 0x32, typeof(UnresolvedToken) },
                { 0x33, typeof(AssertTokenRL) },
                { 0x34, typeof(LetDelegateToken) },
                { 0x35, typeof(UnresolvedToken) },
                { 0x36, typeof(VectorConstToken) },
                { 0x37, typeof(FloatConstToken) },
                { 0x38, typeof(FinalFunctionToken) },
                { 0x39, typeof(GlobalFunctionToken) },
                { 0x3A, typeof(UnresolvedToken) },
                { 0x3B, typeof(ObjectConstToken) },
                { 0x3C, typeof(TwoStepToken) },
                { 0x3D, typeof(UnresolvedToken) },
                { 0x3E, typeof(IntZeroToken) },
                { 0x3F, typeof(UnresolvedToken) },
                { 0x40, typeof(BoolVariableToken) },
                { 0x41, typeof(ClassContextToken) },
                { 0x42, typeof(DefaultParameterToken) },
                { 0x43, typeof(UnresolvedToken) },
                { 0x44, typeof(UnicodeStringConstToken) },
                { 0x45, typeof(EndFunctionParmsToken) },
                { 0x46, typeof(DelegateCmpEqToken) },
                { 0x47, typeof(StopToken) },
                { 0x48, typeof(EventUnsubscribeToken) },
                { 0x49, typeof(FilterEditorOnlyToken) },
                { 0x4A, typeof(DynamicArrayFindToken) },
                { 0x4B, typeof(DelegateCmpNeToken) },
                { 0x4C, typeof(EndFunctionParmsToken) },
                { 0x4D, typeof(LocalVariableToken) },
                { 0x4E, typeof(StructCmpNeToken) },
                { 0x4F, typeof(ObjectConstToken) },
                { 0x50, typeof(UnresolvedToken) },
                { 0x51, typeof(TrueToken) },
                { 0x52, typeof(DynamicCastToken) },
                { 0x53, typeof(BadToken) },
                { 0x54, typeof(UnresolvedToken) },
                { 0x55, typeof(IteratorPopToken) },
                { 0x56, typeof(DebugInfoToken) },
                { 0x57, typeof(BadToken) },
                { 0x58, typeof(StringConstToken) },
                { 0x59, typeof(VirtualFunctionToken) },
                { 0x5A, typeof(UnresolvedToken) },
                { 0x5B, typeof(UnresolvedToken) },
                { 0x5C, typeof(JumpToken) },
                { 0x5D, typeof(StepToken) },
                { 0x5E, typeof(AlternativeExtendedNativeFunctionToken) },
                { 0x5F, typeof(BadToken) },
                { 0x60, typeof(EmptyParmToken) },
                { 0x61, typeof(EmptyDelegateToken) },
                { 0x62, typeof(UnresolvedToken) },
                { 0x63, typeof(IntConstByteToken) },
                { 0x64, typeof(SwitchToken) },
                { 0x65, typeof(LetToken) },
                { 0x66, typeof(SelfToken) },
                { 0x67, typeof(SkipFunctionTokenRL) },
                { 0x68, typeof(UnresolvedToken) },
                { 0x69, typeof(DelegateCmpNeToken) },
                { 0x6A, typeof(EventUnsubscribeToken) },
                { 0x6B, typeof(UnresolvedToken) },
                { 0x6C, typeof(BadToken) },
                { 0x6D, typeof(OutVariableToken) },
                // Prob LetDelegate
                { 0x6E, typeof(UnresolvedToken) },
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
