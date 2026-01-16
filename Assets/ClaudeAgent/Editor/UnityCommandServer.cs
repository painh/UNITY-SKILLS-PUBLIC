using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using WebSocketSharp;
using WebSocketSharp.Server;
using Newtonsoft.Json.Linq;
using UnityEngine.Networking;
using System.Text.RegularExpressions;

namespace ClaudeAgent
{
    /// <summary>
    /// Version information and GitHub update checker
    /// </summary>
    public static class VersionChecker
    {
        public const string CurrentVersion = "0.0.1";
        private const string GitHubOwner = "painh";
        private const string GitHubRepo = "UNITY-SKILLS-PUBLIC";
        private const string GitHubApiUrl = "https://api.github.com/repos/{0}/{1}/releases/latest";
        private const string PrefKeyLastCheckTime = "CommandServer_LastVersionCheck";
        private const string PrefKeySkipVersion = "CommandServer_SkipVersion";

        public static string LatestVersion { get; private set; } = null;
        public static string LatestReleaseUrl { get; private set; } = null;
        public static string LatestZipUrl { get; private set; } = null;
        public static bool IsChecking { get; private set; } = false;
        public static bool HasUpdate => !string.IsNullOrEmpty(LatestVersion) && CompareVersions(LatestVersion, CurrentVersion) > 0;

        private static UnityWebRequest currentRequest;
        private static Action<bool, string> checkCallback;

        /// <summary>
        /// Compare two version strings (e.g., "1.2.3" vs "1.2.4")
        /// Returns: positive if v1 > v2, negative if v1 < v2, 0 if equal
        /// </summary>
        public static int CompareVersions(string v1, string v2)
        {
            var v1Parts = v1.TrimStart('v', 'V').Split('.');
            var v2Parts = v2.TrimStart('v', 'V').Split('.');

            int maxLength = Math.Max(v1Parts.Length, v2Parts.Length);
            for (int i = 0; i < maxLength; i++)
            {
                int p1 = i < v1Parts.Length && int.TryParse(v1Parts[i], out int n1) ? n1 : 0;
                int p2 = i < v2Parts.Length && int.TryParse(v2Parts[i], out int n2) ? n2 : 0;

                if (p1 != p2) return p1 - p2;
            }
            return 0;
        }

        /// <summary>
        /// Check for updates from GitHub (async)
        /// </summary>
        public static void CheckForUpdates(Action<bool, string> callback = null, bool silent = false)
        {
            if (IsChecking)
            {
                callback?.Invoke(false, "Already checking for updates");
                return;
            }

            IsChecking = true;
            checkCallback = callback;

            string url = string.Format(GitHubApiUrl, GitHubOwner, GitHubRepo);
            currentRequest = UnityWebRequest.Get(url);
            currentRequest.SetRequestHeader("User-Agent", "Unity-Claude-Agent");

            var operation = currentRequest.SendWebRequest();
            operation.completed += (op) => OnCheckComplete(silent);

            if (!silent)
                Debug.Log("[CommandServer] Checking for updates...");
        }

        private static void OnCheckComplete(bool silent)
        {
            IsChecking = false;

            try
            {
                if (currentRequest.result != UnityWebRequest.Result.Success)
                {
                    string error = $"Failed to check updates: {currentRequest.error}";
                    if (!silent) Debug.LogWarning($"[CommandServer] {error}");
                    checkCallback?.Invoke(false, error);
                    return;
                }

                string json = currentRequest.downloadHandler.text;
                var release = JObject.Parse(json);

                LatestVersion = release["tag_name"]?.ToString();
                LatestReleaseUrl = release["html_url"]?.ToString();

                // Find zip asset
                var assets = release["assets"] as JArray;
                if (assets != null)
                {
                    foreach (var asset in assets)
                    {
                        string name = asset["name"]?.ToString() ?? "";
                        if (name.EndsWith(".zip") || name.EndsWith(".unitypackage"))
                        {
                            LatestZipUrl = asset["browser_download_url"]?.ToString();
                            break;
                        }
                    }
                }

                // Fallback to zipball
                if (string.IsNullOrEmpty(LatestZipUrl))
                {
                    LatestZipUrl = release["zipball_url"]?.ToString();
                }

                // Save check time
                EditorPrefs.SetString(PrefKeyLastCheckTime, DateTime.Now.ToString("o"));

                if (HasUpdate)
                {
                    string msg = $"New version available: {LatestVersion} (current: {CurrentVersion})";
                    if (!silent) Debug.Log($"[CommandServer] {msg}");
                    checkCallback?.Invoke(true, msg);
                }
                else
                {
                    string msg = $"You have the latest version ({CurrentVersion})";
                    if (!silent) Debug.Log($"[CommandServer] {msg}");
                    checkCallback?.Invoke(true, msg);
                }
            }
            catch (Exception e)
            {
                string error = $"Error parsing update info: {e.Message}";
                if (!silent) Debug.LogError($"[CommandServer] {error}");
                checkCallback?.Invoke(false, error);
            }
            finally
            {
                currentRequest?.Dispose();
                currentRequest = null;
                checkCallback = null;
            }
        }

        /// <summary>
        /// Show update dialog if new version available
        /// </summary>
        public static void ShowUpdateDialogIfAvailable()
        {
            if (!HasUpdate) return;

            string skipVersion = EditorPrefs.GetString(PrefKeySkipVersion, "");
            if (skipVersion == LatestVersion) return;

            int choice = EditorUtility.DisplayDialogComplex(
                "새 버전이 있습니다",
                $"현재 버전: {CurrentVersion}\n새 버전: {LatestVersion}\n\n새 버전으로 업데이트 할까요?",
                "업데이트",
                "이 버전 건너뛰기",
                "나중에"
            );

            switch (choice)
            {
                case 0: // Update
                    StartUpdate();
                    break;
                case 1: // Skip this version
                    EditorPrefs.SetString(PrefKeySkipVersion, LatestVersion);
                    Debug.Log($"[CommandServer] Skipping version {LatestVersion}");
                    break;
                case 2: // Later
                    break;
            }
        }

        /// <summary>
        /// Start the update process
        /// </summary>
        public static void StartUpdate()
        {
            if (string.IsNullOrEmpty(LatestReleaseUrl))
            {
                EditorUtility.DisplayDialog("업데이트 오류", "업데이트 URL을 찾을 수 없습니다.", "확인");
                return;
            }

            // Open GitHub releases page
            Application.OpenURL(LatestReleaseUrl);

            EditorUtility.DisplayDialog(
                "업데이트 안내",
                "GitHub 릴리즈 페이지가 열렸습니다.\n\n" +
                "1. 최신 버전을 다운로드하세요\n" +
                "2. Unity를 종료하세요\n" +
                "3. Assets/ClaudeAgent 폴더를 삭제하세요\n" +
                "4. 새 버전을 임포트하세요",
                "확인"
            );
        }
    }

    /// <summary>
    /// Auto-start Command Server when Unity Editor loads (independent of window)
    /// </summary>
    [InitializeOnLoad]
    public static class CommandServerAutoStart
    {
        private static WebSocketServer server;
        private static bool isRunning = false;
        private static int currentPort = 0;

        private const int BasePort = 8766;
        private const int MaxPortAttempts = 100;
        private const string PortFilePath = "Library/ClaudeAgent/port.txt";
        private const string PrefKeyCustomPort = "CommandServer_CustomPort";

        static CommandServerAutoStart()
        {
            // Start server immediately (independent of window)
            StartServer();

            // Register for editor quit to stop server
            EditorApplication.quitting += StopServer;

            // Process command queue
            EditorApplication.update += ProcessCommandQueue;

            // Check for updates on startup (silent, only show dialog if update available)
            EditorApplication.delayCall += () =>
            {
                VersionChecker.CheckForUpdates((success, message) =>
                {
                    if (success && VersionChecker.HasUpdate)
                    {
                        EditorApplication.delayCall += VersionChecker.ShowUpdateDialogIfAvailable;
                    }
                }, silent: true);
            };
        }

        internal static bool IsRunning => isRunning;
        internal static int Port => currentPort;

        /// <summary>
        /// Get/Set custom port (0 = auto)
        /// </summary>
        internal static int CustomPort
        {
            get => EditorPrefs.GetInt(PrefKeyCustomPort, 0);
            set => EditorPrefs.SetInt(PrefKeyCustomPort, value);
        }

        /// <summary>
        /// Restart server with new port
        /// </summary>
        internal static bool RestartWithPort(int port)
        {
            StopServer();

            if (port > 0)
            {
                // Try specific port
                if (!IsPortAvailable(port))
                {
                    Debug.LogError($"[CommandServer] Port {port} is already in use");
                    // Fallback to auto
                    CustomPort = 0;
                    StartServer();
                    return false;
                }
                CustomPort = port;
            }
            else
            {
                CustomPort = 0;
            }

            StartServer();
            return true;
        }

        /// <summary>
        /// Find an available port starting from BasePort
        /// </summary>
        private static int FindAvailablePort()
        {
            for (int i = 0; i < MaxPortAttempts; i++)
            {
                int port = BasePort + i;
                if (IsPortAvailable(port))
                {
                    return port;
                }
            }
            return -1; // No available port found
        }

        /// <summary>
        /// Check if a port is available
        /// </summary>
        private static bool IsPortAvailable(int port)
        {
            try
            {
                using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
                {
                    socket.Bind(new IPEndPoint(IPAddress.Loopback, port));
                    return true;
                }
            }
            catch (SocketException)
            {
                return false;
            }
        }

        /// <summary>
        /// Save port number to file for external tools to read
        /// </summary>
        private static void SavePortFile(int port)
        {
            try
            {
                var dir = System.IO.Path.GetDirectoryName(PortFilePath);
                if (!System.IO.Directory.Exists(dir))
                    System.IO.Directory.CreateDirectory(dir);

                System.IO.File.WriteAllText(PortFilePath, port.ToString());
                Debug.Log($"[CommandServer] Port file saved: {PortFilePath}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CommandServer] Failed to save port file: {e.Message}");
            }
        }

        /// <summary>
        /// Delete port file on shutdown
        /// </summary>
        private static void DeletePortFile()
        {
            try
            {
                if (System.IO.File.Exists(PortFilePath))
                {
                    System.IO.File.Delete(PortFilePath);
                    Debug.Log($"[CommandServer] Port file deleted: {PortFilePath}");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CommandServer] Failed to delete port file: {e.Message}");
            }
        }

        internal static void StartServer()
        {
            if (isRunning) return;

            try
            {
                int port;
                int customPort = CustomPort;

                if (customPort > 0)
                {
                    // Use custom port if set
                    if (IsPortAvailable(customPort))
                    {
                        port = customPort;
                    }
                    else
                    {
                        Debug.LogWarning($"[CommandServer] Custom port {customPort} unavailable, finding alternative...");
                        port = FindAvailablePort();
                    }
                }
                else
                {
                    // Find available port automatically
                    port = FindAvailablePort();
                }

                if (port < 0)
                {
                    Debug.LogError($"[CommandServer] No available port found (tried {BasePort}-{BasePort + MaxPortAttempts - 1})");
                    return;
                }

                server = new WebSocketServer($"ws://127.0.0.1:{port}");
                server.AddWebSocketService<CommandBehavior>("/");
                server.Start();

                currentPort = port;
                isRunning = true;

                // Save port to file for external tools
                SavePortFile(port);

                Debug.Log($"[CommandServer] Server started on ws://127.0.0.1:{port}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[CommandServer] Failed to start: {e.Message}");
            }
        }

        internal static void StopServer()
        {
            if (!isRunning) return;

            try
            {
                server?.Stop();
                server = null;
                isRunning = false;

                // Delete port file
                DeletePortFile();

                Debug.Log("[CommandServer] Server stopped");
            }
            catch (Exception e)
            {
                Debug.LogError($"[CommandServer] Failed to stop: {e.Message}");
            }
        }

        private static void ProcessCommandQueue()
        {
            if (CommandBehavior.HasPendingCommands())
            {
                var pending = CommandBehavior.DequeueCommand();
                if (pending != null)
                {
                    UnityCommandServerWindow.ProcessCommandStatic(pending);
                }
            }
        }
    }

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
    /// Command Server status window (server runs independently)
    /// </summary>
    public class UnityCommandServerWindow : EditorWindow
    {
        private Vector2 logScrollPosition;
        private static List<string> logs = new List<string>();
        private const int MaxLogs = 100;

        private static CommandExecutor executor = new CommandExecutor();

        // Port settings UI
        private string portInputText = "";
        private bool showPortSettings = false;

        [MenuItem("Tools/Unity Command Server")]
        public static void ShowWindow()
        {
            GetWindow<UnityCommandServerWindow>("Command Server");
        }

        private void OnEnable()
        {
            Application.logMessageReceived += OnLogMessage;
            // Initialize port input with current custom port
            int customPort = CommandServerAutoStart.CustomPort;
            portInputText = customPort > 0 ? customPort.ToString() : "";
        }

        private void OnDisable()
        {
            Application.logMessageReceived -= OnLogMessage;
        }

        /// <summary>
        /// Processes command (called from CommandServerAutoStart)
        /// </summary>
        internal static void ProcessCommandStatic(PendingCommand pending)
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

        private void ApplyPortSettings()
        {
            if (string.IsNullOrEmpty(portInputText))
            {
                // Auto mode
                CommandServerAutoStart.RestartWithPort(0);
            }
            else if (int.TryParse(portInputText, out int port))
            {
                if (port < 1024 || port > 65535)
                {
                    EditorUtility.DisplayDialog("Invalid Port",
                        "Port must be between 1024 and 65535.",
                        "OK");
                    return;
                }
                CommandServerAutoStart.RestartWithPort(port);
            }
            else
            {
                EditorUtility.DisplayDialog("Invalid Port",
                    "Please enter a valid port number.",
                    "OK");
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);

            // Status display
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Status:", GUILayout.Width(50));

            var statusStyle = new GUIStyle(EditorStyles.boldLabel);
            bool isRunning = CommandServerAutoStart.IsRunning;
            statusStyle.normal.textColor = isRunning ? Color.green : Color.red;
            EditorGUILayout.LabelField(isRunning ? "Running" : "Stopped", statusStyle);
            EditorGUILayout.EndHorizontal();

            // Port display with settings toggle
            EditorGUILayout.BeginHorizontal();
            int customPort = CommandServerAutoStart.CustomPort;
            string portMode = customPort > 0 ? "Custom" : "Auto";
            EditorGUILayout.LabelField($"Port: {CommandServerAutoStart.Port} ({portMode})");
            if (GUILayout.Button(showPortSettings ? "▲" : "▼", GUILayout.Width(25)))
            {
                showPortSettings = !showPortSettings;
            }
            EditorGUILayout.EndHorizontal();

            // Port settings panel
            if (showPortSettings)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Custom Port:", GUILayout.Width(80));
                portInputText = EditorGUILayout.TextField(portInputText, GUILayout.Width(60));
                EditorGUILayout.LabelField("(empty = auto)", EditorStyles.miniLabel);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Apply", GUILayout.Width(60)))
                {
                    ApplyPortSettings();
                }
                if (GUILayout.Button("Reset to Auto", GUILayout.Width(100)))
                {
                    portInputText = "";
                    CommandServerAutoStart.RestartWithPort(0);
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.LabelField($"URL: ws://127.0.0.1:{CommandServerAutoStart.Port}");

            EditorGUILayout.Space(5);

            // Version display
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Version: {VersionChecker.CurrentVersion}", GUILayout.Width(120));

            if (VersionChecker.HasUpdate)
            {
                var updateStyle = new GUIStyle(EditorStyles.boldLabel);
                updateStyle.normal.textColor = new Color(1f, 0.5f, 0f); // Orange
                EditorGUILayout.LabelField($"→ {VersionChecker.LatestVersion} available!", updateStyle);
            }

            GUILayout.FlexibleSpace();

            GUI.enabled = !VersionChecker.IsChecking;
            if (GUILayout.Button(VersionChecker.IsChecking ? "Checking..." : "Check Updates", GUILayout.Width(100)))
            {
                VersionChecker.CheckForUpdates((success, message) =>
                {
                    if (success)
                    {
                        if (VersionChecker.HasUpdate)
                        {
                            VersionChecker.ShowUpdateDialogIfAvailable();
                        }
                        else
                        {
                            EditorUtility.DisplayDialog("버전 확인", "최신 버전입니다.", "확인");
                        }
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("버전 확인 실패", message, "확인");
                    }
                    Repaint();
                });
            }
            GUI.enabled = true;

            if (VersionChecker.HasUpdate)
            {
                if (GUILayout.Button("Update", GUILayout.Width(60)))
                {
                    VersionChecker.StartUpdate();
                }
            }

            EditorGUILayout.EndHorizontal();

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
