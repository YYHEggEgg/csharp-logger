using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YYHEggEgg.Logger
{
    /// <summary>
    /// Provide a 'logger' implement for those code pushing logs always with the same sender paramter.
    /// </summary>
    public class LoggerChannel
    {
        public readonly BaseLogger TargetLogger;
        public string? LogSender = null;

        public LoggerChannel(BaseLogger targetLogger, string? globalSender)
        {
            TargetLogger = targetLogger ?? throw new ArgumentNullException(nameof(targetLogger));
            LogSender = globalSender;
        }

        public void LogVerb(string content) => TargetLogger.Verb(content, LogSender);
        public void LogDbug(string content) => TargetLogger.Dbug(content, LogSender);
        public void LogInfo(string content) => TargetLogger.Info(content, LogSender);
        public void LogWarn(string content) => TargetLogger.Warn(content, LogSender);
        public void LogErro(string content) => TargetLogger.Erro(content, LogSender);
    }
}
