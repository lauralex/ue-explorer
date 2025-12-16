using System;
using UELib.Branch.UE3.RL.Tokens;
using UELib.Branch.UE3.SA2.Tokens;
using UELib.Core;
using UELib.Core.Tokens;
using UELib.Tokens;
using static UELib.Core.UStruct.UByteCodeDecompiler;
using EventSubscribeToken = UELib.Branch.UE3.RL.Tokens.EventSubscribeToken;
using EventUnsubscribeToken = UELib.Branch.UE3.RL.Tokens.EventUnsubscribeToken;

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
                { 0x00, typeof(AssertTokenRL) }, // Was NothingToken
                { 0x01, typeof(AlternativeExtendedNativeFunctionToken) }, // Was StateVariableToken
                { 0x02, typeof(InterfaceCastToken) }, // Was IntConstToken
                { 0x03, typeof(SwitchToken) }, // Was StructCmpEqToken
                { 0x04, typeof(FloatConstToken) }, // Was EndOfScriptToken
                { 0x05, typeof(IntZeroToken) }, // Was StructMemberToken
                { 0x06, typeof(VirtualFunctionToken) }, // Was NameConstToken
                { 0x07, typeof(EventUnsubscribeToken) }, // Was ReturnNothingToken
                { 0x08, typeof(ExtendedNativeFunctionToken) },
                { 0x09, typeof(BadToken) }, // Was DefaultVariableToken
                { 0x0A, typeof(StateFunctionTokenRL) }, // Was DelegateCmpEqToken
                { 0x0B, typeof(BadToken) }, // Was DynamicArrayElementToken
                { 0x0C, typeof(JumpIfNotToken) }, // Was EventSubscribeToken
                { 0x0D, typeof(NothingToken) }, // Was UnresolvedToken
                { 0x0E, typeof(EndParmValueToken) }, // Was IteratorNextToken
                { 0x0F, typeof(BadToken) }, // Was UnresolvedToken
                { 0x10, typeof(IntConstByteToken) }, // Was ExtendedNativeFunctionToken
                { 0x11, typeof(SkipToken) }, // Was ContextToken
                { 0x12, typeof(LabelTableToken) }, // Was EatReturnValueToken
                { 0x13, typeof(BadToken) }, // Was NoObjectToken
                { 0x14, typeof(EmptyDelegateToken) }, // Was DynamicArrayLengthToken
                { 0x15, typeof(StateVariableToken) }, // Was InterfaceContextToken
                { 0x16, typeof(BadToken) }, // Was JumpIfNotToken
                { 0x17, typeof(BadToken) }, // Was DelegatePropertyToken
                { 0x18, typeof(DelegateCmpNeToken) }, // Was ConditionalToken
                { 0x19, typeof(VectorConstToken) }, // Was InterfaceCastToken
                { 0x1A, typeof(DebugInfoToken) }, // Was InstanceVariableToken
                { 0x1B, typeof(IntConstToken) }, // Was MetaClassCastToken
                { 0x1C, typeof(NativeParameterToken) }, // Was RotationConstToken
                { 0x1D, typeof(NewToken) }, // Was UnresolvedToken
                { 0x1E, typeof(NameConstToken) }, // Was NothingToken
                { 0x1F, typeof(FinalFunctionTokenRL) }, // Was LetBoolToken
                { 0x20, typeof(DelegatePropertyToken) }, // Was ReturnToken
                { 0x21, typeof(DynamicArrayElementToken) }, // Was UnresolvedToken
                { 0x22, typeof(FilterEditorOnlyToken) }, // Was DelegateFunctionToken
                { 0x23, typeof(ConditionalToken) }, // Was NewToken
                { 0x24, typeof(DelegateCmpEqToken) }, // Was DeprecatedToken
                { 0x25, typeof(EndOfScriptToken) }, // Was FalseToken
                { 0x26, typeof(UStruct.UByteCodeDecompiler.DelegateFunctionToken) }, // Was EndParmValueToken
                { 0x27, typeof(StructMemberToken) }, // Was ByteConstToken
                { 0x28, typeof(MetaClassCastToken) }, // Was BadToken
                { 0x29, typeof(BadToken) }, // Was DynamicArraySortToken
                { 0x2A, typeof(BadToken) }, // Was IntOneToken
                { 0x2B, typeof(ObjectConstToken) }, // Was UnresolvedToken
                { 0x2C, typeof(BoolVariableToken) }, // Was UnresolvedToken
                { 0x2D, typeof(JumpToken) }, // Was UnresolvedToken
                { 0x2E, typeof(TrueToken) }, // Was CaseToken
                { 0x2F, typeof(BadToken) }, // Was GotoLabelToken
                { 0x30, typeof(FalseToken) }, // Was NativeParameterToken
                { 0x31, typeof(BadToken) }, // Was InstanceDelegateToken
                { 0x32, typeof(EndFunctionParmsToken) }, // Was UnresolvedToken
                { 0x33, typeof(FilterEditorOnlyToken) }, // It's a special FilterEditorOnly with struct/property handling. Was AssertTokenRL
                { 0x34, typeof(IntOneToken) }, // Was LetDelegateToken
                { 0x35, typeof(BadToken) }, // Was UnresolvedToken
                { 0x36, typeof(BadToken) }, // Was VectorConstToken
                { 0x37, typeof(DefaultParameterToken) }, // Was FloatConstToken
                { 0x38, typeof(BadToken) }, // Was FinalFunctionTokenRL
                { 0x39, typeof(BadToken) }, // Was GlobalFunctionToken
                { 0x3A, typeof(UnresolvedToken) }, // Was UnresolvedToken
                { 0x3B, typeof(InstanceDelegateToken) }, // Was ObjectConstToken
                { 0x3C, typeof(ContextInitTokenRL) }, // Was TwoStepToken
                { 0x3D, typeof(OutVariableToken) }, // Was UnresolvedToken
                { 0x3E, typeof(UnicodeStringConstToken) }, // Was IntZeroToken
                { 0x3F, typeof(CaseToken) }, // Was UnresolvedToken
                { 0x40, typeof(StructCmpEqToken) }, // Was BoolVariableToken
                { 0x41, typeof(LetDelegateToken) }, // Was ClassContextToken
                { 0x42, typeof(LocalVariableToken) }, // Was DefaultParameterToken
                { 0x43, typeof(InstanceVariableToken) }, // Was UnresolvedToken
                { 0x44, typeof(BadToken) }, // Was UnicodeStringConstToken
                { 0x45, typeof(SkipFunctionTokenRL) }, // Was EndFunctionParmsToken
                { 0x46, typeof(BadToken) }, // Was DelegateCmpEqToken
                { 0x47, typeof(BadToken) }, // Was StopToken
                { 0x48, typeof(StepToken) }, // Was EventSubscribeToken
                { 0x49, typeof(DynamicCastToken) }, // Was FilterEditorOnlyToken
                { 0x4A, typeof(IteratorToken) }, // Was DynamicArrayFindToken
                { 0x4B, typeof(LetToken) }, // Was DelegateCmpNeToken
                { 0x4C, typeof(ByteConstToken) }, // Was EndFunctionParmsToken
                { 0x4D, typeof(RotationConstToken) }, // Was LocalVariableToken
                { 0x4E, typeof(EventSubscribeToken) }, // Was StructCmpNeToken
                { 0x4F, typeof(StructCmpNeToken) }, // Was ObjectConstToken
                { 0x50, typeof(BadToken) }, // Was UnresolvedToken
                { 0x51, typeof(DefaultVariableToken) }, // Was TrueToken
                { 0x52, typeof(ContextToken) }, // Was DynamicCastToken
                { 0x53, typeof(DelegateCmpEqToken) }, // Was BadToken
                { 0x54, typeof(BadToken) }, // Was UnresolvedToken
                { 0x55, typeof(TwoStepToken) }, // Was IteratorPopToken
                { 0x56, typeof(BadToken) }, // Was DebugInfoToken
                { 0x57, typeof(EventUnsubscribeToken) }, // Was BadToken
                { 0x58, typeof(EatReturnValueToken) }, // Was StringConstToken
                { 0x59, typeof(SelfToken) }, // Was VirtualFunctionToken
                { 0x5A, typeof(ReturnToken) }, // Was UnresolvedToken
                { 0x5B, typeof(DynamicArrayFindToken) }, // Was UnresolvedToken
                { 0x5C, typeof(ConditionalToken) }, // Was JumpToken
                { 0x5D, typeof(GotoLabelToken) }, // Was StepToken
                { 0x5E, typeof(DynamicArrayLengthToken) }, // Was AlternativeExtendedNativeFunctionToken
                { 0x5F, typeof(NoObjectToken) }, // Was BadToken
                { 0x60, typeof(StringConstToken) }, // Was EmptyParmToken
                { 0x61, typeof(DelegateCmpNeToken) }, // Was EmptyDelegateToken
                { 0x62, typeof(IteratorPopToken) }, // Was UnresolvedToken
                { 0x63, typeof(BadToken) }, // Was IntConstByteToken
                { 0x64, typeof(EmptyParmToken) }, // Was SwitchToken
                { 0x65, typeof(BadToken) }, // Was LetToken
                { 0x66, typeof(InterfaceContextToken) }, // Was SelfToken
                { 0x67, typeof(DynamicArraySortToken) }, // Was SkipFunctionTokenRL
                { 0x68, typeof(StopToken) }, // Was UnresolvedToken
                { 0x69, typeof(ClassContextToken) }, // Was DelegateCmpNeToken
                { 0x6A, typeof(GlobalFunctionToken) }, // Was EventUnsubscribeToken
                { 0x6B, typeof(Int64ConstToken) }, // Was UnresolvedToken
                { 0x6C, typeof(IteratorNextToken) }, // Was ContextInitTokenRL
                { 0x6D, typeof(BadToken) }, // Was OutVariableToken
                // Prob LetDelegate
                { 0x6E, typeof(ReturnNothingToken) }, // Was UnresolvedToken
                { 0x6F, typeof(LetBoolToken) }, // Was ConditionalToken

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
