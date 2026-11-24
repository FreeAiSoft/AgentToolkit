using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Agent.Core.Contracts;

namespace Agent.Core.Services
{
    /// <summary>
    /// Default <see cref="IFileWriter"/> that delegates straight to <see cref="System.IO"/>.
    /// Provided so models do not depend on the filesystem directly and hosts have a ready-made,
    /// testable-through-the-interface implementation.
    /// </summary>
    public sealed class SystemFileWriter : IFileWriter
    {
        public static readonly SystemFileWriter Instance = new SystemFileWriter();

        public bool FileExists(string path)
            => !string.IsNullOrEmpty(path) && File.Exists(path);

        public string ReadAllText(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return null;

            return File.ReadAllText(path);
        }

        public void WriteAllText(string path, string content)
        {
            if (string.IsNullOrEmpty(path))
                return;

            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(path, content ?? "");
        }

        public void CreateDirectory(string path)
        {
            if (!string.IsNullOrEmpty(path))
                Directory.CreateDirectory(path);
        }

        public void CopyFile(string sourcePath, string destinationPath, bool overwrite)
        {
            if (string.IsNullOrEmpty(sourcePath) || string.IsNullOrEmpty(destinationPath))
                return;

            var directory = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            File.Copy(sourcePath, destinationPath, overwrite);
        }

        public bool DirectoryExists(string path)
            => !string.IsNullOrEmpty(path) && Directory.Exists(path);

        public IReadOnlyList<string> EnumerateFiles(string path)
        {
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
                return new string[0];

            return Directory.GetFiles(path, "*", SearchOption.AllDirectories);
        }

        public void DeleteDirectory(string path, bool recursive)
        {
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
                return;

            if (!recursive)
            {
                Directory.Delete(path, false);
                return;
            }

            foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
            {
                try { File.SetAttributes(file, FileAttributes.Normal); }
                catch { /* best effort: readonly reset before delete */ }
            }

            Directory.Delete(path, true);
        }
    }

    /// <summary>
    /// Default <see cref="IFileOpener"/> that opens a file with the user's preferred external
    /// application via <see cref="Process.Start"/>.
    /// </summary>
    public sealed class SystemFileOpener : IFileOpener
    {
        public static readonly SystemFileOpener Instance = new SystemFileOpener();

        public void Open(string path)
        {
            if (string.IsNullOrEmpty(path))
                return;

            try
            {
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch
            {
                // Opening a file is best effort; failures are intentionally ignored.
            }
        }
    }
}
