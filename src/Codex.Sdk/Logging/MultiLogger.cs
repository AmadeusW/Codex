using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codex.Logging
{
    public class MultiLogger : Logger
    {
        private readonly Logger[] loggers;

        public MultiLogger(params Logger[] loggers)
        {
            this.loggers = loggers;
        }

        public override void LogError(string error)
        {
            foreach (var logger in loggers)
            {
                logger.LogError(error);
            }
        }

        public override void LogWarning(string warning)
        {
            foreach (var logger in loggers)
            {
                logger.LogWarning(warning);
            }
        }

        public override void LogMessage(string message, MessageKind kind)
        {
            foreach (var logger in loggers)
            {
                logger.LogMessage(message, kind);
            }
        }
    }
}
