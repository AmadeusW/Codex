using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codex.Logging
{
    public class TextLogger : Logger
    {
        private readonly TextWriter writer;

        public TextLogger(TextWriter writer)
        {
            this.writer = writer;
        }

        public override void LogError(string error)
        {
            writer.WriteLine($"ERROR: {error}");
        }

        public override void LogWarning(string warning)
        {
            writer.WriteLine($"WARNING: {warning}");
        }

        public override void LogMessage(string message, MessageKind kind)
        {
            writer.WriteLine($"INFO: {message}");
        }
    }
}
