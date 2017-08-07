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
        }

        internal void ExecuteScript(string script)
        {
            if (String.IsNullOrEmpty(script))
                return;

            var scriptPath = Path.Combine(temporaryPath, script);

            if (!File.Exists(scriptPath))
                throw new ArgumentException($"Could not locate {scriptPath}");

            var startInfo = new ProcessStartInfo(scriptPath)
            {
                UseShellExecute = true
            };
            var scriptProcess = Process.Start(startInfo);
            scriptProcess.WaitForExit((int)TimeSpan.FromMinutes(5).TotalMilliseconds);
        }

        internal void ImportToCodex(string name)
        {
            Codex.Application.Program.RunRepoImporter(name, temporaryPath);
        }

        public void Dispose()
        {
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