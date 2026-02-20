// =============================================================================
// XexExtender.cs - XEX File Extension Utility (EXPERIMENTAL)
// =============================================================================
// Attempts to extend XEX files by appending additional data blocks.
// This is an EXPERIMENTAL feature with significant limitations.
//
// WARNING: THIS FEATURE DOES NOT WORK RELIABLY
// =============================================
// Due to Xenia's page table validation, extending XEX files is severely limited.
// The maximum extension is constrained to the difference between image_size and
// the sum of all block memory sizes, typically only ~32KB for GoldenEye XBLA.
//
// XEX STRUCTURE OVERVIEW:
// =======================
// XEX files are Xbox 360 executables with the following layout:
//
// Header (0x0000 - 0x3000):
//   0x000-0x003: Magic "XEX2"
//   0x104: Image size (big-endian u32) - total decompressed memory size
//   0x108: SHA1 hash (20 bytes) - hash of the image data
//   0x1C00: File format info header
//   0x1C08+: Block entries (8 bytes each)
//
// Data Blocks (0x3000+):
//   Each block consists of:
//   - data_size bytes of actual content (stored in file)
//   - zero_size bytes of zero padding (only in memory, not stored)
//
// BLOCK ENTRY FORMAT (8 bytes):
// =============================
//   [0-3] data_size (big-endian u32) - bytes stored in file
//   [4-7] zero_size (big-endian u32) - bytes of zero padding in memory
//
// MEMORY MAPPING:
// ===============
// XEX_BASE_ADDRESS = 0x82000000
// Each block's memory address = BASE + sum of previous (data_size + zero_size)
// New data would be appended at: BASE + total_block_memory
//
// XENIA COMPATIBILITY CONSTRAINT:
// ===============================
// Xenia validates: sum(data_size + zero_size) <= image_size
// We CANNOT change image_size without causing page table validation failures.
// Therefore: max_extension = image_size - sum(data_size + zero_size)
// For GoldenEye XBLA, this is typically only 0x8000 (32KB).
//
// EXTENSION PROCESS:
// ==================
// 1. Analyze XEX to find last block and calculate headroom
// 2. Append new data to end of file
// 3. Update last block's data_size to include appended data
// 4. DO NOT modify image_size or recalculate SHA1
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;

namespace XBLA_Setup_Editor.Data
{
    /// <summary>
    /// EXPERIMENTAL: Attempts to extend XEX files with additional read-only data.
    /// Limited by Xenia's page table validation to ~32KB for GoldenEye XBLA.
    /// </summary>
    public class XexExtender
    {
        // XEX structure constants
        private const int HEADER_IMAGE_SIZE_OFFSET = 0x104;
        private const int HEADER_SHA1_OFFSET = 0x108;
        private const int FILE_FORMAT_INFO_OFFSET = 0x1C00;
        private const int BLOCK_ENTRIES_OFFSET = 0x1C08;
        private const int BLOCK_ENTRY_SIZE = 8;
        private const int DATA_START_OFFSET = 0x3000;

        // Memory mapping
        private const uint XEX_BASE_ADDRESS = 0x82000000;

        /// <summary>
        /// Information about a data block in the XEX
        /// </summary>
        public class BlockInfo
        {
            public int Index { get; set; }
            public int FileOffset { get; set; }
            public int DataSize { get; set; }
            public int ZeroSize { get; set; }
            public uint MemoryAddress { get; set; }

            public int TotalMemorySize => DataSize + ZeroSize;
        }

        /// <summary>
        /// Result of analyzing an XEX file
        /// </summary>
        public class XexAnalysis
        {
            public bool IsValid { get; set; }
            public string Error { get; set; } = string.Empty;
            public int FileSize { get; set; }
            public int ImageSize { get; set; }
            public byte[] CurrentSha1 { get; set; } = Array.Empty<byte>();
            public int CompressionType { get; set; }
            public List<BlockInfo> Blocks { get; set; } = new List<BlockInfo>();
            public int TotalDataSize { get; set; }
            public int TotalZeroSize { get; set; }
            public uint EndMemoryAddress { get; set; }
            public int MaxExtensionSize { get; set; }

            // --- Zero_size method headroom ---
            // The last block's zero_size can be traded for data_size without
            // touching image_size — no 32KB Xenia constraint applies.
            /// <summary>zero_size of the last block. Max bytes the zero_size method can insert.</summary>
            public int LastBlockZeroSize { get; set; }
            /// <summary>VA where zero_size-backed data would start (last_block.VA + last_block.data_size).</summary>
            public uint ZeroSizeInsertAddress { get; set; }

            public override string ToString()
            {
                if (!IsValid) return $"Invalid XEX: {Error}";

                return $"XEX Analysis:\n" +
                       $"  File size: 0x{FileSize:X} ({FileSize / 1024.0 / 1024.0:F2} MB)\n" +
                       $"  Image size: 0x{ImageSize:X} ({ImageSize / 1024.0 / 1024.0:F2} MB)\n" +
                       $"  Compression: {(CompressionType == 1 ? "Basic" : CompressionType == 2 ? "LZX" : $"Type {CompressionType}")}\n" +
                       $"  Blocks: {Blocks.Count}\n" +
                       $"  Data end address: 0x{EndMemoryAddress:X8}\n" +
                       $"  SHA1: {BitConverter.ToString(CurrentSha1).Replace("-", "").ToLower()}";
            }
        }

        /// <summary>
        /// Result of extending an XEX file
        /// </summary>
        public class ExtensionResult
        {
            public bool Success { get; set; }
            public string Error { get; set; } = string.Empty;
            public int BytesAdded { get; set; }
            public uint NewDataMemoryAddress { get; set; }
            public uint NewEndMemoryAddress { get; set; }
            public int NewImageSize { get; set; }
            public byte[] NewSha1 { get; set; } = Array.Empty<byte>();
            public List<string> Log { get; set; } = new List<string>();
        }

        /// <summary>
        /// Analyzes an XEX file to determine its structure and extension capabilities
        /// </summary>
        public static XexAnalysis Analyze(byte[] xexData)
        {
            var result = new XexAnalysis();

            try
            {
                // Verify magic
                if (xexData.Length < 0x2000 ||
                    xexData[0] != 'X' || xexData[1] != 'E' || xexData[2] != 'X' || xexData[3] != '2')
                {
                    result.IsValid = false;
                    result.Error = "Not a valid XEX2 file";
                    return result;
                }

                result.FileSize = xexData.Length;

                // Read image size (big-endian)
                result.ImageSize = ReadU32BE(xexData, HEADER_IMAGE_SIZE_OFFSET);

                // Read SHA1 hash
                result.CurrentSha1 = new byte[20];
                Array.Copy(xexData, HEADER_SHA1_OFFSET, result.CurrentSha1, 0, 20);

                // Read file format info
                int formatInfoSize = ReadU32BE(xexData, FILE_FORMAT_INFO_OFFSET);
                result.CompressionType = ReadU32BE(xexData, FILE_FORMAT_INFO_OFFSET + 4);

                // Only support basic compression for now
                if (result.CompressionType != 1)
                {
                    result.IsValid = false;
                    result.Error = $"Unsupported compression type: {result.CompressionType}. Only basic compression (type 1) is supported.";
                    return result;
                }

                // Parse block entries
                int numBlocks = (formatInfoSize - 8) / BLOCK_ENTRY_SIZE;
                int fileOffset = DATA_START_OFFSET;
                uint memoryOffset = 0;

                for (int i = 0; i < numBlocks; i++)
                {
                    int entryOffset = BLOCK_ENTRIES_OFFSET + i * BLOCK_ENTRY_SIZE;
                    int dataSize = ReadU32BE(xexData, entryOffset);
                    int zeroSize = ReadU32BE(xexData, entryOffset + 4);

                    var block = new BlockInfo
                    {
                        Index = i,
                        FileOffset = fileOffset,
                        DataSize = dataSize,
                        ZeroSize = zeroSize,
                        MemoryAddress = XEX_BASE_ADDRESS + memoryOffset
                    };

                    result.Blocks.Add(block);
                    result.TotalDataSize += dataSize;
                    result.TotalZeroSize += zeroSize;

                    fileOffset += dataSize;
                    memoryOffset += (uint)(dataSize + zeroSize);
                }

                result.EndMemoryAddress = XEX_BASE_ADDRESS + memoryOffset;

                // Calculate max extension based on image_size headroom
                // CRITICAL: Xenia validates that block memory total <= image_size
                // We cannot change image_size without causing page table validation failures
                // So max extension = image_size - block_memory_total
                int blockMemoryTotal = result.TotalDataSize + result.TotalZeroSize;
                result.MaxExtensionSize = result.ImageSize - blockMemoryTotal;

                // Ensure non-negative
                if (result.MaxExtensionSize < 0)
                    result.MaxExtensionSize = 0;

                // Calculate zero_size method headroom.
                // The last block's zero_size can be converted to data_size without
                // changing image_size — no Xenia constraint applies.
                if (result.Blocks.Count > 0)
                {
                    var lb = result.Blocks[result.Blocks.Count - 1];
                    result.LastBlockZeroSize = lb.ZeroSize;
                    // New data would be mapped at: last_block.VA + last_block.data_size
                    result.ZeroSizeInsertAddress = lb.MemoryAddress + (uint)lb.DataSize;
                }

                result.IsValid = true;
            }
            catch (Exception ex)
            {
                result.IsValid = false;
                result.Error = $"Analysis failed: {ex.Message}";
            }

            return result;
        }

        /// <summary>
        /// Extends an XEX file by appending data and updating headers.
        /// 
        /// IMPORTANT: Due to Xenia's page table validation, we can only extend up to the
        /// difference between image_size and block_memory_total. For GoldenEye XBLA,
        /// this is typically 0x8000 (32KB).
        /// 
        /// We ONLY update the block data_size, NOT the image_size, as changing image_size
        /// causes Xenia to fail page table validation.
        /// </summary>
        /// <param name="xexData">Original XEX file data</param>
        /// <param name="newData">Data to append</param>
        /// <param name="recalculateSha1">Ignored - we don't modify image_size so hash stays valid</param>
        /// <returns>Modified XEX data and extension result</returns>
        public static (byte[]? ModifiedXex, ExtensionResult Result) Extend(
            byte[] xexData, 
            byte[] newData, 
            bool recalculateSha1 = false)  // Ignored - we don't change image_size
        {
            var result = new ExtensionResult();

            try
            {
                // Analyze first
                var analysis = Analyze(xexData);
                if (!analysis.IsValid)
                {
                    result.Success = false;
                    result.Error = analysis.Error;
                    return (null, result);
                }

                result.Log.Add($"Original XEX: {analysis.FileSize:N0} bytes");
                result.Log.Add($"Original image size: 0x{analysis.ImageSize:X}");
                result.Log.Add($"Data to append: {newData.Length:N0} bytes");
                result.Log.Add($"Max extension (image_size headroom): {analysis.MaxExtensionSize:N0} bytes ({analysis.MaxExtensionSize / 1024}KB)");

                // Validate
                if (newData == null || newData.Length == 0)
                {
                    result.Success = false;
                    result.Error = "No data to append";
                    return (null, result);
                }

                // Check against the REAL limit: image_size headroom
                // We cannot change image_size without causing Xenia page table validation failures
                if (newData.Length > analysis.MaxExtensionSize)
                {
                    result.Success = false;
                    result.Error = $"Extension size ({newData.Length:N0} bytes) exceeds available headroom ({analysis.MaxExtensionSize:N0} bytes / {analysis.MaxExtensionSize / 1024}KB). " +
                                   $"The XEX can only be extended by the difference between image_size (0x{analysis.ImageSize:X}) and block memory total (0x{analysis.TotalDataSize + analysis.TotalZeroSize:X}).";
                    return (null, result);
                }

                // Find the last block
                var lastBlock = analysis.Blocks[analysis.Blocks.Count - 1];
                result.Log.Add($"Last block: index={lastBlock.Index}, data_size=0x{lastBlock.DataSize:X}, zero_size=0x{lastBlock.ZeroSize:X}");

                // Calculate new values
                int newLastBlockDataSize = lastBlock.DataSize + newData.Length;
                uint newDataMemoryAddress = analysis.EndMemoryAddress;
                uint newEndMemoryAddress = newDataMemoryAddress + (uint)newData.Length;

                result.BytesAdded = newData.Length;
                result.NewDataMemoryAddress = newDataMemoryAddress;
                result.NewEndMemoryAddress = newEndMemoryAddress;
                result.NewImageSize = analysis.ImageSize; // UNCHANGED!

                result.Log.Add($"New data at memory address: 0x{newDataMemoryAddress:X8}");
                result.Log.Add($"New end address: 0x{newEndMemoryAddress:X8}");
                result.Log.Add($"Image size: 0x{analysis.ImageSize:X} (UNCHANGED - critical for Xenia compatibility)");

                // Create the modified XEX
                byte[] modifiedXex = new byte[xexData.Length + newData.Length];
                Array.Copy(xexData, modifiedXex, xexData.Length);
                Array.Copy(newData, 0, modifiedXex, xexData.Length, newData.Length);

                // Update ONLY the last block's data_size - DO NOT change image_size!
                int lastBlockEntryOffset = BLOCK_ENTRIES_OFFSET + lastBlock.Index * BLOCK_ENTRY_SIZE;
                WriteU32BE(modifiedXex, lastBlockEntryOffset, newLastBlockDataSize);
                result.Log.Add($"Updated block {lastBlock.Index} data_size: 0x{lastBlock.DataSize:X} -> 0x{newLastBlockDataSize:X}");

                // DO NOT update image_size - this causes Xenia crashes!
                // DO NOT recalculate SHA1 - image_size is unchanged so original hash is still valid
                result.NewSha1 = analysis.CurrentSha1;
                result.Log.Add("Image hash unchanged (image_size not modified)");

                result.Success = true;
                result.Log.Add($"Extension complete. New file size: {modifiedXex.Length:N0} bytes");

                return (modifiedXex, result);
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Error = $"Extension failed: {ex.Message}";
                return (null, result);
            }
        }

        /// <summary>
        /// Extends an XEX file and writes the result to disk
        /// </summary>
        public static ExtensionResult ExtendFile(
            string inputXexPath,
            string outputXexPath,
            byte[] newData,
            bool recalculateSha1 = false,
            bool createBackup = true)
        {
            var result = new ExtensionResult();

            try
            {
                if (!File.Exists(inputXexPath))
                {
                    result.Success = false;
                    result.Error = $"Input file not found: {inputXexPath}";
                    return result;
                }

                result.Log.Add($"Reading: {inputXexPath}");
                byte[] xexData = File.ReadAllBytes(inputXexPath);

                var (modifiedXex, extResult) = Extend(xexData, newData, recalculateSha1);

                if (!extResult.Success || modifiedXex == null)
                {
                    return extResult;
                }

                // Create backup if requested and output is same as input
                if (createBackup && Path.GetFullPath(inputXexPath) == Path.GetFullPath(outputXexPath))
                {
                    string backupPath = inputXexPath + ".backup";
                    File.Copy(inputXexPath, backupPath, true);
                    extResult.Log.Add($"Backup created: {backupPath}");
                }

                // Write output
                File.WriteAllBytes(outputXexPath, modifiedXex);
                extResult.Log.Add($"Written: {outputXexPath}");

                return extResult;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Error = $"File operation failed: {ex.Message}";
                return result;
            }
        }

        /// <summary>
        /// Extends an XEX file using the zero_size → data_size swap method.
        ///
        /// Unlike <see cref="Extend"/>, this method does NOT consume image_size headroom
        /// and is therefore not limited to ~32KB. Instead it converts the last block's
        /// zero_size (BSS-style zero memory not stored in the file) into file-backed data.
        ///
        /// HOW IT WORKS:
        ///   last_block.data_size += newData.Length
        ///   last_block.zero_size -= newData.Length
        ///   newData appended to end of file
        ///   image_size unchanged → no Xenia page-table constraint
        ///
        /// The new data is accessible at VA = last_block.MemoryAddress + original_data_size
        /// (i.e. <see cref="XexAnalysis.ZeroSizeInsertAddress"/>).
        ///
        /// CAUTION: The zero region must be unused by the game at runtime.
        /// Run Analyze() and inspect ZeroSizeInsertAddress before committing.
        /// </summary>
        public static (byte[]? ModifiedXex, ExtensionResult Result) ExtendViaZeroSize(
            byte[] xexData, byte[] newData)
        {
            var result = new ExtensionResult();

            try
            {
                var analysis = Analyze(xexData);
                if (!analysis.IsValid)
                {
                    result.Success = false;
                    result.Error = analysis.Error;
                    return (null, result);
                }

                result.Log.Add("Method: zero_size swap (no image_size constraint)");
                result.Log.Add($"Last block zero_size available: 0x{analysis.LastBlockZeroSize:X} ({analysis.LastBlockZeroSize / 1024}KB)");
                result.Log.Add($"Data to insert: {newData.Length:N0} bytes ({newData.Length / 1024}KB)");

                if (newData == null || newData.Length == 0)
                {
                    result.Success = false;
                    result.Error = "No data to append";
                    return (null, result);
                }

                if (newData.Length > analysis.LastBlockZeroSize)
                {
                    result.Success = false;
                    result.Error = $"Data ({newData.Length:N0} bytes) exceeds last block zero_size " +
                                   $"({analysis.LastBlockZeroSize:N0} bytes / {analysis.LastBlockZeroSize / 1024}KB). " +
                                   $"The zero_size method requires the last block to have enough zero_size to absorb the new data.";
                    return (null, result);
                }

                var lastBlock = analysis.Blocks[analysis.Blocks.Count - 1];
                int newDataSize = lastBlock.DataSize + newData.Length;
                int newZeroSize = lastBlock.ZeroSize - newData.Length;

                // Append data to end of file
                byte[] modifiedXex = new byte[xexData.Length + newData.Length];
                Array.Copy(xexData, modifiedXex, xexData.Length);
                Array.Copy(newData, 0, modifiedXex, xexData.Length, newData.Length);

                // Update last block header: swap zero_size → data_size
                int lastBlockEntryOffset = BLOCK_ENTRIES_OFFSET + lastBlock.Index * BLOCK_ENTRY_SIZE;
                WriteU32BE(modifiedXex, lastBlockEntryOffset,     newDataSize);
                WriteU32BE(modifiedXex, lastBlockEntryOffset + 4, newZeroSize);

                result.Log.Add($"Block {lastBlock.Index} data_size: 0x{lastBlock.DataSize:X} → 0x{newDataSize:X}");
                result.Log.Add($"Block {lastBlock.Index} zero_size: 0x{lastBlock.ZeroSize:X} → 0x{newZeroSize:X}");
                result.Log.Add($"image_size 0x{analysis.ImageSize:X}: UNCHANGED");
                result.Log.Add($"New data VA: 0x{analysis.ZeroSizeInsertAddress:X8}");

                result.BytesAdded = newData.Length;
                result.NewDataMemoryAddress = analysis.ZeroSizeInsertAddress;
                result.NewEndMemoryAddress = analysis.EndMemoryAddress; // end VA unchanged
                result.NewImageSize = analysis.ImageSize;               // unchanged
                result.NewSha1 = analysis.CurrentSha1;                  // unchanged
                result.Success = true;
                result.Log.Add($"Extension complete. New file size: {modifiedXex.Length:N0} bytes");

                return (modifiedXex, result);
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Error = $"Extension failed: {ex.Message}";
                return (null, result);
            }
        }

        /// <summary>
        /// Validates whether a zero_size-method extension would be feasible.
        /// </summary>
        public static (bool IsValid, string Message) ValidateZeroSizeExtension(byte[] xexData, int extensionSize)
        {
            var analysis = Analyze(xexData);
            if (!analysis.IsValid)
                return (false, analysis.Error);

            if (extensionSize <= 0)
                return (false, "Extension size must be positive");

            if (extensionSize > analysis.LastBlockZeroSize)
                return (false, $"Size ({extensionSize:N0} bytes / {extensionSize / 1024}KB) exceeds " +
                               $"last block zero_size ({analysis.LastBlockZeroSize:N0} bytes / {analysis.LastBlockZeroSize / 1024}KB)");

            return (true, $"Valid. New data at 0x{analysis.ZeroSizeInsertAddress:X8} " +
                          $"({extensionSize / 1024}KB of {analysis.LastBlockZeroSize / 1024}KB zero_size used)");
        }

        /// <summary>
        /// Gets the memory address where appended data would be located
        /// </summary>
        public static uint GetAppendAddress(byte[] xexData)
        {
            var analysis = Analyze(xexData);
            return analysis.IsValid ? analysis.EndMemoryAddress : 0;
        }

        /// <summary>
        /// Validates that an extension would be safe
        /// </summary>
        public static (bool IsValid, string Message) ValidateExtension(byte[] xexData, int extensionSize)
        {
            var analysis = Analyze(xexData);

            if (!analysis.IsValid)
                return (false, analysis.Error);

            if (extensionSize <= 0)
                return (false, "Extension size must be positive");

            if (extensionSize > analysis.MaxExtensionSize)
                return (false, $"Extension size ({extensionSize:N0} bytes) exceeds recommended maximum ({analysis.MaxExtensionSize:N0} bytes)");

            uint newEndAddress = analysis.EndMemoryAddress + (uint)extensionSize;
            if (newEndAddress < analysis.EndMemoryAddress) // Overflow check
                return (false, "Extension would cause address overflow");

            // Check if we'd exceed typical Xbox 360 memory limits
            if (newEndAddress > 0x90000000)
                return (false, $"Extended image would exceed safe memory bounds (0x{newEndAddress:X8})");

            return (true, $"Extension is valid. New data at 0x{analysis.EndMemoryAddress:X8}, ending at 0x{newEndAddress:X8}");
        }

        #region Helper Methods

        private static int ReadU32BE(byte[] data, int offset)
        {
            return (data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3];
        }

        private static void WriteU32BE(byte[] data, int offset, int value)
        {
            data[offset] = (byte)((value >> 24) & 0xFF);
            data[offset + 1] = (byte)((value >> 16) & 0xFF);
            data[offset + 2] = (byte)((value >> 8) & 0xFF);
            data[offset + 3] = (byte)(value & 0xFF);
        }

        #endregion
    }
}
