using Microsoft.VisualStudio.TestTools.UnitTesting;
using UELib.MCP.Models;
using UELib.MCP.Session;
using UELib.MCP.Tools;

namespace UELib.MCP.Test;

[TestClass]
public sealed class PackageToolsTests
{
    private static string Fixture(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    [TestMethod]
    public async Task LoadPackage_TestUC2_ReturnsHandleAndSummary()
    {
        await using var sessions = new PackageSessionManager();
        var tools = new PackageTools(sessions);

        var result = await tools.LoadPackage(Fixture("TestUC2.u"));

        Assert.IsNotNull(result.handle);
        Assert.AreEqual(8, result.handle.Length, "handle should be the 8-char GUID slice");
        Assert.AreEqual(128u, result.summary.version);
        Assert.AreEqual(29, result.summary.licensee_version);
        Assert.AreEqual("UT2004", result.summary.build_name);
        Assert.IsTrue(result.summary.export_count > 0);
        Assert.IsTrue(result.summary.name_count > 0);
    }

    [TestMethod]
    public async Task LoadPackage_MissingFile_ThrowsMcpException()
    {
        await using var sessions = new PackageSessionManager();
        var tools = new PackageTools(sessions);

        await Assert.ThrowsExceptionAsync<ModelContextProtocol.McpException>(
            () => tools.LoadPackage("C:/does/not/exist.upk"));
    }

    [TestMethod]
    public async Task LoadAndUnload_RoundTrip_ListReflectsState()
    {
        await using var sessions = new PackageSessionManager();
        var tools = new PackageTools(sessions);

        var loaded = await tools.LoadPackage(Fixture("TestUC2.u"));

        var open = await tools.ListLoadedPackages();
        Assert.AreEqual(1, open.Count);
        Assert.AreEqual(loaded.handle, open[0].handle);
        Assert.AreEqual("UT2004", open[0].build_name);

        var closed = await tools.UnloadPackage(loaded.handle);
        Assert.IsTrue(closed.ok);

        var afterClose = await tools.ListLoadedPackages();
        Assert.AreEqual(0, afterClose.Count);
    }

    [TestMethod]
    public async Task ListClasses_ReturnsExpectedTestUC2Classes()
    {
        await using var sessions = new PackageSessionManager();
        var tools = new PackageTools(sessions);

        var loaded = await tools.LoadPackage(Fixture("TestUC2.u"));
        var classes = await tools.ListClasses(loaded.handle);

        Assert.IsTrue(classes.Count > 0, "Expected at least one UClass.");
        var names = classes.Select(c => c.name).ToHashSet(StringComparer.Ordinal);
        Assert.IsTrue(names.Contains("ClassDeclarations"),
            $"Expected ClassDeclarations among classes: {string.Join(",", names)}");
    }

    [TestMethod]
    public async Task ListNames_RespectsPagination()
    {
        await using var sessions = new PackageSessionManager();
        var tools = new PackageTools(sessions);

        var loaded = await tools.LoadPackage(Fixture("TestUC2.u"));

        var firstPage = await tools.ListNames(loaded.handle, offset: 0, limit: 10);
        Assert.AreEqual(10, firstPage.Count);

        var secondPage = await tools.ListNames(loaded.handle, offset: 10, limit: 10);
        Assert.AreEqual(10, secondPage.Count);
        Assert.AreNotEqual(firstPage[0].name, secondPage[0].name);

        // Hard cap at 2000 even when limit is huge.
        var huge = await tools.ListNames(loaded.handle, offset: 0, limit: 5000);
        Assert.IsTrue(huge.Count <= 2000);
    }

    [TestMethod]
    public async Task ListNames_FilterMatchesSubstringCaseInsensitive()
    {
        await using var sessions = new PackageSessionManager();
        var tools = new PackageTools(sessions);

        var loaded = await tools.LoadPackage(Fixture("TestUC2.u"));

        var hits = await tools.ListNames(loaded.handle, filter: "class");

        Assert.IsTrue(hits.Count > 0);
        Assert.IsTrue(hits.All(h => h.name.Contains("class", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public async Task UnloadPackage_UnknownHandle_ReturnsOkFalse()
    {
        await using var sessions = new PackageSessionManager();
        var tools = new PackageTools(sessions);

        var result = await tools.UnloadPackage("deadbeef");

        Assert.IsFalse(result.ok);
    }
}
