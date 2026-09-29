# Conversion Methods

| Getting Started | Using the App | Technical Reference | Project |
|---|---|---|---|
| [Home](index.md) | [**Usage Guide**](Usage-Guide.md) | [Architecture](Architecture.md) | [Repository](Repository.md) |
| [Installation](Installation.md) | [**Conversion Methods**](Conversion-Methods.md) | [XDVDFS Technical Docs](XDVDFS-Technical-Documentation.md) | [Building from Source](Building-from-Source.md) |
| | [XISO Explorer](XISO-Explorer.md) | [Troubleshooting & FAQ](Troubleshooting-and-FAQ.md) | [Release Notes](Release-Notes.md) |

---

All conversion is performed in-process by the **[XISOSharp](https://github.com/purelogiccode/XISOSharp)** library, with **[CHDSharp](https://github.com/purelogiccode/CHDSharp)** handling CHD output. No external conversion binaries are required or bundled. The Convert tab can produce optimized **XISO**, compressed **ZAR**, compressed **CSO**, or compressed **CHD** output from the same packing pipeline.

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
- **No external tools** — the conversion engines (XISOSharp and CHDSharp) ship inside the application.
- **Redump-aware** — automatically detects XGD1/XGD2/XGD3 and hybrid partition offsets.
- **Already-optimized files are skipped** — images carrying the optimized tag are not converted again.
- **Optional `$SystemUpdate` skipping** for extra space savings — also honored for already-optimized inputs written as CSO/CHD (they are rewritten through the filter before packing).
- **Optional output integrity check** — the new XISO is structurally audited before being reported as successful.
- Integrated with the application's progress reporting, cancellation, and disk monitoring.

## Output Formats

The Convert tab can produce four output formats from the same optimized packing pipeline:

| Format | Extension | Description |
|:---|:---|:---|
| **XISO** (default) | `.iso` | Optimized, tightly packed XISO image. Already-optimized inputs are skipped. |
| **ZAR** | `.zar` | ZArchive: the game-partition file tree packed with pure-C# zstd (level 6, 64 KiB blocks, raw fallback for incompressible blocks). Byte-compatible with `zarchive.exe`/xboxkit, and Xenia canary loads it directly. |
| **CSO** | `.cso` | CISO v2 (LZ4, byte-identical to `xdvdfs compress`), written as a single file. Redump/non-optimized inputs are first repacked to a temporary optimized XISO, then compressed. |
| **CHD** | `.chd` | CHD v5 (Compressed Hunks of Data) written by CHDSharp with the chdman `createdvd` preset: 4096-byte hunks, 2048-byte units, and the `lzma,zlib,huff,flac` codec list. Redump/non-optimized inputs are first repacked to a temporary optimized XISO, then encoded with the `DVD ` metadata tag; the output is deep-verified against every hunk checksum. |

**Skip $SystemUpdate** applies to all four formats: XISO, CSO, and CHD omit the folder during the
rewrite, and ZAR excludes it from the archive tree. **Check Output Integrity** deep-verifies the
newly created CHD (all hunks and hashes) and audits the newly created XISO; for ZAR/CSO, which
cannot be read by the XISO auditor, the source image that is about to be packed is audited instead.

---

## Archive Support

`.zip`, `.7z`, and `.rar` archives are processed transparently:

1. The archive is extracted to a temporary folder (SharpCompress is used first; the bundled 7-Zip CLI handles complex `.7z` archives).
2. The first ISO inside is converted to the selected output format with XISOSharp/CHDSharp; additional ISOs, non-ISO images, and unsafe entries are reported as skipped.
3. Temporary files are cleaned up.

Encrypted or password-protected archives are detected up front and skipped with a clear message.
With **Delete Originals** enabled, an archive is deleted only when every entry was extracted and
every extracted image was converted — skipped entries keep the archive.
