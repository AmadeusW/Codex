using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codex.Logging
{
    public class ConsoleLogger : Logger
    {
        public override void LogError(string error)
        {
            Console.Error.WriteLine(error);
        }

        public override void LogWarning(string warning)
        {
            Console.WriteLine(warning);
        }

        public override void LogMessage(string message, MessageKind kind)
        {
            if (kind == MessageKind.Informational)
            {
                Console.WriteLine(message);
            }
        }
    }
}
