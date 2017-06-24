using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codex.Logging
{
    public class Logger
    {
        public static readonly Logger Null = new Logger();

        public virtual void LogError(string error)
        {
        }

        public virtual void LogExceptionError(string operation, Exception ex)
        {
            LogError($"Operation: {operation}{Environment.NewLine}{ex.ToString()}");
        }

        public virtual void LogWarning(string warning)
        {
        }

        public virtual void LogMessage(string message, MessageKind kind = MessageKind.Informational)
        {
        }

        public void WriteLine(string message)
        {
            LogMessage(message);
        }
    }

    public enum MessageKind
    {
        Informational,
        Diagnostic,
    }
}
