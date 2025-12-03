using UELib.Core;

namespace UELib.Branch.UE3.RL.Tokens;
public class AllControllersJumpTokenRL : UStruct.UByteCodeDecompiler.JumpIfNotToken
{
    public override void Deserialize(IUnrealStream stream)
    {
#if UE4
        if (stream.UE4Version > 0)
        {
            CodeOffset = (ushort)stream.ReadInt32();
            Decompiler.AlignSize(sizeof(int));
            return;
        }
#endif
        CodeOffset = stream.ReadUInt16();
        Decompiler.AlignSize(sizeof(ushort));

    }

    public override string Decompile()
    {
        string condition = "PLACEHOLDER";

        // Check if we are jumping to the start of a JumpIfNot token.
        // if true, we can assume that this (If) statement is contained within a loop.
        IsLoop = false;
        for (int i = Decompiler.CurrentTokenIndex + 1; i < Decompiler.DeserializedTokens.Count; ++i)
        {
            if (Decompiler.DeserializedTokens[i] is UStruct.UByteCodeDecompiler.JumpToken jt && jt.CodeOffset == Position)
            {
                IsLoop = true;
                break;
            }
        }

        SetEndComment();
        if (IsLoop)
        {
            Decompiler.PreComment += " [Loop If]";
        }

        string output;
        if ((CodeOffset & ushort.MaxValue) < Position)
        {
            string labelName = UDecompilingState.OffsetLabelName(CodeOffset);
            var gotoStatement = $"{UDecompilingState.Tabs}{UnrealConfig.Indention}goto {labelName}";
            // Inverse condition only here as we're explicitly jumping while other cases create proper scopes 
            output = $"if(!({condition}))\r\n{gotoStatement}";
            Decompiler.MarkSemicolon();
            return output;
        }

        output = /*(IsLoop ? "while" : "if") +*/ $"if({condition})";
        RemoveSemicolon();


        if (IsLoop == false)
        {
            int i;
            for (i = Decompiler.DeserializedTokens.IndexOf(this);
                 i < Decompiler.DeserializedTokens.Count &&
                 (Decompiler.DeserializedTokens[i]).Position < CodeOffset;
                 i++)
            {
                // Seek to jump destination
            }

            var prevToken = Decompiler.DeserializedTokens[i - 1];
            var elseStartToken = Decompiler.DeserializedTokens[i];

            // Test to see if this JumpIfNotToken is the if part of an if-else nest
            if (elseStartToken.Position == CodeOffset && prevToken is UStruct.UByteCodeDecompiler.JumpToken ifEndJump)
            {
                if (elseStartToken is UStruct.UByteCodeDecompiler.CaseToken && ifEndJump.JumpsOutOfSwitch())
                {
                    // It's an if containing a break. When the if is *not* taken, execution continues inside of a case below
                    ifEndJump.MarkedAsSwitchBreak = true;
                }
                else if (elseStartToken.Position == CodeOffset &&
                         ifEndJump.CodeOffset != elseStartToken.Position)
                {
                    // Most likely an if-else, mark it as such and let the rest of the logic figure it out further
                    int begin = Position;
                    const UStruct.UByteCodeDecompiler.NestManager.Nest.NestType type = UStruct.UByteCodeDecompiler.NestManager.Nest.NestType.If;
                    Decompiler.Nester.Nests.Add(new UStruct.UByteCodeDecompiler.NestManager.NestBegin
                    { Position = begin, Type = type, Creator = this });
                    var nestEnd = new UStruct.UByteCodeDecompiler.NestManager.NestEnd
                    {
                        Position = CodeOffset,
                        Type = type,
                        Creator = this,
                        HasElseNest = ifEndJump,
                    };
                    Decompiler.Nester.Nests.Add(nestEnd);

                    var outdatedLink = ifEndJump.LinkedIfNest;
                    // This will hint to the jump token that it is likely an else scope
                    ifEndJump.LinkedIfNest = nestEnd;
                    // Let's make sure we break any previous link this jump had with other 'if's
                    // the most recent is the most accurate
                    if (outdatedLink != null)
                        outdatedLink.HasElseNest = null;
                    return output;
                }
            }
        }

        Decompiler.Nester.AddNest(IsLoop
                ? UStruct.UByteCodeDecompiler.NestManager.Nest.NestType.Loop
                : UStruct.UByteCodeDecompiler.NestManager.Nest.NestType.If,
            Position, CodeOffset, this
        );
        return output;
    }
}
