using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System;
using System.Text;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace ClaudeAgent
{
    public partial class CommandExecutor
    {
        /// <summary>
        /// Registers component commands
        /// </summary>
        private void RegisterComponentCommands()
        {
            RegisterCommand("add_component", AddComponent);
            RegisterCommand("remove_component", RemoveComponent);
            RegisterCommand("get_component", GetComponent);
            RegisterCommand("set_component_property", SetComponentProperty);
            RegisterCommand("get_components", GetComponents);
            RegisterCommand("set_object_reference", SetObjectReference);
            RegisterCommand("add_to_list", AddToList);
            RegisterCommand("remove_from_list", RemoveFromList);
        }

        /// <summary>
        /// Adds a component to a GameObject
        /// </summary>
        private (bool, string) AddComponent(CommandParams p)
        {
            try
            {
                if (p == null || string.IsNullOrEmpty(p.path))
                {
                    string error = "Missing required parameter: path";
                    UnityEngine.Debug.LogError($"[CommandExecutor] {error}");
                    return (false, error);
                }

                if (string.IsNullOrEmpty(p.component))
                {
                    string error = "Missing required parameter: component";
                    UnityEngine.Debug.LogError($"[CommandExecutor] {error}");
                    return (false, error);
                }

                var (obj, findError) = FindGameObjectByPath(p.path);
                if (obj == null)
                {
                    string error = findError ?? $"GameObject not found: {p.path}";
                    UnityEngine.Debug.LogWarning($"[CommandExecutor] {error}");
                    return (false, error);
                }

                // Resolve component type
                var (componentType, typeError) = ResolveComponentTypeWithError(p.component);
                if (componentType == null)
                {
                    UnityEngine.Debug.LogWarning($"[CommandExecutor] {typeError}");
                    return (false, typeError);
                }

                // Check if component already exists
                if (obj.GetComponent(componentType) != null)
                {
                    string error = $"{p.component} already exists on {obj.name}";
                    UnityEngine.Debug.LogWarning($"[CommandExecutor] {error}");
                    return (false, error);
                }

                // Add component
                Component newComponent = obj.AddComponent(componentType);
                Undo.RegisterCreatedObjectUndo(newComponent, "Add Component");
                EditorUtility.SetDirty(obj);
                EditorUtility.SetDirty(newComponent);

                string result = $"Added {p.component} to {obj.name}";
                UnityEngine.Debug.Log($"[CommandExecutor] {result}");
                return (true, result);
            }
            catch (Exception e)
            {
                string error = $"Error adding component: {e.Message}";
                UnityEngine.Debug.LogError($"[CommandExecutor] {error}");
                return (false, error);
            }
        }

        /// <summary>
        /// Removes a component from a GameObject
        /// </summary>
        private (bool, string) RemoveComponent(CommandParams p)
        {
            try
            {
                if (p == null || string.IsNullOrEmpty(p.path))
                {
                    string error = "Missing required parameter: path";
                    UnityEngine.Debug.LogError($"[CommandExecutor] {error}");
                    return (false, error);
                }

                if (string.IsNullOrEmpty(p.component))
                {
                    string error = "Missing required parameter: component";
                    UnityEngine.Debug.LogError($"[CommandExecutor] {error}");
                    return (false, error);
                }

                var (obj, findError) = FindGameObjectByPath(p.path);
                if (obj == null)
                {
                    string error = findError ?? $"GameObject not found: {p.path}";
                    UnityEngine.Debug.LogWarning($"[CommandExecutor] {error}");
                    return (false, error);
                }

                // Resolve component type
                var (componentType, typeError) = ResolveComponentTypeWithError(p.component);
                if (componentType == null)
                {
                    UnityEngine.Debug.LogWarning($"[CommandExecutor] {typeError}");
                    return (false, typeError);
                }

                // Get component
                Component component = obj.GetComponent(componentType);
                if (component == null)
                {
                    string error = $"{p.component} not found on {obj.name}";
                    UnityEngine.Debug.LogWarning($"[CommandExecutor] {error}");
                    return (false, error);
                }

                // Delete component
                Undo.DestroyObjectImmediate(component);

                string result = $"Removed {p.component} from {obj.name}";
                UnityEngine.Debug.Log($"[CommandExecutor] {result}");
                return (true, result);
            }
            catch (Exception e)
            {
                string error = $"Error removing component: {e.Message}";
                UnityEngine.Debug.LogError($"[CommandExecutor] {error}");
                return (false, error);
            }
        }

        // Debug log file path
        private static readonly string DefaultDebugLogPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            "ClaudeAgent_Debug.log"
        );

        // Flag to enable/disable file logging
        private static bool _enableFileLogging = false;
        private static string _debugLogPath = null;
        private const string PrefKeyEnableFileLogging = "ClaudeAgent_EnableFileLogging";
        private const string PrefKeyDebugLogPath = "ClaudeAgent_DebugLogPath";

        /// <summary>
        /// Gets or sets whether file logging is enabled
        /// </summary>
        public static bool EnableFileLogging
        {
            get => _enableFileLogging;
            set
            {
                _enableFileLogging = value;
                EditorPrefs.SetBool(PrefKeyEnableFileLogging, value);
                if (value)
                {
                    DebugLogToFile("=== File logging enabled ===");
                }
            }
        }

        /// <summary>
        /// Gets or sets the debug log file path
        /// </summary>
        public static string DebugLogPath
        {
            get
            {
                if (string.IsNullOrEmpty(_debugLogPath))
                {
                    _debugLogPath = EditorPrefs.GetString(PrefKeyDebugLogPath, DefaultDebugLogPath);
                }
                return _debugLogPath;
            }
            set
            {
                _debugLogPath = value;
                EditorPrefs.SetString(PrefKeyDebugLogPath, value);
            }
        }

        /// <summary>
        /// Gets the debug log file path
        /// </summary>
        public static string GetDebugLogPath() => DebugLogPath;

        /// <summary>
        /// Gets the default debug log file path
        /// </summary>
        public static string GetDefaultDebugLogPath() => DefaultDebugLogPath;

        /// <summary>
        /// Initialize file logging setting from EditorPrefs
        /// </summary>
        public static void InitializeFileLogging()
        {
            _enableFileLogging = EditorPrefs.GetBool(PrefKeyEnableFileLogging, false);
            _debugLogPath = EditorPrefs.GetString(PrefKeyDebugLogPath, DefaultDebugLogPath);
        }

        /// <summary>
        /// Opens the debug log file in the default text editor
        /// </summary>
        public static void OpenDebugLogFile()
        {
            if (File.Exists(DebugLogPath))
            {
                EditorUtility.OpenWithDefaultApp(DebugLogPath);
            }
            else
            {
                EditorUtility.DisplayDialog("File Not Found",
                    $"Debug log file does not exist yet.\n\nPath: {DebugLogPath}",
                    "OK");
            }
        }

        /// <summary>
        /// Clears the debug log file
        /// </summary>
        public static void ClearDebugLogFile()
        {
            try
            {
                if (File.Exists(DebugLogPath))
                {
                    File.Delete(DebugLogPath);
                    UnityEngine.Debug.Log("[CommandExecutor] Debug log file cleared");
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"[CommandExecutor] Failed to clear debug log: {e.Message}");
            }
        }

        /// <summary>
        /// Writes debug log to file (survives Unity crash)
        /// </summary>
        private static void DebugLogToFile(string message)
        {
            if (!_enableFileLogging) return;

            try
            {
                string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                string logLine = $"[{timestamp}] {message}\n";
                File.AppendAllText(DebugLogPath, logLine);
            }
            catch { }
        }

        // Properties that can cause Unity to hang when accessed via reflection
        private static readonly HashSet<string> HeavyPropertyBlacklist = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // TerrainCollider - accessing terrainData can serialize huge terrain data
            "terrainData",
            // TerrainCollider - GeometryHolder causes Unity to hang
            "GeometryHolder",
            // MeshCollider - sharedMesh can be large
            "sharedMesh",
            // Renderer materials (can trigger asset loading)
            "materials",
            "sharedMaterials",
            // Mesh related
            "mesh",
            // Animation related (can be heavy)
            "runtimeAnimatorController",
        };

        // Component types that need special handling
        private static readonly HashSet<string> HeavyComponentTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "TerrainCollider",
            "Terrain",
            "MeshCollider",
        };

        /// <summary>
        /// Gets component information
        /// </summary>
        private (bool, string) GetComponent(CommandParams p)
        {
            try
            {
                if (p == null || string.IsNullOrEmpty(p.path))
                {
                    string error = "Missing required parameter: path";
                    UnityEngine.Debug.LogError($"[CommandExecutor] {error}");
                    return (false, error);
                }

                if (string.IsNullOrEmpty(p.component))
                {
                    string error = "Missing required parameter: component";
                    UnityEngine.Debug.LogError($"[CommandExecutor] {error}");
                    return (false, error);
                }

                var (obj, findError) = FindGameObjectByPath(p.path);
                if (obj == null)
                {
                    string error = findError ?? $"GameObject not found: {p.path}";
                    UnityEngine.Debug.LogWarning($"[CommandExecutor] {error}");
                    return (false, error);
                }

                // Resolve component type
                var (componentType, typeError) = ResolveComponentTypeWithError(p.component);
                if (componentType == null)
                {
                    UnityEngine.Debug.LogWarning($"[CommandExecutor] {typeError}");
                    return (false, typeError);
                }

                // Get component
                Component component = obj.GetComponent(componentType);
                if (component == null)
                {
                    string error = $"{p.component} not found on {obj.name}";
                    UnityEngine.Debug.LogWarning($"[CommandExecutor] {error}");
                    return (false, error);
                }

                // Check if this is a heavy component type
                bool isHeavyComponent = HeavyComponentTypes.Contains(componentType.Name);
                DebugLogToFile($"GetComponent START: {componentType.Name} on {obj.name} (isHeavy: {isHeavyComponent})");

                // Get component info (using Reflection)
                var sb = new StringBuilder();
                sb.AppendLine($"Component info for: {obj.name} -> {p.component}");
                sb.AppendLine();

                // Get properties
                var properties = componentType.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                sb.AppendLine("Properties:");
                int skippedCount = 0;
                foreach (var prop in properties)
                {
                    if (prop.CanRead)
                    {
                        // Check if this property should be skipped
                        if (HeavyPropertyBlacklist.Contains(prop.Name))
                        {
                            sb.AppendLine($"  {prop.Name}: <skipped - heavy property>");
                            skippedCount++;
                            DebugLogToFile($"  SKIP (blacklist): {prop.Name}");
                            continue;
                        }

                        // For heavy components, be extra cautious with certain property types
                        if (isHeavyComponent)
                        {
                            var propType = prop.PropertyType;
                            if (propType == typeof(TerrainData) ||
                                propType == typeof(Mesh) ||
                                propType.Name.Contains("Data") ||
                                propType.IsArray)
                            {
                                sb.AppendLine($"  {prop.Name}: <skipped - potentially heavy type: {propType.Name}>");
                                skippedCount++;
                                DebugLogToFile($"  SKIP (heavy type): {prop.Name} ({propType.Name})");
                                continue;
                            }
                        }

                        try
                        {
                            DebugLogToFile($"  READ START: {prop.Name} ({prop.PropertyType.Name})");
                            var sw = Stopwatch.StartNew();
                            object value = prop.GetValue(component);
                            sw.Stop();
                            DebugLogToFile($"  READ DONE: {prop.Name} = {value} ({sw.ElapsedMilliseconds}ms)");
                            sb.AppendLine($"  {prop.Name}: {value}");
                        }
                        catch (Exception ex)
                        {
                            sb.AppendLine($"  {prop.Name}: <unable to read: {ex.Message}>");
                            DebugLogToFile($"  READ FAIL: {prop.Name} - {ex.Message}");
                        }
                    }
                }

                if (skippedCount > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine($"Note: {skippedCount} heavy properties were skipped to prevent hanging.");
                }

                sb.AppendLine();

                // Get fields
                var fields = componentType.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (fields.Length > 0)
                {
                    sb.AppendLine("Fields:");
                    foreach (var field in fields)
                    {
                        // Check if this field should be skipped
                        if (HeavyPropertyBlacklist.Contains(field.Name))
                        {
                            sb.AppendLine($"  {field.Name}: <skipped - heavy field>");
                            continue;
                        }

                        try
                        {
                            object value = field.GetValue(component);
                            sb.AppendLine($"  {field.Name}: {value}");
                        }
                        catch (Exception)
                        {
                            sb.AppendLine($"  {field.Name}: <unable to read>");
                        }
                    }
                }

                string result = sb.ToString();
                DebugLogToFile($"GetComponent DONE: {p.component}");
                return (true, result);
            }
            catch (Exception e)
            {
                string error = $"Error getting component: {e.Message}";
                DebugLogToFile($"GetComponent ERROR: {e.Message}\n{e.StackTrace}");
                UnityEngine.Debug.LogError($"[CommandExecutor] {error}");
                return (false, error);
            }
        }

        /// <summary>
        /// Sets a component property
        /// </summary>
        private (bool, string) SetComponentProperty(CommandParams p)
        {
            try
            {
                if (p == null || string.IsNullOrEmpty(p.path))
                {
                    string error = "Missing required parameter: path";
                    UnityEngine.Debug.LogError($"[CommandExecutor] {error}");
                    return (false, error);
                }

                if (string.IsNullOrEmpty(p.component))
                {
                    string error = "Missing required parameter: component";
                    UnityEngine.Debug.LogError($"[CommandExecutor] {error}");
                    return (false, error);
                }

                if (string.IsNullOrEmpty(p.property))
                {
                    string error = "Missing required parameter: property";
                    UnityEngine.Debug.LogError($"[CommandExecutor] {error}");
                    return (false, error);
                }

                var (obj, findError) = FindGameObjectByPath(p.path);
                if (obj == null)
                {
                    string error = findError ?? $"GameObject not found: {p.path}";
                    UnityEngine.Debug.LogWarning($"[CommandExecutor] {error}");
                    return (false, error);
                }

                // Resolve component type
                var (componentType, typeError) = ResolveComponentTypeWithError(p.component);
                if (componentType == null)
                {
                    UnityEngine.Debug.LogWarning($"[CommandExecutor] {typeError}");
                    return (false, typeError);
                }

                // Get component
                Component component = obj.GetComponent(componentType);
                if (component == null)
                {
                    string error = $"{p.component} not found on {obj.name}";
                    UnityEngine.Debug.LogWarning($"[CommandExecutor] {error}");
                    return (false, error);
                }

                // Get property or field (also supports private SerializeField)
                var bindingFlags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                var propertyInfo = componentType.GetProperty(p.property, bindingFlags);
                var fieldInfo = componentType.GetField(p.property, bindingFlags);

                if (propertyInfo == null && fieldInfo == null)
                {
                    string error = $"Property or field '{p.property}' not found on {p.component}";
                    UnityEngine.Debug.LogWarning($"[CommandExecutor] {error}");
                    return (false, error);
                }

                Undo.RecordObject(component, "Set Component Property");

                // Set value
                Type targetType = propertyInfo?.PropertyType ?? fieldInfo.FieldType;
                object value;

                // If asset path (Assets/...) and type derives from UnityEngine.Object, load as asset
                if (p.value.StartsWith("Assets/") && typeof(UnityEngine.Object).IsAssignableFrom(targetType))
                {
                    var asset = AssetDatabase.LoadAssetAtPath(p.value, targetType);
                    if (asset == null)
                    {
                        string error = $"Asset not found at path: {p.value}";
                        UnityEngine.Debug.LogWarning($"[CommandExecutor] {error}");
                        return (false, error);
                    }
                    value = asset;
                }
                else
                {
                    value = ParsePropertyValue(p.value, targetType);
                }

                if (propertyInfo != null && propertyInfo.CanWrite)
                {
                    propertyInfo.SetValue(component, value);
                }
                else if (fieldInfo != null)
                {
                    fieldInfo.SetValue(component, value);
                }
                else
                {
                    string error = $"Property '{p.property}' is read-only";
                    UnityEngine.Debug.LogWarning($"[CommandExecutor] {error}");
                    return (false, error);
                }

                EditorUtility.SetDirty(obj);

                string result = $"Set {obj.name}.{p.component}.{p.property} = {p.value}";

                // Warn if in play mode (changes will be lost when play mode ends)
                if (EditorApplication.isPlaying)
                {
                    result += "\n⚠️ WARNING: Unity is in Play Mode. Changes will be lost when Play Mode ends!";
                    UnityEngine.Debug.LogWarning($"[CommandExecutor] {result}");
                }
                else
                {
                    UnityEngine.Debug.Log($"[CommandExecutor] {result}");
                }

                return (true, result);
            }
            catch (Exception e)
            {
                string error = $"Error setting component property: {e.Message}";
                UnityEngine.Debug.LogError($"[CommandExecutor] {error}");
                return (false, error);
            }
        }

        /// <summary>
        /// Lists all components on a GameObject
        /// </summary>
        private (bool, string) GetComponents(CommandParams p)
        {
            try
            {
                if (p == null || string.IsNullOrEmpty(p.path))
                {
                    string error = "Missing required parameter: path";
                    UnityEngine.Debug.LogError($"[CommandExecutor] {error}");
                    return (false, error);
                }

                var (obj, findError) = FindGameObjectByPath(p.path);
                if (obj == null)
                {
                    string error = findError ?? $"GameObject not found: {p.path}";
                    UnityEngine.Debug.LogWarning($"[CommandExecutor] {error}");
                    return (false, error);
                }

                // Get all components
                Component[] components = obj.GetComponents<Component>();

                var sb = new StringBuilder();
                sb.AppendLine($"Components on: {obj.name}");
                sb.AppendLine($"Total: {components.Length} component(s)");
                sb.AppendLine();

                foreach (var component in components)
                {
                    if (component == null) continue;

                    string typeName = component.GetType().Name;
                    sb.AppendLine($"- {typeName}");
                }

                string result = sb.ToString();
                UnityEngine.Debug.Log($"[CommandExecutor] Retrieved {components.Length} components");
                return (true, result);
            }
            catch (Exception e)
            {
                string error = $"Error getting components: {e.Message}";
                UnityEngine.Debug.LogError($"[CommandExecutor] {error}");
                return (false, error);
            }
        }

        /// <summary>
        /// Sets a GameObject/Component reference on a component
        /// </summary>
        private (bool, string) SetObjectReference(CommandParams p)
        {
            try
            {
                if (p == null || string.IsNullOrEmpty(p.path))
                {
                    string error = "Missing required parameter: path";
                    UnityEngine.Debug.LogError($"[CommandExecutor] {error}");
                    return (false, error);
                }

                if (string.IsNullOrEmpty(p.component))
                {
                    string error = "Missing required parameter: component";
                    UnityEngine.Debug.LogError($"[CommandExecutor] {error}");
                    return (false, error);
                }

                if (string.IsNullOrEmpty(p.property))
                {
                    string error = "Missing required parameter: property";
                    UnityEngine.Debug.LogError($"[CommandExecutor] {error}");
                    return (false, error);
                }

                if (string.IsNullOrEmpty(p.target_path))
                {
                    string error = "Missing required parameter: target_path";
                    UnityEngine.Debug.LogError($"[CommandExecutor] {error}");
                    return (false, error);
                }

                // Get source object and component
                var (sourceObj, sourceFindError) = FindGameObjectByPath(p.path);
                if (sourceObj == null)
                {
                    string error = sourceFindError ?? $"Source GameObject not found: {p.path}";
                    UnityEngine.Debug.LogWarning($"[CommandExecutor] {error}");
                    return (false, error);
                }

                var (componentType, typeError) = ResolveComponentTypeWithError(p.component);
                if (componentType == null)
                {
                    UnityEngine.Debug.LogWarning($"[CommandExecutor] {typeError}");
                    return (false, typeError);
                }

                Component component = sourceObj.GetComponent(componentType);
                if (component == null)
                {
                    string error = $"{p.component} not found on {sourceObj.name}";
                    UnityEngine.Debug.LogWarning($"[CommandExecutor] {error}");
                    return (false, error);
                }

                // Get property or field
                var bindingFlags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                var propertyInfo = componentType.GetProperty(p.property, bindingFlags);
                var fieldInfo = componentType.GetField(p.property, bindingFlags);

                if (propertyInfo == null && fieldInfo == null)
                {
                    string error = $"Property or field '{p.property}' not found on {p.component}";
                    UnityEngine.Debug.LogWarning($"[CommandExecutor] {error}");
                    return (false, error);
                }

                Type memberType = propertyInfo?.PropertyType ?? fieldInfo.FieldType;

                // Get target object
                var (targetObj, targetFindError) = FindGameObjectByPath(p.target_path);
                if (targetObj == null)
                {
                    string error = targetFindError ?? $"Target GameObject not found: {p.target_path}";
                    UnityEngine.Debug.LogWarning($"[CommandExecutor] {error}");
                    return (false, error);
                }

                // Determine reference value
                object referenceValue = null;
                string targetTypeName = "";

                if (memberType == typeof(GameObject))
                {
                    // Reference to GameObject
                    referenceValue = targetObj;
                    targetTypeName = "GameObject";
                }
                else if (memberType == typeof(Transform))
                {
                    // Reference to Transform
                    referenceValue = targetObj.transform;
                    targetTypeName = "Transform";
                }
                else if (typeof(Component).IsAssignableFrom(memberType))
                {
                    // If target_component is specified, use that type instead of memberType
                    Type targetComponentType = memberType;
                    if (!string.IsNullOrEmpty(p.target_component))
                    {
                        var (resolvedType, resolveError) = ResolveComponentTypeWithError(p.target_component);
                        if (resolvedType == null)
                        {
                            UnityEngine.Debug.LogWarning($"[CommandExecutor] {resolveError}");
                            return (false, resolveError);
                        }
                        // Verify the resolved type is compatible with the member type
                        if (!memberType.IsAssignableFrom(resolvedType))
                        {
                            string error = $"Target component type '{p.target_component}' is not compatible with property type '{memberType.Name}'";
                            UnityEngine.Debug.LogWarning($"[CommandExecutor] {error}");
                            return (false, error);
                        }
                        targetComponentType = resolvedType;
                    }

                    // Reference to specific Component type
                    referenceValue = targetObj.GetComponent(targetComponentType);
                    if (referenceValue == null)
                    {
                        string error = $"Target '{p.target_path}' does not have component '{targetComponentType.Name}'";
                        UnityEngine.Debug.LogWarning($"[CommandExecutor] {error}");
                        return (false, error);
                    }
                    targetTypeName = targetComponentType.Name;
                }
                else
                {
                    string error = $"Property '{p.property}' is not a GameObject or Component reference type (type: {memberType.Name})";
                    UnityEngine.Debug.LogWarning($"[CommandExecutor] {error}");
                    return (false, error);
                }

                // Set reference
                Undo.RecordObject(component, "Set Object Reference");

                if (propertyInfo != null && propertyInfo.CanWrite)
                {
                    propertyInfo.SetValue(component, referenceValue);
                }
                else if (fieldInfo != null)
                {
                    fieldInfo.SetValue(component, referenceValue);
                }
                else
                {
                    string error = $"Property '{p.property}' is read-only";
                    UnityEngine.Debug.LogWarning($"[CommandExecutor] {error}");
                    return (false, error);
                }

                EditorUtility.SetDirty(component);
                EditorUtility.SetDirty(sourceObj);

                string result = $"Set {sourceObj.name}.{p.component}.{p.property} = {targetObj.name} ({targetTypeName})";

                // Warn if in play mode (changes will be lost when play mode ends)
                if (EditorApplication.isPlaying)
                {
                    result += "\n⚠️ WARNING: Unity is in Play Mode. Changes will be lost when Play Mode ends!";
                    UnityEngine.Debug.LogWarning($"[CommandExecutor] {result}");
                }
                else
                {
                    UnityEngine.Debug.Log($"[CommandExecutor] {result}");
                }

                return (true, result);
            }
            catch (Exception e)
            {
                string error = $"Error setting object reference: {e.Message}";
                UnityEngine.Debug.LogError($"[CommandExecutor] {error}");
                return (false, error);
            }
        }

        /// <summary>
        /// Adds an item to a List/Array property on a component
        /// Supports: prefab_path (for GameObject lists), target_path (for scene object references), value (for primitives)
        /// </summary>
        private (bool, string) AddToList(CommandParams p)
        {
            try
            {
                if (p == null || string.IsNullOrEmpty(p.path))
                    return (false, "Missing required parameter: path");

                if (string.IsNullOrEmpty(p.component))
                    return (false, "Missing required parameter: component");

                if (string.IsNullOrEmpty(p.property))
                    return (false, "Missing required parameter: property");

                var (obj, findError) = FindGameObjectByPath(p.path);
                if (obj == null)
                    return (false, findError ?? $"GameObject not found: {p.path}");

                var (componentType, typeError) = ResolveComponentTypeWithError(p.component);
                if (componentType == null)
                    return (false, typeError);

                Component component = obj.GetComponent(componentType);
                if (component == null)
                    return (false, $"{p.component} not found on {obj.name}");

                // Get property or field
                var bindingFlags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                var propertyInfo = componentType.GetProperty(p.property, bindingFlags);
                var fieldInfo = componentType.GetField(p.property, bindingFlags);

                if (propertyInfo == null && fieldInfo == null)
                    return (false, $"Property or field '{p.property}' not found on {p.component}");

                Type memberType = propertyInfo?.PropertyType ?? fieldInfo.FieldType;
                object listObj = propertyInfo != null ? propertyInfo.GetValue(component) : fieldInfo.GetValue(component);

                // Determine the element type
                Type elementType = null;
                if (memberType.IsGenericType && memberType.GetGenericTypeDefinition() == typeof(List<>))
                {
                    elementType = memberType.GetGenericArguments()[0];
                }
                else if (memberType.IsArray)
                {
                    elementType = memberType.GetElementType();
                }
                else
                {
                    return (false, $"Property '{p.property}' is not a List or Array type (type: {memberType.Name})");
                }

                // Determine the item to add
                object itemToAdd = null;
                string itemDescription = "";

                if (elementType == typeof(GameObject))
                {
                    // For GameObject lists, support both prefab_path and target_path
                    if (!string.IsNullOrEmpty(p.prefab_path))
                    {
                        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(p.prefab_path);
                        if (prefab == null)
                            return (false, $"Prefab not found at: {p.prefab_path}");
                        itemToAdd = prefab;
                        itemDescription = p.prefab_path;
                    }
                    else if (!string.IsNullOrEmpty(p.target_path))
                    {
                        var (targetObj, targetError) = FindGameObjectByPath(p.target_path);
                        if (targetObj == null)
                            return (false, targetError ?? $"Target GameObject not found: {p.target_path}");
                        itemToAdd = targetObj;
                        itemDescription = p.target_path;
                    }
                    else
                    {
                        return (false, "Missing required parameter: prefab_path or target_path (for GameObject list)");
                    }
                }
                else if (typeof(UnityEngine.Object).IsAssignableFrom(elementType))
                {
                    // For other UnityEngine.Object types (Prefab assets, etc.)
                    if (!string.IsNullOrEmpty(p.prefab_path) || !string.IsNullOrEmpty(p.asset_path))
                    {
                        string assetPath = !string.IsNullOrEmpty(p.prefab_path) ? p.prefab_path : p.asset_path;
                        var asset = AssetDatabase.LoadAssetAtPath(assetPath, elementType);
                        if (asset == null)
                            return (false, $"Asset not found at: {assetPath}");
                        itemToAdd = asset;
                        itemDescription = assetPath;
                    }
                    else if (!string.IsNullOrEmpty(p.target_path))
                    {
                        var (targetObj, targetError) = FindGameObjectByPath(p.target_path);
                        if (targetObj == null)
                            return (false, targetError ?? $"Target GameObject not found: {p.target_path}");

                        if (elementType == typeof(Transform))
                        {
                            itemToAdd = targetObj.transform;
                        }
                        else if (typeof(Component).IsAssignableFrom(elementType))
                        {
                            itemToAdd = targetObj.GetComponent(elementType);
                            if (itemToAdd == null)
                                return (false, $"Target '{p.target_path}' does not have component '{elementType.Name}'");
                        }
                        else
                        {
                            itemToAdd = targetObj;
                        }
                        itemDescription = p.target_path;
                    }
                    else
                    {
                        return (false, $"Missing required parameter: prefab_path, asset_path, or target_path (for {elementType.Name} list)");
                    }
                }
                else
                {
                    // For primitive types
                    if (string.IsNullOrEmpty(p.value))
                        return (false, $"Missing required parameter: value (for {elementType.Name} list)");
                    itemToAdd = ParsePropertyValue(p.value, elementType);
                    itemDescription = p.value;
                }

                Undo.RecordObject(component, "Add to List");

                // Add to list
                if (memberType.IsGenericType && memberType.GetGenericTypeDefinition() == typeof(List<>))
                {
                    // Create list if null
                    if (listObj == null)
                    {
                        listObj = Activator.CreateInstance(memberType);
                        if (propertyInfo != null && propertyInfo.CanWrite)
                            propertyInfo.SetValue(component, listObj);
                        else if (fieldInfo != null)
                            fieldInfo.SetValue(component, listObj);
                    }

                    var addMethod = memberType.GetMethod("Add");
                    addMethod.Invoke(listObj, new[] { itemToAdd });
                }
                else if (memberType.IsArray)
                {
                    // For arrays, create a new array with the item added
                    Array oldArray = (Array)listObj ?? Array.CreateInstance(elementType, 0);
                    Array newArray = Array.CreateInstance(elementType, oldArray.Length + 1);
                    Array.Copy(oldArray, newArray, oldArray.Length);
                    newArray.SetValue(itemToAdd, oldArray.Length);

                    if (propertyInfo != null && propertyInfo.CanWrite)
                        propertyInfo.SetValue(component, newArray);
                    else if (fieldInfo != null)
                        fieldInfo.SetValue(component, newArray);
                }

                EditorUtility.SetDirty(component);
                EditorUtility.SetDirty(obj);

                string result = $"Added '{itemDescription}' to {obj.name}.{p.component}.{p.property}";
                UnityEngine.Debug.Log($"[CommandExecutor] {result}");
                return (true, result);
            }
            catch (Exception e)
            {
                string error = $"Error adding to list: {e.Message}";
                UnityEngine.Debug.LogError($"[CommandExecutor] {error}");
                return (false, error);
            }
        }

        /// <summary>
        /// Removes an item from a List/Array property on a component
        /// Supports: index (by position), prefab_path/target_path (by reference), value (by value)
        /// </summary>
        private (bool, string) RemoveFromList(CommandParams p)
        {
            try
            {
                if (p == null || string.IsNullOrEmpty(p.path))
                    return (false, "Missing required parameter: path");

                if (string.IsNullOrEmpty(p.component))
                    return (false, "Missing required parameter: component");

                if (string.IsNullOrEmpty(p.property))
                    return (false, "Missing required parameter: property");

                var (obj, findError) = FindGameObjectByPath(p.path);
                if (obj == null)
                    return (false, findError ?? $"GameObject not found: {p.path}");

                var (componentType, typeError) = ResolveComponentTypeWithError(p.component);
                if (componentType == null)
                    return (false, typeError);

                Component component = obj.GetComponent(componentType);
                if (component == null)
                    return (false, $"{p.component} not found on {obj.name}");

                // Get property or field
                var bindingFlags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                var propertyInfo = componentType.GetProperty(p.property, bindingFlags);
                var fieldInfo = componentType.GetField(p.property, bindingFlags);

                if (propertyInfo == null && fieldInfo == null)
                    return (false, $"Property or field '{p.property}' not found on {p.component}");

                Type memberType = propertyInfo?.PropertyType ?? fieldInfo.FieldType;
                object listObj = propertyInfo != null ? propertyInfo.GetValue(component) : fieldInfo.GetValue(component);

                if (listObj == null)
                    return (false, $"List '{p.property}' is null");

                // Determine the element type
                Type elementType = null;
                int listCount = 0;
                if (memberType.IsGenericType && memberType.GetGenericTypeDefinition() == typeof(List<>))
                {
                    elementType = memberType.GetGenericArguments()[0];
                    listCount = (int)memberType.GetProperty("Count").GetValue(listObj);
                }
                else if (memberType.IsArray)
                {
                    elementType = memberType.GetElementType();
                    listCount = ((Array)listObj).Length;
                }
                else
                {
                    return (false, $"Property '{p.property}' is not a List or Array type (type: {memberType.Name})");
                }

                if (listCount == 0)
                    return (false, $"List '{p.property}' is empty");

                Undo.RecordObject(component, "Remove from List");

                // Determine removal method: by index or by reference
                int removeIndex = -1;
                string removedDescription = "";

                if (p.index >= 0)
                {
                    // Remove by index
                    if (p.index >= listCount)
                        return (false, $"Index {p.index} out of range (list has {listCount} items)");
                    removeIndex = p.index;
                    removedDescription = $"index {p.index}";
                }
                else
                {
                    // Remove by reference - find the item first
                    object itemToFind = null;

                    if (!string.IsNullOrEmpty(p.prefab_path))
                    {
                        itemToFind = AssetDatabase.LoadAssetAtPath(p.prefab_path, elementType);
                        removedDescription = p.prefab_path;
                    }
                    else if (!string.IsNullOrEmpty(p.target_path))
                    {
                        var (targetObj, _) = FindGameObjectByPath(p.target_path);
                        if (targetObj != null)
                        {
                            if (elementType == typeof(GameObject))
                                itemToFind = targetObj;
                            else if (elementType == typeof(Transform))
                                itemToFind = targetObj.transform;
                            else if (typeof(Component).IsAssignableFrom(elementType))
                                itemToFind = targetObj.GetComponent(elementType);
                        }
                        removedDescription = p.target_path;
                    }
                    else if (!string.IsNullOrEmpty(p.value))
                    {
                        itemToFind = ParsePropertyValue(p.value, elementType);
                        removedDescription = p.value;
                    }

                    if (itemToFind == null)
                        return (false, "Could not find item to remove. Provide index, prefab_path, target_path, or value.");

                    // Find the index of the item
                    if (memberType.IsGenericType)
                    {
                        var indexOfMethod = memberType.GetMethod("IndexOf");
                        removeIndex = (int)indexOfMethod.Invoke(listObj, new[] { itemToFind });
                    }
                    else if (memberType.IsArray)
                    {
                        removeIndex = Array.IndexOf((Array)listObj, itemToFind);
                    }

                    if (removeIndex < 0)
                        return (false, $"Item '{removedDescription}' not found in list");
                }

                // Remove the item
                if (memberType.IsGenericType && memberType.GetGenericTypeDefinition() == typeof(List<>))
                {
                    var removeAtMethod = memberType.GetMethod("RemoveAt");
                    removeAtMethod.Invoke(listObj, new object[] { removeIndex });
                }
                else if (memberType.IsArray)
                {
                    Array oldArray = (Array)listObj;
                    Array newArray = Array.CreateInstance(elementType, oldArray.Length - 1);
                    if (removeIndex > 0)
                        Array.Copy(oldArray, 0, newArray, 0, removeIndex);
                    if (removeIndex < oldArray.Length - 1)
                        Array.Copy(oldArray, removeIndex + 1, newArray, removeIndex, oldArray.Length - removeIndex - 1);

                    if (propertyInfo != null && propertyInfo.CanWrite)
                        propertyInfo.SetValue(component, newArray);
                    else if (fieldInfo != null)
                        fieldInfo.SetValue(component, newArray);
                }

                EditorUtility.SetDirty(component);
                EditorUtility.SetDirty(obj);

                string result = $"Removed '{removedDescription}' from {obj.name}.{p.component}.{p.property}";
                UnityEngine.Debug.Log($"[CommandExecutor] {result}");
                return (true, result);
            }
            catch (Exception e)
            {
                string error = $"Error removing from list: {e.Message}";
                UnityEngine.Debug.LogError($"[CommandExecutor] {error}");
                return (false, error);
            }
        }

        /// <summary>
        /// Resolves a component type name (with error info)
        /// </summary>
        /// <returns>(type, error message) - type is null on error</returns>
        private (Type type, string error) ResolveComponentTypeWithError(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
                return (null, "Component type name is empty");

            // Try type name as-is first
            Type type = Type.GetType(typeName);
            if (type != null && typeof(Component).IsAssignableFrom(type))
                return (type, null);

            // Try with "UnityEngine." prefix
            type = Type.GetType($"UnityEngine.{typeName}");
            if (type != null && typeof(Component).IsAssignableFrom(type))
                return (type, null);

            // Try full name (with assembly name)
            type = Type.GetType($"UnityEngine.{typeName}, UnityEngine");
            if (type != null && typeof(Component).IsAssignableFrom(type))
                return (type, null);

            // UnityEngine.CoreModule (Unity 2017.3+)
            type = Type.GetType($"UnityEngine.{typeName}, UnityEngine.CoreModule");
            if (type != null && typeof(Component).IsAssignableFrom(type))
                return (type, null);

            // Physics related
            type = Type.GetType($"UnityEngine.{typeName}, UnityEngine.PhysicsModule");
            if (type != null && typeof(Component).IsAssignableFrom(type))
                return (type, null);

            // UI components (UnityEngine.UI namespace)
            type = Type.GetType($"UnityEngine.UI.{typeName}, UnityEngine.UI");
            if (type != null && typeof(Component).IsAssignableFrom(type))
                return (type, null);

            // InputSystem components (UnityEngine.InputSystem namespace)
            type = Type.GetType($"UnityEngine.InputSystem.{typeName}, Unity.InputSystem");
            if (type != null && typeof(Component).IsAssignableFrom(type))
                return (type, null);

            // Search all loaded assemblies (for custom scripts - fully qualified name)
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = assembly.GetType(typeName);
                if (type != null && typeof(Component).IsAssignableFrom(type))
                    return (type, null);
            }

            // Generic search: search all assemblies by type name only (for external packages)
            var matchingTypes = new List<Type>();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    foreach (var t in assembly.GetTypes())
                    {
                        if (t.Name == typeName && typeof(Component).IsAssignableFrom(t))
                            matchingTypes.Add(t);
                    }
                }
                catch (System.Reflection.ReflectionTypeLoadException)
                {
                    // Some assemblies may cause load errors
                    continue;
                }
            }

            if (matchingTypes.Count == 1)
                return (matchingTypes[0], null);

            if (matchingTypes.Count > 1)
            {
                var fullNames = string.Join(", ", matchingTypes.ConvertAll(t => t.FullName));
                return (null, $"Multiple types found for '{typeName}'. Use full qualified name: {fullNames}");
            }

            return (null, $"Component type not found: {typeName}");
        }

        /// <summary>
        /// Parses a property value from a string
        /// </summary>
        private object ParsePropertyValue(string valueString, Type targetType)
        {
            if (string.IsNullOrEmpty(valueString))
                return null;

            try
            {
                if (targetType == typeof(string))
                    return valueString;

                if (targetType == typeof(int))
                    return int.Parse(valueString);

                if (targetType == typeof(float))
                    return float.Parse(valueString);

                if (targetType == typeof(bool))
                    return bool.Parse(valueString);

                if (targetType == typeof(double))
                    return double.Parse(valueString);

                if (targetType == typeof(Vector3))
                {
                    // Supports JSON format {"x":1,"y":2,"z":3} or [x,y,z] or "1,2,3" or "(1, 2, 3)" format
                    if (valueString.StartsWith("{"))
                    {
                        return JsonUtility.FromJson<Vector3>(valueString);
                    }
                    else if (valueString.StartsWith("["))
                    {
                        // JSON array format: [x, y, z]
                        string cleaned = valueString.Trim().TrimStart('[').TrimEnd(']');
                        string[] parts = cleaned.Split(',');
                        if (parts.Length == 3)
                        {
                            return new Vector3(
                                float.Parse(parts[0].Trim()),
                                float.Parse(parts[1].Trim()),
                                float.Parse(parts[2].Trim())
                            );
                        }
                    }
                    else
                    {
                        // Remove parentheses: (0, 0, 0) -> 0, 0, 0
                        string cleaned = valueString.Trim().TrimStart('(').TrimEnd(')');
                        string[] parts = cleaned.Split(',');
                        if (parts.Length == 3)
                        {
                            return new Vector3(
                                float.Parse(parts[0].Trim()),
                                float.Parse(parts[1].Trim()),
                                float.Parse(parts[2].Trim())
                            );
                        }
                    }
                }

                if (targetType == typeof(Vector2))
                {
                    if (valueString.StartsWith("{"))
                    {
                        return JsonUtility.FromJson<Vector2>(valueString);
                    }
                    else if (valueString.StartsWith("["))
                    {
                        // JSON array format: [x, y]
                        string cleaned = valueString.Trim().TrimStart('[').TrimEnd(']');
                        string[] parts = cleaned.Split(',');
                        if (parts.Length == 2)
                        {
                            return new Vector2(
                                float.Parse(parts[0].Trim()),
                                float.Parse(parts[1].Trim())
                            );
                        }
                    }
                    else
                    {
                        // Remove parentheses: (0, 0) -> 0, 0
                        string cleaned = valueString.Trim().TrimStart('(').TrimEnd(')');
                        string[] parts = cleaned.Split(',');
                        if (parts.Length == 2)
                        {
                            return new Vector2(
                                float.Parse(parts[0].Trim()),
                                float.Parse(parts[1].Trim())
                            );
                        }
                    }
                }

                if (targetType == typeof(Color))
                {
                    // Supports JSON format or HTML color format
                    if (valueString.StartsWith("#"))
                    {
                        Color color;
                        if (ColorUtility.TryParseHtmlString(valueString, out color))
                            return color;
                    }
                    else if (valueString.StartsWith("{"))
                    {
                        return JsonUtility.FromJson<Color>(valueString);
                    }
                }

                if (targetType == typeof(Vector4))
                {
                    if (valueString.StartsWith("{"))
                    {
                        return JsonUtility.FromJson<Vector4>(valueString);
                    }
                    else if (valueString.StartsWith("["))
                    {
                        // JSON array format: [x, y, z, w]
                        string cleaned = valueString.Trim().TrimStart('[').TrimEnd(']');
                        string[] parts = cleaned.Split(',');
                        if (parts.Length == 4)
                        {
                            return new Vector4(
                                float.Parse(parts[0].Trim()),
                                float.Parse(parts[1].Trim()),
                                float.Parse(parts[2].Trim()),
                                float.Parse(parts[3].Trim())
                            );
                        }
                    }
                    else
                    {
                        string cleaned = valueString.Trim().TrimStart('(').TrimEnd(')');
                        string[] parts = cleaned.Split(',');
                        if (parts.Length == 4)
                        {
                            return new Vector4(
                                float.Parse(parts[0].Trim()),
                                float.Parse(parts[1].Trim()),
                                float.Parse(parts[2].Trim()),
                                float.Parse(parts[3].Trim())
                            );
                        }
                    }
                }

                if (targetType == typeof(Quaternion))
                {
                    if (valueString.StartsWith("{"))
                    {
                        return JsonUtility.FromJson<Quaternion>(valueString);
                    }
                    else if (valueString.StartsWith("["))
                    {
                        // JSON array format: [x, y, z, w]
                        string cleaned = valueString.Trim().TrimStart('[').TrimEnd(']');
                        string[] parts = cleaned.Split(',');
                        if (parts.Length == 4)
                        {
                            return new Quaternion(
                                float.Parse(parts[0].Trim()),
                                float.Parse(parts[1].Trim()),
                                float.Parse(parts[2].Trim()),
                                float.Parse(parts[3].Trim())
                            );
                        }
                    }
                    else
                    {
                        string cleaned = valueString.Trim().TrimStart('(').TrimEnd(')');
                        string[] parts = cleaned.Split(',');
                        if (parts.Length == 4)
                        {
                            return new Quaternion(
                                float.Parse(parts[0].Trim()),
                                float.Parse(parts[1].Trim()),
                                float.Parse(parts[2].Trim()),
                                float.Parse(parts[3].Trim())
                            );
                        }
                    }
                }

                if (targetType == typeof(Rect))
                {
                    if (valueString.StartsWith("{"))
                    {
                        return JsonUtility.FromJson<Rect>(valueString);
                    }
                    else if (valueString.StartsWith("["))
                    {
                        // JSON array format: [x, y, width, height]
                        string cleaned = valueString.Trim().TrimStart('[').TrimEnd(']');
                        string[] parts = cleaned.Split(',');
                        if (parts.Length == 4)
                        {
                            return new Rect(
                                float.Parse(parts[0].Trim()),
                                float.Parse(parts[1].Trim()),
                                float.Parse(parts[2].Trim()),
                                float.Parse(parts[3].Trim())
                            );
                        }
                    }
                    else
                    {
                        string cleaned = valueString.Trim().TrimStart('(').TrimEnd(')');
                        string[] parts = cleaned.Split(',');
                        if (parts.Length == 4)
                        {
                            return new Rect(
                                float.Parse(parts[0].Trim()),
                                float.Parse(parts[1].Trim()),
                                float.Parse(parts[2].Trim()),
                                float.Parse(parts[3].Trim())
                            );
                        }
                    }
                }

                if (targetType == typeof(Bounds))
                {
                    if (valueString.StartsWith("{"))
                    {
                        return JsonUtility.FromJson<Bounds>(valueString);
                    }
                    else if (valueString.StartsWith("["))
                    {
                        // JSON array format: [centerX, centerY, centerZ, sizeX, sizeY, sizeZ]
                        string cleaned = valueString.Trim().TrimStart('[').TrimEnd(']');
                        string[] parts = cleaned.Split(',');
                        if (parts.Length == 6)
                        {
                            return new Bounds(
                                new Vector3(
                                    float.Parse(parts[0].Trim()),
                                    float.Parse(parts[1].Trim()),
                                    float.Parse(parts[2].Trim())
                                ),
                                new Vector3(
                                    float.Parse(parts[3].Trim()),
                                    float.Parse(parts[4].Trim()),
                                    float.Parse(parts[5].Trim())
                                )
                            );
                        }
                    }
                    else
                    {
                        string cleaned = valueString.Trim().TrimStart('(').TrimEnd(')');
                        string[] parts = cleaned.Split(',');
                        if (parts.Length == 6)
                        {
                            return new Bounds(
                                new Vector3(
                                    float.Parse(parts[0].Trim()),
                                    float.Parse(parts[1].Trim()),
                                    float.Parse(parts[2].Trim())
                                ),
                                new Vector3(
                                    float.Parse(parts[3].Trim()),
                                    float.Parse(parts[4].Trim()),
                                    float.Parse(parts[5].Trim())
                                )
                            );
                        }
                    }
                }

                if (targetType == typeof(LayerMask))
                {
                    if (int.TryParse(valueString, out int mask))
                    {
                        return (LayerMask)mask;
                    }
                    else
                    {
                        return LayerMask.GetMask(valueString);
                    }
                }

                // Enum type
                if (targetType.IsEnum)
                {
                    return Enum.Parse(targetType, valueString, true);
                }

                // UnityEngine.Object type (asset reference)
                if (typeof(UnityEngine.Object).IsAssignableFrom(targetType))
                {
                    // Load from asset path
                    if (valueString.StartsWith("Assets/") || valueString.StartsWith("Packages/"))
                    {
                        var asset = AssetDatabase.LoadAssetAtPath(valueString, targetType);
                        if (asset != null)
                        {
                            return asset;
                        }
                        else
                        {
                            string errorMsg = $"Asset not found at path: {valueString}";
                            UnityEngine.Debug.LogWarning($"[CommandExecutor] {errorMsg}");
                            throw new System.IO.FileNotFoundException(errorMsg);
                        }
                    }
                    // Load from GUID
                    else if (valueString.Length == 32 && System.Text.RegularExpressions.Regex.IsMatch(valueString, "^[a-fA-F0-9]+$"))
                    {
                        string path = AssetDatabase.GUIDToAssetPath(valueString);
                        if (!string.IsNullOrEmpty(path))
                        {
                            return AssetDatabase.LoadAssetAtPath(path, targetType);
                        }
                    }
                    // Search by asset name
                    else
                    {
                        string[] guids = AssetDatabase.FindAssets($"{valueString} t:{targetType.Name}");
                        if (guids.Length > 0)
                        {
                            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                            return AssetDatabase.LoadAssetAtPath(path, targetType);
                        }
                    }
                }

                // Other JSON-compatible types
                return JsonUtility.FromJson(valueString, targetType);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"[CommandExecutor] Error parsing value '{valueString}' to type {targetType.Name}: {e.Message}");
                throw; // Rethrown to caller's catch block as error
            }
        }
    }
}
