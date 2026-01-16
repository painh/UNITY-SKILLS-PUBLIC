using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System;
using System.Text;
using System.Collections.Generic;

namespace ClaudeAgent
{
    public partial class CommandExecutor
    {
        /// <summary>
        /// Registers debugging commands
        /// </summary>
        private void RegisterDebuggingCommands()
        {
            RegisterCommand("logs", LogsCommand);
            RegisterCommand("clear_logs", _ => ClearLogs());
        }

        /// <summary>
        /// Unified logs command
        /// filter: "errors" for errors only, "statistics" for statistics
        /// count: number of entries to retrieve (default 50)
        /// </summary>
        private (bool, string) LogsCommand(CommandParams p)
        {
            try
            {
                string filter = p?.filter?.ToLower() ?? "";
                int count = p?.count ?? 50;

                var sb = new StringBuilder();

                // Retrieve different logs based on filter
                switch (filter)
                {
                    case "errors":
                        // Retrieve only error logs
                        var errors = ConsoleLogManager.GetErrorLogs(count);
                        sb.AppendLine($"Error Logs ({errors.Count} entries):");
                        sb.AppendLine();
                        foreach (var log in errors)
                        {
                            sb.AppendLine(log.ToString());
                            if (!string.IsNullOrEmpty(log.stackTrace))
                            {
                                sb.AppendLine($"  Stack Trace: {log.stackTrace}");
                            }
                        }
                        ConsoleLog($"[CommandExecutor] Retrieved {errors.Count} error logs");
                        break;

                    case "statistics":
                    case "stats":
                        // Retrieve log statistics
                        var stats = ConsoleLogManager.GetLogStatistics();
                        int totalCount = ConsoleLogManager.GetLogCount();
                        sb.AppendLine($"Console Log Statistics (Total: {totalCount}):");
                        sb.AppendLine();
                        sb.AppendLine($"  Log:       {stats[LogType.Log]}");
                        sb.AppendLine($"  Warning:   {stats[LogType.Warning]}");
                        sb.AppendLine($"  Error:     {stats[LogType.Error]}");
                        sb.AppendLine($"  Exception: {stats[LogType.Exception]}");
                        sb.AppendLine($"  Assert:    {stats[LogType.Assert]}");
                        ConsoleLog("[CommandExecutor] Retrieved log statistics");
                        break;

                    default:
                        // Retrieve all logs (default)
                        var logs = ConsoleLogManager.GetRecentLogs(count);
                        sb.AppendLine($"Console Logs ({logs.Count} entries):");
                        sb.AppendLine();
                        foreach (var log in logs)
                        {
                            sb.AppendLine(log.ToString());
                        }
                        ConsoleLog($"[CommandExecutor] Retrieved {logs.Count} console logs");
                        break;
                }

                return (true, sb.ToString());
            }
            catch (Exception e)
            {
                string error = $"Error in logs command: {e.Message}";
                ConsoleLogError($"[CommandExecutor] {error}");
                return (false, error);
            }
        }

        /// <summary>
        /// Clears console logs
        /// </summary>
        private (bool, string) ClearLogs()
        {
            try
            {
                ConsoleLogManager.ClearLogs();
                string result = "Console logs cleared successfully";
                ConsoleLog("[CommandExecutor] " + result);
                return (true, result);
            }
            catch (Exception e)
            {
                string error = $"Error clearing console logs: {e.Message}";
                ConsoleLogError($"[CommandExecutor] {error}");
                return (false, error);
            }
        }
    }
}
