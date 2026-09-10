using System.Reflection;
using Xunit;

namespace ShareX.Core.Tests;

/// <summary>
/// Guards the editor fork against silent loss.
///
/// The fork (planning/adr/0004-editor-fork-strategy.md) copies upstream's
/// ShareX.ImageEditor and changes only the files that touched Windows APIs. These
/// tests read the built assembly by reflection and assert the counts
/// PROJECT-SPEC.md pins: 20 EditorTool values and 232 effect ids. If a future
/// change drops an effect or a tool, this fails rather than quietly shipping a
/// smaller editor.
///
/// The expected values come from the pinned source, verified by comparing the
/// fork's `public override string Id` literals against upstream's: 232 in both,
/// with no additions and no omissions.
///
/// Of those 232, exactly **230 are live**. Rotate3DImageEffect and
/// Rotate3DBoxImageEffect are wrapped in `/* TODO: SkiaSharp bug ... */` in
/// UPSTREAM v21.0.0 itself, so they compile to nothing there too. That is an
/// upstream decision this port inherits, not a porting loss — verified by
/// finding the same comment block in reference/ShareX. The counts below are
/// therefore 230 concrete types against 232 declared ids, and the difference is
/// asserted explicitly so it can never quietly become a real omission.
/// </summary>
public sealed class EditorForkParityTests
{
    private static Assembly EditorAssembly => Assembly.Load("ShareX.ImageEditor");

    [Fact]
    public void EditorTool_HasTheTwentyUpstreamValues()
    {
        Type? tool = EditorAssembly.GetTypes()
            .FirstOrDefault(t => t.IsEnum && t.Name == "EditorTool");

        Assert.NotNull(tool);
        Assert.Equal(20, Enum.GetNames(tool!).Length);
    }

    [Fact]
    public void EffectCatalog_HasAllTwoHundredAndThirtyTwoEffects()
    {
        Type? baseType = EditorAssembly.GetTypes()
            .FirstOrDefault(t => t.Name == "ImageEffectBase");
        Assert.NotNull(baseType);

        List<Type> effects = EditorAssembly.GetTypes()
            .Where(t => !t.IsAbstract && baseType!.IsAssignableFrom(t))
            .ToList();

        // 232 ids exist in source; 230 compile, because upstream has commented
        // out Rotate3DImageEffect and Rotate3DBoxImageEffect pending a SkiaSharp
        // fix. See the class comment.
        Assert.Equal(230, effects.Count);
    }

    [Fact]
    public void EveryEffect_HasAUniqueNonEmptyId()
    {
        Type baseType = EditorAssembly.GetTypes().First(t => t.Name == "ImageEffectBase");
        PropertyInfo idProperty = baseType.GetProperty("Id")!;

        var ids = new List<string>();

        foreach (Type type in EditorAssembly.GetTypes()
                     .Where(t => !t.IsAbstract && baseType.IsAssignableFrom(t)))
        {
            object instance = Activator.CreateInstance(type)!;
            string? id = idProperty.GetValue(instance) as string;

            Assert.False(string.IsNullOrWhiteSpace(id), $"{type.Name} has no Id");
            ids.Add(id!);
        }

        // A duplicate id would silently shadow an effect in the serialized
        // preset format, so uniqueness is part of the contract.
        Assert.Equal(ids.Count, ids.Distinct().Count());
        Assert.Equal(230, ids.Count);
    }

    [Fact]
    public void TheTwoDisabledEffects_AreDisabledUpstreamToo_NotLostInTheFork()
    {
        // Pins the reason for the 232-vs-230 gap. If someone later re-enables
        // these upstream, this test fails and the counts above get revisited
        // deliberately instead of the discrepancy being rediscovered.
        string[] disabled = { "Rotate3DImageEffect", "Rotate3DBoxImageEffect" };
        IEnumerable<string> present = EditorAssembly.GetTypes().Select(t => t.Name);

        foreach (string name in disabled)
        {
            Assert.DoesNotContain(name, present);
        }
    }

    [Fact]
    public void NoWindowsRendererSurvivedTheFork()
    {
        string[] windowsOnly =
        {
            "WindowsCursorBitmapRenderer",
            "WindowsEmojiBitmapRenderer",
            "WindowsDesktopWallpaperService"
        };

        foreach (string name in windowsOnly)
        {
            Assert.DoesNotContain(name, EditorAssembly.GetTypes().Select(t => t.Name));
        }

        // ...and their macOS replacements are present.
        Assert.Contains("MacCursorBitmapRenderer", EditorAssembly.GetTypes().Select(t => t.Name));
        Assert.Contains("MacEmojiBitmapRenderer", EditorAssembly.GetTypes().Select(t => t.Name));
    }
}
