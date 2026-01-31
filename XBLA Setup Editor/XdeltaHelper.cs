using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

namespace XBLA_Setup_Editor
{
    /// <summary>
    /// Helper class for creating xdelta patches.
    /// </summary>
    public static class XdeltaHelper
    {
        /// <summary>
        /// Gets the path to xdelta3.exe in the application directory.
        /// </summary>
        public static string GetXdeltaExePath()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "xdelta3.exe");
        }

        /// <summary>
        /// Checks if xdelta3.exe is available.
        /// </summary>
        public static bool IsXdeltaAvailable()
        {
            return File.Exists(GetXdeltaExePath());
        }

        /// <summary>
        /// Creates an xdelta patch from original data to modified file.
        /// </summary>
        /// <param name="originalData">The original unmodified data</param>
        /// <param name="modifiedFilePath">Path to the modified file</param>
        /// <param name="patchOutputPath">Path where the patch will be saved</param>
        /// <param name="error">Error message if failed</param>
        /// <returns>True if successful</returns>
        public static bool CreatePatch(byte[] originalData, string modifiedFilePath, string patchOutputPath, out string error)
        {
            error = string.Empty;
            var xdeltaPath = GetXdeltaExePath();

            if (!File.Exists(xdeltaPath))
            {
                error = "xdelta3.exe not found in application directory.";
                return false;
            }

            // Write original data to temp file
            var tempOriginal = Path.GetTempFileName();
            try
            {
                File.WriteAllBytes(tempOriginal, originalData);

                // Run xdelta3 -e -s <original> <modified> <patch>
                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = xdeltaPath,
                        Arguments = $"-e -s \"{tempOriginal}\" \"{modifiedFilePath}\" \"{patchOutputPath}\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                var stderr = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    error = $"xdelta3 failed (exit code {process.ExitCode}): {stderr}";
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
            finally
            {
                if (File.Exists(tempOriginal))
                    File.Delete(tempOriginal);
            }
        }

        /// <summary>
        /// Prompts the user to create an xdelta patch after saving.
        /// </summary>
        /// <param name="owner">Parent form</param>
        /// <param name="originalData">Original unmodified XEX data</param>
        /// <param name="savedFilePath">Path where the modified XEX was saved</param>
        public static void OfferCreatePatch(IWin32Window owner, byte[] originalData, string savedFilePath)
        {
            if (!IsXdeltaAvailable())
                return;

            var result = MessageBox.Show(owner,
                "Create an xdelta patch file?",
                "Create XDelta Patch",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result != DialogResult.Yes)
                return;

            var dir = Path.GetDirectoryName(savedFilePath) ?? "";
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

            if (CreatePatch(originalData, savedFilePath, sfd.FileName, out var error))
            {
                MessageBox.Show(owner,
                    $"Patch created successfully!\n\n{sfd.FileName}",
                    "XDelta Patch",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
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

        /// <summary>
        /// Prompts the user to create xdelta patches for split XEX files.
        /// </summary>
        /// <param name="owner">Parent form</param>
        /// <param name="originalData">Original unmodified XEX data</param>
        /// <param name="savedFilePath1">Path where the first modified XEX was saved</param>
        /// <param name="savedFilePath2">Path where the second modified XEX was saved</param>
        public static void OfferCreateSplitPatches(IWin32Window owner, byte[] originalData, string savedFilePath1, string savedFilePath2)
        {
            if (!IsXdeltaAvailable())
                return;

            var result = MessageBox.Show(owner,
                "Create xdelta patch files for both XEX files?",
                "Create XDelta Patches",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result != DialogResult.Yes)
                return;

            var successCount = 0;
            var errors = new System.Collections.Generic.List<string>();

            // Patch 1
            var dir1 = Path.GetDirectoryName(savedFilePath1) ?? "";
            var baseName1 = Path.GetFileNameWithoutExtension(savedFilePath1);
            var patchPath1 = Path.Combine(dir1, $"{baseName1}.xdelta");

            if (CreatePatch(originalData, savedFilePath1, patchPath1, out var error1))
                successCount++;
            else
                errors.Add($"Patch 1: {error1}");

            // Patch 2
            var dir2 = Path.GetDirectoryName(savedFilePath2) ?? "";
            var baseName2 = Path.GetFileNameWithoutExtension(savedFilePath2);
            var patchPath2 = Path.Combine(dir2, $"{baseName2}.xdelta");

            if (CreatePatch(originalData, savedFilePath2, patchPath2, out var error2))
                successCount++;
            else
                errors.Add($"Patch 2: {error2}");

            if (successCount == 2)
            {
                MessageBox.Show(owner,
                    $"Both patches created successfully!\n\n{patchPath1}\n{patchPath2}",
                    "XDelta Patches",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
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
