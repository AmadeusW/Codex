using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Diagnostics;

namespace Codex.API.Logic
{
    internal class UploadAction : IDisposable
    {
        string temporaryPath;

        internal UploadAction()
        {

        }

        internal void MakeLocalCopy(string name, string path)
        {
            temporaryPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Debug.WriteLine($"Uploading source code into temporary location {temporaryPath}");
            Directory.CreateDirectory(temporaryPath);
            var source = new DirectoryInfo(path);
            var destination = new DirectoryInfo(temporaryPath);
            CopyFilesRecursively(source, destination);
        }

        public static void CopyFilesRecursively(DirectoryInfo source, DirectoryInfo target)
        {
            foreach (DirectoryInfo dir in source.GetDirectories())
                CopyFilesRecursively(dir, target.CreateSubdirectory(dir.Name));
            foreach (FileInfo file in source.GetFiles())
                file.CopyTo(Path.Combine(target.FullName, file.Name));
        }

        internal void ExecuteScript(string script)
        {
            if (String.IsNullOrEmpty(script))
                return;

            var scriptPath = Path.Combine(temporaryPath, script);

            if (!File.Exists(scriptPath))
                throw new ArgumentException($"Could not locate {scriptPath}");

            Debug.WriteLine($"Executing {scriptPath}");

            var startInfo = new ProcessStartInfo(scriptPath)
            {
                UseShellExecute = true
            };
            var scriptProcess = Process.Start(startInfo);
            scriptProcess.WaitForExit((int)TimeSpan.FromMinutes(5).TotalMilliseconds);
        }

        internal void ImportToCodex(string name, string solution)
        {
            Debug.WriteLine($"Importing {name}");
            /*
            string workingDirectory = Directory.GetCurrentDirectory();
            Directory.SetCurrentDirectory(temporaryPath);
            Codex.Application.Program.RunRepoImporter(name, temporaryPath);
            */
            var solutionPath = Path.Combine(temporaryPath, solution);
            string codexLocation = @"C:\git\Codex\src\Codex.API\bin\Codex.exe";
            var startInfo = new ProcessStartInfo(codexLocation)
            {
                UseShellExecute = true,
                Arguments = $"{name} {solutionPath}",
                WorkingDirectory = temporaryPath
            };
            var scriptProcess = Process.Start(startInfo);
            scriptProcess.WaitForExit((int)TimeSpan.FromMinutes(5).TotalMilliseconds);
        }

        public void Dispose()
        {
            Debug.WriteLine($"Disposing of the upload artifacts");
            if (Directory.Exists(temporaryPath))
            {
                try
                {
                    Directory.Delete(temporaryPath, true);
                }
                catch
                {
                    // It would be good to log this and manually clean up
                }
            }
        }
    }
}