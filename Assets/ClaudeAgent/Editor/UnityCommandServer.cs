using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using WebSocketSharp;
using WebSocketSharp.Server;
using Newtonsoft.Json.Linq;

namespace ClaudeAgent
{
    /// <summary>
    /// Class for holding pending commands
    /// </summary>
    public class PendingCommand
    {
        public string RawMessage { get; set; }
        public Action<string> SendResponse { get; set; }
    }

    /// <summary>
    /// Simple JSON command execution server
    /// No Agent functionality, just receives JSON, executes, and returns result
    /// </summary>
    public class CommandBehavior : WebSocketBehavior
    {
        // Command queue (thread-safe)
        private static readonly Queue<PendingCommand> commandQueue = new Queue<PendingCommand>();
        private static readonly object queueLock = new object();

        /// <summary>
        /// Checks if there are pending commands in queue
        /// </summary>
        public static bool HasPendingCommands()
        {
            lock (queueLock)
            {
                return commandQueue.Count > 0;
            }
        }

        /// <summary>
        /// Gets command from queue
        /// </summary>
        public static PendingCommand DequeueCommand()
        {
            lock (queueLock)
            {
                if (commandQueue.Count > 0)
                {
                    return commandQueue.Dequeue();
                }
                return null;
            }
        }

        protected override void OnMessage(MessageEventArgs e)
        {
            string rawMessage = e.Data;
            Debug.Log($"[CommandServer] Received: {rawMessage}");

            // Capture reference to WebSocket session
            var session = this;

            // Add command to queue (will be processed on main thread)
            lock (queueLock)
            {
                commandQueue.Enqueue(new PendingCommand
                {
                    RawMessage = rawMessage,
                    SendResponse = (responseJson) =>
                    {
                        try
                        {
                            if (session.State == WebSocketState.Open)
                            {
                                session.Send(responseJson);
                                Debug.Log($"[CommandServer] Response sent: {responseJson}");
                            }
                            else
                            {
                                Debug.LogWarning("[CommandServer] Cannot send response: connection not open");
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"[CommandServer] Error sending response: {ex.Message}");
                        }
                    }
                });
            }

            Debug.Log($"[CommandServer] Command queued. Queue size: {commandQueue.Count}");
        }

        protected override void OnOpen()
        {
            Debug.Log($"[CommandServer] Client connected: {Context.UserEndPoint}");
        }

        protected override void OnClose(CloseEventArgs e)
        {
            Debug.Log($"[CommandServer] Client disconnected: {e.Reason}");
        }

        protected override void OnError(ErrorEventArgs e)
        {
            Debug.LogError($"[CommandServer] WebSocket error: {e.Message}");
        }
    }

    /// <summary>
    /// Simple command server Editor Window
    /// </summary>
    public class UnityCommandServerWindow : EditorWindow
    {
        private WebSocketServer server;
        private const int Port = 8766;
        private bool isRunning = false;
        private Vector2 logScrollPosition;
        private List<string> logs = new List<string>();
        private const int MaxLogs = 100;

        private static CommandExecutor executor = new CommandExecutor();

        [MenuItem("Tools/Unity Command Server")]
        public static void ShowWindow()
        {
            var window = GetWindow<UnityCommandServerWindow>("Command Server");
            window.minSize = new Vector2(400, 300);
        }

        private void OnEnable()
        {
            // Auto-start
            StartServer();
            Application.logMessageReceived += OnLogMessage;

            // Process queue via EditorApplication.update
            EditorApplication.update += ProcessCommandQueue;

            // Configure to run in background
            // This allows processing to continue even when Unity loses focus
            EditorApplication.wantsToQuit += OnWantsToQuit;
        }

        private bool OnWantsToQuit()
        {
            StopServer();
            return true;
        }

        private void OnDisable()
        {
            EditorApplication.update -= ProcessCommandQueue;
            Application.logMessageReceived -= OnLogMessage;
            StopServer();
        }

        /// <summary>
        /// Processes queued commands (runs on main thread)
        /// </summary>
        private void ProcessCommandQueue()
        {
            // Process one command per frame
            if (CommandBehavior.HasPendingCommands())
            {
                var pending = CommandBehavior.DequeueCommand();
                if (pending != null)
                {
                    ProcessCommand(pending);
                }
            }
        }

        /// <summary>
        /// Processes command and returns result
        /// </summary>
        private void ProcessCommand(PendingCommand pending)
        {
            JObject response = new JObject();
            response["timestamp"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            try
            {
                // Parse outer wrapper {"message": "..."}
                JObject wrapper = JObject.Parse(pending.RawMessage);
                string commandJson = wrapper["message"]?.Value<string>();

                if (string.IsNullOrEmpty(commandJson))
                {
                    response["success"] = false;
                    response["error"] = "Missing 'message' field in request";
                    pending.SendResponse(response.ToString(Newtonsoft.Json.Formatting.None));
                    return;
                }

                Debug.Log($"[CommandServer] Command JSON: {commandJson}");

                // Execute command
                var (success, resultJson) = executor.ExecuteCommand(commandJson);

                response["success"] = success;

                if (!string.IsNullOrEmpty(resultJson))
                {
                    // If resultJson is JSON format, parse and add
                    try
                    {
                        response["result"] = JToken.Parse(resultJson);
                    }
                    catch
                    {
                        // If not JSON, add as string
                        response["result"] = resultJson;
                    }
                }

                if (!success)
                {
                    response["error"] = "Command execution failed. Check Unity Console for details.";
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CommandServer] Error: {ex.Message}\n{ex.StackTrace}");
                response["success"] = false;
                response["error"] = ex.Message;
            }

            pending.SendResponse(response.ToString(Newtonsoft.Json.Formatting.None));
        }

        private void OnLogMessage(string logString, string stackTrace, LogType type)
        {
            if (logString.Contains("[CommandServer]"))
            {
                logs.Add($"[{DateTime.Now:HH:mm:ss}] {logString}");
                if (logs.Count > MaxLogs)
                {
                    logs.RemoveAt(0);
                }
                Repaint();
            }
        }

        private void StartServer()
        {
            if (isRunning) return;

            try
            {
                server = new WebSocketServer($"ws://127.0.0.1:{Port}");
                server.AddWebSocketService<CommandBehavior>("/");
                server.Start();
                isRunning = true;
                logs.Add($"[{DateTime.Now:HH:mm:ss}] Server started on ws://127.0.0.1:{Port}");
                Debug.Log($"[CommandServer] Server started on ws://127.0.0.1:{Port}");
            }
            catch (Exception e)
            {
                logs.Add($"[{DateTime.Now:HH:mm:ss}] ERROR: {e.Message}");
                Debug.LogError($"[CommandServer] Failed to start: {e.Message}");
            }
        }

        private void StopServer()
        {
            if (!isRunning) return;

            try
            {
                server?.Stop();
                server = null;
                isRunning = false;
                logs.Add($"[{DateTime.Now:HH:mm:ss}] Server stopped");
                Debug.Log("[CommandServer] Server stopped");
            }
            catch (Exception e)
            {
                Debug.LogError($"[CommandServer] Failed to stop: {e.Message}");
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);

            // Status display
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Status:", GUILayout.Width(50));

            var statusStyle = new GUIStyle(EditorStyles.boldLabel);
            statusStyle.normal.textColor = isRunning ? Color.green : Color.red;
            EditorGUILayout.LabelField(isRunning ? "Running" : "Stopped", statusStyle);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField($"Port: {Port}");
            EditorGUILayout.LabelField($"URL: ws://127.0.0.1:{Port}");

            EditorGUILayout.Space(10);

            // Log display
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Logs:", EditorStyles.boldLabel);
            if (GUILayout.Button("Copy All", GUILayout.Width(70)))
            {
                EditorGUIUtility.systemCopyBuffer = string.Join("\n", logs);
            }
            EditorGUILayout.EndHorizontal();

            logScrollPosition = EditorGUILayout.BeginScrollView(logScrollPosition,
                GUILayout.ExpandHeight(true));

            // 선택 가능한 텍스트로 표시
            foreach (var log in logs)
            {
                EditorGUILayout.SelectableLabel(log, EditorStyles.wordWrappedLabel,
                    GUILayout.Height(EditorGUIUtility.singleLineHeight * Mathf.Max(1, Mathf.CeilToInt(log.Length / 50f))));
            }

            EditorGUILayout.EndScrollView();

            // Clear button
            if (GUILayout.Button("Clear Logs"))
            {
                logs.Clear();
            }
        }
    }
}
