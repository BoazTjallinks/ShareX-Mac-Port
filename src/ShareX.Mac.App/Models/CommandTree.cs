using ShareX.Core.Enums;

namespace ShareX.Mac.App.Models;

/// <summary>
/// One entry in ShareX's command surface. <see cref="Hotkey"/> carries the
/// upstream <see cref="HotkeyType"/> identity so a rail button, a menu item, a
/// hotkey and the CLI all dispatch the same command (ICommandDispatcher in
/// docs/INTERFACES-AND-DATA.md).
/// </summary>
public sealed record CommandEntry(
    string Label,
    HotkeyType Hotkey = HotkeyType.None,
    IReadOnlyList<CommandEntry>? Children = null,
    bool IsSeparator = false)
{
    public static CommandEntry Separator { get; } = new("-", IsSeparator: true);

    public bool HasChildren => Children is { Count: > 0 };

    /// <summary>True when this entry maps to a real upstream command.</summary>
    public bool IsExecutable => Hotkey != HotkeyType.None;
}

/// <summary>
/// ShareX's command structure, transcribed from the pinned upstream designer.
///
/// Source: reference/ShareX/ShareX/Forms/MainForm.Designer.cs @
/// d2502561f63fc3ff502cacd91514e3f7f2948c74, extracted mechanically into
/// planning/ui-layout/ShareX__MainForm.json and rendered as
/// planning/ui-layout/MainForm-command-tree.md.
///
/// ADR 0005 governs this: the commands, their grouping, their order and their
/// labels are upstream's and are not to be reordered, renamed or merged. Only
/// the rendering is macOS-native.
/// </summary>
public static class CommandTree
{
    public static IReadOnlyList<CommandEntry> MainRail { get; } = new List<CommandEntry>
    {
        new("Capture", Children: new List<CommandEntry>
        {
            new("Fullscreen", HotkeyType.PrintScreen),
            new("Window", HotkeyType.ActiveWindow),
            new("Monitor", HotkeyType.ActiveMonitor),
            new("Region", HotkeyType.RectangleRegion),
            new("Region (Light)", HotkeyType.RectangleLight),
            new("Region (Transparent)", HotkeyType.RectangleTransparent),
            new("Last region", HotkeyType.LastRegion),
            new("Screen recording", HotkeyType.ScreenRecorder),
            new("Screen recording (GIF)", HotkeyType.ScreenRecorderGIF),
            new("Scrolling capture...", HotkeyType.ScrollingCapture),
            new("Auto capture...", HotkeyType.AutoCapture),
            CommandEntry.Separator,
            new("Show cursor"),
            new("Screenshot delay", Children: new List<CommandEntry>
            {
                new("No delay"),
                new("1 second"),
                new("2 seconds"),
                new("3 seconds"),
                new("4 seconds"),
                new("5 seconds")
            })
        }),

        new("Upload", Children: new List<CommandEntry>
        {
            new("Upload file...", HotkeyType.FileUpload),
            new("Upload folder...", HotkeyType.FolderUpload),
            new("Upload from clipboard...", HotkeyType.ClipboardUpload),
            new("Upload text...", HotkeyType.UploadText),
            new("Upload from URL...", HotkeyType.UploadURL),
            new("Drag and drop upload...", HotkeyType.DragDropUpload),
            new("Shorten URL...", HotkeyType.ShortenURL)
        }),

        // Populated at runtime from the user's configured workflows, exactly as
        // upstream does (the designer declares the drop-down with no static items).
        new("Workflows"),

        new("Tools", Children: new List<CommandEntry>
        {
            new("Color picker...", HotkeyType.ColorPicker),
            new("Screen color picker...", HotkeyType.ScreenColorPicker),
            new("Ruler...", HotkeyType.Ruler),
            new("Pin to screen...", HotkeyType.PinToScreen),
            CommandEntry.Separator,
            new("Image editor...", HotkeyType.ImageEditor),
            new("Image beautifier...", HotkeyType.ImageBeautifier),
            new("Image effects...", HotkeyType.ImageEffects),
            new("Image viewer...", HotkeyType.ImageViewer),
            new("Background remover...", HotkeyType.BackgroundRemover),
            new("Image comparer...", HotkeyType.ImageComparer),
            new("Image combiner...", HotkeyType.ImageCombiner),
            new("Image splitter...", HotkeyType.ImageSplitter),
            new("Image thumbnailer...", HotkeyType.ImageThumbnailer),
            CommandEntry.Separator,
            new("Video converter...", HotkeyType.VideoConverter),
            new("Video thumbnailer...", HotkeyType.VideoThumbnailer),
            CommandEntry.Separator,
            new("Analyze image...", HotkeyType.AnalyzeImage),
            new("OCR...", HotkeyType.OCR),
            new("QR code...", HotkeyType.QRCode),
            new("Hash checker...", HotkeyType.HashCheck),
            new("Metadata...", HotkeyType.Metadata),
            new("Directory indexer...", HotkeyType.IndexFolder),
            CommandEntry.Separator,
            new("Clipboard viewer...", HotkeyType.ClipboardViewer),
            new("Borderless window...", HotkeyType.BorderlessWindow),
            new("Inspect window...", HotkeyType.InspectWindow),
            new("Monitor test...", HotkeyType.MonitorTest)
        }),

        CommandEntry.Separator,

        // These three are flag pickers, not command lists; their items come from
        // AfterCaptureTasks / AfterUploadTasks / the destination enums.
        new("After capture tasks"),
        new("After upload tasks"),
        new("Destinations", Children: new List<CommandEntry>
        {
            new("Image uploaders"),
            new("Text uploaders"),
            new("File uploaders"),
            new("URL shorteners"),
            new("URL sharing services")
        }),

        CommandEntry.Separator,

        // These five are ToolStripButtons upstream, not HotkeyType commands: the
        // pinned enum has no members for them, so they are deliberately not
        // hotkeyable here either. Verified against reference/ShareX/ShareX/Enums.cs.
        new("Application settings..."),
        new("Task settings..."),
        new("Hotkey settings..."),
        new("Destination settings..."),
        new("Custom uploader settings..."),

        CommandEntry.Separator,

        new("Screenshots folder...", HotkeyType.OpenScreenshotsFolder),
        new("History...", HotkeyType.OpenHistory),
        new("Image history...", HotkeyType.OpenImageHistory),

        CommandEntry.Separator,

        new("Debug", Children: new List<CommandEntry>
        {
            new("Debug log..."),
            new("Test image upload"),
            new("Test text upload"),
            new("Test file upload"),
            new("Test URL shortener"),
            new("Test URL sharing")
        }),
        new("Donate..."),
        new("Follow @ShareX..."),
        new("Discord..."),
        new("About...")
    };

    /// <summary>Flattened executable commands, for the menu bar and the dispatcher.</summary>
    public static IEnumerable<CommandEntry> Executable(IEnumerable<CommandEntry>? from = null)
    {
        foreach (CommandEntry entry in from ?? MainRail)
        {
            if (entry.IsExecutable)
            {
                yield return entry;
            }

            if (entry.Children is { } children)
            {
                foreach (CommandEntry nested in Executable(children))
                {
                    yield return nested;
                }
            }
        }
    }
}
