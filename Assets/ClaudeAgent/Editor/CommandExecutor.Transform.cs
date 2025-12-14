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
        /// Registers transform commands
        /// </summary>
        private void RegisterTransformCommands()
        {
            RegisterCommand("transform", TransformCommand);
            RegisterCommand("set_name", SetName);
            RegisterCommand("set_parent", SetParent);
            RegisterCommand("look_at", LookAt);
        }

        /// <summary>
        /// Unified transform command (get/set integration)
        /// Use get: true to retrieve, or specify position/rotation/scale to set
        /// </summary>
        private (bool, string) TransformCommand(CommandParams p)
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

                // If get mode
                if (p.get)
                {
                    // Check for simultaneous specification of get and property values
                    if (p.position != null || p.rotation != null || p.scale != null)
                    {
                        string error = "Cannot specify both 'get' and property values (position, rotation, scale)";
                        Debug.LogError($"[CommandExecutor] {error}");
                        return (false, error);
                    }

                    return GetTransformInfo(obj);
                }

                // If set mode
                bool hasPosition = p.position != null && p.position.Length == 3;
                bool hasRotation = p.rotation != null && p.rotation.Length == 3;
                bool hasScale = p.scale != null && p.scale.Length == 3;

                if (!hasPosition && !hasRotation && !hasScale)
                {
                    string error = "At least one property must be specified (position, rotation, or scale), or use 'get: true' for retrieval";
                    Debug.LogError($"[CommandExecutor] {error}");
                    return (false, error);
                }

                // Parse space parameter ("local" or "world", default is "local")
                bool useWorld = !string.IsNullOrEmpty(p.space) && p.space.ToLower() == "world";

                Undo.RecordObject(obj.transform, "Transform");

                var results = new List<string>();

                if (hasPosition)
                {
                    Vector3 newPosition = new Vector3(p.position[0], p.position[1], p.position[2]);
                    if (useWorld)
                    {
                        obj.transform.position = newPosition;
                    }
                    else
                    {
                        obj.transform.localPosition = newPosition;
                    }
                    results.Add($"position={newPosition}");
                }

                if (hasRotation)
                {
                    Vector3 newRotation = new Vector3(p.rotation[0], p.rotation[1], p.rotation[2]);
                    if (useWorld)
                    {
                        obj.transform.eulerAngles = newRotation;
                    }
                    else
                    {
                        obj.transform.localEulerAngles = newRotation;
                    }
                    results.Add($"rotation={newRotation}");
                }

                if (hasScale)
                {
                    Vector3 newScale = new Vector3(p.scale[0], p.scale[1], p.scale[2]);
                    obj.transform.localScale = newScale;
                    results.Add($"scale={newScale}");
                }

                string spaceType = useWorld ? "world" : "local";
                string result = $"Set {obj.name} ({spaceType}): {string.Join(", ", results)}";
                Debug.Log($"[CommandExecutor] {result}");
                return (true, result);
            }
            catch (Exception e)
            {
                string error = $"Error in transform command: {e.Message}";
                Debug.LogError($"[CommandExecutor] {error}");
                return (false, error);
            }
        }

        /// <summary>
        /// Gets transform information (internal helper)
        /// </summary>
        private (bool, string) GetTransformInfo(GameObject obj)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Transform info for: {obj.name}");
            sb.AppendLine($"  Path: {GetGameObjectPath(obj)}");
            sb.AppendLine();
            sb.AppendLine($"  World Position: {obj.transform.position}");
            sb.AppendLine($"  Local Position: {obj.transform.localPosition}");
            sb.AppendLine();
            sb.AppendLine($"  World Rotation: {obj.transform.eulerAngles}");
            sb.AppendLine($"  Local Rotation: {obj.transform.localEulerAngles}");
            sb.AppendLine();
            sb.AppendLine($"  Local Scale: {obj.transform.localScale}");
            sb.AppendLine();
            sb.AppendLine($"  Parent: {(obj.transform.parent != null ? obj.transform.parent.name : "None")}");
            sb.AppendLine($"  Children: {obj.transform.childCount}");

            string result = sb.ToString();
            Debug.Log($"[CommandExecutor] Retrieved transform info for {obj.name}");
            return (true, result);
        }

    }
}
