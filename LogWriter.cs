using System;
using System.Diagnostics;

namespace ServiceSafe
{
    public static class LogWriter
    {
        static EventLog myLog = new EventLog();
        static LogWriter()
        {
            myLog.Source = System.IO.Path.GetFileNameWithoutExtension(typeof(LogWriter).Assembly.Location);
        }

        /// <summary>
        /// Errors the specified ex.
        /// </summary>
        /// <param name="ex">The ex.</param>
        public static void Error(this Exception ex)
        {
            if (null != ex.InnerException)
                myLog.WriteEntry(ex.InnerException.ToString(), EventLogEntryType.Error);
            else
                myLog.WriteEntry(ex.ToString(), EventLogEntryType.Error);
        }

        static void Write(string format, EventLogEntryType logType, params object[] args)
        {
            string strMsg = format;
            if (args != null && args.Length > 0)
            {
                strMsg = string.Format(format, args);
            }
            myLog.WriteEntry(strMsg, logType);
        }

        /// <summary>
        /// Infors the specified format.
        /// </summary>
        /// <param name="format">The format.</param>
        /// <param name="args">The args.</param>
        public static void Infor(this string format, params object[] args)
        {
            Write(format, EventLogEntryType.Information, args);
        }

        /// <summary>
        /// Warns the specified format.
        /// </summary>
        /// <param name="format">The format.</param>
        /// <param name="args">The args.</param>
        public static void Warn(this string format, params object[] args)
        {
            Write(format, EventLogEntryType.Warning, args);
        }

    }

    /// <summary>
    /// 事件日志写入
    /// </summary>
    public class EventLogWriter
    {
        /// <summary>
        /// 日志层次，越高日志越少0-2.
        /// </summary>
        public byte Level { get; set; }

        public void Infor(string format, params object[] args)
        {
            if (Level >= 2)
            {
                LogWriter.Infor(format, args);
            }
        }

        public void Error(Exception ex)
        {
            if (Level >= 0)
            {
                LogWriter.Error(ex);
            }
        }

        public void Warn(string format, params object[] args)
        {
            if (Level >= 1)
            {
                LogWriter.Warn(format, args);
            }
        }
    }
}
