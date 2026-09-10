using ShareX.Tools.Indexing;
using Xunit;

namespace ShareX.Tools.Tests;

public sealed class IndexerTests : IDisposable
{
    private readonly string _root;

    public IndexerTests()
    {
        _root = Directory.CreateTempSubdirectory("sharex-indexer-").FullName;

        Directory.CreateDirectory(Path.Combine(_root, "alpha"));
        Directory.CreateDirectory(Path.Combine(_root, "alpha", "nested"));
        Directory.CreateDirectory(Path.Combine(_root, "empty"));

        File.WriteAllText(Path.Combine(_root, "top.txt"), "top");
        File.WriteAllText(Path.Combine(_root, "alpha", "one.txt"), new string('x', 2048));
        File.WriteAllText(Path.Combine(_root, "alpha", "nested", "deep.txt"), "deep");
        // A unicode filename, to catch encoding problems in every writer.
        File.WriteAllText(Path.Combine(_root, "alpha", "café-日本.txt"), "unicode");
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, true);
        }
        catch (IOException)
        {
            // A leftover temp directory must not fail the test run.
        }
    }

    private static IndexerSettings Settings(IndexerOutput output) => new()
    {
        Output = output,
        IndentationText = "  ",
        ShowSizeInfo = true
    };

    [Fact]
    public void Text_ListsFoldersAndFiles()
    {
        string result = Indexer.Index(_root, Settings(IndexerOutput.Txt));

        Assert.Contains("alpha", result);
        Assert.Contains("nested", result);
        Assert.Contains("deep.txt", result);
        Assert.Contains("top.txt", result);
        Assert.Contains("café-日本.txt", result);
    }

    [Fact]
    public void Text_ShowsSizeInfoWhenEnabled()
    {
        string with = Indexer.Index(_root, Settings(IndexerOutput.Txt));
        string without = Indexer.Index(_root, new IndexerSettings
        {
            Output = IndexerOutput.Txt, IndentationText = "  ", ShowSizeInfo = false
        });

        // 2048 bytes formats as "2.05 KB" with decimal units.
        Assert.Contains("KB", with);
        Assert.DoesNotContain("KB", without);
    }

    [Fact]
    public void Text_IndentsByDepth()
    {
        string result = Indexer.Index(_root, Settings(IndexerOutput.Txt));
        string[] lines = result.Split('\n');

        string nested = lines.First(l => l.Contains("nested"));
        string alpha = lines.First(l => l.TrimEnd().EndsWith("alpha")
                                        || l.Contains("alpha ["));

        int nestedIndent = nested.Length - nested.TrimStart().Length;
        int alphaIndent = alpha.Length - alpha.TrimStart().Length;

        Assert.True(nestedIndent > alphaIndent,
            "a nested folder must be indented deeper than its parent");
    }

    [Fact]
    public void Html_IsWellFormedAndEscaped()
    {
        string result = Indexer.Index(_root, Settings(IndexerOutput.Html));

        Assert.StartsWith("<!DOCTYPE html>", result);
        Assert.Contains("</html>", result);
        // The embedded stylesheet must actually be present.
        Assert.Contains("<style type=\"text/css\">", result);
        Assert.Contains("font-family", result);

        // Upstream's HtmlEncode turns every character above 127 into a numeric
        // character reference, so the document is pure ASCII. "é" is U+00E9 =
        // 233 and the CJK characters likewise; asserting the entity form pins
        // that behaviour rather than the more lenient WebUtility default.
        Assert.Contains("caf&#233;", result);
        Assert.DoesNotContain("café", result);
    }

    [Fact]
    public void Html_EscapesMarkupInFilenames()
    {
        // A filename containing markup must not inject into the document.
        string tricky = Path.Combine(_root, "a<b>&c.txt");
        File.WriteAllText(tricky, "x");

        string result = Indexer.Index(_root, Settings(IndexerOutput.Html));

        Assert.Contains("a&lt;b&gt;&amp;c.txt", result);
        Assert.DoesNotContain("<b>&c.txt", result);
    }

    [Fact]
    public void Xml_IsParseable()
    {
        string result = Indexer.Index(_root, Settings(IndexerOutput.Xml));

        var document = System.Xml.Linq.XDocument.Parse(result);
        Assert.Equal("Folder", document.Root!.Name.LocalName);
        Assert.Contains("deep.txt", result);
    }

    [Fact]
    public void Xml_UseAttribute_SwitchesTheShape()
    {
        var asElements = new IndexerSettings { Output = IndexerOutput.Xml, UseAttribute = false };
        var asAttributes = new IndexerSettings { Output = IndexerOutput.Xml, UseAttribute = true };

        string elements = Indexer.Index(_root, asElements);
        string attributes = Indexer.Index(_root, asAttributes);

        Assert.Contains("<Name>", elements);
        Assert.DoesNotContain("<Name>", attributes);
        Assert.Contains("Name=\"", attributes);
    }

    [Fact]
    public void Json_IsParseableInBothShapes()
    {
        string simple = Indexer.Index(_root, new IndexerSettings
        {
            Output = IndexerOutput.Json, CreateParseableJson = false
        });
        string parseable = Indexer.Index(_root, new IndexerSettings
        {
            Output = IndexerOutput.Json, CreateParseableJson = true
        });

        // Both must be valid JSON; they are deliberately different shapes.
        Newtonsoft.Json.Linq.JObject.Parse(simple);
        var parsed = Newtonsoft.Json.Linq.JObject.Parse(parseable);

        Assert.NotNull(parsed["Name"]);
        Assert.NotNull(parsed["Folders"]);
    }

    [Fact]
    public void MaxDepthLevel_LimitsRecursion()
    {
        var shallow = new IndexerSettings
        {
            Output = IndexerOutput.Txt, IndentationText = "  ", MaxDepthLevel = 1
        };

        string result = Indexer.Index(_root, shallow);

        Assert.Contains("alpha", result);
        // "nested" is two levels down, so depth 1 must not reach it.
        Assert.DoesNotContain("deep.txt", result);
    }

    [Fact]
    public void SkipFiles_OmitsFiles()
    {
        var noFiles = new IndexerSettings
        {
            Output = IndexerOutput.Txt, IndentationText = "  ", SkipFiles = true
        };

        string result = Indexer.Index(_root, noFiles);

        Assert.Contains("alpha", result);
        Assert.DoesNotContain("top.txt", result);
    }

    [Fact]
    public void SymlinkCycle_DoesNotHang()
    {
        // The macOS-specific hazard the port added a guard for: a directory
        // symlinked into one of its own descendants. Upstream has no cycle
        // protection because directory junctions behave differently on Windows.
        string linkPath = Path.Combine(_root, "alpha", "nested", "loop");
        try
        {
            Directory.CreateSymbolicLink(linkPath, _root);
        }
        catch (Exception)
        {
            // Some filesystems disallow it; nothing to assert then.
            return;
        }

        string result = Indexer.Index(_root, Settings(IndexerOutput.Txt));

        // The real assertion is that this returned at all rather than
        // recursing until the stack died.
        Assert.False(string.IsNullOrWhiteSpace(result));
    }

    [Fact]
    public void Footer_IsAddedOnlyWhenRequested()
    {
        string with = Indexer.Index(_root, new IndexerSettings
        {
            Output = IndexerOutput.Txt, IndentationText = "  ", AddFooter = true
        });
        string without = Indexer.Index(_root, new IndexerSettings
        {
            Output = IndexerOutput.Txt, IndentationText = "  ", AddFooter = false
        });

        // Note AddFooter defaults to TRUE upstream, so the opt-out is explicit.
        Assert.Contains("Directory Indexer", with);
        Assert.DoesNotContain("Directory Indexer", without);
    }

    [Fact]
    public void BinaryUnits_ChangeTheSuffix()
    {
        var binary = new IndexerSettings
        {
            Output = IndexerOutput.Txt, IndentationText = "  ",
            ShowSizeInfo = true, BinaryUnits = true
        };

        string result = Indexer.Index(_root, binary);

        Assert.Contains("KiB", result);
        Assert.DoesNotContain("KB]", result);
    }
}
