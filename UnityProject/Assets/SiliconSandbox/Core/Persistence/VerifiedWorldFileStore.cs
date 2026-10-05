using System;
using System.IO;

namespace SiliconSandbox.Persistence
{
    // Caller supplies a path in the game's chosen local save directory and a
    // complete archive validator. A failed candidate never replaces the last
    // good current file. Replacement and backup share the same filesystem.
    public static class VerifiedWorldFileStore
    {
        public static void Save(string currentPath, byte[] archiveBytes,
            Action<Stream> validateArchive)
        {
            if (string.IsNullOrWhiteSpace(currentPath) || archiveBytes == null ||
                validateArchive == null)
                throw new ArgumentException("Complete save path, bytes, and validator are required.");
            currentPath = Path.GetFullPath(currentPath);
            var directory = Path.GetDirectoryName(currentPath);
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
                throw new DirectoryNotFoundException(
                    "The world save directory does not exist.");
            var pending = currentPath + "." + Guid.NewGuid().ToString("N") +
                ".pending";
            var backup = currentPath + ".previous";
            try
            {
                using (var output = new FileStream(pending, FileMode.CreateNew,
                    FileAccess.Write, FileShare.None))
                {
                    output.Write(archiveBytes, 0, archiveBytes.Length);
                    output.Flush(true);
                }
                using (var input = new FileStream(pending, FileMode.Open,
                    FileAccess.Read, FileShare.Read))
                    validateArchive(input);
                if (File.Exists(currentPath))
                    File.Replace(pending, currentPath, backup);
                else File.Move(pending, currentPath);
            }
            catch
            {
                if (File.Exists(pending)) File.Delete(pending);
                throw;
            }
        }
    }
}
