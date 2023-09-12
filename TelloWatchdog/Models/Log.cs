using System;

namespace TelloWatchdog.Models
{
    public enum LogLevel
    {
        Info,
        Warn,
        Error,
    }

    public class Log
    {
        public LogLevel Level { get; set; }
        public DateTime DateTime { get; set; }
        public string Message { get; set; }

        public Log(LogLevel level, string message)
        {
            this.Level = level;
            this.DateTime = DateTime.Now;
            this.Message = message;
        }
    }
}
