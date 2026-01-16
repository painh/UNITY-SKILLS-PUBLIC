using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System;
using System.Text;
using System.Collections;
using System.Collections.Generic;

namespace ClaudeAgent
{
    public partial class CommandExecutor
    {
        /// <summary>
        /// Registers GameObject commands
        /// </summary>
        private void RegisterGameObjectCommands()
        {
            RegisterCommand("create_primitive", CreatePrimitive);
            RegisterCommand("create_empty", CreateEmpty);
            RegisterCommand("delete_gameobject", DeleteGameObject);
            RegisterCommand("set_active", SetActive);
            RegisterCommand("tag", TagCommand);
            RegisterCommand("create_tag", CreateTag);
            RegisterCommand("delete_tag", DeleteTag);
            RegisterCommand("find_gameobject", FindGameObject);
            RegisterCommand("duplicate_gameobject", DuplicateGameObject);
            RegisterCommand("create_line", CreateLine);
        }

        /// <summary>
        /// Creates a primitive shape
        /// </summary>
        private (bool, string) CreatePrimitive(CommandParams p)
        {
            try
            {
                if (p == null)
                {
                    string error = "Missing required parameters";
                    Debug.LogError($"[CommandExecutor] {error}");
                    return (false, error);
                }

                // Parse PrimitiveType
                var (typeSuccess, primitiveType, typeError) = TryParsePrimitiveType(p.type);
                if (!typeSuccess)
                {
                    Debug.LogError($"[CommandExecutor] {typeError}");
                    return (false, typeError);
                }

                // Smart default: with parent → local, without parent → world
                string defaultSpace = string.IsNullOrEmpty(p.parent) ? "world" : "local";

                // Validate space parameter (before object creation)
                string posSpace = p.position_space;
                var (posValid, posError) = ValidateSpaceParameter("position", p.position, ref posSpace, defaultSpace);
                if (!posValid)
                {
                    Debug.LogError($"[CommandExecutor] {posError}");
                    return (false, posError);
                }

                string rotSpace = p.rotation_space;
                var (rotValid, rotError) = ValidateSpaceParameter("rotation", p.rotation, ref rotSpace, defaultSpace);
                if (!rotValid)
                {
                    Debug.LogError($"[CommandExecutor] {rotError}");
                    return (false, rotError);
                }

                string scaleSpace = p.scale_space;
                var (scaleValid, scaleError) = ValidateSpaceParameter("scale", p.scale, ref scaleSpace, defaultSpace);
                if (!scaleValid)
                {
                    Debug.LogError($"[CommandExecutor] {scaleError}");
                    return (false, scaleError);
                }

                // Check for duplicate names (before creation)
                string desiredName = !string.IsNullOrEmpty(p.name) ? p.name : primitiveType.ToString();
                string duplicateError = CheckDuplicateRootName(desiredName);
                if (duplicateError != null)
                {
                    Debug.LogError($"[CommandExecutor] {duplicateError}");
                    return (false, duplicateError);
                }

                // Create primitive
                var gameObject = GameObject.CreatePrimitive(primitiveType);
                gameObject.name = desiredName;

                // Set color
                if (!string.IsNullOrEmpty(p.color))
                {
                    var (colorSuccess, parsedColor, colorError) = TryParseColor(p.color);
                    if (!colorSuccess)
                    {
                        // Primitive already created, destroy and return error
                        GameObject.DestroyImmediate(gameObject);
                        Debug.LogError($"[CommandExecutor] {colorError}");
                        return (false, colorError);
                    }

                    var renderer = gameObject.GetComponent<Renderer>();
                    if (renderer != null)
                    {
                        // Create new material to prevent material leak in Edit Mode
                        var material = new Material(renderer.sharedMaterial);
                        material.color = parsedColor;  // For Standard Shader (_Color)
                        // For URP Lit Shader (_BaseColor)
                        if (material.HasProperty("_BaseColor"))
                        {
                            material.SetColor("_BaseColor", parsedColor);
                        }
                        renderer.sharedMaterial = material;
                    }
                }

                // Set parent (before transform settings)
                if (!string.IsNullOrEmpty(p.parent))
                {
                    var (parentObj, parentFindError) = FindGameObjectByPath(p.parent);
                    if (parentObj != null)
                    {
                        gameObject.transform.SetParent(parentObj.transform, false);
                    }
                    else
                    {
                        GameObject.DestroyImmediate(gameObject);
                        string error = parentFindError ?? $"Parent object not found: {p.parent}";
                        Debug.LogWarning($"[CommandExecutor] {error}");
                        return (false, error);
                    }
                }

                // Set position (based on space parameter)
                if (p.position != null && p.position.Length == 3)
                {
                    Vector3 pos = new Vector3(p.position[0], p.position[1], p.position[2]);
                    if (posSpace == "local")
                    {
                        gameObject.transform.localPosition = pos;
                    }
                    else // "world"
                    {
                        gameObject.transform.position = pos;
                    }
                }

                // Set rotation (based on space parameter)
                if (p.rotation != null && p.rotation.Length == 3)
                {
                    Vector3 rot = new Vector3(p.rotation[0], p.rotation[1], p.rotation[2]);
                    if (rotSpace == "local")
                    {
                        gameObject.transform.localEulerAngles = rot;
                    }
                    else // "world"
                    {
                        gameObject.transform.eulerAngles = rot;
                    }
                }

                // Set scale (always local scale, space is for semantic clarity only)
                if (p.scale != null && p.scale.Length == 3)
                {
                    // Note: Unity doesn't have direct world scale setting,
                    // so localScale is set even when scale_space="world"
                    // However, calculation is needed to maintain world scale when parented
                    Vector3 scale = new Vector3(p.scale[0], p.scale[1], p.scale[2]);
                    if (scaleSpace == "world" && gameObject.transform.parent != null)
                    {
                        // Divide by parent's scale to maintain world scale
                        Vector3 parentScale = gameObject.transform.parent.lossyScale;
                        scale = new Vector3(
                            scale.x / parentScale.x,
                            scale.y / parentScale.y,
                            scale.z / parentScale.z
                        );
                    }
                    gameObject.transform.localScale = scale;
                }

                // Register Undo (to allow Ctrl+Z deletion)
                Undo.RegisterCreatedObjectUndo(gameObject, "Create Primitive");

                // Collect used space information (only for specified parameters)
                var spaceInfo = new List<string>();
                if (p.position != null && p.position.Length == 3) spaceInfo.Add($"position: {posSpace}");
                if (p.rotation != null && p.rotation.Length == 3) spaceInfo.Add($"rotation: {rotSpace}");
                if (p.scale != null && p.scale.Length == 3) spaceInfo.Add($"scale: {scaleSpace}");

                string result = $"Created {p.type}: {gameObject.name}";
                if (spaceInfo.Count > 0)
                {
                    result += $" ({string.Join(", ", spaceInfo)})";
                }
                Debug.Log($"[CommandExecutor] {result}");
                return (true, result);
            }
            catch (Exception e)
            {
                string error = $"Error creating primitive: {e.Message}";
                Debug.LogError($"[CommandExecutor] {error}");
                return (false, error);
            }
        }

        /// <summary>
        /// Creates an empty GameObject
        /// </summary>
        private (bool, string) CreateEmpty(CommandParams p)
        {
            try
            {
                // Smart default: with parent → local, without parent → world
                string defaultSpace = string.IsNullOrEmpty(p?.parent) ? "world" : "local";

                // Validate space parameter (before object creation)
                string posSpace = p?.position_space;
                var (posValid, posError) = ValidateSpaceParameter("position", p?.position, ref posSpace, defaultSpace);
                if (!posValid)
                {
                    Debug.LogError($"[CommandExecutor] {posError}");
                    return (false, posError);
                }

                string desiredName = !string.IsNullOrEmpty(p?.name) ? p.name : "GameObject";

                // Check for duplicates when placing at root (before creation)
                if (string.IsNullOrEmpty(p?.parent))
                {
                    string duplicateError = CheckDuplicateRootName(desiredName);
                    if (duplicateError != null)
                    {
                        Debug.LogError($"[CommandExecutor] {duplicateError}");
                        return (false, duplicateError);
                    }
                }

                var gameObject = new GameObject(desiredName);

                // Set parent (before transform settings)
                if (!string.IsNullOrEmpty(p?.parent))
                {
                    var (parentObj, parentFindError) = FindGameObjectByPath(p.parent);
                    if (parentObj != null)
                    {
                        gameObject.transform.SetParent(parentObj.transform, false);
                    }
                    else
                    {
                        // Destroy the created object and return error
                        UnityEngine.Object.DestroyImmediate(gameObject);
                        string error = parentFindError ?? $"Parent object not found: {p.parent}";
                        Debug.LogWarning($"[CommandExecutor] {error}");
                        return (false, error);
                    }
                }

                // Set position (based on space parameter)
                if (p?.position != null && p.position.Length == 3)
                {
                    Vector3 pos = new Vector3(p.position[0], p.position[1], p.position[2]);
                    if (posSpace == "local")
                    {
                        gameObject.transform.localPosition = pos;
                    }
                    else // "world"
                    {
                        gameObject.transform.position = pos;
                    }
                }

                // Register Undo
                Undo.RegisterCreatedObjectUndo(gameObject, "Create Empty GameObject");

                // Collect used space information (only for specified parameters)
                string result = $"Created empty GameObject: {gameObject.name}";
                if (p?.position != null && p.position.Length == 3)
                {
                    result += $" (position: {posSpace})";
                }
                Debug.Log($"[CommandExecutor] {result}");
                return (true, result);
            }
            catch (Exception e)
            {
                string error = $"Error creating empty GameObject: {e.Message}";
                Debug.LogError($"[CommandExecutor] {error}");
                return (false, error);
            }
        }

        /// <summary>
        /// Deletes a GameObject
        /// </summary>
        private (bool, string) DeleteGameObject(CommandParams p)
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

                string objName = obj.name;
                Undo.DestroyObjectImmediate(obj);

                string result = $"Deleted GameObject: {objName}";
                Debug.Log($"[CommandExecutor] {result}");
                return (true, result);
            }
            catch (Exception e)
            {
                string error = $"Error deleting GameObject: {e.Message}";
                Debug.LogError($"[CommandExecutor] {error}");
                return (false, error);
            }
        }

        /// <summary>
        /// Sets the active state of a GameObject
        /// </summary>
        private (bool, string) SetActive(CommandParams p)
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

                Undo.RecordObject(obj, "Set Active");
                obj.SetActive(p.active);

                string result = $"Set {obj.name} active to: {p.active}";
                Debug.Log($"[CommandExecutor] {result}");
                return (true, result);
            }
            catch (Exception e)
            {
                string error = $"Error setting active state: {e.Message}";
                Debug.LogError($"[CommandExecutor] {error}");
                return (false, error);
            }
        }

        /// <summary>
        /// Unified Tag command (get/set integrated)
        /// Use get: true to retrieve, specify tag to set
        /// </summary>
        private (bool, string) TagCommand(CommandParams p)
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

                bool hasTag = !string.IsNullOrEmpty(p.tag);

                // If get mode
                if (p.get)
                {
                    // Check for simultaneous get and set parameter specification
                    if (hasTag)
                    {
                        string error = "Cannot specify both 'get' and 'tag'";
                        Debug.LogError($"[CommandExecutor] {error}");
                        return (false, error);
                    }

                    string result = $"Tag of {obj.name}: {obj.tag}";
                    Debug.Log($"[CommandExecutor] {result}");
                    return (true, result);
                }

                // If set mode
                if (!hasTag)
                {
                    string error = "Must specify 'tag' or use 'get: true' for retrieval";
                    Debug.LogError($"[CommandExecutor] {error}");
                    return (false, error);
                }

                // Pre-check if tag exists (Unity silently fails on non-existent tags without throwing exceptions)
                string[] availableTags = UnityEditorInternal.InternalEditorUtility.tags;
                bool tagExists = false;
                foreach (string availableTag in availableTags)
                {
                    if (availableTag == p.tag)
                    {
                        tagExists = true;
                        break;
                    }
                }

                if (!tagExists)
                {
                    string error = $"Tag not found: {p.tag}. Use 'create_tag' operation or add the tag in Edit > Project Settings > Tags and Layers first.";
                    Debug.LogWarning($"[CommandExecutor] {error}");
                    return (false, error);
                }

                Undo.RecordObject(obj, "Set Tag");
                obj.tag = p.tag;

                string result2 = $"Set tag of {obj.name} to: {p.tag}";
                Debug.Log($"[CommandExecutor] {result2}");
                return (true, result2);
            }
            catch (Exception e)
            {
                string error = $"Error setting tag: {e.Message}";
                Debug.LogError($"[CommandExecutor] {error}");
                return (false, error);
            }
        }

        /// <summary>
        /// Creates a tag in the project
        /// </summary>
        private (bool, string) CreateTag(CommandParams p)
        {
            try
            {
                if (p == null || string.IsNullOrEmpty(p.tag))
                {
                    string error = "Missing required parameter: tag";
                    Debug.LogError($"[CommandExecutor] {error}");
                    return (false, error);
                }

                // Get TagManager
                SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
                SerializedProperty tagsProperty = tagManager.FindProperty("tags");

                // Check existing tags
                for (int i = 0; i < tagsProperty.arraySize; i++)
                {
                    SerializedProperty tag = tagsProperty.GetArrayElementAtIndex(i);
                    if (tag.stringValue == p.tag)
                    {
                        string info = $"Tag already exists: {p.tag}";
                        Debug.Log($"[CommandExecutor] {info}");
                        return (true, info);
                    }
                }

                // Add new tag
                tagsProperty.InsertArrayElementAtIndex(tagsProperty.arraySize);
                SerializedProperty newTag = tagsProperty.GetArrayElementAtIndex(tagsProperty.arraySize - 1);
                newTag.stringValue = p.tag;
                tagManager.ApplyModifiedProperties();

                string result = $"Created tag: {p.tag}";
                Debug.Log($"[CommandExecutor] {result}");
                return (true, result);
            }
            catch (Exception e)
            {
                string error = $"Error creating tag: {e.Message}";
                Debug.LogError($"[CommandExecutor] {error}");
                return (false, error);
            }
        }

        /// <summary>
        /// Deletes a tag from the project
        /// </summary>
        private (bool, string) DeleteTag(CommandParams p)
        {
            try
            {
                if (p == null || string.IsNullOrEmpty(p.tag))
                {
                    string error = "Missing required parameter: tag";
                    Debug.LogError($"[CommandExecutor] {error}");
                    return (false, error);
                }

                // Built-in tags cannot be deleted
                string[] builtInTags = { "Untagged", "Respawn", "Finish", "EditorOnly", "MainCamera", "Player", "GameController" };
                foreach (string builtInTag in builtInTags)
                {
                    if (p.tag == builtInTag)
                    {
                        string error = $"Cannot delete built-in tag: {p.tag}";
                        Debug.LogError($"[CommandExecutor] {error}");
                        return (false, error);
                    }
                }

                // Get TagManager
                SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
                SerializedProperty tagsProperty = tagManager.FindProperty("tags");

                // Find and delete tag
                for (int i = 0; i < tagsProperty.arraySize; i++)
                {
                    SerializedProperty tag = tagsProperty.GetArrayElementAtIndex(i);
                    if (tag.stringValue == p.tag)
                    {
                        tagsProperty.DeleteArrayElementAtIndex(i);
                        tagManager.ApplyModifiedProperties();

                        string result = $"Deleted tag: {p.tag}";
                        Debug.Log($"[CommandExecutor] {result}");
                        return (true, result);
                    }
                }

                string notFoundError = $"Tag not found: {p.tag}";
                Debug.LogWarning($"[CommandExecutor] {notFoundError}");
                return (false, notFoundError);
            }
            catch (Exception e)
            {
                string error = $"Error deleting tag: {e.Message}";
                Debug.LogError($"[CommandExecutor] {error}");
                return (false, error);
            }
        }

        /// <summary>
        /// Finds GameObjects (supports Prefab Mode)
        /// </summary>
        private (bool, string) FindGameObject(CommandParams p)
        {
            try
            {
                var sb = new StringBuilder();
                var foundObjects = new List<GameObject>();

                // Check if in Prefab Mode
                var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();

                // If path specified (exact match)
                if (!string.IsNullOrEmpty(p.path))
                {
                    var (obj, findError) = FindGameObjectByPath(p.path);
                    if (obj != null)
                    {
                        foundObjects.Add(obj);
                    }
                    else if (findError != null)
                    {
                        // Return duplicate error as is
                        Debug.LogWarning($"[CommandExecutor] {findError}");
                        return (false, findError);
                    }
                }
                // Search by tag (only in scene mode, tags don't work in prefab stage)
                else if (!string.IsNullOrEmpty(p.tag))
                {
                    if (prefabStage != null)
                    {
                        // Search by tag in prefab hierarchy
                        FindGameObjectsByTagInHierarchy(prefabStage.prefabContentsRoot.transform, p.tag, foundObjects);
                    }
                    else
                    {
                        GameObject[] taggedObjects = GameObject.FindGameObjectsWithTag(p.tag);
                        foundObjects.AddRange(taggedObjects);
                    }
                }
                // Search by name (partial match)
                else if (!string.IsNullOrEmpty(p.name))
                {
                    if (prefabStage != null)
                    {
                        // Search in prefab hierarchy
                        var root = prefabStage.prefabContentsRoot;
                        if (root.name.Contains(p.name))
                        {
                            foundObjects.Add(root);
                        }
                        FindGameObjectsByNameContainsInHierarchy(root.transform, p.name, foundObjects);
                    }
                    else
                    {
                        GameObject[] allObjects = GameObject.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
                        foreach (var obj in allObjects)
                        {
                            if (obj.name.Contains(p.name))
                            {
                                foundObjects.Add(obj);
                            }
                        }
                    }
                }

                // Build header based on context
                if (prefabStage != null)
                {
                    sb.AppendLine($"Found {foundObjects.Count} GameObject(s) in prefab:");
                }
                else
                {
                    sb.AppendLine($"Found {foundObjects.Count} GameObject(s):");
                }
                sb.AppendLine();

                foreach (var obj in foundObjects)
                {
                    sb.AppendLine($"Name: {obj.name}");
                    sb.AppendLine($"  Path: {GetGameObjectPath(obj)}");
                    sb.AppendLine($"  Position: {obj.transform.position}");
                    sb.AppendLine($"  Active: {obj.activeSelf}");
                    sb.AppendLine($"  Tag: {obj.tag}");
                    sb.AppendLine();
                }

                string result = sb.ToString();
                Debug.Log($"[CommandExecutor] Found {foundObjects.Count} objects");
                return (true, result);
            }
            catch (Exception e)
            {
                string error = $"Error finding GameObject: {e.Message}";
                Debug.LogError($"[CommandExecutor] {error}");
                return (false, error);
            }
        }

        /// <summary>
        /// Recursively finds GameObjects by name (partial match) in hierarchy
        /// </summary>
        private void FindGameObjectsByNameContainsInHierarchy(Transform parent, string name, List<GameObject> results)
        {
            foreach (Transform child in parent)
            {
                if (child.name.Contains(name))
                {
                    results.Add(child.gameObject);
                }
                FindGameObjectsByNameContainsInHierarchy(child, name, results);
            }
        }

        /// <summary>
        /// Recursively finds GameObjects by tag in hierarchy
        /// </summary>
        private void FindGameObjectsByTagInHierarchy(Transform parent, string tag, List<GameObject> results)
        {
            if (parent.gameObject.CompareTag(tag))
            {
                results.Add(parent.gameObject);
            }
            foreach (Transform child in parent)
            {
                FindGameObjectsByTagInHierarchy(child, tag, results);
            }
        }

        /// <summary>
        /// Changes the name of a GameObject
        /// </summary>
        private (bool, string) SetName(CommandParams p)
        {
            try
            {
                if (p == null || string.IsNullOrEmpty(p.path))
                {
                    string error = "Missing required parameter: path";
                    Debug.LogError($"[CommandExecutor] {error}");
                    return (false, error);
                }

                // Accept new_name or name parameter (name is an alias)
                string newName = !string.IsNullOrEmpty(p.new_name) ? p.new_name : p.name;
                if (string.IsNullOrEmpty(newName))
                {
                    string error = "Missing required parameter: new_name (or name)";
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

                string oldName = obj.name;
                Undo.RecordObject(obj, "Set Name");
                obj.name = newName;

                string result = $"Renamed '{oldName}' to '{newName}'";
                Debug.Log($"[CommandExecutor] {result}");
                return (true, result);
            }
            catch (Exception e)
            {
                string error = $"Error setting name: {e.Message}";
                Debug.LogError($"[CommandExecutor] {error}");
                return (false, error);
            }
        }

        /// <summary>
        /// Sets the parent-child relationship of a GameObject
        /// </summary>
        private (bool, string) SetParent(CommandParams p)
        {
            try
            {
                if (p == null || string.IsNullOrEmpty(p.path))
                {
                    string error = "Missing required parameter: path";
                    Debug.LogError($"[CommandExecutor] {error}");
                    return (false, error);
                }

                var (child, childFindError) = FindGameObjectByPath(p.path);
                if (child == null)
                {
                    string error = childFindError ?? $"GameObject not found: {p.path}";
                    Debug.LogWarning($"[CommandExecutor] {error}");
                    return (false, error);
                }

                Transform newParent = null;
                string parentName = "root";

                if (!string.IsNullOrEmpty(p.parent))
                {
                    var (parentObj, parentFindError) = FindGameObjectByPath(p.parent);
                    if (parentObj == null)
                    {
                        string error = parentFindError ?? $"Parent GameObject not found: {p.parent}";
                        Debug.LogWarning($"[CommandExecutor] {error}");
                        return (false, error);
                    }
                    newParent = parentObj.transform;
                    parentName = parentObj.name;
                }

                Undo.SetTransformParent(child.transform, newParent, p.world_position_stays, "Set Parent");

                string result = $"Set {child.name}'s parent to: {parentName} (world_position_stays: {p.world_position_stays})";
                Debug.Log($"[CommandExecutor] {result}");
                return (true, result);
            }
            catch (Exception e)
            {
                string error = $"Error setting parent: {e.Message}";
                Debug.LogError($"[CommandExecutor] {error}");
                return (false, error);
            }
        }

        /// <summary>
        /// Duplicates a GameObject
        /// </summary>
        private (bool, string) DuplicateGameObject(CommandParams p)
        {
            try
            {
                if (p == null || string.IsNullOrEmpty(p.path))
                {
                    string error = "Missing required parameter: path";
                    Debug.LogError($"[CommandExecutor] {error}");
                    return (false, error);
                }

                var (original, findError) = FindGameObjectByPath(p.path);
                if (original == null)
                {
                    string error = findError ?? $"GameObject not found: {p.path}";
                    Debug.LogWarning($"[CommandExecutor] {error}");
                    return (false, error);
                }

                // Check for duplicates when placing at root level (before creation)
                if (original.transform.parent == null)
                {
                    string desiredName = !string.IsNullOrEmpty(p.new_name) ? p.new_name : original.name + "(Clone)";
                    string duplicateError = CheckDuplicateRootName(desiredName);
                    if (duplicateError != null)
                    {
                        Debug.LogError($"[CommandExecutor] {duplicateError}");
                        return (false, duplicateError);
                    }
                }

                GameObject duplicate = GameObject.Instantiate(original);

                // Set name (use specified if provided, otherwise use default "(Clone)")
                if (!string.IsNullOrEmpty(p.new_name))
                {
                    duplicate.name = p.new_name;
                }
                // Instantiate adds "(Clone)" by default, keep it

                // Maintain parent
                duplicate.transform.SetParent(original.transform.parent);

                Undo.RegisterCreatedObjectUndo(duplicate, "Duplicate GameObject");

                string result = $"Duplicated '{original.name}' as '{duplicate.name}'";
                Debug.Log($"[CommandExecutor] {result}");
                return (true, result);
            }
            catch (Exception e)
            {
                string error = $"Error duplicating GameObject: {e.Message}";
                Debug.LogError($"[CommandExecutor] {error}");
                return (false, error);
            }
        }

        /// <summary>
        /// Points a GameObject at a target
        /// </summary>
        private (bool, string) LookAt(CommandParams p)
        {
            try
            {
                if (p == null || string.IsNullOrEmpty(p.path))
                {
                    string error = "Missing required parameter: path";
                    Debug.LogError($"[CommandExecutor] {error}");
                    return (false, error);
                }

                if (string.IsNullOrEmpty(p.target_path))
                {
                    string error = "Missing required parameter: target_path";
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

                var (target, targetFindError) = FindGameObjectByPath(p.target_path);
                if (target == null)
                {
                    string error = targetFindError ?? $"Target GameObject not found: {p.target_path}";
                    Debug.LogWarning($"[CommandExecutor] {error}");
                    return (false, error);
                }

                Undo.RecordObject(obj.transform, "Look At");
                obj.transform.LookAt(target.transform);

                string result = $"Set {obj.name} to look at {target.name}";
                Debug.Log($"[CommandExecutor] {result}");
                return (true, result);
            }
            catch (Exception e)
            {
                string error = $"Error with LookAt: {e.Message}";
                Debug.LogError($"[CommandExecutor] {error}");
                return (false, error);
            }
        }

        /// <summary>
        /// Creates a line between two points
        /// </summary>
        private (bool, string) CreateLine(CommandParams p)
        {
            try
            {
                // Validate and resolve start parameter
                var (startSuccess, startPos, startError) = ResolvePosition(p.start, "start");
                if (!startSuccess)
                {
                    Debug.LogError($"[CommandExecutor] {startError}");
                    return (false, startError);
                }

                // Validate and resolve end parameter
                var (endSuccess, endPos, endError) = ResolvePosition(p.end, "end");
                if (!endSuccess)
                {
                    Debug.LogError($"[CommandExecutor] {endError}");
                    return (false, endError);
                }

                // Smart default: with parent → local, without parent → world
                string defaultSpace = string.IsNullOrEmpty(p.parent) ? "world" : "local";

                // Validate position_space parameter (before object creation)
                // Note: start and end use the same space
                string posSpace = p.position_space;
                float[] dummyCoords = new float[] { 0, 0, 0 }; // For ValidateSpaceParameter null check
                var (posValid, posError) = ValidateSpaceParameter("position", dummyCoords, ref posSpace, defaultSpace);
                if (!posValid)
                {
                    Debug.LogError($"[CommandExecutor] {posError}");
                    return (false, posError);
                }

                // Check for duplicate names (root level only)
                string name = !string.IsNullOrEmpty(p.name) ? p.name : "Line";
                if (string.IsNullOrEmpty(p.parent))
                {
                    string duplicateError = CheckDuplicateRootName(name);
                    if (duplicateError != null)
                    {
                        Debug.LogError($"[CommandExecutor] {duplicateError}");
                        return (false, duplicateError);
                    }
                }

                // Create line using LineRenderer
                GameObject lineObj = new GameObject(name);
                LineRenderer lr = lineObj.AddComponent<LineRenderer>();

                // Set parent (before coordinate settings)
                Transform parentTransform = null;
                if (!string.IsNullOrEmpty(p.parent))
                {
                    var (parentObj, parentFindError) = FindGameObjectByPath(p.parent);
                    if (parentObj != null)
                    {
                        parentTransform = parentObj.transform;
                        lineObj.transform.SetParent(parentTransform, false);
                    }
                    else
                    {
                        GameObject.DestroyImmediate(lineObj);
                        string error = parentFindError ?? $"Parent object not found: {p.parent}";
                        Debug.LogError($"[CommandExecutor] {error}");
                        return (false, error);
                    }
                }

                // Set coordinates (based on space parameter)
                lr.positionCount = 2;
                if (posSpace == "local" && parentTransform != null)
                {
                    // Local coordinate mode: useWorldSpace=false, use local coordinates directly
                    lr.useWorldSpace = false;
                    lr.SetPosition(0, startPos);
                    lr.SetPosition(1, endPos);
                }
                else
                {
                    // World coordinate mode (or local without parent)
                    lr.useWorldSpace = true;
                    lr.SetPosition(0, startPos);
                    lr.SetPosition(1, endPos);
                }

                // Width (default to 0.01 if <= 0)
                float width = p.width > 0 ? p.width : 0.01f;
                lr.startWidth = width;
                lr.endWidth = width;

                // Color
                Color color = Color.white;
                if (!string.IsNullOrEmpty(p.color))
                {
                    var (colorSuccess, parsed, colorError) = TryParseColor(p.color);
                    if (!colorSuccess)
                    {
                        GameObject.DestroyImmediate(lineObj);
                        Debug.LogError($"[CommandExecutor] {colorError}");
                        return (false, colorError);
                    }
                    color = parsed;
                }

                // Select shader based on whether URP is being used
                var currentRP = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
                string shaderName;
                if (currentRP != null && currentRP.GetType().Name.Contains("Universal"))
                {
                    shaderName = "Universal Render Pipeline/Unlit";
                }
                else
                {
                    shaderName = "Unlit/Color";
                }

                var shader = Shader.Find(shaderName);
                if (shader == null)
                {
                    GameObject.DestroyImmediate(lineObj);
                    string error = $"Shader not found: {shaderName}";
                    Debug.LogError($"[CommandExecutor] {error}");
                    return (false, error);
                }

                var material = new Material(shader);
                material.color = color;
                lr.material = material;

                Undo.RegisterCreatedObjectUndo(lineObj, "Create Line");

                string result = $"Created line: {name} (position: {posSpace})";
                Debug.Log($"[CommandExecutor] {result}");
                return (true, result);
            }
            catch (Exception e)
            {
                string error = $"Error creating line: {e.Message}";
                Debug.LogError($"[CommandExecutor] {error}");
                return (false, error);
            }
        }

        /// <summary>
        /// Resolves position parameter (coordinate array or GameObject path)
        /// </summary>
        private (bool success, Vector3 position, string error) ResolvePosition(object param, string paramName)
        {
            if (param == null)
            {
                return (false, Vector3.zero, $"Missing required parameter: {paramName}");
            }

            // If coordinate array (Newtonsoft.Json.Linq.JArray or IList)
            if (param is IList list && list.Count >= 3)
            {
                try
                {
                    float x = Convert.ToSingle(list[0]);
                    float y = Convert.ToSingle(list[1]);
                    float z = Convert.ToSingle(list[2]);
                    return (true, new Vector3(x, y, z), null);
                }
                catch (Exception e)
                {
                    return (false, Vector3.zero, $"Invalid {paramName} coordinates: {e.Message}");
                }
            }

            // If GameObject path
            if (param is string path)
            {
                var obj = GameObject.Find(path);
                if (obj == null)
                {
                    return (false, Vector3.zero, $"GameObject not found: {path}");
                }
                return (true, obj.transform.position, null);
            }

            return (false, Vector3.zero, $"Invalid {paramName}: expected [x,y,z] array or GameObject path string");
        }
    }
}
