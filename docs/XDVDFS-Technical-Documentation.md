# XDVDFS Technical Documentation

| Getting Started | Using the App | Technical Reference | Project |
|---|---|---|---|
| [Home](index.md) | [Usage Guide](Usage-Guide.md) | [Architecture](Architecture.md) | [Repository](Repository.md) |
| [Installation](Installation.md) | [Conversion Methods](Conversion-Methods.md) | [**XDVDFS Technical Docs**](XDVDFS-Technical-Documentation.md) | [Building from Source](Building-from-Source.md) |
| | [XISO Explorer](XISO-Explorer.md) | [Troubleshooting & FAQ](Troubleshooting-and-FAQ.md) | [Release Notes](Release-Notes.md) |

---

## Table of Contents

1. [Introduction](#introduction)
2. [What is XDVDFS?](#what-is-xdvdfs)
3. [XISO File Structure](#xiso-file-structure)
4. [The XDVDFS Format in Code](#the-xdvdfs-format-in-code)
5. [Algorithm Deep Dive](#algorithm-deep-dive)
6. [File Entry Structure](#file-entry-structure)
7. [Volume Descriptor](#volume-descriptor)
8. [Process Flow Diagrams](#process-flow-diagrams)
9. [Integration with XISOSharp](#integration-with-xisosharp)
10. [Summary](#summary)

---

## Introduction

This page documents the Xbox Disc Volume Descriptor File System (XDVDFS) binary format and the traversal/optimization algorithm used to identify, validate, and extract meaningful data from Xbox and Xbox 360 ISO images.

> **Note:** The application delegates all XISO encoding and decoding to the [XISOSharp](https://github.com/purelogiccode/XISOSharp) library. The in-repo parser described by earlier revisions of this page was removed in version 2.8.0. The format description and algorithms below remain accurate — they describe how XISOSharp processes images.

## What is XDVDFS?

**XDVDFS** (Xbox Disc Volume Descriptor File System) is Microsoft's proprietary file system used on original Xbox and Xbox 360 game discs. It is based on a binary tree structure for directory entries and uses 2048-byte sectors (standard CD/DVD sector size).

### Key Characteristics

| Feature | Value | Description |
|---------|-------|-------------|
| Sector Size | 2048 bytes | Standard DVD sector size |
| Header Offset | 0x10000 (65536 bytes) | XISO header location |
| Magic String | `MICROSOFT*XBOX*MEDIA` | Volume descriptor identifier |
| Tree Structure | Binary Search Tree | Directory entries organized as BST |

## XISO File Structure

An XISO file has a specific layout that differs from standard ISO 9660:

```text
┌─────────────────────────────────────────────────────────────────────────────┐
│                           XISO FILE LAYOUT                                   │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│  ┌─────────────────────┐                                                     │
│  │   Volume Descriptor │  Sector 32 (0x10000 bytes from start)              │
│  │     (0x800 bytes)   │  Contains:                                         │
│  │                     │  - Magic ID ("MICROSOFT*XBOX*MEDIA")               │
│  │                     │  - Root Directory Table Sector                     │
│  │                     │  - Volume size info                                │
│  └──────────┬──────────┘                                                     │
│             │                                                                │
│             ▼                                                                │
│  ┌─────────────────────┐                                                     │
│  │  Root Directory     │  Contains file entries as binary tree nodes        │
│  │  Table              │  Each entry: 14 bytes header + filename            │
│  └──────────┬──────────┘                                                     │
│             │                                                                │
│             ▼                                                                │
│  ┌─────────────────────┐                                                     │
│  │  Subdirectories &   │  More directory tables or file data                │
│  │  File Data          │  organized throughout the image                    │
│  └─────────────────────┘                                                     │
│                                                                              │
│  Note: Standard Xbox ISOs (Redump format) have the game partition at        │
│        offset 0x18300000 (XGD1), 0xFD90000 (XGD2), etc.                      │
└─────────────────────────────────────────────────────────────────────────────┘
```

## The XDVDFS Format in Code

### Where the Algorithm Lives

Version 2.8.0 removed the in-repo parser (`BatchConvertIsoToXiso/Services/XisoServices/`). The equivalent
traversal, sector collection, and range consolidation logic now lives in the
[XISOSharp](https://github.com/purelogiccode/XISOSharp) library:

| Concern | XISOSharp API |
|:---|:---|
| Volume descriptor validation | `XisoReader.GetVolumeInfo` / `VolumeInfo` |
| Directory traversal and sector/ranges | `XisoRanges.GetXisoRanges` |
| Image rewriting (conversion) | `XisoReader.Rewrite` |
| Structural audit | `XisoReader.AuditXiso` (used by `XisoIntegrityService`) |

### Core Responsibilities

The traversal performs three critical functions:

1. **Filesystem Traversal** — navigates the binary tree structure of directory entries
2. **Sector Collection** — identifies all sectors containing valid data (files + metadata)
3. **Range Optimization** — consolidates contiguous sectors into ranges for efficient copying

### Key Constants

| Constant | Value | Purpose |
|----------|-------|---------|
| `XisoHeaderOffset` | 0x10000 (65536) | Offset to the XISO volume descriptor from the partition start |
| `Magic` | `MICROSOFT*XBOX*MEDIA` | Volume descriptor signature (checked at `0x000` and `0x7EC`) |
| `SectorSize` | 2048 | Bytes per sector |

### Internal Structures

#### DirectoryWorkItem

A lightweight struct used for the iterative (non-recursive) tree traversal:

```csharp
private struct DirectoryWorkItem
{
    public long RootOffset;    // Byte offset of directory table
    public uint RootSize;      // Size of directory table in bytes
    public long ChildOffset;   // Current offset within directory (tree node position)
}
```

## Algorithm Deep Dive

### Directory Traversal

The XDVDFS uses an **iterative depth-first traversal** using a stack, avoiding recursion limits for deep directory structures:

```text
┌─────────────────────────────────────────────────────────────────────────────┐
│                    BINARY TREE TRAVERSAL VISUALIZATION                       │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│  Directory Table Layout (each entry is a tree node):                        │
│                                                                              │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │  Entry Structure (variable length):                                  │    │
│  │  ┌──────────┬───────────┬───────────┬──────────┬──────────┬────────┐ │    │
│  │  │ Left Ptr │ Right Ptr │  Sector   │   Size   │  Attr    │ Name   │ │    │
│  │  │ (2 bytes)│ (2 bytes) │ (4 bytes) │ (4 bytes)│ (1 byte) │(n bytes│ │    │
│  │  └──────────┴───────────┴───────────┴──────────┴──────────┴────────┘ │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
│                                                                              │
│  Example Directory Tree:                                                    │
│                                                                              │
│                    ┌─────────┐                                              │
│                    │  Root   │                                              │
│                    │ Entry 1 │                                              │
│                    └────┬────┘                                              │
│           ┌─────────────┼─────────────┐                                     │
│           ▼             ▼             ▼                                     │
│      ┌─────────┐   ┌─────────┐   ┌─────────┐                               │
│      │  Left   │   │ Current │   │  Right  │                               │
│      │  Child  │   │ Entry   │   │  Child  │                               │
│      │ (0x0002)│   │         │   │ (0x0005)│                               │
│      └────┬────┘   └─────────┘   └────┬────┘                               │
│           │                          │                                     │
│           ▼                          ▼                                     │
│      ┌─────────┐               ┌─────────┐                                 │
│      │ Sub-Dir │               │ Sub-Dir │                                 │
│      │ Entry A │               │ Entry B │                                 │
│      └─────────┘               └─────────┘                                 │
│                                                                              │
│  Traversal Order: Right → Current → Left (stack-based DFS)                  │
└─────────────────────────────────────────────────────────────────────────────┘
```

#### Traversal Algorithm Steps

1. **Initialize Stack** — push root directory onto the stack with `ChildOffset = 0`
2. **Pop Entry** — get the next work item from the stack
3. **Cycle Detection** — check if this position was visited before (prevents infinite loops)
4. **Read Entry** — parse the binary entry structure from the file stream
5. **Process Children** — push the right child, then after processing the current entry, push the left child
6. **Collect Sectors** — add file/directory sectors to the valid sectors list

```csharp
// The stack-based iterative approach (simplified)
while (stack.Count > 0)
{
    var item = stack.Pop();

    // Read entry at current position
    var leftChildOffset = Utils.ReadUShort(isoFs);   // 0xFFFF = no child
    var rightChildOffset = Utils.ReadUShort(isoFs);
    var entrySector = Utils.ReadUInt(isoFs);
    var entrySize = Utils.ReadUInt(isoFs);
    var attributes = (byte)isoFs.ReadByte();
    var nameLength = (byte)isoFs.ReadByte();

    // Push right child to stack (processed later)
    if (rightChildOffset != 0xFFFF)
        stack.Push(item with { ChildOffset = rightChildOffset * 4 });

    // Process current entry (file or directory)
    if (isDirectory)
        ProcessDirectory(entrySector, entrySize);
    else
        ProcessFile(entrySector, entrySize);

    // Push left child to stack
    if (leftChildOffset != 0xFFFF)
        stack.Push(item with { ChildOffset = leftChildOffset * 4 });
}
```

### Sector Collection

As the tree is traversed, the algorithm collects sectors containing valid data:

```text
┌─────────────────────────────────────────────────────────────────────────────┐
│                         SECTOR COLLECTION LOGIC                              │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│  Valid sectors include:                                                     │
│                                                                              │
│  1. HEADER SECTORS (always included)                                        │
│     ┌──────────────────────────────────────────────────────────────────┐    │
│     │  Sector (offset / 2048)                                          │    │
│     │  Sector (offset / 2048) + 1  (second header sector)              │    │
│     └──────────────────────────────────────────────────────────────────┘    │
│                                                                              │
│  2. DIRECTORY TABLE SECTORS                                                 │
│     ┌──────────────────────────────────────────────────────────────────┐    │
│     │  For each directory:                                             │    │
│     │  - Start: RootOffset / 2048                                      │    │
│     │  - Count: (RootSize + 2047) / 2048  (rounded up)                 │    │
│     │                                                                  │    │
│     │  Example: Directory at offset 0x10000 with size 0x1800 bytes     │    │
│     │  - Sector 8  (0x10000 / 2048)                                    │    │
│     │  - Sector 9  (0x10800 / 2048)                                    │    │
│     │  - Sector 10 (0x11000 / 2048)                                    │    │
│     └──────────────────────────────────────────────────────────────────┘    │
│                                                                              │
│  3. FILE DATA SECTORS                                                       │
│     ┌──────────────────────────────────────────────────────────────────┐    │
│     │  For each file:                                                  │    │
│     │  - Start: (ISO_Offset + entryOffset) / 2048                      │    │
│     │  - Count: (entrySize + 2047) / 2048  (rounded up)                │    │
│     │                                                                  │    │
│     │  Example: File at sector 500 with size 5000 bytes                │    │
│     │  - Start Sector: 500                                             │    │
│     │  - Sector Count: 3  (ceil(5000/2048))                            │    │
│     │  - Sectors: 500, 501, 502                                        │    │
│     └──────────────────────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────────────────────┘
```

### Range Consolidation

After collecting all valid sectors, they are sorted and consolidated into contiguous ranges:

```text
┌─────────────────────────────────────────────────────────────────────────────┐
│                      RANGE CONSOLIDATION ALGORITHM                           │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│  Input:  [8, 9, 10, 50, 51, 52, 53, 100, 200, 201, 202]                     │
│                                                                              │
│  Step 1: Sort (already sorted in this example)                              │
│                                                                              │
│  Step 2: Group contiguous sequences                                         │
│                                                                              │
│          [8, 9, 10]        → Range (8, 10)                                  │
│          [50, 51, 52, 53]  → Range (50, 53)                                 │
│          [100]             → Range (100, 100)                               │
│          [200, 201, 202]   → Range (200, 202)                               │
│                                                                              │
│  Output: [(8, 10), (50, 53), (100, 100), (200, 202)]                        │
│                                                                              │
│  Visualization:                                                             │
│                                                                              │
│  Sector: 0──────8───10──────50────53──100───200───202────→                  │
│          │███████░░░░░░░░░░████████░░███░░░░████████│                      │
│          │       └─Range 1─┘        └2┘ └3┘ └─Range 4─┘                      │
│          └─Padding/Unused─┘                                                  │
│                                                                              │
│  The ranges represent:                                                      │
│  - Range 1: Header + Root Directory                                         │
│  - Range 2: Subdirectory Table                                              │
│  - Range 3: Small File                                                      │
│  - Range 4: Large File                                                      │
│                                                                              │
│  Result: Only 4 copy operations instead of 11 individual sector copies!     │
└─────────────────────────────────────────────────────────────────────────────┘
```

## File Entry Structure

Each file/directory entry in XDVDFS follows this binary format:

```text
┌─────────────────────────────────────────────────────────────────────────────┐
│                      FILE ENTRY BINARY STRUCTURE                             │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│  Offset  Size    Field              Description                              │
│  ─────────────────────────────────────────────────────────────────────────── │
│  0x00    2 bytes LeftSubTree        Offset/4 to left child (0xFFFF = none)   │
│  0x02    2 bytes RightSubTree       Offset/4 to right child (0xFFFF = none)  │
│  0x04    4 bytes StartSector        Sector where file data begins            │
│  0x08    4 bytes FileSize           Size of file in bytes                    │
│  0x0C    1 byte  Attributes         File attributes bitmask                  │
│  0x0D    1 byte  NameLength         Length of filename (0 = empty)           │
│  0x0E    N bytes FileName           ASCII filename (not null-terminated)     │
│  ─────── ──────  Padding            To 4-byte boundary                       │
│                                                                              │
│  Attributes Bitmask:                                                         │
│  ┌─────┬─────┬─────┬─────┬─────┬─────┬─────┬─────┐                          │
│  │  7  │  6  │  5  │  4  │  3  │  2  │  1  │  0  │                          │
│  ├─────┴─────┴─────┴─────┴─────┴─────┴─────┴─────┤                          │
│  │              │Dir │     │     │     │     │     │                          │
│  └──────────────┴────┴─────┴─────┴─────┴─────┴─────┘                          │
│                                                                              │
│  Bit 4 (0x10): Directory flag - Set if entry is a directory                  │
│                                                                              │
│  Example Entry (hex dump):                                                   │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │ FF FF  05 00  2A 00 00 00  80 02 00 00  00  0A  64 65 66 61 75 6C │    │
│  │ 74 2E 78 62 65 00 00 00                                           │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
│                                                                              │
│  Decoded:                                                                    │
│  - LeftSubTree:  0xFFFF (no left child)                                     │
│  - RightSubTree: 0x0005 (right child at offset 0x05 * 4 = 0x14)             │
│  - StartSector:  0x0000002A (sector 42)                                     │
│  - FileSize:     0x00000280 (640 bytes)                                     │
│  - Attributes:   0x00 (regular file)                                        │
│  - NameLength:   0x0A (10 characters)                                       │
│  - FileName:     "default.xbe"                                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

## Volume Descriptor

XISOSharp validates the volume descriptor (`XisoReader.GetVolumeInfo`, returned as `VolumeInfo`) at each known location until a valid signature is found:

```text
┌─────────────────────────────────────────────────────────────────────────────┐
│                      VOLUME DESCRIPTOR STRUCTURE                             │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│  Location: Sector 32 (offset 0x10000 from partition start)                   │
│  Size: 2048 bytes (one sector)                                               │
│                                                                              │
│  Layout:                                                                     │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │ Offset  Size    Field              Value                             │    │
│  ├─────────────────────────────────────────────────────────────────────┤    │
│  │ 0x000   20 bytes Magic ID 1        "MICROSOFT*XBOX*MEDIA"            │    │
│  │ 0x014   4 bytes  RootDirSector     Sector of root directory table    │    │
│  │ 0x018   4 bytes  RootDirSize       Size of root directory in bytes   │    │
│  │ ...     ...      ...               ...                               │    │
│  │ 0x7EC   20 bytes Magic ID 2        "MICROSOFT*XBOX*MEDIA" (verify)   │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
│                                                                              │
│  Validation Strategy (tried in order):                                      │
│                                                                              │
│  ┌──────────┐     ┌──────────┐     ┌──────────┐                            │
│  │  Try 1   │     │  Try 2   │     │  Try 3   │                            │
│  │ Sector   │     │ Game     │     │ Sector   │                            │
│  │ 32 @ 0   │     │ Partition│     │ 0 @ 0    │                            │
│  │ (Standard│     │ Offset   │     │ (Rebuilt │                            │
│  │  XISO)   │     │ (Redump) │     │  XISO)   │                            │
│  └──────────┘     └──────────┘     └──────────┘                            │
│       │                │                │                                   │
│       ▼                ▼                ▼                                   │
│    Success?          Success?         Success?                              │
│    ┌──────┐          ┌──────┐         ┌──────┐                              │
│    │ Yes  │          │ Yes  │         │ Yes  │                              │
│    └──┬───┘          └──┬───┘         └──┬───┘                              │
│       │                 │                │                                  │
│       ▼                 ▼                ▼                                  │
│    Return Volume     Return Volume    Return Volume                         │
│    Descriptor        Descriptor       Descriptor                           │
│                                                                              │
│    If all fail → Throw InvalidDataException                                 │
└─────────────────────────────────────────────────────────────────────────────┘
```

## Process Flow Diagrams

### Complete XISO Processing Flow

```text
┌─────────────────────────────────────────────────────────────────────────────┐
│                    COMPLETE XISO CONVERSION FLOW                             │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│  ┌──────────────┐                                                            │
│  │  Start with  │                                                            │
│  │  Source ISO  │                                                            │
│  └──────┬───────┘                                                            │
│         │                                                                    │
│         ▼                                                                    │
│  ┌─────────────────────────────────┐                                         │
│  │  Detect ISO Type                │                                         │
│  │  ├─ Check file size against     │                                         │
│  │  │  known Redump lengths        │                                         │
│  │  ├─ If match → Redump ISO       │                                         │
│  │  │  (set inputOffset)           │                                         │
│  │  └─ No match → Standard XISO    │                                         │
│  │     (inputOffset = 0)           │                                         │
│  └────────┬────────────────────────┘                                         │
│           │                                                                  │
│           ▼                                                                  │
│  ┌─────────────────────────────────┐                                         │
│  │  GetXisoRanges() (XISOSharp)    │◄────────────────────────────┐          │
│  │                                 │                             │          │
│  │  1. Read Volume Descriptor      │                             │          │
│  │     └─ Validate magic IDs       │                             │          │
│  │  2. Add Header Sectors          │                             │          │
│  │     └─ Sectors at 0x10000       │                             │          │
│  │  3. Traverse File Tree ────────►│                             │          │
│  │     (iterative DFS)             │                             │          │
│  │     ├─ For each directory:      │                             │          │
│  │     │  Add dir table sectors    │                             │          │
│  │     │  Recurse into subdirs     │                             │          │
│  │     └─ For each file:           │                             │          │
│  │        Add file data sectors    │                             │          │
│  │  4. Sort & Consolidate          │                             │          │
│  │     └─ Create ranges from       │                             │          │
│  │        contiguous sectors       │                             │          │
│  └────────┬────────────────────────┘                             │          │
│           │                                                      │          │
│           │ Returns List<(Start, End)>                           │          │
│           ▼                                                      │          │
│  ┌─────────────────────────────────┐                             │          │
│  │  Copy Valid Ranges to Output    │                             │          │
│  │  For each range (start, end):   │                             │          │
│  │  ┌───────────────────────────┐  │                             │          │
│  │  │ Seek to start * 2048      │  │                             │          │
│  │  │ Read (end-start+1)*2048   │  │                             │          │
│  │  │ bytes                     │  │                             │          │
│  │  │ Write to output file      │  │                             │          │
│  │  │ Report progress           │  │                             │          │
│  │  └───────────────────────────┘  │                             │          │
│  └────────┬────────────────────────┘                             │          │
│           │                                                      │          │
│           ▼                                                      │          │
│  ┌─────────────────────────────────┐                             │          │
│  │  Optional: Verify Output        │                             │          │
│  │  └─ Run XDVDFS validation on    │─────────────────────────────┘          │
│  │     the newly created XISO      │   (Recursive call for verification)    │
│  └────────┬────────────────────────┘                                         │
│           ▼                                                                  │
│  ┌──────────────┐                                                            │
│  │  Optimized   │                                                            │
│  │  XISO File   │                                                            │
│  └──────────────┘                                                            │
└─────────────────────────────────────────────────────────────────────────────┘
```

## Integration with XISOSharp

The application converts images through `XisoSharpService`, which delegates the traversal and rewrite to
the XISOSharp library (`XisoReader.Rewrite`):

```csharp
// From XisoSharpService.cs — XISOSharp locates the volume, traverses the tree,
// repacks the file data, and writes the optimized output with the requested name.

var result = XisoReader.Rewrite(inputFile, outputFolder, out var outIsoPath, token,
    outputName: Path.GetFileName(outputPath), progress: progressAdapter);
```

The service complements the library with:

- **Pre-flight checks** — verifies the destination drive has enough free space (plus a safety margin) and
  respects the FAT32 4 GB limit before starting.
- **Already-optimized detection** — `XisoReader.IsOptimizedImage` skips images that are already XISOs.
- **Partial-output cleanup** — the output file is removed if the rewrite fails or is cancelled.
- **Optional verification** — when *Check Output Integrity* is enabled, `XisoReader.AuditXiso` runs
  against the produced image before success is reported.

## Summary

XDVDFS traversal is responsible for:

1. **Parsing binary structures** — decoding the proprietary XDVDFS format
2. **Traversing efficiently** — iterative depth-first traversal avoids stack overflows on deep trees
3. **Validating thoroughly** — detects cycles and validates magic signatures
4. **Optimizing storage** — consolidates sectors into minimal copy ranges
5. **Supporting variants** — handles standard XISOs, Redump ISOs, and rebuilt images

This algorithm enables the application to strip away padding and system update data, producing
optimized XISO files that are smaller but fully functional for emulation and preservation purposes.
In version 2.8.0 and later it is implemented by [XISOSharp](https://github.com/purelogiccode/XISOSharp).
