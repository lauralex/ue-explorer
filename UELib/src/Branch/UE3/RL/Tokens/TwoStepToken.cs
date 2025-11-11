using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UELib.Core;

namespace UELib.Branch.UE3.RL.Tokens;
public class TwoStepToken : UStruct.UByteCodeDecompiler.Token
{
    public override void Deserialize(IUnrealStream stream)
    {
        DeserializeNext();
        DeserializeNext();
    }

    public override string Decompile()
    {
        return $"{DecompileNext()} {DecompileNext()}";
    }
}
