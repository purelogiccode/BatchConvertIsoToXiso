# Conversion Methods

| Getting Started | Using the App | Technical Reference | Project |
|---|---|---|---|
| [Home](index.md) | [**Usage Guide**](Usage-Guide.md) | [Architecture](Architecture.md) | [Repository](Repository.md) |
| [Installation](Installation.md) | [**Conversion Methods**](Conversion-Methods.md) | [XDVDFS Technical Docs](XDVDFS-Technical-Documentation.md) | [Building from Source](Building-from-Source.md) |
| | [XISO Explorer](XISO-Explorer.md) | [Troubleshooting & FAQ](Troubleshooting-and-FAQ.md) | |

---

The application offers **three conversion methods**, each using a different approach to convert Redump ISOs into XISO format. All three remove the same wasted content; they differ in how the remaining data is laid out and how safe/fast the process is.

## Method Comparison

| Feature | extract-xiso | xdvdfs | Built-in (Modified Deterous Logic) |
|:--------|:-------------|:-------|:-----------------------------------|
| **Approach** | Repack | Repack | Trim |
| **Output Size** | Smallest | Smallest | Slightly larger |
| **Safety** | High | High | **Highest** |
| **Preserves Layout** | No | No | **Yes** |
| **External Tool** | Required | Required | **Built-in** |
| **Speed** | Fast | Fast | **Fastest** (selective sector copy only) |

## What Gets Removed

All three methods remove these parts from Redump ISOs:

- **Video Partition** (DVD movie/demonstration content) — ~7–387 MB removed
- **End Padding** (empty sectors after the last file) — variable
- **System Update** (optional, when *Skip $SystemUpdate* is enabled) — ~100–300 MB removed

## Visual Comparison

```text
Redump ISO (Original):
[Video Partition][XDVDFS: Header][Dir][File A][gap][File B][gap][File C][Padding]

extract-xiso / xdvdfs Output:
[XDVDFS: Header][Dir][File A][File B][File C]   (gaps removed, tightly packed)
                      ^    ^    ^
                 Files repositioned for maximum compression

Built-in (Trim) Output:
[XDVDFS: Header][Dir][File A][gap][File B][gap][File C]
                      ^         ^
                 Original layout preserved; only video/padding removed
```

---

## extract-xiso (External Tool)

- **Developed by:** the [XboxDev](https://github.com/XboxDev/extract-xiso) team
- **Approach:** reads the entire ISO and creates a new optimized XISO with files packed tightly together.
- **Pros:** smallest output size; long-standing, well-tested tool.
- **Cons:** requires an external executable (`extract-xiso.exe`, bundled with the application).
- **Best for:** maximum storage savings with a battle-tested tool.

## xdvdfs (External Tool)

- **Developed by:** [antangelo](https://github.com/antangelo/xdvdfs)
- **Approach:** modern Rust implementation that rebuilds the XISO from scratch.
- **Pros:** smallest output size; modern, actively maintained implementation.
- **Cons:** requires an external executable (`xdvdfs.exe`, bundled with the application).
- **Best for:** maximum compatibility and storage savings.

## Built-in Method (Modified Deterous Logic)

- **Original source:** based on the [XboxKit by Deterous](https://github.com/Deterous/XboxKit) `XDVDFS.cs` implementation for traversing the XDVDFS filesystem.
- **Approach:** **trims** the ISO by identifying and copying only valid sectors (header, directory tree, file data) while preserving the original file layout and the gaps between files.

### Key Modifications Compared to the Original

- Converted recursive directory traversal to an **iterative stack-based** approach, avoiding stack overflows on deep directory structures.
- Added **cycle detection** using a `HashSet` to prevent infinite loops on malformed images.
- **Enhanced signature detection** supporting multiple known XGD1/XGD2/XGD3 partition offsets, robust validation, and fallback sector scanning for non-standard/Redump variants.
- **Optional `$SystemUpdate` skipping** for extra space savings.
- Improved directory-entry parsing, name reading, attribute handling, and comprehensive error handling and validation.
- Modern C# implementation integrated with the application's progress reporting, cancellation, and disk monitoring.

### Pros and Cons

- **Pros:**
  - **Fastest** — no repacking, just selective sector copying.
  - **Safest** — preserves the exact original XDVDFS structure and layout.
  - **No external dependencies** — pure C# implementation.
- **Cons:** output is larger (gaps between files from the original ISO are preserved).

- **Best for:** preserving the original structure, debugging, and maximum safety/compatibility.

The algorithm behind this method is documented in depth on the [XDVDFS Technical Documentation](XDVDFS-Technical-Documentation.md) page.

---

## Which Should You Choose?

| Use Case | Recommended Method |
|:---------|:-------------------|
| Maximum storage savings | xdvdfs or extract-xiso |
| Preserving exact game structure | Built-in (Modified Deterous Logic) |
| Debugging / development | Built-in (Modified Deterous Logic) |
| FTP transfer to Xbox | xdvdfs or extract-xiso |

**For most users:** if storage space is critical, use **xdvdfs** or **extract-xiso** for maximum compression. Use the **built-in logic** when you want to preserve the original file layout and structure from the source ISO.

---

## CUE/BIN Support

Classic disc images distributed as a `.cue` + `.bin` pair are handled automatically:

1. The bundled `bchunk` tool converts the pair into a standard ISO.
2. The ISO is then converted with your selected method.

Both files must be present in the same folder with matching names.

## Archive Support

`.zip`, `.7z`, and `.rar` archives are processed transparently:

1. The archive is extracted to a temporary folder (SharpCompress is used first; the bundled 7-Zip CLI handles complex `.7z` archives).
2. Any ISO inside is converted with your selected method.
3. Temporary files are cleaned up.

Encrypted or password-protected archives are detected up front and skipped with a clear message.
