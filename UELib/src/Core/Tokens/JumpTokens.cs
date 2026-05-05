using System;
using System.Collections.Generic;
using UELib.Branch;
using UELib.ObjectModel.Annotations;
using UELib.Tokens;

namespace UELib.Core
{
    public partial class UStruct
    {
        public partial class UByteCodeDecompiler
        {
            [ExprToken(ExprToken.Return)]
            public class ReturnToken : Token
            {
                public override void Deserialize(IUnrealStream stream)
                {
                    if (Decompiler._Buffer.Version < (uint)PackageObjectLegacyVersion.ReturnExpressionAddedToReturnToken)
                    {
                        return;
                    }

                    // Expression
                    DeserializeNext();
                }

                public override string Decompile()
                {
                    // HACK: for case's that end with a return instead of a break.
                    if (Decompiler.IsInNest(NestManager.Nest.NestType.Default) != null)
                    {
                        Decompiler._Nester.TryAddNestEnd(NestManager.Nest.NestType.Switch, Position + Size);
                    }

                    Decompiler.MarkSemicolon();
                    if (Decompiler._Buffer.Version < (uint)PackageObjectLegacyVersion.ReturnExpressionAddedToReturnToken)
                    {
                        // FIXME: Transport the emitted "ReturnValue = Expression" over here.
                        return "return";
                    }

                    string returnValue = DecompileNext();
                    return "return" + (returnValue.Length != 0
                        ? " " + returnValue
                        : string.Empty);
                }
            }

            [ExprToken(ExprToken.ReturnNothing)]
            public class ReturnNothingToken : EatReturnValueToken
            {
                public override string Decompile()
                {
                    // HACK: for case's that end with a return instead of a break.
                    if (Decompiler.IsInNest(NestManager.Nest.NestType.Default) != null)
                    {
                        Decompiler._Nester.TryAddNestEnd(NestManager.Nest.NestType.Switch, Position + Size);
                    }

                    // EX_ReturnNothing is the compiler-emitted safety net at the end of any
                    // value-returning function (zeroes the OUT param so control reaching the
                    // end of a non-void function still returns a deterministic value). It
                    // corresponds to no user-visible source — if the user wrote `return X;`
                    // the compiler emits EX_Return + sub-expr instead. Render nothing so the
                    // bare property name doesn't leak into the output as an orphan statement.
                    return string.Empty;
                }
            }

            [ExprToken(ExprToken.GotoLabel)]
            public class GotoLabelToken : Token
            {
                public override void Deserialize(IUnrealStream stream)
                {
                    // Expression
                    DeserializeNext();
                }

                public override string Decompile()
                {
                    Decompiler.MarkSemicolon();
                    return $"goto {DecompileNext()}";
                }
            }

            [ExprToken(ExprToken.Jump)]
            public class JumpToken : Token
            {
                public bool MarkedAsSwitchBreak;
                public NestManager.NestEnd LinkedIfNest;
                public ushort CodeOffset;

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

                public override void PostDeserialized()
                {
                    if (GetType() == typeof(JumpToken))
                        Decompiler._Labels.Add
                        (
                            new ULabelEntry
                            {
                                Name = UDecompilingState.OffsetLabelName(CodeOffset),
                                Position = CodeOffset
                            }
                        );
                }

                protected void SetEndComment()
                {
                    Decompiler.PreComment = $"// End:0x{CodeOffset:X2}";
                }

                private void SetStatementComment(string statement)
                {
                    Decompiler.PreComment = $"// [{statement}]";
                }

                protected int GetForEachNestEnd()
                {
                    int endPosition = CodeOffset;
                    bool sawTerminatorStart = false;

                    for (int i = Decompiler.DeserializedTokens.IndexOf(this) + 1;
                         i < Decompiler.DeserializedTokens.Count;
                         i++)
                    {
                        var token = Decompiler.DeserializedTokens[i];
                        if (token.Position < CodeOffset)
                        {
                            continue;
                        }

                        if (!sawTerminatorStart && token.Position > CodeOffset
                            && token.Position - CodeOffset > 2)
                        {
                            break;
                        }

                        sawTerminatorStart = true;

                        if (token is IteratorNextToken or IteratorPopToken
                            || token is NothingToken && token.Size <= 1)
                        {
                            endPosition = token.Position + token.Size;
                            if (token is IteratorPopToken)
                            {
                                break;
                            }

                            continue;
                        }

                        break;
                    }

                    return endPosition;
                }

                protected void AddForEachNest()
                {
                    Decompiler._Nester.AddNest(NestManager.Nest.NestType.ForEach, Position, GetForEachNestEnd(), this);
                }

                protected void SuppressSemicolon()
                {
                    Decompiler._CanAddSemicolon = false;
                }

                /// <summary>
                /// FORMATION ISSUESSES:
                ///     1:(-> Logic remains the same)   (Continue) statements are decompiled to (Else) statements e.g.
                ///         -> Original
                ///         if( continueCondition )
                ///         {
                ///             continue;
                ///         }
                ///
                ///         // Actual code
                ///
                ///         -> Decompiled
                ///         if( continueCodition )
                ///         {
                ///         }
                ///         else
                ///         {
                ///             // Actual code
                ///         }
                ///
                ///
                ///     2:(-> ...)  ...
                ///         -> Original
                ///             ...
                ///         -> Decompiled
                ///             ...
                ///
                /// </summary>
                public override string Decompile()
                {
                    // Remove 'else' marking as long as other fallback are better suited
                    var tempLinkedIf = LinkedIfNest?.HasElseNest;
                    if (tempLinkedIf != null)
                    {
                        LinkedIfNest.HasElseNest = null;
                    }

                    // Break offset!
                    if (CodeOffset >= Position)
                    {
                        if (MarkedAsSwitchBreak
                            //==================We're inside a Case and at the end of it!
                            || JumpsOutOfSwitch() && Decompiler.IsInNest(NestManager.Nest.NestType.Case) != null
                            //==================We're inside a Default and at the end of it!
                            || Decompiler.IsInNest(NestManager.Nest.NestType.Default) != null)
                        {
                            NoJumpLabel();
                            SetEndComment();
                            // 'break' CodeOffset sits at the end of the switch,
                            // check that it doesn't exist already and add it
                            int switchEnd = Decompiler.IsInNest(NestManager.Nest.NestType.Default) != null
                                ? Position + Size
                                : CodeOffset;
                            Decompiler._Nester.TryAddNestEnd(NestManager.Nest.NestType.Switch, switchEnd);
                            Decompiler._CanAddSemicolon = true;
                            return "break";
                        }

                        if (Decompiler.IsWithinNest(NestManager.Nest.NestType.ForEach)?.Creator is IteratorToken
                            iteratorToken)
                        {
                            // Jumps to the end of the foreach ?
                            if (CodeOffset == iteratorToken.CodeOffset)
                            {
                                if (Decompiler.PreviousToken is IteratorNextToken)
                                {
                                    NoJumpLabel();
                                    return string.Empty;
                                }

                                NoJumpLabel();
                                SetEndComment();
                                Decompiler._CanAddSemicolon = true;
                                return "break";
                            }

                            if (Decompiler.TokenAt(CodeOffset) is IteratorNextToken)
                            {
                                NoJumpLabel();
                                SetEndComment();
                                Decompiler._CanAddSemicolon = true;
                                return "continue";
                            }
                        }
                        else if (Decompiler.IsWithinNest(NestManager.Nest.NestType.ForEach)?.Creator is DynamicArrayIteratorToken
                            dynamicIteratorToken)
                        {
                            // Jumps to the end of the foreach ?
                            if (CodeOffset == dynamicIteratorToken.CodeOffset)
                            {
                                if (Decompiler.PreviousToken is IteratorNextToken)
                                {
                                    NoJumpLabel();
                                    return string.Empty;
                                }

                                NoJumpLabel();
                                SetEndComment();
                                Decompiler._CanAddSemicolon = true;
                                return "break";
                            }

                            if (Decompiler.TokenAt(CodeOffset) is IteratorNextToken)
                            {
                                NoJumpLabel();
                                SetEndComment();
                                Decompiler._CanAddSemicolon = true;
                                return "continue";
                            }
                        }

                        if (Decompiler.IsWithinNest(NestManager.Nest.NestType.Loop)?.Creator is JumpToken destJump)
                        {
                            if (CodeOffset + 10 == destJump.CodeOffset)
                            {
                                SetStatementComment("Explicit Continue");
                                goto gotoJump;
                            }

                            if (CodeOffset == destJump.CodeOffset)
                            {
                                SetStatementComment("Explicit Break");
                                goto gotoJump;
                            }
                        }

                        if (tempLinkedIf != null)
                        {
                            // Would this potential else scope break out of one of its parent scope
                            foreach (var nest in Decompiler._Nester.Nests)
                            {
                                if (nest is NestManager.NestEnd outerNestEnd
                                    && CodeOffset > outerNestEnd.Position
                                    // It's not this if-else scope
                                    && LinkedIfNest.Creator != outerNestEnd.Creator)
                                {
                                    // this is more likely a continue within a for(;;) loop
                                    SetStatementComment("Explicit Continue");
                                    goto gotoJump;
                                }
                            }

                            // this is indeed the else part of an if-else, re-instate the link
                            // and let nest decompilation handle the rest
                            LinkedIfNest.HasElseNest = tempLinkedIf;
                            NoJumpLabel();
                            Decompiler._CanAddSemicolon = false;
                            return "";
                        }

                        // This can be inaccurate if the source goto jumps from within a case to in the middle of a default
                        // If that's the case the nest decompilation process should spew comments about it
                        if (JumpsOutOfSwitch())
                        {
                            NoJumpLabel();
                            SetEndComment();
                            // 'break' CodeOffset sits at the end of the switch,
                            // check that it doesn't exist already and add it
                            Decompiler._Nester.TryAddNestEnd(NestManager.Nest.NestType.Switch, CodeOffset);

                            Decompiler._CanAddSemicolon = true;
                            return "break";
                        }
                    }

                    if (CodeOffset < Position)
                    {
                        // Suppress only the actual loop back-edge — the JumpIfNot's
                        // detection stashed it on `LoopBackEdge`. Any OTHER backward goto
                        // inside the loop body (e.g. a `continue` statement, which compiles
                        // to a backward jump to the loop start) must still render.
                        if (Decompiler.IsWithinNest(NestManager.Nest.NestType.Loop)?.Creator is JumpIfNotToken
                            loopJumpIfNot && loopJumpIfNot.LoopBackEdge == this)
                        {
                            NoJumpLabel();
                            return "";
                        }

                        // Backward goto that's NOT the loop's own back-edge — most likely
                        // a `continue` (jump to test-expression load).
                        if (Decompiler.IsWithinNest(NestManager.Nest.NestType.Loop)?.Creator is JumpIfNotToken
                            loopOuter && CodeOffset <= loopOuter.Position)
                        {
                            NoJumpLabel();
                            Decompiler._CanAddSemicolon = true;
                            return "continue";
                        }

                        SetStatementComment("Loop Continue");
                    }

                gotoJump:
                    if (Position + Size == CodeOffset)
                    {
                        // Remove jump to next token
                        NoJumpLabel();
                        return "";
                    }

                    // This is an implicit GoToToken.
                    Decompiler._CanAddSemicolon = true;
                    return $"goto {UDecompilingState.OffsetLabelName(CodeOffset)}";
                }

                public bool JumpsOutOfSwitch()
                {
                    Token t;
                    for (int i = Decompiler.DeserializedTokens.IndexOf(this) + 1;
                         i < Decompiler.DeserializedTokens.Count &&
                         (t = Decompiler.DeserializedTokens[i]).Position <= CodeOffset;
                         i++)
                    {
                        // Skip switch nests
                        if (t is SwitchToken)
                        {
                            var switchBalance = 1;
                            for (i += 1;
                                 i < Decompiler.DeserializedTokens.Count && switchBalance > 0 &&
                                 (t = Decompiler.DeserializedTokens[i]).Position <= CodeOffset;
                                 i++)
                            {
                                if (t is CaseToken ct && ct.IsDefault)
                                    switchBalance -= 1;
                                else if (t is SwitchToken)
                                    switchBalance += 1;
                            }
                        }
                        else if (t is CaseToken ct && ct.IsDefault && CodeOffset > ct.Position)
                        {
                            return true;
                        }
                    }

                    return false;
                }

                private void NoJumpLabel()
                {
                    int i = Decompiler._TempLabels.FindIndex(p => p.entry.Position == CodeOffset);
                    if (i != -1)
                    {
                        var data = Decompiler._TempLabels[i];
                        if (data.refs == 1)
                        {
                            Decompiler._TempLabels.RemoveAt(i);
                        }
                        else
                        {
                            data.refs -= 1;
                            Decompiler._TempLabels[i] = data;
                        }
                    }
                }
            }

            [ExprToken(ExprToken.JumpIfNot)]
            public class JumpIfNotToken : JumpToken
            {
                public bool IsLoop;
                public JumpToken LoopBackEdge;

                protected void RemoveSemicolon()
                {
                    Decompiler._CanAddSemicolon = false;
                }

                private static bool IsExitLikeToken(Token t)
                {
                    if (t == null) return false;
                    var name = t.GetType().Name;
                    return name == "ContextAwareReturnTokenRL"
                        || name == "ReturnToken"
                        || name == "ReturnNothingToken";
                }

                public override void PostDeserialized()
                {
                    base.PostDeserialized();
                    // Add jump label for 'do until' jump pattern
                    if ((CodeOffset & ushort.MaxValue) < Position)
                    {
                        Decompiler._Labels.Add
                        (
                            new ULabelEntry
                            {
                                Name = UDecompilingState.OffsetLabelName(CodeOffset),
                                Position = CodeOffset
                            }
                        );
                    }
                }

                public override void Deserialize(IUnrealStream stream)
                {
                    // CodeOffset
                    base.Deserialize(stream);

                    // Condition
                    DeserializeNext();
                }

                public override string Decompile()
                {
                    string condition = DecompileNext();

                    // Detect `while(cond) { body }` back-edge: a JumpToken in or just past
                    // this if's body that jumps backward to at-or-before this JumpIfNot's
                    // position. Cooked UE3 places the back-edge IMMEDIATELY after the if's
                    // CodeOffset (one token past the closing brace), and aims at the
                    // test-expression's first load (typically a few bytes before the
                    // JumpIfNot opcode), so we accept any CodeOffset <= our Position.
                    IsLoop = false;
                    for (int i = Decompiler.CurrentTokenIndex + 1; i < Decompiler.DeserializedTokens.Count; ++i)
                    {
                        var bodyToken = Decompiler.DeserializedTokens[i];
                        // GetType() == typeof(JumpToken): only the unconditional Jump, not its
                        // subclasses (JumpIfNot, Case, Iterator) — those have their own semantics.
                        if (bodyToken.GetType() == typeof(JumpToken)
                            && bodyToken is JumpToken jt
                            && jt.CodeOffset <= Position
                            && jt.CodeOffset < jt.Position
                            && jt.Position <= CodeOffset + 10)
                        {
                            IsLoop = true;
                            break;
                        }
                        // Don't scan too far past this if — avoid false positives from later loops.
                        if (bodyToken.Position > CodeOffset + 10) break;
                    }

                    SetEndComment();

                    string output;
                    if ((CodeOffset & ushort.MaxValue) < Position)
                    {
                        string labelName = UDecompilingState.OffsetLabelName(CodeOffset);
                        var gotoStatement = $"{UDecompilingState.Tabs}{UnrealConfig.Indention}goto {labelName}";
                        // Inverse condition only here as we're explicitly jumping while other cases create proper scopes 
                        output = $"if(!({condition}))\r\n{gotoStatement}";
                        Decompiler._CanAddSemicolon = true;
                        return output;
                    }

                    output = $"{(IsLoop ? "while" : "if")}({condition})";
                    Decompiler._CanAddSemicolon = false;

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

                        // Bounds-check: if CodeOffset is past the last token's Position (e.g.
                        // truncated function body or an if jumping to the closing brace), `i`
                        // walks off the end. Skip the if-else detection in that case.
                        if (i <= 0 || i >= Decompiler.DeserializedTokens.Count)
                        {
                            goto addNest;
                        }

                        var prevToken = Decompiler.DeserializedTokens[i - 1];
                        var elseStartToken = Decompiler.DeserializedTokens[i];

                        // Test to see if this JumpIfNotToken is the if part of an if-else nest
                        if (elseStartToken.Position == CodeOffset && prevToken is JumpToken ifEndJump)
                        {
                            if (elseStartToken is CaseToken && ifEndJump.JumpsOutOfSwitch())
                            {
                                // It's an if containing a break. When the if is *not* taken, execution continues inside of a case below
                                ifEndJump.MarkedAsSwitchBreak = true;
                            }
                            else if (elseStartToken.Position == CodeOffset &&
                                     ifEndJump.CodeOffset != elseStartToken.Position)
                            {
                                // Most likely an if-else, mark it as such and let the rest of the logic figure it out further
                                int begin = Position;
                                const NestManager.Nest.NestType type = NestManager.Nest.NestType.If;
                                Decompiler._Nester.Nests.Add(new NestManager.NestBegin
                                { Position = begin, Type = type, Creator = this });
                                var nestEnd = new NestManager.NestEnd
                                {
                                    Position = CodeOffset,
                                    Type = type,
                                    Creator = this,
                                    HasElseNest = ifEndJump,
                                };
                                Decompiler._Nester.Nests.Add(nestEnd);

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

                addNest:
                    // Initialize Nester if null
                    Decompiler._Nester ??= new NestManager { Decompiler = Decompiler };

                    int nestEndPosition = CodeOffset;

                    // RL cooker bug: stored CodeOffset can be undercounted by some
                    // number of bytes due to 4→8 in-memory expansions in the
                    // condition or body. Two distinct shapes show up:
                    //
                    //   Case A — CodeOffset lands INSIDE JumpIfNot's own bytes
                    //   (CodeOffset > Position && CodeOffset < Position+Size).
                    //   Body is a single short statement immediately following.
                    //   Recover by ending just past the first sibling.
                    //
                    //   Case B — CodeOffset lands PAST JumpIfNot but doesn't
                    //   align with any sibling boundary; it falls mid-body-token
                    //   instead (typical when the body is a long native call
                    //   followed by a short `return X;` and the cooker undercount
                    //   places CodeOffset just inside the long call). Recover by
                    //   snapping to the next sibling boundary, then including
                    //   that sibling iff it's a return-like exit (the empirical
                    //   pattern of this RL cooker bug — see RegisterClient).
                    //
                    // The two cases need different semantics; merging them would
                    // either give case A an empty body or case B's exit-token
                    // would over-include. Keep them as parallel branches.
                    if (!IsLoop)
                    {
                        int afterJump = Position + Size;
                        int thisIdx = Decompiler.DeserializedTokens.IndexOf(this);
                        if (thisIdx >= 0)
                        {
                            // Case A
                            if (CodeOffset > Position && CodeOffset < afterJump)
                            {
                                for (int j = thisIdx + 1; j < Decompiler.DeserializedTokens.Count; j++)
                                {
                                    var t = Decompiler.DeserializedTokens[j];
                                    if (t == null) continue;
                                    if (t.Position >= afterJump)
                                    {
                                        nestEndPosition = t.Position + t.Size;
                                        break;
                                    }
                                }
                            }
                            // Case B
                            else if (CodeOffset >= afterJump)
                            {
                                int currentEnd = afterJump;
                                Token prevSibling = null;
                                for (int j = thisIdx + 1; j < Decompiler.DeserializedTokens.Count; j++)
                                {
                                    var t = Decompiler.DeserializedTokens[j];
                                    if (t == null) continue;
                                    // Skip tokens nested inside an earlier sibling.
                                    if (t.Position < currentEnd) continue;

                                    if (t.Position == CodeOffset)
                                    {
                                        // Aligned at a sibling boundary — original
                                        // CodeOffset is correct, no recovery needed.
                                        break;
                                    }
                                    if (t.Position > CodeOffset)
                                    {
                                        // CodeOffset overshot a sibling and landed
                                        // mid-next-sibling. Snap to start of `t`.
                                        nestEndPosition = t.Position;
                                        // Include `t` iff it's a return-like exit AND
                                        // the previous sibling (the one CodeOffset
                                        // landed inside) is NOT itself an exit. The
                                        // empirical pattern is "long non-exit body
                                        // statement + trailing short return"; if the
                                        // body is itself a return-with-expression,
                                        // the next sibling exit is OUTSIDE the if.
                                        if (IsExitLikeToken(t) && prevSibling != null && !IsExitLikeToken(prevSibling))
                                        {
                                            nestEndPosition = t.Position + t.Size;
                                        }
                                        break;
                                    }
                                    prevSibling = t;
                                    currentEnd = t.Position + t.Size;
                                }
                            }
                        }
                    }

                    if (IsLoop)
                    {
                        // Extend the Loop nest's end past the back-edge JumpToken so the
                        // closing `}` lands AFTER the back-edge, and the JumpToken's
                        // Decompile sees we're still inside a Loop nest (which lets it
                        // suppress the implicit-goto-back rendering). Stash the back-edge
                        // reference so JumpToken.Decompile can identify it precisely (not
                        // any backward goto inside the body — `continue` statements are
                        // also backward gotos to the loop start and must NOT be suppressed).
                        for (int i = Decompiler.CurrentTokenIndex + 1; i < Decompiler.DeserializedTokens.Count; ++i)
                        {
                            var t = Decompiler.DeserializedTokens[i];
                            if (t.Position < CodeOffset) continue;
                            if (t.GetType() == typeof(JumpToken)
                                && t is JumpToken jt
                                && jt.CodeOffset <= Position
                                && jt.CodeOffset < jt.Position)
                            {
                                nestEndPosition = jt.Position + jt.Size;
                                LoopBackEdge = jt;
                                break;
                            }
                            if (t.Position > CodeOffset + 10) break;
                        }
                    }

                    Decompiler._Nester.AddNest(IsLoop
                            ? NestManager.Nest.NestType.Loop
                            : NestManager.Nest.NestType.If,
                        Position, nestEndPosition, this
                    );
                    return output;
                }
            }

            [ExprToken(ExprToken.FilterEditorOnly)]
            public class FilterEditorOnlyToken : JumpToken
            {
                public override string Decompile()
                {
                    Decompiler._Nester.AddNest(NestManager.Nest.NestType.Scope, Position, CodeOffset);
                    SetEndComment();
                    return "filtereditoronly";
                }
            }

            [ExprToken(ExprToken.Switch)]
            public class SwitchToken : Token
            {
                public UField ExpressionField;
                public ushort PropertyType;

                public override void Deserialize(IUnrealStream stream)
                {
                    // Skip Tera (610)
                    if (stream.Version >= 611)
                    {
                        // Points to the object that was passed to the switch,
                        // beware that the followed token chain contains it as well!
                        stream.Read(out ExpressionField);
                        Decompiler.AlignObjectSize();
                    }

                    // FIXME: version
                    if ((stream.Version >= 536 && stream.Version <= 587)
#if DNF
                        || stream.Package.Build == UnrealPackage.GameBuild.BuildName.DNF
#endif
#if TERA
                        || stream.Package.Build == UnrealPackage.GameBuild.BuildName.Tera
#endif
                        )
                    {
                        PropertyType = stream.ReadUInt16();
                        Decompiler.AlignSize(sizeof(ushort));
                    }
                    else
                    {
                        PropertyType = stream.ReadByte();
                        Decompiler.AlignSize(sizeof(byte));
                    }

                deserialize:
                    // Expression
                    DeserializeNext();
                }

                /// <summary>
                /// FORMATION ISSUESSES:
                ///     1:(-> ...)  NestEnd is based upon the break in a default case, however a case does not always break/return,
                ///         causing that there will be no default with a break detected, thus no ending for the Switch block.
                ///
                ///         -> Original
                ///             Switch( A )
                ///             {
                ///                 case 0:
                ///                     CallA();
                ///             }
                ///
                ///             CallB();
                ///
                ///         -> Decompiled
                ///             Switch( A )
                ///             {
                ///                 case 0:
                ///                     CallA();
                ///                 default:    // End is detect of case 0 due some other hack :)
                ///                     CallB();
                /// </summary>
                public override string Decompile()
                {
                    Decompiler._Nester.AddNestBegin(NestManager.Nest.NestType.Switch, Position);

                    string expr = DecompileNext();
                    Decompiler._CanAddSemicolon = false; // In case the decompiled token was a function call
                    return $"switch({expr})";
                }
            }

            [ExprToken(ExprToken.Case)]
            public class CaseToken : JumpToken
            {
                public bool IsDefault => CodeOffset == ushort.MaxValue;

                public override void Deserialize(IUnrealStream stream)
                {
                    base.Deserialize(stream);
                    if (CodeOffset != ushort.MaxValue)
                    {
                        DeserializeNext(); // Condition
                    } // Else "Default:"
                }

                public override string Decompile()
                {
                    SetEndComment();
                    if (CodeOffset != ushort.MaxValue)
                    {
                        Decompiler._Nester.AddNest(NestManager.Nest.NestType.Case, Position, CodeOffset);
                        var output = $"case {DecompileNext()}:";
                        Decompiler._CanAddSemicolon = false;
                        return output;
                    }

                    Decompiler._Nester.AddNestBegin(NestManager.Nest.NestType.Default, Position, this);
                    Decompiler._CanAddSemicolon = false;
                    return "default:";
                }
            }

            [ExprToken(ExprToken.Iterator)]
            public class IteratorToken : JumpToken
            {
                protected void AddNest()
                {
                    Decompiler._Nester.AddNest(NestManager.Nest.NestType.ForEach, Position, GetForEachNestEnd(), this);
                }

                protected void RemoveSemicolon()
                {
                    Decompiler._CanAddSemicolon = false;
                }

                public override void Deserialize(IUnrealStream stream)
                {
                    DeserializeNext(); // Expression
                    base.Deserialize(stream);
                }

                public override string Decompile()
                {
                    AddNest();
                    SetEndComment();

                    // foreach FunctionCall
                    string expression = DecompileNext();
                    Decompiler._CanAddSemicolon = false; // Undo
                    return $"foreach {expression}";
                }
            }

            [ExprToken(ExprToken.DynArrayIterator)]
            public class DynamicArrayIteratorToken : JumpToken
            {
                public byte WithIndexParam;

                public override void Deserialize(IUnrealStream stream)
                {
                    // Expression
                    DeserializeNext();

                    // Item param
                    DeserializeNext();

                    WithIndexParam = stream.ReadByte();
                    Decompiler.AlignSize(sizeof(byte));

                    // Index param
                    DeserializeNext();

                    base.Deserialize(stream);
                }

                protected void DeserializeBase(IUnrealStream stream)
                {
                    base.Deserialize(stream);
                }

                public override string Decompile()
                {
                    Decompiler._Nester.AddNest(NestManager.Nest.NestType.ForEach, Position, GetForEachNestEnd(), this);

                    SetEndComment();

                    // foreach ArrayVariable( Parameters )
                    string output;
                    if (WithIndexParam > 0)
                    {
                        output = $"foreach {DecompileNext()}({DecompileNext()}, {DecompileNext()})";
                    }
                    else
                    {
                        output = $"foreach {DecompileNext()}({DecompileNext()})";
                        // Skip Index param — bounds-checked, the index sub-token may not be in the
                        // list when bytecode parse recovery has shortened the function body.
                        if (Decompiler.CurrentTokenIndex + 1 < Decompiler.DeserializedTokens.Count)
                        {
                            NextToken();
                        }
                    }

                    Decompiler._CanAddSemicolon = false;
                    return output;
                }
            }

            [ExprToken(ExprToken.IteratorNext)]
            public class IteratorNextToken : Token
            {
                public override string Decompile()
                {
                    if (Decompiler.PeekToken is IteratorPopToken)
                    {
                        return string.Empty;
                    }

                    Decompiler._CanAddSemicolon = true;
                    return "continue";
                }
            }

            [ExprToken(ExprToken.IteratorPop)]
            public class IteratorPopToken : Token
            {
                public override string Decompile()
                {
                    if (Decompiler.IsWithinNest(NestManager.Nest.NestType.ForEach)?.Creator is JumpToken iterator
                        && Position >= iterator.CodeOffset)
                    {
                        return string.Empty;
                    }

                    if (Decompiler.PreviousToken is IteratorNextToken
                        || Decompiler.PeekToken is ReturnToken)
                    {
                        return string.Empty;
                    }

                    Decompiler._CanAddSemicolon = true;
                    return "break";
                }
            }

            private List<ULabelEntry> _Labels;
            private List<(ULabelEntry entry, int refs)> _TempLabels;

            /// <summary>
            /// Snap forward CodeOffsets in every JumpToken/JumpIfNot/Case/Iterator
            /// to the nearest sibling-token boundary. Recovers from the RL cooker
            /// undercount where in-memory 4→8 expansions in the body were not
            /// accounted for and the recorded u16 lands mid-token. Two effects:
            ///   1. JumpToken auto-labels (J0xXX) line up with a token's Position
            ///      so `DecompileLabelForToken` actually prints them.
            ///   2. JumpIfNot's if-else detection (elseStartToken.Position ==
            ///      CodeOffset &amp;&amp; prevToken is JumpToken) starts firing for
            ///      cooker-bugged shapes — was failing because CodeOffset landed
            ///      one expansion short of the else-body start.
            /// For JumpIfNot specifically, an additional snap-past-trailing-exit
            /// step runs when the previous body sibling is non-exit and the
            /// snapped sibling is exit-like — empirical pattern of cooker
            /// undercount producing "long body stmt + short trailing return"
            /// in functions like RegisterClient.
            /// </summary>
            private void FixupJumpCodeOffsets()
            {
                if (DeserializedTokens == null || DeserializedTokens.Count == 0) return;
                if (_Labels == null) return;

                // Identify sibling-boundary positions. A token starts a new
                // sibling iff its Position is at-or-past the cumulative end of
                // the prior sibling chain — sub-tokens of an earlier sibling
                // never qualify because their Position is inside the prior end.
                var siblingPositions = new SortedSet<int>();
                var siblingByPos = new Dictionary<int, Token>();
                int siblingEnd = 0;
                foreach (var t in DeserializedTokens)
                {
                    if (t == null) continue;
                    if (t.Position >= siblingEnd)
                    {
                        siblingPositions.Add(t.Position);
                        siblingByPos[t.Position] = t;
                        siblingEnd = t.Position + t.Size;
                    }
                }
                // Function-end virtual boundary so a CodeOffset just past the
                // last sibling (typical for a final `return`) can still snap.
                siblingPositions.Add(siblingEnd);

                foreach (var token in DeserializedTokens)
                {
                    if (token is not JumpToken jt) continue;
                    if (jt.CodeOffset >= ushort.MaxValue) continue;       // 0xFFFF default-case sentinel
                    if (jt.CodeOffset == 0) continue;                     // unset
                    if (jt.CodeOffset <= jt.Position) continue;           // backward (loop back-edge)
                    if (jt.CodeOffset < jt.Position + jt.Size) continue;  // Case A — handled in JumpIfNotToken.Decompile
                    if (siblingPositions.Contains(jt.CodeOffset)) continue; // already aligned

                    int snapped = -1;
                    foreach (var b in siblingPositions)
                    {
                        if (b > jt.CodeOffset) { snapped = b; break; }
                    }
                    if (snapped == -1) continue;
                    // Cooker undercount is a small number of bytes (one or a
                    // few 4-byte expansions). Reject snaps over a long
                    // distance to avoid corrupting unrelated jumps that
                    // genuinely target a non-boundary position (rare, but
                    // possible for e.g. malformed bytecode after parse drift).
                    if (snapped - jt.CodeOffset > 16) continue;

                    // For JumpIfNotToken: when CodeOffset lands just before a
                    // single-statement exit immediately following a non-exit
                    // body statement, the cooker's intent was to include the
                    // exit in the if-body. This is the "long body stmt +
                    // trailing return" pattern (RegisterClient) and the
                    // "if-body terminated by goto for if-else" pattern
                    // (HandleClientActionRequired). Snap one more sibling past.
                    if (jt is JumpIfNotToken)
                    {
                        Token prevSibling = null;
                        int curEnd = jt.Position + jt.Size;
                        foreach (var t in DeserializedTokens)
                        {
                            if (t == null) continue;
                            if (t.Position < curEnd) continue;
                            if (t.Position >= snapped) break;
                            prevSibling = t;
                            curEnd = t.Position + t.Size;
                        }

                        siblingByPos.TryGetValue(snapped, out var tokenAtSnap);
                        if (prevSibling != null
                            && !IsBodyExitLikeToken(prevSibling)
                            && tokenAtSnap != null
                            && IsBodyExitLikeToken(tokenAtSnap))
                        {
                            int afterSnap = tokenAtSnap.Position + tokenAtSnap.Size;
                            if (siblingPositions.Contains(afterSnap))
                            {
                                snapped = afterSnap;
                            }
                        }
                    }

                    ushort oldOffset = jt.CodeOffset;
                    jt.CodeOffset = (ushort)snapped;

                    // Update the corresponding auto-label entry. JumpToken adds
                    // a J0xXX label at its CodeOffset in PostDeserialized; if
                    // we changed the offset, the label needs to follow. Filter
                    // by the J0x prefix so state-label entries (added by
                    // LabelTableToken from the function's actual labels) are
                    // left alone.
                    for (int i = 0; i < _Labels.Count; i++)
                    {
                        if (_Labels[i].Position == oldOffset
                            && _Labels[i].Name != null
                            && _Labels[i].Name.StartsWith("J0x", StringComparison.Ordinal))
                        {
                            _Labels[i] = new ULabelEntry
                            {
                                Name = UDecompilingState.OffsetLabelName((ushort)snapped),
                                Position = snapped
                            };
                            break;
                        }
                    }
                }
            }

            private static bool IsBodyExitLikeToken(Token t)
            {
                if (t == null) return false;
                var typeName = t.GetType().Name;
                if (typeName == "ContextAwareReturnTokenRL"
                    || typeName == "ReturnToken"
                    || typeName == "ReturnNothingToken")
                {
                    return true;
                }
                // A bare JumpToken (not subclasses) with a forward jump past
                // its own bytes is the if-body's terminating goto in an
                // if-else compilation — should be considered part of the body
                // for snap-past purposes.
                if (t.GetType() == typeof(JumpToken))
                {
                    var jt = (JumpToken)t;
                    return jt.CodeOffset > t.Position + t.Size;
                }
                return false;
            }

            [ExprToken(ExprToken.LabelTable)]
            public class LabelTableToken : Token
            {
                public override void Deserialize(IUnrealStream stream)
                {
                    var label = string.Empty;
                    int labelPos = -1;
                    do
                    {
                        if (label != string.Empty)
                        {
                            Decompiler._Labels.Add
                            (
                                new ULabelEntry
                                {
                                    Name = label,
                                    Position = labelPos
                                }
                            );
                        }

                        label = stream.ReadName();
                        Decompiler.AlignNameSize();
                        labelPos = stream.ReadInt32();
                        Decompiler.AlignSize(sizeof(int));
                    } while (string.Compare(label, "None", StringComparison.OrdinalIgnoreCase) != 0);
                }
            }

            [ExprToken(ExprToken.Skip)]
            public class SkipToken : Token
            {
                public ushort Size;

                public override void Deserialize(IUnrealStream stream)
                {
                    Size = stream.ReadUInt16();
                    Decompiler.AlignSize(sizeof(ushort));

                    DeserializeNext();
                }

                public override string Decompile()
                {
                    return DecompileNext();
                }
            }

            [ExprToken(ExprToken.Stop)]
            public class StopToken : Token
            {
                public override string Decompile()
                {
                    Decompiler._CanAddSemicolon = true;
                    return "stop";
                }
            }
        }
    }
}
