using System;
using System.Collections.Generic;
using UELib.Core;

namespace UELib.Branch.UE3.RL.Tokens;

internal abstract class DynamicArrayProjectionTokenRL : UStruct.UByteCodeDecompiler.DynamicArrayMethodToken
{
    protected void DeserializeArrayWithSkip(IUnrealStream stream)
    {
        DeserializeNext();
        stream.Skip(2);
        Decompiler.AlignSize(sizeof(ushort));
    }

    protected void DeserializeHiddenResultProperty(IUnrealStream stream)
    {
        try
        {
            stream.ReadObject<UProperty>();
        }
        catch (ArgumentOutOfRangeException)
        {
        }
        catch (InvalidCastException)
        {
        }
        Decompiler.AlignObjectSize();
    }

    protected void DeserializeEndParmsAndDebug()
    {
        if (Package.Version >= (uint)PackageObjectLegacyVersion.EndTokenAppendedToArrayTokenIntrinsics)
        {
            DeserializeNext();
        }

        DeserializeDebugToken();
    }

    protected string DecompileMethod(string methodName, params string[] args)
    {
        Decompiler.MarkSemicolon();
        string context = DecompileNext();
        var parts = new List<string>(args.Length + 1);

        foreach (string arg in args)
        {
            if (!string.IsNullOrEmpty(arg))
            {
                parts.Add(arg);
            }
        }

        if (Package.Version >= (uint)PackageObjectLegacyVersion.EndTokenAppendedToArrayTokenIntrinsics)
        {
            AssertSkipCurrentToken<UStruct.UByteCodeDecompiler.EndFunctionParmsToken>();
        }

        string output = DecompileNext();
        if (!string.IsNullOrEmpty(output))
        {
            parts.Add($"out {output}");
        }

        return $"{context}.{methodName}({string.Join(", ", parts)})";
    }
}

internal class DynamicArraySortedCopyTokenRL : DynamicArrayProjectionTokenRL
{
    public override void Deserialize(IUnrealStream stream)
    {
        DeserializeArrayWithSkip(stream);
        DeserializeNext();
        DeserializeHiddenResultProperty(stream);
        DeserializeEndParmsAndDebug();
        DeserializeNext();
    }

    public override string Decompile()
    {
        return DecompileMethod("Sort", DecompileNext());
    }
}

internal class DynamicArrayReduceTokenRL : DynamicArrayProjectionTokenRL
{
    public override void Deserialize(IUnrealStream stream)
    {
        DeserializeArrayWithSkip(stream);
        DeserializeNext();
        DeserializeHiddenResultProperty(stream);
        DeserializeNext();
        DeserializeEndParmsAndDebug();
        DeserializeNext();
    }

    public override string Decompile()
    {
        return DecompileMethod("Reduce", DecompileNext(), DecompileNext());
    }
}

internal class DynamicArrayDistinctTokenRL : DynamicArrayProjectionTokenRL
{
    public override void Deserialize(IUnrealStream stream)
    {
        DeserializeArrayWithSkip(stream);
        DeserializeHiddenResultProperty(stream);
        DeserializeEndParmsAndDebug();
        DeserializeNext();
    }

    public override string Decompile()
    {
        return DecompileMethod("Distinct");
    }
}

internal class DynamicArrayOfTypeTokenRL : DynamicArrayProjectionTokenRL
{
    public override void Deserialize(IUnrealStream stream)
    {
        DeserializeArrayWithSkip(stream);
        DeserializeNext();
        DeserializeHiddenResultProperty(stream);
        DeserializeEndParmsAndDebug();
        DeserializeNext();
    }

    public override string Decompile()
    {
        return DecompileMethod("OfType", DecompileNext());
    }
}

internal class DynamicArrayFlatMapTokenRL : DynamicArrayProjectionTokenRL
{
    public override void Deserialize(IUnrealStream stream)
    {
        DeserializeArrayWithSkip(stream);
        DeserializeNext();
        DeserializeHiddenResultProperty(stream);
        DeserializeEndParmsAndDebug();
        DeserializeNext();
    }

    public override string Decompile()
    {
        return DecompileMethod("FlatMap", DecompileNext());
    }
}

internal class DynamicArrayDifferenceTokenRL : DynamicArrayProjectionTokenRL
{
    public override void Deserialize(IUnrealStream stream)
    {
        DeserializeArrayWithSkip(stream);
        DeserializeArrayWithSkip(stream);
        DeserializeHiddenResultProperty(stream);
        DeserializeEndParmsAndDebug();
        DeserializeNext();
    }

    public override string Decompile()
    {
        return DecompileMethod("Difference", DecompileNext());
    }
}

internal class DynamicArrayIntersectTokenRL : DynamicArrayProjectionTokenRL
{
    public override void Deserialize(IUnrealStream stream)
    {
        DeserializeArrayWithSkip(stream);
        DeserializeArrayWithSkip(stream);
        DeserializeHiddenResultProperty(stream);
        DeserializeEndParmsAndDebug();
        DeserializeNext();
    }

    public override string Decompile()
    {
        return DecompileMethod("Intersect", DecompileNext());
    }
}

internal class DynamicArrayToStringTokenRL : UStruct.UByteCodeDecompiler.Token
{
    public override void Deserialize(IUnrealStream stream)
    {
        DeserializeNext();
    }

    public override string Decompile()
    {
        return $"{DecompileNext()}.ToString()";
    }
}

internal class DynamicArrayLastTokenRL : UStruct.UByteCodeDecompiler.Token
{
    public byte Flags;

    public override void Deserialize(IUnrealStream stream)
    {
        Flags = stream.ReadByte();
        Decompiler.AlignSize(sizeof(byte));

        DeserializeNext();
        DeserializeNext();
    }

    public override string Decompile()
    {
        DecompileNext();
        return $"{DecompileNext()}.Last()";
    }
}
