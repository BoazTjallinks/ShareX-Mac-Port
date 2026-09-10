# ADR 0003 — Dependency pins and deviations from the upstream baseline

Status: accepted (stage 0)
Date: 2026-09-10

## Context

PROJECT-SPEC.md section 2 lists the upstream v21.0.0 package versions and says to
"Start with compatible versions, check official package support/advisories at
implementation time, and pin the selected working set" — explicitly *not* a
blanket instruction to keep old patches.

## Decision

`Directory.Packages.props` uses central package management. Versions carried over
unchanged from `reference/ShareX/Directory.Packages.props`:

| Package | Version | Note |
| --- | --- | --- |
| Avalonia (+ Desktop, Themes.Fluent, Fonts.Inter, Controls.ColorPicker) | 12.0.5 | same as upstream |
| SkiaSharp | 3.119.4 | same as upstream |
| Newtonsoft.Json | 13.0.4 | same as upstream; required for serializer-semantic parity |
| CommunityToolkit.Mvvm | 8.4.2 | same as upstream |
| FluentFTP | 54.2.0 | same as upstream |
| SSH.NET | 2025.1.0 | same as upstream |
| ZXing.Net | 0.16.11 | same as upstream |

Deviations:

| Package | Upstream | Selected | Reason |
| --- | --- | --- | --- |
| Microsoft.Data.Sqlite | 10.0.9 | **10.0.11** | 10.0.9 resolves SQLitePCLRaw.lib.e_sqlite3 2.1.11, which NuGet reports as GHSA-2m69-gcr7-jv3q (high). 10.0.11 restores advisory-clean. Verified by `dotnet restore` on both. |

Dropped entirely (Windows-only, not portable):

| Package | Why |
| --- | --- |
| Avalonia.Win32.Interoperability | Win32 interop |
| SkiaSharp.Views.WindowsForms | WinForms host |
| ZXing.Net.Bindings.Windows.Compatibility | System.Drawing/GDI+ binding; replaced by a portable pixel adapter |
| Microsoft.ML.OnnxRuntime.DirectML | DirectML execution provider; replaced by a portable ONNX Runtime build when stage 9 lands |

`global.json` pins SDK **10.0.400** (the installed SDK feature band) with
`rollForward: latestFeature`. Note that 10.0.11 is the *runtime* version and is
deliberately not used here.

Build properties are **not** copied from `reference/ShareX/Directory.build.props`:
that file sets `win-x64`/`win-arm64` RuntimeIdentifiers unconditionally. No
project in this solution sets a RuntimeIdentifier; the publish step supplies
`osx-arm64` explicitly.

Trimming and AOT stay off. Upstream relies on reflection-driven JSON type
discovery and enum/description lookup; trimming is premature.
