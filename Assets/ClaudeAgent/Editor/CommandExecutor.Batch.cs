using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ClaudeAgent
{
    public partial class CommandExecutor
    {
        // ====== Batch Operations ======

        /// <summary>
        /// Executes multiple commands as a batch
        /// </summary>
        private (bool, string) ExecuteBatch(CommandParams p)
        {
            // This method is not used because batch-specific parsing is required
            // Use ExecuteBatchFromJson instead
            return (false, "Use ExecuteBatchFromJson instead");
        }

        /// <summary>
        /// Executes batch commands from a JSON string
        /// </summary>
        /// <param name="jsonCommand">Batch command JSON</param>
        /// <returns>(success flag, result JSON)</returns>
        public (bool success, string resultJson) ExecuteBatchFromJson(string jsonCommand)
        {
            var results = new List<BatchCommandResult>();
            int succeeded = 0;
            int failed = 0;
            int cancelled = 0;
            bool hasError = false;

            try
            {
                var json = JObject.Parse(jsonCommand);
                var commandsArray = json["params"]?["commands"] as JArray;

                if (commandsArray == null || commandsArray.Count == 0)
                {
                    return (false, JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = "Missing or empty 'commands' array in params"
                    }));
                }

                Debug.Log($"[CommandExecutor] Executing batch with {commandsArray.Count} commands");

                // Start Undo group
                Undo.IncrementCurrentGroup();
                Undo.SetCurrentGroupName("Batch Command");
                int undoGroup = Undo.GetCurrentGroup();

                // Execute each command sequentially
                for (int i = 0; i < commandsArray.Count; i++)
                {
                    var cmdJson = commandsArray[i] as JObject;

                    if (hasError)
                    {
                        // If previous command failed, cancel remaining commands
                        results.Add(new BatchCommandResult
                        {
                            index = i,
                            success = false,
                            error = "Cancelled: previous command failed"
                        });
                        cancelled++;
                        continue;
                    }

                    try
                    {
                        string cmdString = cmdJson.ToString(Formatting.None);
                        var (success, result) = ExecuteCommand(cmdString);

                        if (success)
                        {
                            results.Add(new BatchCommandResult
                            {
                                index = i,
                                success = true,
                                result = result
                            });
                            succeeded++;
                        }
                        else
                        {
                            results.Add(new BatchCommandResult
                            {
                                index = i,
                                success = false,
                                error = result
                            });
                            failed++;
                            hasError = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        results.Add(new BatchCommandResult
                        {
                            index = i,
                            success = false,
                            error = ex.Message
                        });
                        failed++;
                        hasError = true;
                    }
                }

                // Close Undo group
                Undo.CollapseUndoOperations(undoGroup);

                // Build result
                var response = new
                {
                    success = !hasError,
                    results = results,
                    summary = new
                    {
                        total = commandsArray.Count,
                        succeeded = succeeded,
                        failed = failed,
                        cancelled = cancelled
                    }
                };

                string responseJson = JsonConvert.SerializeObject(response);
                Debug.Log($"[CommandExecutor] Batch completed: {succeeded} succeeded, {failed} failed, {cancelled} cancelled");

                return (!hasError, responseJson);
            }
            catch (Exception e)
            {
                string error = $"Error executing batch: {e.Message}";
                Debug.LogError($"[CommandExecutor] {error}");

                var errorResponse = new
                {
                    success = false,
                    error = error,
                    results = results,
                    summary = new
                    {
                        total = results.Count,
                        succeeded = succeeded,
                        failed = failed,
                        cancelled = cancelled
                    }
                };

                return (false, JsonConvert.SerializeObject(errorResponse));
            }
        }

        /// <summary>
        /// Batch command result
        /// </summary>
        [Serializable]
        private class BatchCommandResult
        {
            public int index;
            public bool success;
            public string result;
            public string error;
        }
    }
}
