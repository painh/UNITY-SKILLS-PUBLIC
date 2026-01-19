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
        public const string CurrentVersion = "0.0.19";
        private const string GitHubOwner = "painh";
        private const string GitHubRepo = "UNITY-SKILLS-PUBLIC";
        private const string GitHubApiUrl = "https://api.github.com/repos/{0}/{1}/releases/latest";
        private const string PrefKeyLastCheckTime = "CommandServer_LastVersionCheck";
        private const string PrefKeySkipVersion = "CommandServer_SkipVersion";

        public static string LatestVersion { get; private set; } = null;
        public static string LatestReleaseUrl { get; private set; } = null;
        public static string LatestZipUrl { get; private set; } = null;
        public static bool IsChecking { get; private set; } = false;
        public static string LastCheckResult { get; private set; } = null;
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
                Debug.Log("[CommandServer] Already checking, skipping...");
                callback?.Invoke(false, "Already checking for updates");
                return;
            }

            IsChecking = true;
            checkCallback = callback;
            checkStartTime = EditorApplication.timeSinceStartup;

            string url = string.Format(GitHubApiUrl, GitHubOwner, GitHubRepo);
            Debug.Log($"[CommandServer] Checking for updates... URL: {url}");

            currentRequest = UnityWebRequest.Get(url);
            currentRequest.SetRequestHeader("User-Agent", "Unity-Claude-Agent");
            currentRequest.timeout = 10; // 10 second timeout

            Debug.Log("[CommandServer] Sending web request...");
            var operation = currentRequest.SendWebRequest();
            operation.completed += (op) => {
                Debug.Log("[CommandServer] Web request completed callback fired");
                OnCheckComplete(silent);
            };

            // Register timeout check
            EditorApplication.update += CheckTimeout;
        }

        private static double checkStartTime;
        private const double TimeoutSeconds = 15.0;

        private static void CheckTimeout()
        {
            if (!IsChecking)
            {
                EditorApplication.update -= CheckTimeout;
                return;
            }

            if (EditorApplication.timeSinceStartup - checkStartTime > TimeoutSeconds)
            {
                EditorApplication.update -= CheckTimeout;
                IsChecking = false;
                currentRequest?.Abort();
                currentRequest?.Dispose();
                currentRequest = null;

                LastCheckResult = "Timeout";
                if (CommandExecutor.EnableConsoleLogging) Debug.LogWarning("[CommandServer] Update check timed out");
                checkCallback?.Invoke(false, "Update check timed out");
                checkCallback = null;
            }
        }

        private static void OnCheckComplete(bool silent)
        {
            EditorApplication.update -= CheckTimeout;
            IsChecking = false;

            Debug.Log($"[CommandServer] OnCheckComplete called. Request null? {currentRequest == null}");

            try
            {
                if (currentRequest == null)
                {
                    Debug.LogError("[CommandServer] currentRequest is null!");
                    checkCallback?.Invoke(false, "Request was null");
                    return;
                }

                Debug.Log($"[CommandServer] Response code: {currentRequest.responseCode}, Result: {currentRequest.result}");

                // Handle 404 (no releases yet)
                if (currentRequest.responseCode == 404)
                {
                    Debug.Log("[CommandServer] No releases yet (404)");
                    LatestVersion = null;
                    LastCheckResult = "No releases yet";
                    checkCallback?.Invoke(true, LastCheckResult);
                    return;
                }

                if (currentRequest.result != UnityWebRequest.Result.Success)
                {
                    string error = $"Failed to check updates: {currentRequest.error}";
                    Debug.LogWarning($"[CommandServer] {error}");
                    checkCallback?.Invoke(false, error);
                    return;
                }

                string json = currentRequest.downloadHandler.text;
                Debug.Log($"[CommandServer] Response length: {json?.Length ?? 0}");
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

                Debug.Log($"[CommandServer] Latest version: {LatestVersion}, Current: {CurrentVersion}, HasUpdate: {HasUpdate}");

                if (HasUpdate)
                {
                    LastCheckResult = $"v{LatestVersion} available";
                    Debug.Log($"[CommandServer] Update available: {LastCheckResult}");
                    checkCallback?.Invoke(true, LastCheckResult);
                }
                else
                {
                    LastCheckResult = "Latest";
                    Debug.Log($"[CommandServer] Already on latest version");
                    checkCallback?.Invoke(true, LastCheckResult);
                }
            }
            catch (Exception e)
            {
                string error = $"Error parsing update info: {e.Message}";
                Debug.LogError($"[CommandServer] {error}\n{e.StackTrace}");
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
                    if (CommandExecutor.EnableConsoleLogging) Debug.Log($"[CommandServer] Skipping version {LatestVersion}");
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

            // Initialize debug file logging settings
            CommandExecutor.InitializeFileLogging();

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
                    if (CommandExecutor.EnableConsoleLogging) Debug.LogError($"[CommandServer] Port {port} is already in use");
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
                if (CommandExecutor.EnableConsoleLogging) Debug.Log($"[CommandServer] Port file saved: {PortFilePath}");
            }
            catch (Exception e)
            {
                if (CommandExecutor.EnableConsoleLogging) Debug.LogWarning($"[CommandServer] Failed to save port file: {e.Message}");
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
                    if (CommandExecutor.EnableConsoleLogging) Debug.Log($"[CommandServer] Port file deleted: {PortFilePath}");
                }
            }
            catch (Exception e)
            {
                if (CommandExecutor.EnableConsoleLogging) Debug.LogWarning($"[CommandServer] Failed to delete port file: {e.Message}");
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
                        if (CommandExecutor.EnableConsoleLogging) Debug.LogWarning($"[CommandServer] Custom port {customPort} unavailable, finding alternative...");
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
                    if (CommandExecutor.EnableConsoleLogging) Debug.LogError($"[CommandServer] No available port found (tried {BasePort}-{BasePort + MaxPortAttempts - 1})");
                    return;
                }

                server = new WebSocketServer($"ws://127.0.0.1:{port}");
                server.AddWebSocketService<CommandBehavior>("/");
                server.Start();

                currentPort = port;
                isRunning = true;

                // Save port to file for external tools
                SavePortFile(port);

                if (CommandExecutor.EnableConsoleLogging) Debug.Log($"[CommandServer] Server started on ws://127.0.0.1:{port}");
            }
            catch (Exception e)
            {
                if (CommandExecutor.EnableConsoleLogging) Debug.LogError($"[CommandServer] Failed to start: {e.Message}");
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

                if (CommandExecutor.EnableConsoleLogging) Debug.Log("[CommandServer] Server stopped");
            }
            catch (Exception e)
            {
                if (CommandExecutor.EnableConsoleLogging) Debug.LogError($"[CommandServer] Failed to stop: {e.Message}");
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
            if (CommandExecutor.EnableConsoleLogging) Debug.Log($"[CommandServer] Received: {rawMessage}");

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
                                if (CommandExecutor.EnableConsoleLogging) Debug.Log($"[CommandServer] Response sent: {responseJson}");
                            }
                            else
                            {
                                if (CommandExecutor.EnableConsoleLogging) Debug.LogWarning("[CommandServer] Cannot send response: connection not open");
                            }
                        }
                        catch (Exception ex)
                        {
                            if (CommandExecutor.EnableConsoleLogging) Debug.LogError($"[CommandServer] Error sending response: {ex.Message}");
                        }
                    }
                });
            }

            if (CommandExecutor.EnableConsoleLogging) Debug.Log($"[CommandServer] Command queued. Queue size: {commandQueue.Count}");
        }

        protected override void OnOpen()
        {
            if (CommandExecutor.EnableConsoleLogging) Debug.Log($"[CommandServer] Client connected: {Context.UserEndPoint}");
        }

        protected override void OnClose(CloseEventArgs e)
        {
            if (CommandExecutor.EnableConsoleLogging) Debug.Log($"[CommandServer] Client disconnected: {e.Reason}");
        }

        protected override void OnError(ErrorEventArgs e)
        {
            if (CommandExecutor.EnableConsoleLogging) Debug.LogError($"[CommandServer] WebSocket error: {e.Message}");
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
        private bool showFeaturePermissions = false;
        private bool showPropertyBlacklist = false;
        private string newBlacklistEntry = "";

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
            // Initialize file logging setting
            CommandExecutor.InitializeFileLogging();
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

                if (CommandExecutor.EnableConsoleLogging) Debug.Log($"[CommandServer] Command JSON: {commandJson}");

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
                if (CommandExecutor.EnableConsoleLogging) Debug.LogError($"[CommandServer] Error: {ex.Message}\n{ex.StackTrace}");
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
            EditorGUILayout.LabelField($"Version: {VersionChecker.CurrentVersion}", GUILayout.Width(100));

            if (!string.IsNullOrEmpty(VersionChecker.LastCheckResult))
            {
                var versionStyle = new GUIStyle(EditorStyles.miniLabel);
                if (VersionChecker.HasUpdate)
                    versionStyle.normal.textColor = new Color(1f, 0.5f, 0f); // Orange
                else
                    versionStyle.normal.textColor = new Color(0.4f, 0.7f, 0.4f); // Green
                EditorGUILayout.LabelField($"({VersionChecker.LastCheckResult})", versionStyle);
            }

            GUILayout.FlexibleSpace();

            GUI.enabled = !VersionChecker.IsChecking;
            if (GUILayout.Button(VersionChecker.IsChecking ? "Checking..." : "Check Updates", GUILayout.Width(100)))
            {
                VersionChecker.CheckForUpdates((success, message) =>
                {
                    if (success && VersionChecker.HasUpdate)
                    {
                        VersionChecker.ShowUpdateDialogIfAvailable();
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

            EditorGUILayout.Space(5);

            // Console logging toggle
            EditorGUILayout.BeginHorizontal();
            bool newEnableConsoleLogging = EditorGUILayout.ToggleLeft("Enable Console Logging", CommandExecutor.EnableConsoleLogging, GUILayout.Width(160));
            if (newEnableConsoleLogging != CommandExecutor.EnableConsoleLogging)
            {
                CommandExecutor.EnableConsoleLogging = newEnableConsoleLogging;
            }
            EditorGUILayout.LabelField("(Show command logs in Unity Console)", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // Feature permissions section
            showFeaturePermissions = EditorGUILayout.Foldout(showFeaturePermissions, "Feature Permissions", true);
            if (showFeaturePermissions)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("Enable/disable command categories:", EditorStyles.miniLabel);
                EditorGUILayout.Space(2);

                foreach (var category in CommandExecutor.GetAllCategories())
                {
                    bool currentEnabled = CommandExecutor.IsFeatureEnabled(category);
                    bool newEnabled = EditorGUILayout.ToggleLeft(
                        CommandExecutor.GetCategoryDisplayName(category),
                        currentEnabled
                    );
                    if (newEnabled != currentEnabled)
                    {
                        CommandExecutor.SetFeatureEnabled(category, newEnabled);
                    }
                }

                EditorGUILayout.Space(2);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Enable All", GUILayout.Width(80)))
                {
                    foreach (var category in CommandExecutor.GetAllCategories())
                        CommandExecutor.SetFeatureEnabled(category, true);
                }
                if (GUILayout.Button("Disable All", GUILayout.Width(80)))
                {
                    foreach (var category in CommandExecutor.GetAllCategories())
                        CommandExecutor.SetFeatureEnabled(category, false);
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.Space(5);

            // Property blacklist section
            showPropertyBlacklist = EditorGUILayout.Foldout(showPropertyBlacklist, "Property Blacklist", true);
            if (showPropertyBlacklist)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("Properties to skip when reading components:", EditorStyles.miniLabel);
                EditorGUILayout.Space(2);

                // Default entries
                EditorGUILayout.LabelField("Default Properties:", EditorStyles.boldLabel);
                foreach (var entry in CommandExecutor.GetDefaultPropertyBlacklist())
                {
                    EditorGUILayout.BeginHorizontal();
                    bool isEnabled = CommandExecutor.IsDefaultBlacklistEnabled(entry);
                    bool newEnabled = EditorGUILayout.ToggleLeft(entry, isEnabled);
                    if (newEnabled != isEnabled)
                    {
                        CommandExecutor.SetDefaultBlacklistEnabled(entry, newEnabled);
                    }
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.Space(5);

                // Custom entries
                var customEntries = CommandExecutor.GetCustomBlacklist();
                if (customEntries.Count > 0)
                {
                    EditorGUILayout.LabelField("Custom Properties:", EditorStyles.boldLabel);
                    string entryToRemove = null;
                    foreach (var entry in customEntries)
                    {
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField(entry);
                        if (GUILayout.Button("X", GUILayout.Width(20)))
                        {
                            entryToRemove = entry;
                        }
                        EditorGUILayout.EndHorizontal();
                    }
                    if (entryToRemove != null)
                    {
                        CommandExecutor.RemoveCustomBlacklistEntry(entryToRemove);
                    }
                    EditorGUILayout.Space(5);
                }

                // Add new entry
                EditorGUILayout.LabelField("Add Custom Property:", EditorStyles.boldLabel);
                EditorGUILayout.BeginHorizontal();
                newBlacklistEntry = EditorGUILayout.TextField(newBlacklistEntry);
                GUI.enabled = !string.IsNullOrWhiteSpace(newBlacklistEntry);
                if (GUILayout.Button("Add", GUILayout.Width(50)))
                {
                    CommandExecutor.AddCustomBlacklistEntry(newBlacklistEntry);
                    newBlacklistEntry = "";
                    GUI.FocusControl(null);
                }
                GUI.enabled = true;
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(2);

                // Reset button
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Reset to Defaults", GUILayout.Width(120)))
                {
                    CommandExecutor.ResetBlacklistToDefaults();
                    newBlacklistEntry = "";
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.Space(5);

            // Debug file logging section
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();

            bool newEnableLogging = EditorGUILayout.ToggleLeft("Enable Debug File Logging", CommandExecutor.EnableFileLogging, GUILayout.Width(180));
            if (newEnableLogging != CommandExecutor.EnableFileLogging)
            {
                CommandExecutor.EnableFileLogging = newEnableLogging;
            }

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Clear", GUILayout.Width(50)))
            {
                CommandExecutor.ClearDebugLogFile();
            }

            EditorGUILayout.EndHorizontal();

            // Log file path with edit options
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Log File:", GUILayout.Width(55));

            string logPath = CommandExecutor.GetDebugLogPath();

            // Clickable path label
            var linkStyle = new GUIStyle(EditorStyles.miniLabel);
            linkStyle.normal.textColor = new Color(0.3f, 0.5f, 1f);
            linkStyle.hover.textColor = new Color(0.5f, 0.7f, 1f);
            linkStyle.stretchWidth = true;

            if (GUILayout.Button(logPath, linkStyle, GUILayout.ExpandWidth(true)))
            {
                CommandExecutor.OpenDebugLogFile();
            }
            EditorGUIUtility.AddCursorRect(GUILayoutUtility.GetLastRect(), MouseCursor.Link);

            // Browse button
            if (GUILayout.Button("...", GUILayout.Width(25)))
            {
                string directory = System.IO.Path.GetDirectoryName(logPath);
                string filename = System.IO.Path.GetFileName(logPath);

                string newPath = EditorUtility.SaveFilePanel(
                    "Select Debug Log File Location",
                    directory,
                    filename,
                    "log"
                );

                if (!string.IsNullOrEmpty(newPath))
                {
                    CommandExecutor.DebugLogPath = newPath;
                }
            }

            // Reset to default button
            if (GUILayout.Button("Reset", GUILayout.Width(45)))
            {
                CommandExecutor.DebugLogPath = CommandExecutor.GetDefaultDebugLogPath();
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();

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

            // 전체 선택 가능한 TextArea로 표시
            string allLogs = string.Join("\n", logs);
            var textAreaStyle = new GUIStyle(EditorStyles.textArea)
            {
                wordWrap = true,
                richText = false
            };

            EditorGUILayout.TextArea(allLogs, textAreaStyle, GUILayout.ExpandHeight(true), GUILayout.MinHeight(200));

            EditorGUILayout.EndScrollView();

            // Clear button
            if (GUILayout.Button("Clear Logs"))
            {
                logs.Clear();
            }
        }
    }
}
