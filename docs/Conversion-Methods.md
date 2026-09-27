# Conversion Methods

| Getting Started | Using the App | Technical Reference | Project |
|---|---|---|---|
| [Home](index.md) | [**Usage Guide**](Usage-Guide.md) | [Architecture](Architecture.md) | [Repository](Repository.md) |
| [Installation](Installation.md) | [**Conversion Methods**](Conversion-Methods.md) | [XDVDFS Technical Docs](XDVDFS-Technical-Documentation.md) | [Building from Source](Building-from-Source.md) |
| | [XISO Explorer](XISO-Explorer.md) | [Troubleshooting & FAQ](Troubleshooting-and-FAQ.md) | |

---

All ISO to XISO conversion is performed in-process by the **[XISOSharp](https://github.com/purelogiccode/XISOSharp)** library. No external conversion binaries are required or bundled.

## How It Works

XISOSharp **repacks** the game partition into an optimized XISO:

1. The XDVDFS volume descriptor is located (auto-detecting Redump/XGD partition offsets).
2. The directory tree is read and the file data is copied into a new, tightly packed XISO.
3. Gaps between files are removed, and the optimized tag is written so later runs can skip already-converted files.

## What Gets Removed

- **Video Partition** (DVD movie/demonstration content) — ~7–387 MB removed
- **End Padding** (empty sectors after the last file) — variable
- **System Update** (optional, when *Skip $SystemUpdate* is enabled) — ~100–300 MB removed

## Visual Comparison

```text
Redump ISO (Original):
[Video Partition][XDVDFS: Header][Dir][File A][gap][File B][gap][File C][Padding]

XISOSharp Output:
[XDVDFS: Header][Dir][File A][File B][File C]   (gaps removed, tightly packed)
                      ^    ^    ^
                 Files repositioned for maximum compression
```

## Features

- **Smallest output size** — files are packed tightly, with video partition and padding removed.
- **No external tools** — the conversion engine ships inside the application.
- **Redump-aware** — automatically detects XGD1/XGD2/XGD3 and hybrid partition offsets.
- **Already-optimized files are skipped** — images carrying the optimized tag are not converted again.
- **Optional `$SystemUpdate` skipping** for extra space savings.
- **Optional output integrity check** — the new XISO is structurally audited before being reported as successful.
- Integrated with the application's progress reporting, cancellation, and disk monitoring.

---

## CUE/BIN Support

Classic disc images distributed as a `.cue` + `.bin` pair are handled automatically:

1. The bundled `bchunk` tool converts the pair into a standard ISO.
2. The ISO is then converted to XISO with XISOSharp.

Both files must be present in the same folder with matching names.

## Archive Support

`.zip`, `.7z`, and `.rar` archives are processed transparently:

1. The archive is extracted to a temporary folder (SharpCompress is used first; the bundled 7-Zip CLI handles complex `.7z` archives).
2. Any ISO inside is converted to XISO with XISOSharp.
3. Temporary files are cleaned up.

Encrypted or password-protected archives are detected up front and skipped with a clear message.
