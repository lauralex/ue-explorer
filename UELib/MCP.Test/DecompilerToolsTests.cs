using Microsoft.VisualStudio.TestTools.UnitTesting;
using ModelContextProtocol;
using UELib.MCP.Session;
using UELib.MCP.Tools;

namespace UELib.MCP.Test;

[TestClass]
public sealed class DecompilerToolsTests
{
    private static string Fixture(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    private static async Task<(PackageSessionManager sessions, PackageTools pkg, ObjectTools obj, DecompilerTools dec, string handle)>
        OpenAsync(string fixture)
    {
        var sessions = new PackageSessionManager();
        var pkg = new PackageTools(sessions);
        var obj = new ObjectTools(sessions);
        var dec = new DecompilerTools(sessions);
        var loaded = await pkg.LoadPackage(Fixture(fixture));
        return (sessions, pkg, obj, dec, loaded.handle);
    }

    [TestMethod]
    public async Task GetClassInfo_ClassDeclarations_PopulatesMembers()
    {
        var (sessions, _, obj, _, handle) = await OpenAsync("TestUC2.u");
        await using var _disp = sessions;

        var info = await obj.GetClassInfo(handle, "ClassDeclarations");

        Assert.AreEqual("ClassDeclarations", info.name);
        Assert.IsTrue(info.functions.Count > 0, "ClassDeclarations should declare functions.");
        Assert.IsTrue(info.consts.Count > 0, "ClassDeclarations should declare consts.");
        Assert.IsTrue(info.enums.Count > 0, "ClassDeclarations should declare enums.");
        Assert.IsTrue(info.structs.Count > 0, "ClassDeclarations should declare a struct.");
    }

    [TestMethod]
    public async Task GetFunctionInfo_KnownFunction_HasParams()
    {
        var (sessions, _, obj, _, handle) = await OpenAsync("TestUC2.u");
        await using var _disp = sessions;

        var info = await obj.GetFunctionInfo(handle, "ClassDeclarations", "Function2");

        Assert.AreEqual("Function2", info.name);
        Assert.IsTrue(info.@params.Count >= 2, "Function2 takes at least 2 parameters.");
        Assert.IsNotNull(info.return_property);
    }

    [TestMethod]
    public async Task GetFunctionInfo_UnknownFunction_ThrowsMcpException()
    {
        var (sessions, _, obj, _, handle) = await OpenAsync("TestUC2.u");
        await using var _disp = sessions;

        await Assert.ThrowsExceptionAsync<McpException>(
            () => obj.GetFunctionInfo(handle, "ClassDeclarations", "ThisDoesNotExist"));
    }

    [TestMethod]
    public async Task DecompileClass_ProducesSourceText()
    {
        var (sessions, _, _, dec, handle) = await OpenAsync("TestUC2.u");
        await using var _disp = sessions;

        var result = await dec.DecompileClass(handle, "ClassDeclarations");

        Assert.IsNotNull(result.source);
        Assert.IsTrue(result.source.Length > 0,
            $"Expected non-empty decompiled source. Warning: {result.warning}");
        Assert.IsTrue(result.source.Contains("ClassDeclarations"),
            "Decompiled source should mention the class name.");
    }

    [TestMethod]
    public async Task DecompileFunction_NeverThrows_OnTokenError()
    {
        var (sessions, _, _, dec, handle) = await OpenAsync("TestUC2.u");
        await using var _disp = sessions;

        // Function2 should be decompilable; even if a token failed, the call must
        // return a result (with warning) rather than bubbling an exception.
        var result = await dec.DecompileFunction(handle, "ClassDeclarations", "Function2");

        Assert.IsNotNull(result);
        // Either source is non-empty, or warning explains why.
        Assert.IsTrue(result.source.Length > 0 || !string.IsNullOrEmpty(result.warning),
            "Either source or warning must be populated.");
    }

    [TestMethod]
    public async Task DisassembleFunction_ReturnsTokensWithIncreasingPositions()
    {
        var (sessions, _, _, dec, handle) = await OpenAsync("TestUC2.u");
        await using var _disp = sessions;

        var result = await dec.DisassembleFunction(handle, "ClassDeclarations", "Function2");

        Assert.AreEqual("Function2", result.function_name);
        Assert.IsTrue(result.tokens.Count > 0,
            $"Expected tokens. Warning: {result.warning}");

        int last = -1;
        foreach (var tok in result.tokens)
        {
            Assert.IsTrue(tok.position >= last,
                $"Token positions should be monotonically non-decreasing. Got {tok.position} after {last}.");
            last = tok.position;
        }
    }

    [TestMethod]
    public async Task SearchObjects_FindsByCaseInsensitiveSubstring()
    {
        var (sessions, _, obj, _, handle) = await OpenAsync("TestUC2.u");
        await using var _disp = sessions;

        var results = await obj.SearchObjects(handle, "function", max_results: 50);

        Assert.IsTrue(results.Count > 0);
        Assert.IsTrue(results.All(r => r.name.Contains("function", StringComparison.OrdinalIgnoreCase)
                                       || r.name.Contains("Function", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task FindObject_ReturnsNullForMisses()
    {
        var (sessions, _, obj, _, handle) = await OpenAsync("TestUC2.u");
        await using var _disp = sessions;

        var miss = await obj.FindObject(handle, "Nope.NotAClass.NotAMember");
        Assert.IsNull(miss);
    }

    [TestMethod]
    public async Task DecompileObject_OnClass_MatchesDecompileClass()
    {
        var (sessions, _, _, dec, handle) = await OpenAsync("TestUC2.u");
        await using var _disp = sessions;

        var viaObject = await dec.DecompileObject(handle, "ClassDeclarations");
        var viaClass = await dec.DecompileClass(handle, "ClassDeclarations");

        Assert.IsTrue(viaObject.source.Length > 0,
            $"decompile_object should return source. warning={viaObject.warning}");
        // Both code paths reach UClass.Decompile() under the hood, so the text should match.
        Assert.AreEqual(viaClass.source, viaObject.source);
    }

    [TestMethod]
    public async Task DecompileObject_UnknownPath_ThrowsMcpException()
    {
        var (sessions, _, _, dec, handle) = await OpenAsync("TestUC2.u");
        await using var _disp = sessions;

        await Assert.ThrowsExceptionAsync<McpException>(
            () => dec.DecompileObject(handle, "Definitely.Does.Not.Exist"));
    }
}
