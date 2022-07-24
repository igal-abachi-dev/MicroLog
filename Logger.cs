using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MicroLog
{
    public static class Logger
    {
        [NonSerialized] private static readonly object _syncRoot = new object();
        [SecurityCritical] [NonSerialized] public volatile static bool Enabled = true;
        private const bool _writeFile = true;
        //        private const bool _writeDebug = false;

        private static readonly FileLogger _fileLog = _writeFile ? new FileLogger() : null;

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.General)
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = true,
            //ReferenceHandler = ReferenceHandler.Preserve,
            NumberHandling = JsonNumberHandling.AllowReadingFromString
        };

        public static void Information(string message = "",
        [CallerMemberName] string fn = "",
        [CallerFilePath] string src = "")
        {
            if (Enabled)
                Write(LogEventLevel.Information, message, FormatCaller(fn, src));
        }
        public static void Information<T>(T obj,
      [CallerMemberName] string fn = "",
      [CallerFilePath] string src = "")
        {
            if (Enabled)
                Write(LogEventLevel.Information, JsonSerializer.Serialize<T>(obj, JsonOptions), FormatCaller(fn, src));
        }

        public static void Warning(string message,
        [CallerMemberName] string fn = "",
        [CallerFilePath] string src = "")
        {
            if (Enabled)
                Write(LogEventLevel.Warning, message, FormatCaller(fn, src));
        }

        public static void Error(string message,
        [CallerMemberName] string fn = "",
        [CallerFilePath] string src = "")
        {
            if (Enabled)
                Write(LogEventLevel.Error, message, FormatCaller(fn, src));
        }

        public static void Error(Exception ex,
        [CallerMemberName] string fn = "",
        [CallerFilePath] string src = "")
        {
            if (Enabled)
                Write(LogEventLevel.Error, null, FormatCaller(fn, src), ex);
        }

        private static string FormatCaller(string memberName, string sourceFilePath)
        {
            var src = Path.GetFileNameWithoutExtension(sourceFilePath);
            return src + "." + memberName + "()";
        }

        private static void Write(LogEventLevel level, string message, string fn = null, Exception exception = null)
        {
            var threadId = Environment.CurrentManagedThreadId;// Thread.CurrentThread.Name;

            var logEvent = new LogEvent(DateTime.Now, level, exception, message, fn, threadId);
            Emit(logEvent);
        }

        private static void Emit(LogEvent logEvent)
        {
            TextWriter output = logEvent.Level != LogEventLevel.Error ? Console.Out : Console.Error;
            lock (_syncRoot)
            {
                string formattedMsg = logEvent.ToString();
                output.WriteLine(formattedMsg);
                output.Flush();

                //                if (_writeDebug)
                //                    Debug.WriteLine(formattedMsg);

                _fileLog?.Append(formattedMsg);
            }
        }

        public static bool HasDebugModeFlag(this string[] args)
        {
            return (args.Length == 1 && args[0].ToUpperInvariant() == "-DEBUG");
        }
    }

    internal class FileLogger
    {
        private static readonly Encoding _utf8Encoder = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        private readonly string _path;

        public FileLogger(string path = "_log.txt")
        {
            _path = path;
            try
            {
                if (File.Exists(_path))
                    File.Delete(_path);
            }
            catch { }
        }

        public void Append(string msg)
        {
            try
            {
                using (var sw = new StreamWriter(_path, append: true, _utf8Encoder))
                {
                    sw.WriteLine(msg);
                }
            }
            catch { }
        }
    }

    internal enum LogEventLevel
    {
        Information,
        Warning,
        Error
    }

    [Serializable]
    internal class LogEvent
    {
        public LogEvent(DateTime now, LogEventLevel level, Exception exception, string message, string fn, int threadId)
        {
            Timestamp = now;
            Level = level;
            Exception = exception;
            Message = message;
            FunctionName = fn;
            ThreadId = threadId;
        }

        public DateTime Timestamp { get; }

        public LogEventLevel Level { get; }

        public string Message { get; }

        public string FunctionName { get; }

        public Exception Exception { get; }

        public int ThreadId { get; }


        public override string ToString()
        {
            var msg = Exception == null ? Message : FormatException(Exception);

            return $"{Timestamp.ToString("s")} [{ThreadId}] [{FormatLevel(Level)}] {FunctionName} : {msg}";
        }

        private static string FormatLevel(LogEventLevel level)
        {
            switch (level)
            {
                case LogEventLevel.Error:
                    return "Err";
                case LogEventLevel.Warning:
                    return "Wrn";
                case LogEventLevel.Information:
                default:
                    return "Inf";
            }
        }
        private static string FormatException(Exception e)
        {
            var inner = e.GetBaseException();
            if (inner == e)
            {
                return e.ToString();
            }
            else
            {
                return e.ToString() + Environment.NewLine + inner.ToString();
            }
        }
    }
}

