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
        /// Registers prefab commands
        /// </summary>
        private void RegisterPrefabCommands()
        {
            RegisterCommand("create_prefab", CreatePrefab);
            RegisterCommand("instantiate_prefab", InstantiatePrefab);
            RegisterCommand("open_prefab", OpenPrefab);
            RegisterCommand("save_prefab", SavePrefab);
        }

        /// <summary>
        /// Creates a prefab from a GameObject
        /// </summary>
        private (bool, string) CreatePrefab(CommandParams p)
        {
            try
            {
                if (p == null || string.IsNullOrEmpty(p.path) || string.IsNullOrEmpty(p.prefab_path))
                {
                    string error = "Missing required parameters: path and prefab_path";
                    ConsoleLogError($"[CommandExecutor] {error}");
                    return (false, error);
                }

                // Get GameObject
                var (obj, findError) = FindGameObjectByPath(p.path);
                if (obj == null)
                {
                    string error = findError ?? $"GameObject not found: {p.path}";
                    ConsoleLogWarning($"[CommandExecutor] {error}");
                    return (false, error);
                }

                // Create prefab destination directory
                EnsureDirectoryExists(p.prefab_path);

                // Create prefab
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(obj, p.prefab_path);
                if (prefab == null)
                {
                    string error = $"Failed to create prefab at: {p.prefab_path}";
                    ConsoleLogWarning($"[CommandExecutor] {error}");
                    return (false, error);
                }

                string result = $"Created prefab from {obj.name} at: {p.prefab_path}";
                ConsoleLog($"[CommandExecutor] {result}");
                return (true, result);
            }
            catch (Exception e)
            {
                string error = $"Error creating prefab: {e.Message}";
                ConsoleLogError($"[CommandExecutor] {error}");
                return (false, error);
            }
        }

        /// <summary>
        /// Instantiates a prefab
        /// </summary>
        private (bool, string) InstantiatePrefab(CommandParams p)
        {
            try
            {
                if (p == null || string.IsNullOrEmpty(p.prefab_path))
                {
                    string error = "Missing required parameter: prefab_path";
                    ConsoleLogError($"[CommandExecutor] {error}");
                    return (false, error);
                }

                // Smart default: with parent -> local, without parent -> world
                string defaultSpace = string.IsNullOrEmpty(p.parent) ? "world" : "local";

                // Validate space parameter (before instantiation)
                string posSpace = p.position_space;
                var (posValid, posError) = ValidateSpaceParameter("position", p.position, ref posSpace, defaultSpace);
                if (!posValid)
                {
                    ConsoleLogError($"[CommandExecutor] {posError}");
                    return (false, posError);
                }

                string rotSpace = p.rotation_space;
                var (rotValid, rotError) = ValidateSpaceParameter("rotation", p.rotation, ref rotSpace, defaultSpace);
                if (!rotValid)
                {
                    ConsoleLogError($"[CommandExecutor] {rotError}");
                    return (false, rotError);
                }

                string scaleSpace = p.scale_space;
                var (scaleValid, scaleError) = ValidateSpaceParameter("scale", p.scale, ref scaleSpace, defaultSpace);
                if (!scaleValid)
                {
                    ConsoleLogError($"[CommandExecutor] {scaleError}");
                    return (false, scaleError);
                }

                // Load Prefab
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(p.prefab_path);
                if (prefab == null)
                {
                    string error = $"Prefab not found: {p.prefab_path}";
                    ConsoleLogWarning($"[CommandExecutor] {error}");
                    return (false, error);
                }

                // Get parent object (if specified)
                Transform parent = null;
                if (!string.IsNullOrEmpty(p.parent))
                {
                    var (parentObj, parentFindError) = FindGameObjectByPath(p.parent);
                    if (parentObj != null)
                    {
                        parent = parentObj.transform;
                    }
                    else if (parentFindError != null)
                    {
                        // Return duplicate error as is
                        ConsoleLogWarning($"[CommandExecutor] {parentFindError}");
                        return (false, parentFindError);
                    }
                }

                // Check for duplicates when placing at root (before instantiation)
                string desiredName = !string.IsNullOrEmpty(p.name) ? p.name : prefab.name;
                if (parent == null)
                {
                    string duplicateError = CheckDuplicateRootName(desiredName);
                    if (duplicateError != null)
                    {
                        ConsoleLogError($"[CommandExecutor] {duplicateError}");
                        return (false, duplicateError);
                    }
                }

                // Instantiate prefab
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                if (instance == null)
                {
                    string error = $"Failed to instantiate prefab: {p.prefab_path}";
                    ConsoleLogWarning($"[CommandExecutor] {error}");
                    return (false, error);
                }

                // Register with Undo
                Undo.RegisterCreatedObjectUndo(instance, "Instantiate Prefab");

                // Set name
                if (!string.IsNullOrEmpty(p.name))
                {
                    instance.name = p.name;
                }

                // Set position (based on space parameter)
                if (p.position != null && p.position.Length == 3)
                {
                    Vector3 pos = new Vector3(p.position[0], p.position[1], p.position[2]);
                    if (posSpace == "local")
                    {
                        instance.transform.localPosition = pos;
                    }
                    else // "world"
                    {
                        instance.transform.position = pos;
                    }
                }

                // Set rotation (based on space parameter)
                if (p.rotation != null && p.rotation.Length == 3)
                {
                    Vector3 rot = new Vector3(p.rotation[0], p.rotation[1], p.rotation[2]);
                    if (rotSpace == "local")
                    {
                        instance.transform.localEulerAngles = rot;
                    }
                    else // "world"
                    {
                        instance.transform.eulerAngles = rot;
                    }
                }

                // Set scale (based on space parameter)
                if (p.scale != null && p.scale.Length == 3)
                {
                    Vector3 scale = new Vector3(p.scale[0], p.scale[1], p.scale[2]);
                    if (scaleSpace == "world" && instance.transform.parent != null)
                    {
                        // Divide by parent's scale to maintain world scale
                        Vector3 parentScale = instance.transform.parent.lossyScale;
                        scale = new Vector3(
                            scale.x / parentScale.x,
                            scale.y / parentScale.y,
                            scale.z / parentScale.z
                        );
                    }
                    instance.transform.localScale = scale;
                }

                // Collect used space information
                var spaceInfo = new List<string>();
                if (p.position != null && p.position.Length == 3) spaceInfo.Add($"position: {posSpace}");
                if (p.rotation != null && p.rotation.Length == 3) spaceInfo.Add($"rotation: {rotSpace}");
                if (p.scale != null && p.scale.Length == 3) spaceInfo.Add($"scale: {scaleSpace}");

                string result = $"Instantiated prefab: {instance.name} from {p.prefab_path}";
                if (spaceInfo.Count > 0)
                {
                    result += $" ({string.Join(", ", spaceInfo)})";
                }
                ConsoleLog($"[CommandExecutor] {result}");
                return (true, result);
            }
            catch (Exception e)
            {
                string error = $"Error instantiating prefab: {e.Message}";
                ConsoleLogError($"[CommandExecutor] {error}");
                return (false, error);
            }
        }

        /// <summary>
        /// Opens a prefab in edit mode
        /// </summary>
        private (bool, string) OpenPrefab(CommandParams p)
        {
            try
            {
                if (p == null || string.IsNullOrEmpty(p.prefab_path))
                {
                    string error = "Missing required parameter: prefab_path";
                    ConsoleLogError($"[CommandExecutor] {error}");
                    return (false, error);
                }

                // Load prefab
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(p.prefab_path);
                if (prefab == null)
                {
                    string error = $"Prefab not found: {p.prefab_path}";
                    ConsoleLogWarning($"[CommandExecutor] {error}");
                    return (false, error);
                }

                // Open in prefab edit mode
                string assetPath = AssetDatabase.GetAssetPath(prefab);
                PrefabStageUtility.OpenPrefab(assetPath);

                string result = $"Opened prefab in edit mode: {p.prefab_path}";
                ConsoleLog($"[CommandExecutor] {result}");
                return (true, result);
            }
            catch (Exception e)
            {
                string error = $"Error opening prefab: {e.Message}";
                ConsoleLogError($"[CommandExecutor] {error}");
                return (false, error);
            }
        }

        /// <summary>
        /// Saves the currently editing prefab
        /// </summary>
        private (bool, string) SavePrefab(CommandParams p)
        {
            try
            {
                // Get current Prefab Stage
                var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
                if (prefabStage == null)
                {
                    string error = "No prefab is currently open in Prefab Mode";
                    ConsoleLogWarning($"[CommandExecutor] {error}");
                    return (false, error);
                }

                // Save prefab
                bool saved = PrefabUtility.SaveAsPrefabAsset(prefabStage.prefabContentsRoot, prefabStage.assetPath);
                if (!saved)
                {
                    string error = "Failed to save prefab";
                    ConsoleLogWarning($"[CommandExecutor] {error}");
                    return (false, error);
                }

                string result = $"Saved prefab: {prefabStage.assetPath}";
                ConsoleLog($"[CommandExecutor] {result}");
                return (true, result);
            }
            catch (Exception e)
            {
                string error = $"Error saving prefab: {e.Message}";
                ConsoleLogError($"[CommandExecutor] {error}");
                return (false, error);
            }
        }
    }
}
