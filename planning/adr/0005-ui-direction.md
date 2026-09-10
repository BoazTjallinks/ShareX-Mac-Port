# ADR 0005 — UI direction: ShareX's structure, macOS's idiom

Status: accepted
Date: 2026-09-10
Supersedes: the literal reading of PROJECT-SPEC.md section 11 ("Match the pinned
WinForms layout and resources for the main app")

## Context

The operator gave two successive instructions:

1. "UI should be a 1:1 copy of ShareX"
2. "make the user interface slightly more modern but keep it in the same style as
   ShareX has. Make it more like a Mac application instead of the Windows look.
   But still it being a ShareX Mac port."

(2) refines (1). The intent is: this must be recognisably ShareX — same commands,
same grouping, same density, same vocabulary — rendered as a Mac app rather than
a transplanted Win32 window.

PROJECT-SPEC.md section 11 already permits this: "Native macOS title bars, system
dialogs, menu-bar placement, permission prompts, and equivalent Cmd shortcuts are
explicitly allowed platform adaptations."

## Decision

**Structure is copied. Chrome is native.**

Preserved exactly, and verified against `planning/ui-layout/*.json` (mechanically
extracted from the upstream `*.Designer.cs`):

- Every command, in the upstream order, under the upstream grouping and label.
  The main toolbar's 24 entries stay in this order and keep these captions:
  Capture · Upload · Workflows · Tools · ─ · After capture tasks · After upload
  tasks · Destinations · ─ · Application settings… · Task settings… · Hotkey
  settings… · Destination settings… · Custom uploader settings… · ─ · Screenshots
  folder… · History… · Image history… · ─ · Debug · Donate… · Follow @ShareX… ·
  Discord… · About…
- The three-region main window: a docked command rail, the task list, and the
  preview pane (upstream: `pToolbars` docked Left, `pMain` filling, `scMain`
  splitting `lvUploads` from `pbPreview`).
- Every dialog's control set, option set, defaults, tab order and help text.
- Functional density. No progressive disclosure that hides options ShareX shows.

Adapted to macOS:

- Native title bar and window controls; unified toolbar styling rather than a
  WinForms `ToolStrip` border.
- The system menu bar carries the same command tree, with Cmd-based equivalents.
  Upstream shortcuts remain available as a selectable compatibility profile.
- System font stack (SF), macOS control metrics, spacing and corner radii instead
  of 96-dpi Segoe UI metrics.
- Light/dark follows the system appearance.
- Standard macOS file/colour/print panels.
- A menu-bar status item for the tray behaviour.

Not adapted: nothing is removed, renamed, reordered or merged for tidiness. If a
control exists upstream it exists here.

## Consequences

- `planning/ui-layout/*.json` remains the authority for *what* exists and in what
  order. It is no longer the authority for pixel geometry.
- Parity claims about the UI are claims about command coverage and option
  coverage, never about pixel-identical rendering. Screenshot comparison against
  Windows is therefore not a pass/fail gate for the main window; per-control
  presence and behaviour is.
- The ledger records UI entries as implemented only when the control exists, is
  wired to real behaviour, and is reachable by keyboard with an accessibility name.
