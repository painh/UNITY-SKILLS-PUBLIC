using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;

namespace ClaudeAgent
{
    /// <summary>
    /// Class for collecting and managing Unity console logs
    /// </summary>
    [InitializeOnLoad]
    public static class ConsoleLogManager
    {
        private static readonly List<LogEntry> logEntries = new List<LogEntry>();
        private static readonly object logLock = new object();
        private const int MaxLogCount = 1000; // Maximum log count to retain

        /// <summary>
        /// Log entry
        /// </summary>
        [Serializable]
        public class LogEntry
        {
            public string message;
            public string stackTrace;
            public LogType type;
            public DateTime timestamp;

            public LogEntry(string message, string stackTrace, LogType type)
            {
                this.message = message;
                this.stackTrace = stackTrace;
                this.type = type;
                this.timestamp = DateTime.Now;
            }

            public string GetTypeString()
            {
                return type switch
                {
                    LogType.Error => "Error",
                    LogType.Assert => "Assert",
                    LogType.Warning => "Warning",
                    LogType.Log => "Log",
                    LogType.Exception => "Exception",
                    _ => "Unknown"
                };
            }

            public override string ToString()
            {
                return $"[{timestamp:HH:mm:ss}] [{GetTypeString()}] {message}";
            }
        }

        static ConsoleLogManager()
        {
            // Register for log message received event
            Application.logMessageReceived += OnLogMessageReceived;
            Debug.Log("[ConsoleLogManager] Initialized and listening to console logs");
        }

        private static void OnLogMessageReceived(string logString, string stackTrace, LogType type)
        {
            lock (logLock)
            {
                // Add new log entry
                logEntries.Add(new LogEntry(logString, stackTrace, type));

                // Remove old logs when exceeding max count
                while (logEntries.Count > MaxLogCount)
                {
                    logEntries.RemoveAt(0);
                }
            }
        }

        /// <summary>
        /// Gets the most recent N logs
        /// </summary>
        public static List<LogEntry> GetRecentLogs(int count = 50)
        {
            lock (logLock)
            {
                int startIndex = Math.Max(0, logEntries.Count - count);
                int actualCount = Math.Min(count, logEntries.Count);
                return logEntries.GetRange(startIndex, actualCount);
            }
        }

        /// <summary>
        /// Gets only logs of the specified type
        /// </summary>
        public static List<LogEntry> GetLogsByType(LogType type, int count = 50)
        {
            lock (logLock)
            {
                var filtered = new List<LogEntry>();
                for (int i = logEntries.Count - 1; i >= 0 && filtered.Count < count; i--)
                {
                    if (logEntries[i].type == type)
                    {
                        filtered.Insert(0, logEntries[i]);
                    }
                }
                return filtered;
            }
        }

        /// <summary>
        /// Gets only error and exception logs
        /// </summary>
        public static List<LogEntry> GetErrorLogs(int count = 50)
        {
            lock (logLock)
            {
                var errors = new List<LogEntry>();
                for (int i = logEntries.Count - 1; i >= 0 && errors.Count < count; i--)
                {
                    if (logEntries[i].type == LogType.Error || logEntries[i].type == LogType.Exception)
                    {
                        errors.Insert(0, logEntries[i]);
                    }
                }
                return errors;
            }
        }

        /// <summary>
        /// Clears all logs
        /// </summary>
        public static void ClearLogs()
        {
            lock (logLock)
            {
                logEntries.Clear();
                Debug.Log("[ConsoleLogManager] All logs cleared");
            }
        }

        /// <summary>
        /// Gets the current log count
        /// </summary>
        public static int GetLogCount()
        {
            lock (logLock)
            {
                return logEntries.Count;
            }
        }

        /// <summary>
        /// Gets log statistics
        /// </summary>
        public static Dictionary<LogType, int> GetLogStatistics()
        {
            lock (logLock)
            {
                var stats = new Dictionary<LogType, int>
                {
                    { LogType.Log, 0 },
                    { LogType.Warning, 0 },
                    { LogType.Error, 0 },
                    { LogType.Exception, 0 },
                    { LogType.Assert, 0 }
                };

                foreach (var entry in logEntries)
                {
                    stats[entry.type]++;
                }

                return stats;
            }
        }
    }
}
