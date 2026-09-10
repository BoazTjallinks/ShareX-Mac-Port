# Attribution and provenance

## This is an unofficial port. All credit for ShareX belongs to the ShareX project.

**ShareX** is created and maintained by the **ShareX Team** — Jaex, McoreD and its
contributors — at <https://github.com/ShareX/ShareX>, and is licensed under the
GNU General Public License v3.

> Copyright (c) 2007-2026 ShareX Team

This repository is a **derivative work**: a personal macOS port. It is:

- **not** an official ShareX release,
- **not** affiliated with, endorsed by, or supported by the ShareX Team,
- **not** a replacement for ShareX on Windows, which remains the real product.

If you like this software, the credit is theirs. Please star, support and use the
upstream project: <https://github.com/ShareX/ShareX>. Please do not report issues
with this port to the upstream ShareX issue tracker.

## Upstream baseline

Everything here is derived from one pinned upstream revision. It is not tracking
`main`.

| | |
| --- | --- |
| Project | ShareX/ShareX |
| Tag | `v21.0.0` |
| Commit | `d2502561f63fc3ff502cacd91514e3f7f2948c74` |
| Released | 3 July 2026 |
| Archive SHA-256 | `8576bd33618960578e143215f3867b3335099d3e148c7366267f8ba9825043f1` |

The pin, the archive hash and per-file hashes are recorded in `upstream.lock.json`
and `inventory/upstream-audit.json`. `scripts/verify-package.py` checks them;
`scripts/restore-upstream.py` unpacks the verified archive into `reference/ShareX`.

`vendor/ShareX-v21.0.0-source.tar.gz` is a verbatim copy of that upstream source,
redistributed under the GPL together with its licence in
`vendor/SHAREX-LICENSE.txt`. `reference/ShareX` is a read-only extraction of it and
is deliberately not tracked in git.

## How upstream code is reused

Ported files keep the original GPL header and carry a provenance line naming the
exact upstream file they came from, for example:

```csharp
// Ported from ShareX v21.0.0 (commit d2502561f63fc3ff502cacd91514e3f7f2948c74),
// file ShareX/Enums.cs.
```

Modifications made for macOS (replacing Win32/GDI+/DirectML/WinForms code with
AppKit, ScreenCaptureKit, SkiaSharp and Avalonia equivalents) are recorded in
`planning/adr/` and in per-project `PORTING-NOTES.md` files.

## Licence

This work is licensed under the **GNU General Public License v3**, the same
licence as upstream ShareX. See `LICENSE`. Third-party dependencies keep their
own licences.

## Trademarks

"ShareX" is the name of the upstream project. It is used here only to describe
what this software is a port of. No endorsement is implied or claimed.
