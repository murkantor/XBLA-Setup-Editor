// =============================================================================
// XdeltaHelper.cs - XDelta Patch Creation Utility
// =============================================================================
// This helper class provides functionality to create xdelta binary patches.
// XDelta patches are compact binary diffs that can transform an original file
// into a modified file, making them ideal for distributing mods.
//
// OVERVIEW:
// =========
// XDelta is a binary delta compression tool that computes the differences
// between two files and creates a small patch file. Users can then apply
// the patch to their original file to get the modified version.
//
// Benefits of XDelta patches for modding:
// - Small file size (only stores differences)
// - Legal to distribute (doesn't include copyrighted content)
// - Reliable (verifies original file before patching)
// - Widely supported by modding tools
//
// DEPENDENCIES:
// =============
// Requires xdelta3.exe to be present in the application directory.
// The xdelta3 tool can be downloaded from: https://github.com/jmacd/xdelta
//
// USAGE WORKFLOW:
// ===============
// 1. User loads original XEX file
// 2. User makes modifications through the editor tabs
// 3. User saves modified XEX file
// 4. OfferCreatePatch() prompts to create a patch
// 5. Patch is created using: xdelta3 -e -s <original> <modified> <patch>
// 6. Patch can be applied later with: xdelta3 -d -s <original> <patch> <output>
//
// SPLIT XEX SUPPORT:
// ==================
// For mods that require splitting content across two XEX files (due to size
// constraints), OfferCreateSplitPatches() creates two separate patch files.
// =============================================================================

using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace XBLA_Setup_Editor
{
    /// <summary>
    /// Helper class for creating xdelta binary patches.
    /// Provides methods to create patches for single or split XEX files.
    /// </summary>
    /// <remarks>
    /// XDelta patches are the preferred distribution method for mods because:
    /// <list type="bullet">
    ///   <item>They don't contain copyrighted game data</item>
    ///   <item>They're much smaller than full XEX files (~MBs vs ~16MB)</item>
    ///   <item>They verify the original file is correct before patching</item>
    /// </list>
    /// </remarks>
    public static class XdeltaHelper
    {
        // =====================================================================
        // XDELTA3 PATH HELPERS
        // =====================================================================

        /// <summary>
        /// Gets the expected path to xdelta3.exe in the application directory.
        /// </summary>
        /// <returns>Full path to xdelta3.exe.</returns>
        public static string GetXdeltaExePath()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "xdelta3.exe");
        }

        /// <summary>
        /// Checks if xdelta3.exe is available for use.
        /// </summary>
        /// <returns>True if xdelta3.exe exists in the application directory.</returns>
        public static bool IsXdeltaAvailable()
        {
            return File.Exists(GetXdeltaExePath());
        }

        // =====================================================================
        // PATCH CREATION
        // =====================================================================

        /// <summary>
        /// Creates an xdelta patch from original data to a modified file.
        /// </summary>
        /// <param name="originalData">The original unmodified file bytes.</param>
        /// <param name="modifiedFilePath">Path to the modified file on disk.</param>
        /// <param name="patchOutputPath">Path where the patch file will be saved.</param>
        /// <param name="error">Error message if the operation fails.</param>
        /// <returns>True if patch was created successfully, false otherwise.</returns>
        /// <remarks>
        /// The patch creation process:
        /// 1. Write original data to a temporary file
        /// 2. Run xdelta3 with: -e (encode) -s (source file)
        /// 3. xdelta3 computes differences and writes patch
        /// 4. Clean up temporary file
        ///
        /// Command line: xdelta3 -e -s "original" "modified" "patch"
        /// </remarks>
        public static bool CreatePatch(byte[] originalData, string modifiedFilePath, string patchOutputPath, out string error)
        {
            error = string.Empty;
            var xdeltaPath = GetXdeltaExePath();

            // Verify xdelta3.exe exists
            if (!File.Exists(xdeltaPath))
            {
                error = "xdelta3.exe not found in application directory.";
                return false;
            }

            // Write original data to a temp file for xdelta3 to read
            var tempOriginal = Path.GetTempFileName();
            try
            {
                File.WriteAllBytes(tempOriginal, originalData);

                // Run xdelta3 to create the patch
                // -e = encode (create patch)
                // -s = source file (the original)
                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = xdeltaPath,
                        Arguments = $"-f -e -s \"{tempOriginal}\" \"{modifiedFilePath}\" \"{patchOutputPath}\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true  // Don't show console window
                    }
                };

                process.Start();

                // Read both streams concurrently to avoid deadlocks when
                // xdelta3 fills its stdout/stderr pipe buffers.
                var stdoutTask = Task.Run(() => process.StandardOutput.ReadToEnd());
                var stderrTask = Task.Run(() => process.StandardError.ReadToEnd());
                process.WaitForExit();
                var stderr = stderrTask.Result;

                // Some xdelta3 builds emit "Checksum verification is disabled" to
                // stderr and exit non-zero even though the patch file was written
                // successfully (it just lacks source-window Adler32 checksums).
                // Treat the patch as valid if the output file exists and is non-empty;
                // surface any xdelta3 output as a non-fatal warning instead.
                bool patchExists = File.Exists(patchOutputPath) &&
                                   new FileInfo(patchOutputPath).Length > 0;

                if (process.ExitCode != 0 && !patchExists)
                {
                    error = $"xdelta3 failed (exit code {process.ExitCode}): {stderr}".Trim();
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(stderr))
                    error = $"xdelta3 warning: {stderr}".Trim();

                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
            finally
            {
                // Clean up temp file
                if (File.Exists(tempOriginal))
                    File.Delete(tempOriginal);
            }
        }

        // =====================================================================
        // USER INTERACTION - Single File
        // =====================================================================

        /// <summary>
        /// Prompts the user to create an xdelta patch after saving a modified XEX.
        /// Always asks the user to select the vanilla/baseline XEX so the patch
        /// works correctly even when starting from a pre-modified file.
        /// </summary>
        /// <param name="owner">Parent form for dialogs.</param>
        /// <param name="originalData">No longer used — kept for API compatibility.</param>
        /// <param name="savedFilePath">Path where the modified XEX was just saved.</param>
        public static void OfferCreatePatch(IWin32Window owner, byte[] originalData, string savedFilePath)
        {
            // Don't offer if xdelta3 isn't available
            if (!IsXdeltaAvailable())
                return;

            // Ask user if they want to create a patch
            var result = MessageBox.Show(owner,
                "Create an xdelta patch file?",
                "Create XDelta Patch",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result != DialogResult.Yes)
                return;

            var dir = Path.GetDirectoryName(savedFilePath) ?? "";

            // Ask the user to select the vanilla/baseline XEX.
            // This is necessary because the loaded XEX may already be pre-modified;
            // the patch must diff against the unmodified original (e.g. defaultCE.xex).
            using var ofd = new OpenFileDialog
            {
                Title = "Select the vanilla / unmodified XEX to patch FROM (e.g. defaultCE.xex)",
                Filter = "XEX files (*.xex)|*.xex|All files (*.*)|*.*",
                InitialDirectory = dir,
                FileName = "defaultCE.xex"
            };

            if (ofd.ShowDialog(owner) != DialogResult.OK)
                return;

            byte[] baselineData;
            try { baselineData = File.ReadAllBytes(ofd.FileName); }
            catch (Exception ex)
            {
                MessageBox.Show(owner, $"Could not read baseline XEX:\n{ex.Message}",
                    "XDelta Patch Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Show save dialog for patch location
            var baseName = Path.GetFileNameWithoutExtension(savedFilePath);
            using var sfd = new SaveFileDialog
            {
                Title = "Save XDelta Patch",
                Filter = "XDelta Patch (*.xdelta)|*.xdelta|All files (*.*)|*.*",
                InitialDirectory = dir,
                FileName = $"{baseName}.xdelta"
            };

            if (sfd.ShowDialog(owner) != DialogResult.OK)
                return;

            if (CreatePatch(baselineData, savedFilePath, sfd.FileName, out var error))
            {
                var msg = string.IsNullOrWhiteSpace(error)
                    ? $"Patch created successfully!\n\n{sfd.FileName}"
                    : $"Patch created successfully!\n\n{sfd.FileName}\n\nNote: {error}";
                MessageBox.Show(owner, msg, "XDelta Patch",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show(owner,
                    $"Failed to create patch:\n{error}",
                    "XDelta Patch Failed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        // =====================================================================
        // USER INTERACTION - Split XEX Files
        // =====================================================================

        /// <summary>
        /// Prompts the user to create xdelta patches for two split XEX files.
        /// Used when mods are too large to fit in a single XEX and must be
        /// split across two separate files.
        /// </summary>
        /// <param name="owner">Parent form for dialogs.</param>
        /// <param name="originalData">Original unmodified XEX bytes.</param>
        /// <param name="savedFilePath1">Path to first split XEX file.</param>
        /// <param name="savedFilePath2">Path to second split XEX file.</param>
        /// <remarks>
        /// Split XEX mode creates two separate patches:
        /// - One for the first XEX (typically solo mode levels)
        /// - One for the second XEX (typically remaining content)
        ///
        /// Both patches are created from the same original file, allowing
        /// users to patch their copy into either version.
        /// </remarks>
        public static void OfferCreateSplitPatches(IWin32Window owner, byte[] originalData, string savedFilePath1, string savedFilePath2)
        {
            // Don't offer if xdelta3 isn't available
            if (!IsXdeltaAvailable())
                return;

            // Ask user if they want to create patches for both files
            var result = MessageBox.Show(owner,
                "Create xdelta patch files for both XEX files?",
                "Create XDelta Patches",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result != DialogResult.Yes)
                return;

            var dir1 = Path.GetDirectoryName(savedFilePath1) ?? "";

            // Ask the user to select the vanilla/baseline XEX.
            // Both patches diff against the same unmodified original.
            using var ofd = new OpenFileDialog
            {
                Title = "Select the vanilla / unmodified XEX to patch FROM (e.g. defaultCE.xex)",
                Filter = "XEX files (*.xex)|*.xex|All files (*.*)|*.*",
                InitialDirectory = dir1,
                FileName = "defaultCE.xex"
            };

            if (ofd.ShowDialog(owner) != DialogResult.OK)
                return;

            byte[] baselineData;
            try { baselineData = File.ReadAllBytes(ofd.FileName); }
            catch (Exception ex)
            {
                MessageBox.Show(owner, $"Could not read baseline XEX:\n{ex.Message}",
                    "XDelta Patch Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var successCount = 0;
            var errors = new System.Collections.Generic.List<string>();
            var warnings = new System.Collections.Generic.List<string>();

            // Create patch for first XEX file
            var baseName1 = Path.GetFileNameWithoutExtension(savedFilePath1);
            var patchPath1 = Path.Combine(dir1, $"{baseName1}.xdelta");

            if (CreatePatch(baselineData, savedFilePath1, patchPath1, out var error1))
            {
                successCount++;
                if (!string.IsNullOrWhiteSpace(error1)) warnings.Add($"Patch 1: {error1}");
            }
            else
                errors.Add($"Patch 1: {error1}");

            // Create patch for second XEX file
            var dir2 = Path.GetDirectoryName(savedFilePath2) ?? "";
            var baseName2 = Path.GetFileNameWithoutExtension(savedFilePath2);
            var patchPath2 = Path.Combine(dir2, $"{baseName2}.xdelta");

            if (CreatePatch(baselineData, savedFilePath2, patchPath2, out var error2))
            {
                successCount++;
                if (!string.IsNullOrWhiteSpace(error2)) warnings.Add($"Patch 2: {error2}");
            }
            else
                errors.Add($"Patch 2: {error2}");

            // Report results to user
            if (successCount == 2)
            {
                var msg = $"Both patches created successfully!\n\n{patchPath1}\n{patchPath2}";
                if (warnings.Count > 0) msg += $"\n\nNote: {string.Join("\n", warnings)}";
                MessageBox.Show(owner, msg, "XDelta Patches",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else if (successCount == 1)
            {
                MessageBox.Show(owner,
                    $"One patch created, one failed:\n\n{string.Join("\n", errors)}",
                    "XDelta Patches",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
            else
            {
                MessageBox.Show(owner,
                    $"Failed to create patches:\n\n{string.Join("\n", errors)}",
                    "XDelta Patches Failed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
    }
}
