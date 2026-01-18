using UnityEngine;
using UnityEditor;
using System;
using System.Text;

namespace ClaudeAgent
{
    public partial class CommandExecutor
    {
        /// <summary>
        /// Registers layer commands
        /// </summary>
        private void RegisterLayerCommands()
        {
            RegisterCommand("list_layers", ListLayers);
            RegisterCommand("create_layer", CreateLayer);
            RegisterCommand("delete_layer", DeleteLayer);
            RegisterCommand("set_layer", SetLayer);
            RegisterCommand("get_layer", GetLayer);
        }

        /// <summary>
        /// Get the TagManager SerializedObject
        /// </summary>
        private SerializedObject GetTagManager()
        {
            var tagManager = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>("ProjectSettings/TagManager.asset");
            if (tagManager == null)
            {
                return null;
            }
            return new SerializedObject(tagManager);
        }

        /// <summary>
        /// List all layers
        /// </summary>
        private (bool, string) ListLayers(CommandParams p)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("Unity Layers:");
                sb.AppendLine();

                // Builtin layers (0-7)
                sb.AppendLine("Builtin Layers (0-7):");
                for (int i = 0; i <= 7; i++)
                {
                    string layerName = LayerMask.LayerToName(i);
                    if (!string.IsNullOrEmpty(layerName))
                    {
                        sb.AppendLine($"  [{i}] {layerName}");
                    }
                    else
                    {
                        sb.AppendLine($"  [{i}] (empty)");
                    }
                }

                sb.AppendLine();
                sb.AppendLine("User Layers (8-31):");

                int usedCount = 0;
                int emptyCount = 0;

                for (int i = 8; i <= 31; i++)
                {
                    string layerName = LayerMask.LayerToName(i);
                    if (!string.IsNullOrEmpty(layerName))
                    {
                        sb.AppendLine($"  [{i}] {layerName}");
                        usedCount++;
                    }
                    else
                    {
                        emptyCount++;
                    }
                }

                if (usedCount == 0)
                {
                    sb.AppendLine("  (no user layers defined)");
                }

                sb.AppendLine();
                sb.AppendLine($"Summary: {usedCount} user layers defined, {emptyCount} slots available");

                string result = sb.ToString().TrimEnd();
                ConsoleLog($"[CommandExecutor] Listed {usedCount + 8} layers");
                return Success(result);
            }
            catch (Exception e)
            {
                string error = $"Error listing layers: {e.Message}";
                ConsoleLogError($"[CommandExecutor] {error}");
                return Error(error);
            }
        }

        /// <summary>
        /// Create a new layer
        /// </summary>
        private (bool, string) CreateLayer(CommandParams p)
        {
            try
            {
                if (p == null || string.IsNullOrEmpty(p.name))
                {
                    return Error("Missing required parameter: name");
                }

                // Check if layer name already exists
                int existingIndex = LayerMask.NameToLayer(p.name);
                if (existingIndex != -1)
                {
                    return Error($"Layer '{p.name}' already exists at index {existingIndex}");
                }

                var so = GetTagManager();
                if (so == null)
                {
                    return Error("Failed to load TagManager");
                }

                var layersProperty = so.FindProperty("layers");
                if (layersProperty == null)
                {
                    return Error("Failed to find layers property in TagManager");
                }

                int targetIndex = -1;

                // If index specified, use it (must be 8-31)
                if (p.index >= 0)
                {
                    if (p.index < 8 || p.index > 31)
                    {
                        return Error($"Layer index must be between 8 and 31 (got {p.index}). Layers 0-7 are builtin.");
                    }

                    string existingName = layersProperty.GetArrayElementAtIndex(p.index).stringValue;
                    if (!string.IsNullOrEmpty(existingName))
                    {
                        return Error($"Layer index {p.index} is already in use by '{existingName}'");
                    }

                    targetIndex = p.index;
                }
                else
                {
                    // Find first empty slot (8-31)
                    for (int i = 8; i <= 31; i++)
                    {
                        string existingName = layersProperty.GetArrayElementAtIndex(i).stringValue;
                        if (string.IsNullOrEmpty(existingName))
                        {
                            targetIndex = i;
                            break;
                        }
                    }

                    if (targetIndex == -1)
                    {
                        return Error("No empty layer slots available (all 24 user layers are in use)");
                    }
                }

                // Set the layer name
                layersProperty.GetArrayElementAtIndex(targetIndex).stringValue = p.name;
                so.ApplyModifiedProperties();

                string result = $"Created layer '{p.name}' at index {targetIndex}";
                ConsoleLog($"[CommandExecutor] {result}");
                return Success(result);
            }
            catch (Exception e)
            {
                string error = $"Error creating layer: {e.Message}";
                ConsoleLogError($"[CommandExecutor] {error}");
                return Error(error);
            }
        }

        /// <summary>
        /// Delete a layer (clear its name)
        /// </summary>
        private (bool, string) DeleteLayer(CommandParams p)
        {
            try
            {
                int targetIndex = -1;
                string layerName = null;

                // Find by name or index
                if (!string.IsNullOrEmpty(p?.name))
                {
                    targetIndex = LayerMask.NameToLayer(p.name);
                    if (targetIndex == -1)
                    {
                        return Error($"Layer '{p.name}' not found");
                    }
                    layerName = p.name;
                }
                else if (p?.index >= 0)
                {
                    targetIndex = p.index;
                    layerName = LayerMask.LayerToName(targetIndex);
                }
                else
                {
                    return Error("Missing required parameter: name or index");
                }

                // Cannot delete builtin layers
                if (targetIndex < 8)
                {
                    return Error($"Cannot delete builtin layer {targetIndex} ('{layerName}'). Only user layers (8-31) can be deleted.");
                }

                if (targetIndex > 31)
                {
                    return Error($"Invalid layer index: {targetIndex}. Valid range is 0-31.");
                }

                var so = GetTagManager();
                if (so == null)
                {
                    return Error("Failed to load TagManager");
                }

                var layersProperty = so.FindProperty("layers");
                if (layersProperty == null)
                {
                    return Error("Failed to find layers property in TagManager");
                }

                string currentName = layersProperty.GetArrayElementAtIndex(targetIndex).stringValue;
                if (string.IsNullOrEmpty(currentName))
                {
                    return Error($"Layer at index {targetIndex} is already empty");
                }

                // Clear the layer name
                layersProperty.GetArrayElementAtIndex(targetIndex).stringValue = "";
                so.ApplyModifiedProperties();

                string result = $"Deleted layer '{currentName}' at index {targetIndex}";
                ConsoleLog($"[CommandExecutor] {result}");
                return Success(result);
            }
            catch (Exception e)
            {
                string error = $"Error deleting layer: {e.Message}";
                ConsoleLogError($"[CommandExecutor] {error}");
                return Error(error);
            }
        }

        /// <summary>
        /// Set a GameObject's layer
        /// </summary>
        private (bool, string) SetLayer(CommandParams p)
        {
            try
            {
                if (p == null || string.IsNullOrEmpty(p.path))
                {
                    return Error("Missing required parameter: path");
                }

                if (string.IsNullOrEmpty(p.layer) && p.index < 0)
                {
                    return Error("Missing required parameter: layer (name or index)");
                }

                var (obj, findError) = FindGameObjectByPath(p.path);
                if (obj == null)
                {
                    return Error(findError ?? $"GameObject not found: {p.path}");
                }

                int layerIndex;

                // Get layer by name or index
                if (!string.IsNullOrEmpty(p.layer))
                {
                    layerIndex = LayerMask.NameToLayer(p.layer);
                    if (layerIndex == -1)
                    {
                        return Error($"Layer '{p.layer}' not found");
                    }
                }
                else
                {
                    layerIndex = p.index;
                    if (layerIndex < 0 || layerIndex > 31)
                    {
                        return Error($"Invalid layer index: {layerIndex}. Valid range is 0-31.");
                    }
                }

                string layerName = LayerMask.LayerToName(layerIndex);

                // Check if recursive
                bool recursive = p.recursive;

                if (recursive)
                {
                    SetLayerRecursively(obj, layerIndex);
                    string result = $"Set layer of '{obj.name}' and children to '{layerName}' ({layerIndex})";
                    ConsoleLog($"[CommandExecutor] {result}");
                    return Success(result);
                }
                else
                {
                    Undo.RecordObject(obj, "Set Layer");
                    obj.layer = layerIndex;
                    EditorUtility.SetDirty(obj);

                    string result = $"Set layer of '{obj.name}' to '{layerName}' ({layerIndex})";
                    ConsoleLog($"[CommandExecutor] {result}");
                    return Success(result);
                }
            }
            catch (Exception e)
            {
                string error = $"Error setting layer: {e.Message}";
                ConsoleLogError($"[CommandExecutor] {error}");
                return Error(error);
            }
        }

        /// <summary>
        /// Set layer recursively for all children
        /// </summary>
        private void SetLayerRecursively(GameObject obj, int layer)
        {
            Undo.RecordObject(obj, "Set Layer Recursive");
            obj.layer = layer;
            EditorUtility.SetDirty(obj);

            foreach (Transform child in obj.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }

        /// <summary>
        /// Get a GameObject's layer
        /// </summary>
        private (bool, string) GetLayer(CommandParams p)
        {
            try
            {
                if (p == null || string.IsNullOrEmpty(p.path))
                {
                    return Error("Missing required parameter: path");
                }

                var (obj, findError) = FindGameObjectByPath(p.path);
                if (obj == null)
                {
                    return Error(findError ?? $"GameObject not found: {p.path}");
                }

                int layerIndex = obj.layer;
                string layerName = LayerMask.LayerToName(layerIndex);

                string result = $"Layer of '{obj.name}': '{layerName}' (index: {layerIndex})";
                ConsoleLog($"[CommandExecutor] {result}");
                return Success(result);
            }
            catch (Exception e)
            {
                string error = $"Error getting layer: {e.Message}";
                ConsoleLogError($"[CommandExecutor] {error}");
                return Error(error);
            }
        }
    }
}
