using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YYHEggEgg.Logger
{
    public class LogTraceListener : TraceListener
    {
        public override bool IsThreadSafe => true;
        public string LogSender { get; set; }

        public LogTraceListener(string logSender = "TraceListener")
        {
            NeedIndent = false;
            LogSender = logSender;
        }

        public override void Fail(string? message)
        {
            if (message != null) Log.Erro(message, LogSender);
        }

        public override void Fail(string? message, string? detailMessage)
        {
            if (message != null) Log.Erro(message, LogSender);
            if (detailMessage != null) Log.Erro(detailMessage, LogSender);
        }

        public override void Write(object? o)
        {
            if (o != null) Log.Info(o.ToString() ?? "", LogSender);
        }

        public override void Write(string? message, string? category)
        {
            if (message != null) Log.Info(message, $"{LogSender}:{category}");
        }

        public override void Write(string? message)
        {
            if (message != null) Log.Info(message, LogSender);
        }

        public override void Write(object? o, string? category)
        {
            if (o != null) Log.Info(o.ToString() ?? "", $"{LogSender}:{category}");
        }

        public override void WriteLine(string? message)
        {
            if (message != null) Log.Info(message, LogSender);
        }

        public override void WriteLine(string? message, string? category)
        {
            if (message != null) Log.Info(message, $"{LogSender}:{category}");
        }

        public override void WriteLine(object? o, string? category)
        {
            if (o != null) Log.Info(o.ToString() ?? "", $"{LogSender}:{category}");
        }
    }
}
