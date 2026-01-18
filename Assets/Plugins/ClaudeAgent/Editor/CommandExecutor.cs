using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System;
using System.Text;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace ClaudeAgent
{
    /// <summary>
    /// Class that executes JSON commands immediately (no compilation required)
    /// </summary>
    public partial class CommandExecutor
    {
        // Dictionary for command handler registration
        private Dictionary<string, Func<CommandParams, (bool, string)>> _commands;

        /// <summary>
        /// Registers a command
        /// </summary>
        private void RegisterCommand(string operation, Func<CommandParams, (bool, string)> handler)
        {
            _commands[operation] = handler;
        }

        /// <summary>
        /// Initializes all commands
        /// </summary>
        private void InitializeCommands()
        {
            _commands = new Dictionary<string, Func<CommandParams, (bool, string)>>();

            // Call registration methods from each partial class
            RegisterGameObjectCommands();
            RegisterTransformCommands();
            RegisterDebuggingCommands();
            RegisterComponentCommands();
            RegisterMaterialCommands();
            RegisterSceneCommands();
            RegisterAssetCommands();
            RegisterPrefabCommands();
            RegisterLightCommands();
            RegisterCameraCommands();
            RegisterUICommands();
            RegisterEditorCommands();
            RegisterAnimatorCommands();
            RegisterTerrainCommands();
            RegisterProBuilderCommands();
            RegisterScreenshotCommands();
            RegisterRuntimeCommands();
            RegisterLayerCommands();
        }

        /// <summary>
        /// Executes a JSON command
        /// </summary>
        /// <param name="jsonCommand">JSON command</param>
        /// <returns>(success flag, result JSON)</returns>
        public (bool success, string resultJson) ExecuteCommand(string jsonCommand)
        {
            try
            {
                // Special handling for batch command (needs to parse JSON array directly)
                if (jsonCommand.Contains("\"operation\":\"batch\"") || jsonCommand.Contains("\"operation\": \"batch\""))
                {
                    return ExecuteBatchFromJson(jsonCommand);
                }

                // Validate parameter format
                var validationError = ValidateJsonFormat(jsonCommand);
                if (validationError != null)
                {
                    ConsoleLogError($"[CommandExecutor] {validationError}");
                    return (false, validationError);
                }

                // Detect unknown parameters
                var unknownParamWarning = GetUnknownParameterWarning(jsonCommand);
                if (unknownParamWarning != null)
                {
                    ConsoleLogWarning($"[CommandExecutor] {unknownParamWarning}");
                }

                var command = JsonUtility.FromJson<Command>(jsonCommand);

                if (command == null || string.IsNullOrEmpty(command.operation))
                {
                    string error = "Invalid command format";
                    ConsoleLogError($"[CommandExecutor] {error}");
                    return (false, error);
                }

                // JsonUtility doesn't support object type, manually parse special fields
                ParseSpecialFields(jsonCommand, command.@params);

                ConsoleLog($"[CommandExecutor] Executing command: {command.operation}");

                // Check feature permission
                string permissionError = CheckOperationPermission(command.operation);
                if (permissionError != null)
                {
                    ConsoleLogWarning($"[CommandExecutor] {permissionError}");
                    return (false, permissionError);
                }

                // Initialize commands on first execution
                if (_commands == null)
                {
                    InitializeCommands();
                }

                // Execute command and get result
                (bool success, string result) commandResult;

                // Find and execute command handler
                if (_commands.TryGetValue(command.operation, out var handler))
                {
                    commandResult = handler(command.@params);
                }
                else
                {
                    string unknownOpError = $"Unknown operation: {command.operation}";
                    ConsoleLogWarning($"[CommandExecutor] {unknownOpError}");
                    return (false, unknownOpError);
                }

                // Add unknown parameter warning to result
                if (unknownParamWarning != null)
                {
                    commandResult = (commandResult.success, commandResult.result + "\n" + unknownParamWarning);
                }

                return commandResult;
            }
            catch (Exception e)
            {
                string error = $"Error executing command: {e.Message}";
                ConsoleLogError($"[CommandExecutor] {error}");
                return (false, error);
            }
        }

        /// <summary>
        /// Validates JSON parameter format
        /// position, scale, rotation must be array format [x, y, z]
        /// </summary>
        /// <param name="jsonCommand">JSON command</param>
        /// <returns>Error message (null if valid)</returns>
        // Valid field names for CommandParams (auto-generated via reflection)
        private static HashSet<string> _validParamNames;
        private static HashSet<string> ValidParamNames
        {
            get
            {
                if (_validParamNames == null)
                {
                    _validParamNames = new HashSet<string>(
                        typeof(CommandParams)
                            .GetFields(BindingFlags.Public | BindingFlags.Instance)
                            .Select(f => f.Name)
                    );
                }
                return _validParamNames;
            }
        }

        #region Error Handling Helpers

        /// <summary>
        /// Returns error (with Debug.LogError)
        /// </summary>
        private (bool, string) Error(string message)
        {
            ConsoleLogError($"[CommandExecutor] {message}");
            return (false, message);
        }

        /// <summary>
        /// Returns warning (with Debug.LogWarning)
        /// </summary>
        private (bool, string) Warning(string message)
        {
            ConsoleLogWarning($"[CommandExecutor] {message}");
            return (false, message);
        }

        /// <summary>
        /// Returns success
        /// </summary>
        private (bool, string) Success(string message)
        {
            return (true, message);
        }

        /// <summary>
        /// Gets GameObject from path parameter (required)
        /// </summary>
        /// <returns>(success flag, GameObject, error message)</returns>
        private (bool success, GameObject obj, string error) GetRequiredGameObject(CommandParams p)
        {
            if (p == null || string.IsNullOrEmpty(p.path))
                return (false, null, "Missing required parameter: path");

            var (obj, findError) = FindGameObjectByPath(p.path);
            if (obj == null)
                return (false, null, findError ?? $"GameObject not found: {p.path}");

            return (true, obj, null);
        }

        /// <summary>
        /// Gets Component on GameObject from path parameter (required)
        /// </summary>
        /// <returns>(success flag, Component, GameObject, error message)</returns>
        private (bool success, T component, GameObject obj, string error) GetRequiredComponent<T>(
            CommandParams p, string componentName = null) where T : Component
        {
            var (success, obj, error) = GetRequiredGameObject(p);
            if (!success)
                return (false, null, null, error);

            var component = obj.GetComponent<T>();
            if (component == null)
            {
                string name = componentName ?? typeof(T).Name;
                return (false, null, obj, $"{name} component not found on: {p.path}");
            }

            return (true, component, obj, null);
        }

        #endregion

        /// <summary>
        /// Detects unknown parameters and returns warning string
        /// </summary>
        private string GetUnknownParameterWarning(string jsonCommand)
        {
            try
            {
                var json = JObject.Parse(jsonCommand);
                var paramsObj = json["params"] as JObject;

                if (paramsObj == null)
                {
                    return null;
                }

                var unknownParams = new List<string>();
                foreach (var prop in paramsObj.Properties())
                {
                    if (!ValidParamNames.Contains(prop.Name))
                    {
                        unknownParams.Add(prop.Name);
                    }
                }

                if (unknownParams.Count > 0)
                {
                    return $"[WARNING] Unknown parameters ignored: {string.Join(", ", unknownParams)}";
                }

                return null;
            }
            catch
            {
                return null; // Parse errors handled elsewhere
            }
        }

        private string ValidateJsonFormat(string jsonCommand)
        {
            try
            {
                var json = JObject.Parse(jsonCommand);
                var paramsObj = json["params"] as JObject;

                if (paramsObj == null)
                {
                    return null; // OK if no params (handled by other validation)
                }

                // Check fields that should be arrays
                string[] arrayFields = { "position", "scale", "rotation" };

                foreach (var fieldName in arrayFields)
                {
                    var field = paramsObj[fieldName];
                    if (field != null)
                    {
                        if (field.Type != JTokenType.Array)
                        {
                            return $"Invalid format for '{fieldName}': expected array [x, y, z], got {field.Type}. Example: \"position\": [1.0, 2.0, 3.0]";
                        }

                        var array = field as JArray;
                        if (array.Count < 2 || array.Count > 3)
                        {
                            return $"Invalid format for '{fieldName}': expected 2 or 3 elements [x, y] or [x, y, z], got {array.Count}";
                        }

                        // Check if each element is a number
                        for (int i = 0; i < array.Count; i++)
                        {
                            if (array[i].Type != JTokenType.Integer && array[i].Type != JTokenType.Float)
                            {
                                return $"Invalid format for '{fieldName}[{i}]': expected number, got {array[i].Type}";
                            }
                        }
                    }
                }

                return null; // Validation OK
            }
            catch (Exception e)
            {
                return $"JSON parse error: {e.Message}";
            }
        }

        /// <summary>
        /// Manually parse special fields not supported by JsonUtility
        /// (object type: default_value, start, end, object[] type: children, conditions)
        /// </summary>
        private void ParseSpecialFields(string jsonCommand, CommandParams p)
        {
            if (p == null) return;

            try
            {
                var json = JObject.Parse(jsonCommand);
                var paramsObj = json["params"] as JObject;
                if (paramsObj == null) return;

                // Parse default_value (object type)
                var defaultValueToken = paramsObj["default_value"];
                if (defaultValueToken != null)
                {
                    switch (defaultValueToken.Type)
                    {
                        case JTokenType.Float:
                        case JTokenType.Integer:
                            p.default_value = defaultValueToken.ToObject<double>();
                            break;
                        case JTokenType.Boolean:
                            p.default_value = defaultValueToken.ToObject<bool>();
                            break;
                        case JTokenType.String:
                            p.default_value = defaultValueToken.ToObject<string>();
                            break;
                    }
                    ConsoleLog($"[CommandExecutor] Parsed default_value: {p.default_value} (type: {p.default_value?.GetType().Name})");
                }

                // Parse children (object[] type)
                var childrenToken = paramsObj["children"] as JArray;
                if (childrenToken != null && childrenToken.Count > 0)
                {
                    p.children = new object[childrenToken.Count];
                    for (int i = 0; i < childrenToken.Count; i++)
                    {
                        p.children[i] = childrenToken[i]; // Keep as JObject
                    }
                    ConsoleLog($"[CommandExecutor] Parsed children: {p.children.Length} items");
                }

                // Parse conditions (object[] type)
                var conditionsToken = paramsObj["conditions"] as JArray;
                if (conditionsToken != null && conditionsToken.Count > 0)
                {
                    p.conditions = new object[conditionsToken.Count];
                    for (int i = 0; i < conditionsToken.Count; i++)
                    {
                        p.conditions[i] = conditionsToken[i]; // Keep as JObject
                    }
                    ConsoleLog($"[CommandExecutor] Parsed conditions: {p.conditions.Length} items");
                }

                // Parse start (object type: coordinate array or GameObject path)
                var startToken = paramsObj["start"];
                if (startToken != null)
                {
                    if (startToken.Type == JTokenType.Array)
                    {
                        p.start = startToken as JArray;
                    }
                    else if (startToken.Type == JTokenType.String)
                    {
                        p.start = startToken.ToObject<string>();
                    }
                }

                // Parse end (object type: coordinate array or GameObject path)
                var endToken = paramsObj["end"];
                if (endToken != null)
                {
                    if (endToken.Type == JTokenType.Array)
                    {
                        p.end = endToken as JArray;
                    }
                    else if (endToken.Type == JTokenType.String)
                    {
                        p.end = endToken.ToObject<string>();
                    }
                }

                // Parse vertices (object type: [[x,y,z], [x,y,z], ...])
                var verticesToken = paramsObj["vertices"];
                if (verticesToken != null && verticesToken.Type == JTokenType.Array)
                {
                    p.vertices = verticesToken as JArray;
                    ConsoleLog($"[CommandExecutor] Parsed vertices: {((JArray)p.vertices).Count} points");
                }

                // Parse value (can be array, object, or primitive - convert to string for parsing)
                var valueToken = paramsObj["value"];
                if (valueToken != null)
                {
                    switch (valueToken.Type)
                    {
                        case JTokenType.Array:
                        case JTokenType.Object:
                            // Convert array/object to JSON string for ParsePropertyValue
                            p.value = valueToken.ToString(Newtonsoft.Json.Formatting.None);
                            break;
                        case JTokenType.String:
                            p.value = valueToken.ToObject<string>();
                            break;
                        case JTokenType.Integer:
                        case JTokenType.Float:
                        case JTokenType.Boolean:
                            p.value = valueToken.ToString();
                            break;
                    }
                }
            }
            catch (Exception e)
            {
                ConsoleLogWarning($"[CommandExecutor] Error parsing special fields: {e.Message}");
            }
        }

        /// <summary>
        /// Converts string to PrimitiveType
        /// </summary>
        /// <returns>(success flag, PrimitiveType, error message)</returns>
        private (bool success, PrimitiveType type, string error) TryParsePrimitiveType(string type)
        {
            if (string.IsNullOrEmpty(type))
            {
                return (false, PrimitiveType.Sphere, "Missing required parameter: type");
            }

            switch (type.ToLower())
            {
                case "sphere":
                    return (true, PrimitiveType.Sphere, null);
                case "cube":
                    return (true, PrimitiveType.Cube, null);
                case "cylinder":
                    return (true, PrimitiveType.Cylinder, null);
                case "capsule":
                    return (true, PrimitiveType.Capsule, null);
                case "plane":
                    return (true, PrimitiveType.Plane, null);
                case "quad":
                    return (true, PrimitiveType.Quad, null);
                default:
                    return (false, PrimitiveType.Sphere, $"Unknown primitive type: '{type}'. Valid types: sphere, cube, cylinder, capsule, plane, quad");
            }
        }

        /// <summary>
        /// Validates Component on GameObject
        /// </summary>
        private (bool success, T component, string error) ValidateComponent<T>(GameObject obj, string componentName) where T : Component
        {
            if (obj == null)
            {
                return (false, null, "GameObject is null");
            }

            T component = obj.GetComponent<T>();
            if (component == null)
            {
                return (false, null, $"{componentName} component not found on: {obj.name}");
            }

            return (true, component, null);
        }

        /// <summary>
        /// Recursively creates directories for asset path
        /// </summary>
        private void EnsureDirectoryExists(string assetPath)
        {
            string directory = System.IO.Path.GetDirectoryName(assetPath);
            if (string.IsNullOrEmpty(directory))
                return;

            directory = directory.Replace('\\', '/');
            string[] folders = directory.Split('/');
            string currentPath = "";

            for (int i = 0; i < folders.Length; i++)
            {
                if (i == 0)
                {
                    currentPath = folders[i];
                }
                else
                {
                    string parentPath = currentPath;
                    currentPath = currentPath + "/" + folders[i];
                    string fullPath = System.IO.Path.Combine(Application.dataPath, "..", currentPath);

                    if (!System.IO.Directory.Exists(fullPath))
                    {
                        AssetDatabase.CreateFolder(parentPath, folders[i]);
                    }
                }
            }
        }

        /// <summary>
        /// Converts color string (#RRGGBB or color name) to Color
        /// </summary>
        /// <returns>(success flag, Color, error message)</returns>
        private (bool success, Color color, string error) TryParseColor(string colorString)
        {
            if (string.IsNullOrEmpty(colorString))
            {
                return (false, Color.white, "Missing required parameter: color");
            }

            // #RRGGBB format
            if (colorString.StartsWith("#"))
            {
                if (ColorUtility.TryParseHtmlString(colorString, out Color color))
                {
                    return (true, color, null);
                }
                return (false, Color.white, $"Invalid color format: '{colorString}'. Expected format: #RRGGBB or #RRGGBBAA");
            }

            // Color name
            switch (colorString.ToLower())
            {
                case "red":
                    return (true, Color.red, null);
                case "green":
                    return (true, Color.green, null);
                case "blue":
                    return (true, Color.blue, null);
                case "yellow":
                    return (true, Color.yellow, null);
                case "cyan":
                    return (true, Color.cyan, null);
                case "magenta":
                    return (true, Color.magenta, null);
                case "white":
                    return (true, Color.white, null);
                case "black":
                    return (true, Color.black, null);
                case "gray":
                case "grey":
                    return (true, Color.gray, null);
                // Custom colors (not in Unity's Color class)
                case "orange":
                    return (true, new Color(1f, 0.647f, 0f), null);  // #FFA500
                case "brown":
                    return (true, new Color(0.545f, 0.271f, 0.075f), null);  // #8B4513 (Saddle Brown)
                default:
                    return (false, Color.white, $"Unknown color: '{colorString}'. Valid colors: red, green, blue, yellow, cyan, magenta, white, black, gray, orange, brown, or #RRGGBB format");
            }
        }

        /// <summary>
        /// Validates Transform space parameter and applies default values
        /// If value (position/rotation/scale) is specified, applies default value to space parameter
        /// </summary>
        /// <param name="paramName">Parameter name ("position", "rotation", "scale")</param>
        /// <param name="value">Value array (nullable)</param>
        /// <param name="space">Space parameter value (modifiable via ref)</param>
        /// <param name="defaultSpace">Default space value ("world" or "local")</param>
        /// <returns>(success flag, error message) - error is null on success</returns>
        private (bool success, string error) ValidateSpaceParameter(string paramName, float[] value, ref string space, string defaultSpace)
        {
            // No validation needed if value not specified
            if (value == null || value.Length == 0)
            {
                return (true, null);
            }

            // If value is specified and space is not, apply default value
            if (string.IsNullOrEmpty(space))
            {
                space = defaultSpace;
            }

            // Only "local" or "world" allowed for space
            string spaceLower = space.ToLower();
            if (spaceLower != "local" && spaceLower != "world")
            {
                return (false, $"Invalid value for '{paramName}_space': '{space}'. Must be 'local' or 'world'.");
            }

            // Normalize (to lowercase)
            space = spaceLower;

            return (true, null);
        }

        /// <summary>
        /// Common helper method to find GameObject by path/name
        /// Returns error if multiple objects exist at the same path
        /// Supports Prefab Mode - searches in prefab stage when open
        /// </summary>
        /// <returns>(found GameObject, error message) - obj is null and error is set on error</returns>
        private (GameObject obj, string error) FindGameObjectByPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return (null, null);
            }

            // Check if in Prefab Mode
            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage != null)
            {
                // Search within Prefab Stage
                var prefabResult = FindGameObjectInPrefabStage(prefabStage, path);
                if (prefabResult.obj != null || prefabResult.error != null)
                {
                    return prefabResult;
                }
                // If not found in prefab, return not found (don't fall through to scene search)
                return (null, $"GameObject not found in prefab: {path}");
            }

            // Search all objects in scene (excluding prefab assets etc.)
            var allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
            var matches = new List<GameObject>();

            bool isPathFormat = path.Contains("/");

            foreach (var go in allObjects)
            {
                // Scene objects only
                if (!go.scene.isLoaded) continue;

                if (isPathFormat)
                {
                    // For path format, exact match with full path
                    if (GetGameObjectFullPath(go) == path)
                    {
                        matches.Add(go);
                    }
                }
                else
                {
                    // For name only, match by name
                    if (go.name == path)
                    {
                        matches.Add(go);
                    }
                }
            }

            if (matches.Count == 0)
            {
                return (null, null);
            }
            else if (matches.Count == 1)
            {
                return (matches[0], null);
            }
            else
            {
                // Multiple objects exist - return error
                var pathList = new List<string>();
                string examplePath = null;
                foreach (var go in matches)
                {
                    string fullPath = GetGameObjectFullPath(go);
                    bool isRoot = go.transform.parent == null;
                    pathList.Add(isRoot ? $"{fullPath} (root)" : fullPath);
                    // Use non-root path as example
                    if (!isRoot && examplePath == null)
                    {
                        examplePath = fullPath;
                    }
                }
                string suggestion = examplePath != null
                    ? $"\nSuggestion: Use full path like '{examplePath}' to specify the target."
                    : "\nSuggestion: Use full path to specify the target.";
                string error = $"Multiple GameObjects match '{path}' ({matches.Count}). Cannot determine target. Found:\n  " +
                               string.Join("\n  ", pathList) + suggestion;
                return (null, error);
            }
        }

        /// <summary>
        /// Finds GameObject within Prefab Stage
        /// </summary>
        private (GameObject obj, string error) FindGameObjectInPrefabStage(PrefabStage prefabStage, string path)
        {
            var root = prefabStage.prefabContentsRoot;
            bool isPathFormat = path.Contains("/");

            // Case 1: Root object name matches exactly
            if (path == root.name)
            {
                return (root, null);
            }

            // Case 2: Path starts with root name (e.g., "RootName/Child/GrandChild")
            if (isPathFormat && path.StartsWith(root.name + "/"))
            {
                string relativePath = path.Substring(root.name.Length + 1);
                var child = root.transform.Find(relativePath);
                if (child != null)
                {
                    return (child.gameObject, null);
                }
            }

            // Case 3: Direct child search (path without root name, e.g., "Child/GrandChild" or "Child")
            var directResult = root.transform.Find(path);
            if (directResult != null)
            {
                return (directResult.gameObject, null);
            }

            // Case 4: Search by name only (not path format) - search all descendants
            if (!isPathFormat)
            {
                var matches = new List<GameObject>();
                FindGameObjectsByNameInHierarchy(root.transform, path, matches);

                if (matches.Count == 1)
                {
                    return (matches[0], null);
                }
                else if (matches.Count > 1)
                {
                    var pathList = new List<string>();
                    foreach (var go in matches)
                    {
                        pathList.Add(GetPrefabRelativePath(root, go));
                    }
                    string error = $"Multiple GameObjects match '{path}' in prefab ({matches.Count}). Use full path to specify the target. Found:\n  " +
                                   string.Join("\n  ", pathList);
                    return (null, error);
                }
            }

            return (null, null);
        }

        /// <summary>
        /// Recursively finds GameObjects by name in hierarchy
        /// </summary>
        private void FindGameObjectsByNameInHierarchy(Transform parent, string name, List<GameObject> results)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name)
                {
                    results.Add(child.gameObject);
                }
                FindGameObjectsByNameInHierarchy(child, name, results);
            }
        }

        /// <summary>
        /// Gets the path of a GameObject relative to prefab root
        /// </summary>
        private string GetPrefabRelativePath(GameObject root, GameObject obj)
        {
            if (obj == root)
            {
                return root.name;
            }

            string path = obj.name;
            Transform current = obj.transform.parent;

            while (current != null)
            {
                path = current.name + "/" + path;
                if (current.gameObject == root)
                {
                    break;
                }
                current = current.parent;
            }

            return path;
        }

        /// <summary>
        /// Gets the full path of a GameObject
        /// </summary>
        private string GetGameObjectFullPath(GameObject go)
        {
            string path = go.name;
            Transform parent = go.transform.parent;
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
            return path;
        }

        /// <summary>
        /// Checks if an object with the same name exists at root level
        /// </summary>
        /// <param name="name">Name to check</param>
        /// <returns>Error message if duplicate exists, null otherwise</returns>
        private string CheckDuplicateRootName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }

            // Check if root object with same name exists
            GameObject existing = GameObject.Find(name);
            if (existing != null && existing.transform.parent == null)
            {
                return $"GameObject with name '{name}' already exists at root level. Delete it first or use a different name.";
            }

            return null;
        }

        /// <summary>
        /// Gets the hierarchy path of a GameObject
        /// </summary>
        private string GetGameObjectPath(GameObject obj)
        {
            if (obj == null)
            {
                return "";
            }

            string path = obj.name;
            Transform current = obj.transform.parent;

            while (current != null)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }

            return path;
        }

        /// <summary>
        /// Recursively appends hierarchy information
        /// </summary>
        private void AppendHierarchy(StringBuilder sb, GameObject obj, int depth, int maxDepth)
        {
            if (maxDepth >= 0 && depth > maxDepth)
                return;

            // Indentation
            string indent = new string(' ', depth * 2);

            // Object information
            sb.AppendLine($"{indent}- {obj.name} (Active: {obj.activeSelf}, Components: {obj.GetComponents<Component>().Length})");

            // Recursively process child objects
            foreach (Transform child in obj.transform)
            {
                AppendHierarchy(sb, child.gameObject, depth + 1, maxDepth);
            }
        }

        /// <summary>
        /// JSON structure for command
        /// </summary>
        [Serializable]
        private class Command
        {
            public string operation;
            public CommandParams @params;
        }

        /// <summary>
        /// Command parameters
        /// </summary>
        [Serializable]
        private class CommandParams
        {
            public string type;        // sphere, cube, cylinder, capsule, plane, quad
            public string name;        // Object name
            public string color;       // #RRGGBB or color name
            public float[] position;   // [x, y, z]
            public float[] scale;      // [x, y, z]
            public float[] rotation;   // [x, y, z] (Euler angles)
            public string position_space;  // "local" | "world" - required when position is specified
            public string rotation_space;  // "local" | "world" - required when rotation is specified
            public string scale_space;     // "local" | "world" - required when scale is specified
            public int count = 50;     // Number of logs to retrieve (default 50)

            // Parameters for GameObject operations
            public string path;                // Path/name of GameObject
            public string parent;              // Path/name of parent object
            public bool active;                // Active state (true/false)
            public string tag;                 // Search by tag
            public string layer_name;          // Layer name (for set_layer)
            public bool local;                 // Use local coordinates/rotation (default: false=world)
            public string action;              // For playmode: play, stop, pause, resume

            // Phase 6C parameters
            public string new_name;            // For SetName: new name
            public bool world_position_stays;  // For SetParent: maintain world position
            public string target_path;         // For LookAt: path of target to look at
            public string target_component;    // For set_object_reference: specific component type on target

            // Phase 7A parameters (Component operations)
            public string component;           // Component type name (e.g., "Rigidbody", "BoxCollider")
            public string property;            // Property name (e.g., "mass", "isKinematic")
            public string value;               // Property value (JSON string)
            public int index = -1;             // For list operations: index position (-1 = not specified)

            // Phase 7B parameters (Material/Renderer operations)
            public string material_path;       // Material path (e.g., "Assets/Materials/MyMaterial.mat")
            public string material_name;       // Material name to create
            public string shader_name;         // Shader name (e.g., "Standard", "Unlit/Color")
            public string shader;              // Target shader name (for material operation extension)
            public string from_shader;         // Source shader filter (for batch processing)
            public float metallic = -1f;       // Metallic value (0-1, -1=not set)
            public float smoothness = -1f;     // Smoothness value (0-1, -1=not set)

            // Phase 7C parameters (Scene operations)
            public string scene_path;          // Scene path (e.g., "Assets/Scenes/MyScene.unity")
            public string scene_name;          // Scene name
            public bool save_current;          // Save current scene (default: true)
            public int max_depth;              // Max depth for hierarchy retrieval (default: -1=unlimited)

            // Phase 7D parameters (Asset/Prefab operations)
            public string asset_path;          // Asset path (e.g., "Assets/Textures/MyTexture.png")
            public string asset_content;       // Asset content (for create_asset, text assets etc.)
            public string from_path;           // Source path (for copy_asset)
            public string to_path;             // Destination path (for copy_asset)
            public string prefab_path;         // Prefab path (e.g., "Assets/Prefabs/MyPrefab.prefab")
            public string gameobject_path;     // GameObject path (for create_prefab)
            public string package_path;        // .unitypackage path (for import_package)
            public string folder_path;         // Folder path (for list_assets)
            public string filter;              // Search filter (for list_assets, e.g., "t:Prefab")
            public bool recursive;             // Search recursively (for list_assets)

            // Parameters for Light/Camera operations
            public float intensity;            // Light intensity
            public float r = -1f;              // RGB color (0-1), -1=not set
            public float g = -1f;
            public float b = -1f;
            public float fov;                  // Camera field of view
            public float near;                 // Camera near clip plane
            public float far;                  // Camera far clip plane

            // Parameters for UI operations
            public string text;                // UI text
            public float a;                    // Alpha value (0-1)
            public float[] size;               // RectTransform size [width, height]
            public string placeholder;         // InputField placeholder text
            public string scroll_direction;    // vertical, horizontal, both
            public int font_size;              // Font size
            public string anchor;              // Anchor preset (center, top, bottom, left, right, stretch, etc.)

            // Parameters for Editor operations
            public string menu_path;           // Menu path (e.g., "GameObject/Create Empty")

            // Parameters for Animator operations
            public string param_name;          // Animator parameter name
            public bool param_bool;            // Bool parameter value
            public float param_float;          // Float parameter value
            public int param_int;              // Integer parameter value

            // Parameters for Terrain operations
            public float terrain_width;        // Terrain width (X axis)
            public float terrain_length;       // Terrain length (Z axis)
            public float terrain_height;       // Terrain max height (Y axis)
            public int heightmap_resolution;   // Heightmap resolution (2^n + 1)
            public float[] heights;            // Height array (1D, resolution * resolution)
            public float flatten_height = -1f; // Height to flatten (-1=not specified)
            public string texture_path;        // Texture path
            public string normal_path;         // Normal map path
            public float tile_size;            // Tile size
            public float[] center;             // Center coordinates [x, z] (world coordinates)
            public float radius;               // Radius (world units)
            public float height_delta;         // Height change amount (positive:raise, negative:lower)
            public string falloff;             // Falloff type (smooth, linear, flat)
            public int layer_index = -1;       // Texture layer index
            public float strength;             // Paint strength (0-1)

            // Parameters for UIToolkit operations
            public string scale_mode;              // PanelSettings scale mode
            public float[] reference_resolution;   // [width, height] for scaling
            public int sort_order;                 // Rendering sort order
            public string theme_path;              // ThemeStyleSheet path
            public string panel_settings_path;     // PanelSettings asset path
            public string uxml_path;               // UXML file path
            public string stylesheet_path;         // USS file path (for UXML reference)
            public string elements;                // JSON string of UXML elements
            public string rules;                   // JSON string of USS rules

            // Parameters for unified API (get/set integrated)
            public bool get;                       // true for get mode
            public string space;                   // "local"(default) or "world" for transform
            public string parameter;               // animator parameter name
            public float param_value;              // animator parameter value (float/int/bool as float)
            public bool fill;                      // terrain_texture fill mode

            // Parameters for AnimatorController editing
            public string controller_path;         // AnimatorController path
            public string from_state;              // Source state for transition
            public string to_state;                // Destination state for transition
            public string param_type;              // Parameter type (Bool/Float/Int/Trigger)
            public string blend_type;              // BlendTree type
            public string blending_mode;           // Layer blending mode
            public string avatar_mask;             // AvatarMask path
            public string motion;                  // AnimationClip path
            public int layer = -1;                 // Layer index (-1=not set, default 0)
            public float weight = -1f;             // Layer weight (-1=not set)
            public float duration = -1f;           // Transition duration (-1=not set)
            public float exit_time = -1f;          // Exit time (-1=not set)
            public float threshold;                // Condition threshold
            public float speed = -1f;              // State speed (-1=not set)
            public bool has_exit_time;             // Exit time based transition
            public bool is_default;                // Default state
            public object[] conditions;            // Transition conditions array
            public object[] children;              // BlendTree children array
            public object default_value;           // Parameter default value (bool/float/int)
            public string parameter_y;             // Y-axis parameter for 2D blend tree

            // Parameters for ProBuilder operations
            public string shape;                   // ProBuilder shape type
            public float width = -1f;              // Width (-1=not set)
            public float height = -1f;             // Height (-1=not set)
            public float depth = -1f;              // Depth (-1=not set)
            public int steps = -1;                 // Number of stair steps (-1=not set)
            public bool build_sides = true;        // Build stair sides
            public float inner_radius = -1f;       // Inner radius for spiral stairs (-1=not set)
            public float circumference = -1f;      // Circumference angle for spiral stairs (degrees, -1=not set)
            public int axis_divisions = -1;        // Circumference divisions (-1=not set)
            public int height_cuts = -1;           // Height divisions (-1=not set)
            public int width_cuts = -1;            // Width divisions (-1=not set)
            public float ledge_height = -1f;       // Door ledge height (-1=not set)
            public float leg_width = -1f;          // Door leg width (-1=not set)
            public float thickness = -1f;          // Thickness (for Arch/Pipe, -1=not set)
            public float arc_degrees = -1f;        // Arc angle (for Arch, -1=not set)
            public float tube_radius = -1f;        // Tube radius (for Torus, -1=not set)
            public int rows = -1;                  // Row divisions (for Torus, -1=not set)
            public int columns = -1;               // Column divisions (for Torus, -1=not set)
            public int sides = -1;                 // Number of sides (alias for axis_divisions, -1=not set)
            public float cross_size = -1f;         // Cross-section size (for create_fitted 2-point cube, square cross-section, -1=not set)
            public object vertices;                // Vertex coordinates array (for create_probuilder_fitted, held as JArray)

            // Parameters for Line operations
            public object start;                   // Start point (float[3] or string GameObject path)
            public object end;                     // End point (float[3] or string GameObject path)
            public string line;                    // LineRenderer name/path (for get_line_transform)

            // Parameters for Screenshot operations
            public string target;                  // Path of object to look at
            public float distance = -1f;           // Distance from target (-1=not set)
            public float[] angle;                  // [pitch, yaw] angle to view target
            public string output_path;             // Screenshot output path

            // Runtime code execution parameters (Roslyn C#)
            public string code;                    // C# source code to compile and execute
            public string method;                  // Method name to invoke (default: "Run")
            public string class_name;              // Class name (optional, for multi-class code)

            // Runtime animation parameters (play_animation)
            // Note: uses existing 'layer' property from Animator params
            public string state;                   // Animation state name to play
            public float normalized_time = -1f;    // Start position (0.0-1.0, -1 = use default 0)
        }
    }
}

