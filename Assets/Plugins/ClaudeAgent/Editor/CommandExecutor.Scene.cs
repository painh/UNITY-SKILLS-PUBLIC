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
        /// Registers scene commands
        /// </summary>
        private void RegisterSceneCommands()
        {
            RegisterCommand("open_scene", OpenScene);
            RegisterCommand("save_scene", SaveScene);
            RegisterCommand("create_scene", CreateScene);
            RegisterCommand("get_scene_hierarchy", GetSceneHierarchy);
            RegisterCommand("get_active_scene", GetActiveScene);
        }

        /// <summary>
        /// Opens a scene
        /// </summary>
        private (bool, string) OpenScene(CommandParams p)
        {
            try
            {
                if (p == null || string.IsNullOrEmpty(p.scene_path))
                {
                    string error = "Missing required parameter: scene_path";
                    Debug.LogError($"[CommandExecutor] {error}");
                    return (false, error);
                }

                // Check whether to save current scene (default: true)
                bool saveCurrentScene = p.save_current;

                if (saveCurrentScene)
                {
                    var currentScene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
                    if (currentScene.isDirty)
                    {
                        bool saved = UnityEditor.SceneManagement.EditorSceneManager.SaveScene(currentScene);
                        if (!saved)
                        {
                            string error = "Failed to save current scene before opening new scene";
                            Debug.LogWarning($"[CommandExecutor] {error}");
                            return (false, error);
                        }
                    }
                }

                // Open scene
                var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                    p.scene_path,
                    UnityEditor.SceneManagement.OpenSceneMode.Single
                );

                string result = $"Opened scene: {scene.name} (path: {p.scene_path})";
                Debug.Log($"[CommandExecutor] {result}");
                return (true, result);
            }
            catch (Exception e)
            {
                string error = $"Error opening scene: {e.Message}";
                Debug.LogError($"[CommandExecutor] {error}");
                return (false, error);
            }
        }

        /// <summary>
        /// Saves the current scene
        /// </summary>
        private (bool, string) SaveScene(CommandParams p)
        {
            try
            {
                var currentScene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();

                // Save as new file if scene_path is specified
                if (p != null && !string.IsNullOrEmpty(p.scene_path))
                {
                    // Check if directory exists
                    EnsureDirectoryExists(p.scene_path);

                    bool saved = UnityEditor.SceneManagement.EditorSceneManager.SaveScene(currentScene, p.scene_path);
                    if (!saved)
                    {
                        string error = $"Failed to save scene to: {p.scene_path}";
                        Debug.LogWarning($"[CommandExecutor] {error}");
                        return (false, error);
                    }

                    string result = $"Saved scene to: {p.scene_path}";
                    Debug.Log($"[CommandExecutor] {result}");
                    return (true, result);
                }
                else
                {
                    // Overwrite save to existing path
                    if (string.IsNullOrEmpty(currentScene.path))
                    {
                        string error = "Current scene has no path. Use scene_path parameter to save as new file.";
                        Debug.LogWarning($"[CommandExecutor] {error}");
                        return (false, error);
                    }

                    bool savedToCurrentPath = UnityEditor.SceneManagement.EditorSceneManager.SaveScene(currentScene);
                    if (!savedToCurrentPath)
                    {
                        string error = "Failed to save current scene";
                        Debug.LogWarning($"[CommandExecutor] {error}");
                        return (false, error);
                    }

                    string result = $"Saved scene: {currentScene.name} ({currentScene.path})";
                    Debug.Log($"[CommandExecutor] {result}");
                    return (true, result);
                }
            }
            catch (Exception e)
            {
                string error = $"Error saving scene: {e.Message}";
                Debug.LogError($"[CommandExecutor] {error}");
                return (false, error);
            }
        }

        /// <summary>
        /// Creates a new scene
        /// </summary>
        private (bool, string) CreateScene(CommandParams p)
        {
            try
            {
                if (p == null || string.IsNullOrEmpty(p.scene_path))
                {
                    string error = "Missing required parameter: scene_path";
                    Debug.LogError($"[CommandExecutor] {error}");
                    return (false, error);
                }

                // Check whether to save current scene (default: true)
                bool saveCurrentScene = p.save_current;

                if (saveCurrentScene)
                {
                    var currentScene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
                    if (currentScene.isDirty)
                    {
                        bool saved = UnityEditor.SceneManagement.EditorSceneManager.SaveScene(currentScene);
                        if (!saved)
                        {
                            string error = "Failed to save current scene before creating new scene";
                            Debug.LogWarning($"[CommandExecutor] {error}");
                            return (false, error);
                        }
                    }
                }

                // Create new scene
                var newScene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(
                    UnityEditor.SceneManagement.NewSceneSetup.DefaultGameObjects,
                    UnityEditor.SceneManagement.NewSceneMode.Single
                );

                // Check and create directory
                EnsureDirectoryExists(p.scene_path);

                // Save scene
                bool sceneSaved = UnityEditor.SceneManagement.EditorSceneManager.SaveScene(newScene, p.scene_path);
                if (!sceneSaved)
                {
                    string error = $"Failed to save new scene to: {p.scene_path}";
                    Debug.LogWarning($"[CommandExecutor] {error}");
                    return (false, error);
                }

                string result = $"Created new scene at: {p.scene_path}";
                Debug.Log($"[CommandExecutor] {result}");
                return (true, result);
            }
            catch (Exception e)
            {
                string error = $"Error creating scene: {e.Message}";
                Debug.LogError($"[CommandExecutor] {error}");
                return (false, error);
            }
        }

        /// <summary>
        /// Gets scene hierarchy (or prefab hierarchy when in Prefab Mode)
        /// </summary>
        private (bool, string) GetSceneHierarchy(CommandParams p)
        {
            try
            {
                int maxDepth = (p != null && p.max_depth > 0) ? p.max_depth : -1;
                var sb = new StringBuilder();

                // Check if in Prefab Mode
                var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
                if (prefabStage != null)
                {
                    // Return prefab hierarchy
                    var root = prefabStage.prefabContentsRoot;
                    sb.AppendLine($"Prefab Hierarchy: {prefabStage.assetPath}");
                    sb.AppendLine($"Root: {root.name}");
                    sb.AppendLine();

                    AppendHierarchy(sb, root, 0, maxDepth);

                    string prefabResult = sb.ToString();
                    Debug.Log($"[CommandExecutor] Retrieved hierarchy for prefab: {prefabStage.assetPath}");
                    return (true, prefabResult);
                }

                // Return scene hierarchy
                var activeScene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();

                sb.AppendLine($"Scene Hierarchy: {activeScene.name}");
                sb.AppendLine($"Path: {activeScene.path}");
                sb.AppendLine($"Root GameObjects: {activeScene.rootCount}");
                sb.AppendLine();

                // Get root objects
                GameObject[] rootObjects = activeScene.GetRootGameObjects();

                foreach (var rootObj in rootObjects)
                {
                    AppendHierarchy(sb, rootObj, 0, maxDepth);
                }

                string result = sb.ToString();
                Debug.Log($"[CommandExecutor] Retrieved hierarchy for scene: {activeScene.name}");
                return (true, result);
            }
            catch (Exception e)
            {
                string error = $"Error getting scene hierarchy: {e.Message}";
                Debug.LogError($"[CommandExecutor] {error}");
                return (false, error);
            }
        }

        /// <summary>
        /// Gets active scene information
        /// </summary>
        private (bool, string) GetActiveScene(CommandParams p)
        {
            try
            {
                var activeScene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();

                var sb = new StringBuilder();
                sb.AppendLine($"Active Scene Information:");
                sb.AppendLine();
                sb.AppendLine($"Name: {activeScene.name}");
                sb.AppendLine($"Path: {activeScene.path}");
                sb.AppendLine($"Build Index: {activeScene.buildIndex}");
                sb.AppendLine($"Is Loaded: {activeScene.isLoaded}");
                sb.AppendLine($"Is Dirty: {activeScene.isDirty}");
                sb.AppendLine($"Root GameObject Count: {activeScene.rootCount}");
                sb.AppendLine($"Is Valid: {activeScene.IsValid()}");

                string result = sb.ToString();
                Debug.Log($"[CommandExecutor] Retrieved active scene info: {activeScene.name}");
                return (true, result);
            }
            catch (Exception e)
            {
                string error = $"Error getting active scene: {e.Message}";
                Debug.LogError($"[CommandExecutor] {error}");
                return (false, error);
            }
        }
    }
}
