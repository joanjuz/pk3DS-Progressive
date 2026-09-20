using System;
using System.IO;

namespace pk3DS.WinForms;

internal static class PatchBackupManager
{
    private const string BackupRootName = "pk3DS-backups";

    internal static string BackupOnce(string sourcePath, string patchId)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
            throw new ArgumentException("Backup source path cannot be empty.", nameof(sourcePath));

        if (!File.Exists(sourcePath))
            throw new FileNotFoundException("Backup source file was not found.", sourcePath);

        if (string.IsNullOrWhiteSpace(patchId))
            throw new ArgumentException("Patch backup ID cannot be empty.", nameof(patchId));

        string projectRoot = GetProjectRoot(sourcePath);
        string backupDirectory = Path.Combine(projectRoot, BackupRootName, patchId);
        Directory.CreateDirectory(backupDirectory);

        string backupPath = Path.Combine(backupDirectory, Path.GetFileName(sourcePath));
        if (!File.Exists(backupPath))
            File.Copy(sourcePath, backupPath);

        return backupPath;
    }

    private static string GetProjectRoot(string sourcePath)
    {
        string fullSourcePath = Path.GetFullPath(sourcePath);

        string[] loadedRoots =
        [
            Main.RomFSPath,
            Main.ExeFSPath,
        ];

        foreach (string loadedRoot in loadedRoots)
        {
            if (string.IsNullOrWhiteSpace(loadedRoot))
                continue;

            string fullRoot = Path.GetFullPath(loadedRoot);
            if (!IsInside(fullSourcePath, fullRoot))
                continue;

            return Directory.GetParent(fullRoot)?.FullName
                ?? Path.GetDirectoryName(fullSourcePath)
                ?? Environment.CurrentDirectory;
        }

        string sourceDirectory = Path.GetDirectoryName(fullSourcePath)
            ?? Environment.CurrentDirectory;

        var current = new DirectoryInfo(sourceDirectory);
        while (current is not null)
        {
            if (current.Name.Equals("ExtractedRomFS", StringComparison.OrdinalIgnoreCase) ||
                current.Name.Equals("ExtractedExeFS", StringComparison.OrdinalIgnoreCase))
            {
                return current.Parent?.FullName ?? sourceDirectory;
            }

            current = current.Parent;
        }

        return sourceDirectory;
    }

    private static bool IsInside(string filePath, string directoryPath)
    {
        string root = directoryPath.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

        return filePath.StartsWith(root, StringComparison.OrdinalIgnoreCase);
    }
}
