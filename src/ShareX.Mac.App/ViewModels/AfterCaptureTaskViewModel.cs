using CommunityToolkit.Mvvm.ComponentModel;
using ShareX.Core.Enums;

namespace ShareX.Mac.App.ViewModels;

/// <summary>
/// One toggleable after-capture task, mirroring upstream's "After capture tasks"
/// drop-down. <see cref="Flag"/> keeps the upstream enum identity so the set
/// round-trips through settings unchanged.
/// </summary>
public sealed partial class AfterCaptureTaskViewModel : ObservableObject
{
    public AfterCaptureTaskViewModel(
        AfterCaptureTasks flag, string label, bool isEnabled, bool implemented, string? note = null)
    {
        Flag = flag;
        Label = label;
        _isChecked = isEnabled;
        Implemented = implemented;
        Note = note;
    }

    public AfterCaptureTasks Flag { get; }
    public string Label { get; }

    /// <summary>
    /// False when this build cannot yet perform the task. The control stays
    /// visible with an explanation rather than being hidden or silently ignored.
    /// </summary>
    public bool Implemented { get; }

    public string? Note { get; }
    public bool HasNote => !string.IsNullOrEmpty(Note);

    [ObservableProperty] private bool _isChecked;
}
