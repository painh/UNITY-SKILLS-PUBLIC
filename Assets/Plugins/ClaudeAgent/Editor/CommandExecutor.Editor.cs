using UnityEngine;
using UnityEditor;
using System;
using System.Text;

namespace ClaudeAgent
{
    public partial class CommandExecutor
    {
        /// <summary>
        /// Registers editor commands
        /// </summary>
        private void RegisterEditorCommands()
        {
            RegisterCommand("execute_menu_item", ExecuteMenuItem);
            RegisterCommand("get_editor_state", GetEditorState);
            RegisterCommand("get_selection", GetSelection);
            RegisterCommand("set_selection", SetSelection);
            RegisterCommand("get_window_title", GetWindowTitle);
            RegisterCommand("playmode", PlayMode);
        }

        // ====== Editor Operations ======

        /// <summary>
        /// Executes a menu item
        /// </summary>
        private (bool, string) ExecuteMenuItem(CommandParams p)
        {
            try
            {
                if (p == null || string.IsNullOrEmpty(p.menu_path))
                {
                    string error = "Missing required parameter: menu_path";
                    Debug.LogError($"[CommandExecutor] {error}");
                    return (false, error);
                }

                // Execute menu item
                bool executed = EditorApplication.ExecuteMenuItem(p.menu_path);

                if (!executed)
                {
                    string error = $"Menu item not found: {p.menu_path}";
                    Debug.LogError($"[CommandExecutor] {error}");
                    return (false, error);
                }

                string result = $"Executed menu item: {p.menu_path}";
                Debug.Log($"[CommandExecutor] {result}");
                return (true, result);
            }
            catch (Exception e)
            {
                string error = $"Error executing menu item: {e.Message}";
                Debug.LogError($"[CommandExecutor] {error}");
                return (false, error);
            }
        }

        /// <summary>
        /// Gets editor state
        /// </summary>
        private (bool, string) GetEditorState(CommandParams p)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("Unity Editor State:");
                sb.AppendLine();
                sb.AppendLine($"  Is Playing: {EditorApplication.isPlaying}");
                sb.AppendLine($"  Is Paused: {EditorApplication.isPaused}");
                sb.AppendLine($"  Is Compiling: {EditorApplication.isCompiling}");
                sb.AppendLine($"  Is Updating: {EditorApplication.isUpdating}");
                sb.AppendLine($"  Application Path: {EditorApplication.applicationPath}");
                sb.AppendLine($"  Unity Version: {Application.unityVersion}");
                sb.AppendLine($"  Platform: {Application.platform}");

                // Active scene
                var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
                sb.AppendLine($"  Active Scene: {scene.name}");
                sb.AppendLine($"  Scene Path: {scene.path}");
                sb.AppendLine($"  Is Scene Dirty: {scene.isDirty}");

                string result = sb.ToString();
                Debug.Log("[CommandExecutor] Retrieved editor state");
                return (true, result);
            }
            catch (Exception e)
            {
                string error = $"Error getting editor state: {e.Message}";
                Debug.LogError($"[CommandExecutor] {error}");
                return (false, error);
            }
        }

        /// <summary>
        /// Gets selected objects
        /// </summary>
        private (bool, string) GetSelection(CommandParams p)
        {
            try
            {
                var selectedObjects = Selection.objects;
                var sb = new StringBuilder();
                sb.AppendLine($"Selected Objects ({selectedObjects.Length}):");
                sb.AppendLine();

                if (selectedObjects.Length == 0)
                {
                    sb.AppendLine("  No objects selected");
                }
                else
                {
                    foreach (var obj in selectedObjects)
                    {
                        sb.AppendLine($"  - {obj.name}");
                        sb.AppendLine($"    Type: {obj.GetType().Name}");

                        if (obj is GameObject go)
                        {
                            sb.AppendLine($"    Path: {GetGameObjectPath(go)}");
                            sb.AppendLine($"    Active: {go.activeSelf}");
                        }
                        else
                        {
                            // Asset case
                            string assetPath = AssetDatabase.GetAssetPath(obj);
                            if (!string.IsNullOrEmpty(assetPath))
                            {
                                sb.AppendLine($"    Asset Path: {assetPath}");
                            }
                        }
                        sb.AppendLine();
                    }
                }

                // Active object
                if (Selection.activeGameObject != null)
                {
                    sb.AppendLine($"Active GameObject: {Selection.activeGameObject.name}");
                }

                string result = sb.ToString();
                Debug.Log("[CommandExecutor] Retrieved selection");
                return (true, result);
            }
            catch (Exception e)
            {
                string error = $"Error getting selection: {e.Message}";
                Debug.LogError($"[CommandExecutor] {error}");
                return (false, error);
            }
        }

        /// <summary>
        /// Gets Unity window title
        /// </summary>
        private (bool, string) GetWindowTitle(CommandParams p)
        {
            try
            {
                // Build Unity window title
                // Actual Unity window title format: "ProductName - SceneName - Unity VERSION"
                var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
                string sceneName = string.IsNullOrEmpty(scene.name) ? "Untitled" : scene.name;

                // Match Unity editor window title format
                string title = $"{Application.productName} - {sceneName} - Unity {Application.unityVersion}";

                Debug.Log($"[CommandExecutor] Window title: {title}");
                return (true, title);
            }
            catch (Exception e)
            {
                string error = $"Error getting window title: {e.Message}";
                Debug.LogError($"[CommandExecutor] {error}");
                return (false, error);
            }
        }

        /// <summary>
        /// Selects an object
        /// </summary>
        private (bool, string) SetSelection(CommandParams p)
        {
            try
            {
                if (p == null || string.IsNullOrEmpty(p.path))
                {
                    string error = "Missing required parameter: path";
                    Debug.LogError($"[CommandExecutor] {error}");
                    return (false, error);
                }

                var (obj, findError) = FindGameObjectByPath(p.path);
                if (obj == null)
                {
                    string error = findError ?? $"GameObject not found: {p.path}";
                    Debug.LogWarning($"[CommandExecutor] {error}");
                    return (false, error);
                }

                // Select object
                Selection.activeGameObject = obj;

                // Ping in Hierarchy
                EditorGUIUtility.PingObject(obj);

                string result = $"Selected GameObject: {obj.name}";
                Debug.Log($"[CommandExecutor] {result}");
                return (true, result);
            }
            catch (Exception e)
            {
                string error = $"Error setting selection: {e.Message}";
                Debug.LogError($"[CommandExecutor] {error}");
                return (false, error);
            }
        }

        /// <summary>
        /// Controls play mode (play, stop, pause, resume)
        /// Returns state when no parameters provided
        /// </summary>
        private (bool, string) PlayMode(CommandParams p)
        {
            try
            {
                // No parameters or no action -> get state (default)
                if (p == null || string.IsNullOrEmpty(p.action))
                {
                    return GetPlayModeState();
                }

                // Action mode
                switch (p.action.ToLower())
                {
                    case "play":
                        if (EditorApplication.isPlaying)
                        {
                            string warning = "[WARNING] Already in play mode";
                            Debug.LogWarning($"[CommandExecutor] {warning}");
                            return (true, warning);
                        }
                        if (EditorApplication.isCompiling)
                        {
                            string error = "Cannot enter play mode while compiling";
                            Debug.LogError($"[CommandExecutor] {error}");
                            return (false, error);
                        }
                        EditorApplication.EnterPlaymode();
                        Debug.Log("[CommandExecutor] Entered play mode");
                        return (true, "Entered play mode");

                    case "stop":
                        if (!EditorApplication.isPlaying)
                        {
                            string warning = "[WARNING] Not in play mode";
                            Debug.LogWarning($"[CommandExecutor] {warning}");
                            return (true, warning);
                        }
                        EditorApplication.ExitPlaymode();
                        Debug.Log("[CommandExecutor] Exited play mode");
                        return (true, "Exited play mode");

                    case "pause":
                        if (!EditorApplication.isPlaying)
                        {
                            string error = "Cannot pause: not in play mode";
                            Debug.LogError($"[CommandExecutor] {error}");
                            return (false, error);
                        }
                        if (EditorApplication.isPaused)
                        {
                            string warning = "[WARNING] Already paused";
                            Debug.LogWarning($"[CommandExecutor] {warning}");
                            return (true, warning);
                        }
                        EditorApplication.isPaused = true;
                        Debug.Log("[CommandExecutor] Paused play mode");
                        return (true, "Paused play mode");

                    case "resume":
                        if (!EditorApplication.isPlaying)
                        {
                            string error = "Cannot resume: not in play mode";
                            Debug.LogError($"[CommandExecutor] {error}");
                            return (false, error);
                        }
                        if (!EditorApplication.isPaused)
                        {
                            string warning = "[WARNING] Not paused";
                            Debug.LogWarning($"[CommandExecutor] {warning}");
                            return (true, warning);
                        }
                        EditorApplication.isPaused = false;
                        Debug.Log("[CommandExecutor] Resumed play mode");
                        return (true, "Resumed play mode");

                    default:
                        string unknownError = $"Unknown action: '{p.action}'. Valid actions: play, stop, pause, resume";
                        Debug.LogError($"[CommandExecutor] {unknownError}");
                        return (false, unknownError);
                }
            }
            catch (Exception e)
            {
                string error = $"Error in playmode: {e.Message}";
                Debug.LogError($"[CommandExecutor] {error}");
                return (false, error);
            }
        }

        /// <summary>
        /// Gets play mode state (internal helper)
        /// </summary>
        private (bool, string) GetPlayModeState()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Play Mode State:");
            sb.AppendLine();
            sb.AppendLine($"  Is Playing: {EditorApplication.isPlaying}");
            sb.AppendLine($"  Is Paused: {EditorApplication.isPaused}");
            sb.AppendLine($"  Is Compiling: {EditorApplication.isCompiling}");

            string state = !EditorApplication.isPlaying ? "Edit Mode"
                : EditorApplication.isPaused ? "Play Mode (Paused)"
                : "Play Mode (Running)";
            sb.AppendLine($"  State: {state}");

            Debug.Log("[CommandExecutor] Retrieved play mode state");
            return (true, sb.ToString());
        }
    }
}
