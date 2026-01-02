using System;
using System.Collections.Generic;
using System.Text;

namespace Azathrix.MiniPanda.Exceptions
{
    public class MiniPandaException : Exception
    {
        public List<StackFrame> PandaStackTrace { get; } = new List<StackFrame>();

        public MiniPandaException(string message) : base(message) { }

        public MiniPandaException(string message, List<StackFrame> stackTrace)
            : base(FormatMessage(message, stackTrace))
        {
            PandaStackTrace = stackTrace;
        }

        private static string FormatMessage(string message, List<StackFrame> stackTrace)
        {
            if (stackTrace == null || stackTrace.Count == 0)
                return message;

            var sb = new StringBuilder(message);
            sb.AppendLine();
            sb.AppendLine("MiniPanda Stack Trace:");
            foreach (var frame in stackTrace)
            {
                sb.AppendLine($"  at {frame.Function} ({frame.File}:{frame.Line})");
            }
            return sb.ToString();
        }
    }

    public class MiniPandaRuntimeException : MiniPandaException
    {
        public MiniPandaRuntimeException(string message) : base(message) { }
        public MiniPandaRuntimeException(string message, List<StackFrame> stackTrace) : base(message, stackTrace) { }
    }

    public class MiniPandaSyntaxException : MiniPandaException
    {
        public int Line { get; }
        public int Column { get; }

        public MiniPandaSyntaxException(string message, int line, int column) : base(message)
        {
            Line = line;
            Column = column;
        }
    }

    public class StackFrame
    {
        public string Function { get; set; }
        public string File { get; set; }
        public int Line { get; set; }

        public StackFrame(string function, string file, int line)
        {
            Function = function;
            File = file;
            Line = line;
        }
    }
}
