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
                    Debug.LogError($"[CommandExecutor] {error}");
                    return (false, error);
                }

                if (string.IsNullOrEmpty(p.component))
                {
                    string error = "Missing required parameter: component";
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

                // Resolve component type
                var (componentType, typeError) = ResolveComponentTypeWithError(p.component);
                if (componentType == null)
                {
                    Debug.LogWarning($"[CommandExecutor] {typeError}");
                    return (false, typeError);
                }

                // Check if component already exists
                if (obj.GetComponent(componentType) != null)
                {
                    string error = $"{p.component} already exists on {obj.name}";
                    Debug.LogWarning($"[CommandExecutor] {error}");
                    return (false, error);
                }

                // Add component
                Component newComponent = obj.AddComponent(componentType);
                Undo.RegisterCreatedObjectUndo(newComponent, "Add Component");
                EditorUtility.SetDirty(obj);
                EditorUtility.SetDirty(newComponent);

                string result = $"Added {p.component} to {obj.name}";
                Debug.Log($"[CommandExecutor] {result}");
                return (true, result);
            }
            catch (Exception e)
            {
                string error = $"Error adding component: {e.Message}";
                Debug.LogError($"[CommandExecutor] {error}");
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
                    Debug.LogError($"[CommandExecutor] {error}");
                    return (false, error);
                }

                if (string.IsNullOrEmpty(p.component))
                {
                    string error = "Missing required parameter: component";
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

                // Resolve component type
                var (componentType, typeError) = ResolveComponentTypeWithError(p.component);
                if (componentType == null)
                {
                    Debug.LogWarning($"[CommandExecutor] {typeError}");
                    return (false, typeError);
                }

                // Get component
                Component component = obj.GetComponent(componentType);
                if (component == null)
                {
                    string error = $"{p.component} not found on {obj.name}";
                    Debug.LogWarning($"[CommandExecutor] {error}");
                    return (false, error);
                }

                // Delete component
                Undo.DestroyObjectImmediate(component);

                string result = $"Removed {p.component} from {obj.name}";
                Debug.Log($"[CommandExecutor] {result}");
                return (true, result);
            }
            catch (Exception e)
            {
                string error = $"Error removing component: {e.Message}";
                Debug.LogError($"[CommandExecutor] {error}");
                return (false, error);
            }
        }

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
                    Debug.LogError($"[CommandExecutor] {error}");
                    return (false, error);
                }

                if (string.IsNullOrEmpty(p.component))
                {
                    string error = "Missing required parameter: component";
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

                // Resolve component type
                var (componentType, typeError) = ResolveComponentTypeWithError(p.component);
                if (componentType == null)
                {
                    Debug.LogWarning($"[CommandExecutor] {typeError}");
                    return (false, typeError);
                }

                // Get component
                Component component = obj.GetComponent(componentType);
                if (component == null)
                {
                    string error = $"{p.component} not found on {obj.name}";
                    Debug.LogWarning($"[CommandExecutor] {error}");
                    return (false, error);
                }

                // Get component info (using Reflection)
                var sb = new StringBuilder();
                sb.AppendLine($"Component info for: {obj.name} -> {p.component}");
                sb.AppendLine();

                // Get properties
                var properties = componentType.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                sb.AppendLine("Properties:");
                foreach (var prop in properties)
                {
                    if (prop.CanRead)
                    {
                        try
                        {
                            object value = prop.GetValue(component);
                            sb.AppendLine($"  {prop.Name}: {value}");
                        }
                        catch (Exception)
                        {
                            sb.AppendLine($"  {prop.Name}: <unable to read>");
                        }
                    }
                }

                sb.AppendLine();

                // Get fields
                var fields = componentType.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (fields.Length > 0)
                {
                    sb.AppendLine("Fields:");
                    foreach (var field in fields)
                    {
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
                Debug.Log($"[CommandExecutor] Retrieved component info for {p.component}");
                return (true, result);
            }
            catch (Exception e)
            {
                string error = $"Error getting component: {e.Message}";
                Debug.LogError($"[CommandExecutor] {error}");
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
                    Debug.LogError($"[CommandExecutor] {error}");
                    return (false, error);
                }

                if (string.IsNullOrEmpty(p.component))
                {
                    string error = "Missing required parameter: component";
                    Debug.LogError($"[CommandExecutor] {error}");
                    return (false, error);
                }

                if (string.IsNullOrEmpty(p.property))
                {
                    string error = "Missing required parameter: property";
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

                // Resolve component type
                var (componentType, typeError) = ResolveComponentTypeWithError(p.component);
                if (componentType == null)
                {
                    Debug.LogWarning($"[CommandExecutor] {typeError}");
                    return (false, typeError);
                }

                // Get component
                Component component = obj.GetComponent(componentType);
                if (component == null)
                {
                    string error = $"{p.component} not found on {obj.name}";
                    Debug.LogWarning($"[CommandExecutor] {error}");
                    return (false, error);
                }

                // Get property or field (also supports private SerializeField)
                var bindingFlags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                var propertyInfo = componentType.GetProperty(p.property, bindingFlags);
                var fieldInfo = componentType.GetField(p.property, bindingFlags);

                if (propertyInfo == null && fieldInfo == null)
                {
                    string error = $"Property or field '{p.property}' not found on {p.component}";
                    Debug.LogWarning($"[CommandExecutor] {error}");
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
                        Debug.LogWarning($"[CommandExecutor] {error}");
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
                    Debug.LogWarning($"[CommandExecutor] {error}");
                    return (false, error);
                }

                EditorUtility.SetDirty(obj);

                string result = $"Set {obj.name}.{p.component}.{p.property} = {p.value}";
                Debug.Log($"[CommandExecutor] {result}");
                return (true, result);
            }
            catch (Exception e)
            {
                string error = $"Error setting component property: {e.Message}";
                Debug.LogError($"[CommandExecutor] {error}");
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
                Debug.Log($"[CommandExecutor] Retrieved {components.Length} components");
                return (true, result);
            }
            catch (Exception e)
            {
                string error = $"Error getting components: {e.Message}";
                Debug.LogError($"[CommandExecutor] {error}");
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
                    Debug.LogError($"[CommandExecutor] {error}");
                    return (false, error);
                }

                if (string.IsNullOrEmpty(p.component))
                {
                    string error = "Missing required parameter: component";
                    Debug.LogError($"[CommandExecutor] {error}");
                    return (false, error);
                }

                if (string.IsNullOrEmpty(p.property))
                {
                    string error = "Missing required parameter: property";
                    Debug.LogError($"[CommandExecutor] {error}");
                    return (false, error);
                }

                if (string.IsNullOrEmpty(p.target_path))
                {
                    string error = "Missing required parameter: target_path";
                    Debug.LogError($"[CommandExecutor] {error}");
                    return (false, error);
                }

                // Get source object and component
                var (sourceObj, sourceFindError) = FindGameObjectByPath(p.path);
                if (sourceObj == null)
                {
                    string error = sourceFindError ?? $"Source GameObject not found: {p.path}";
                    Debug.LogWarning($"[CommandExecutor] {error}");
                    return (false, error);
                }

                var (componentType, typeError) = ResolveComponentTypeWithError(p.component);
                if (componentType == null)
                {
                    Debug.LogWarning($"[CommandExecutor] {typeError}");
                    return (false, typeError);
                }

                Component component = sourceObj.GetComponent(componentType);
                if (component == null)
                {
                    string error = $"{p.component} not found on {sourceObj.name}";
                    Debug.LogWarning($"[CommandExecutor] {error}");
                    return (false, error);
                }

                // Get property or field
                var bindingFlags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                var propertyInfo = componentType.GetProperty(p.property, bindingFlags);
                var fieldInfo = componentType.GetField(p.property, bindingFlags);

                if (propertyInfo == null && fieldInfo == null)
                {
                    string error = $"Property or field '{p.property}' not found on {p.component}";
                    Debug.LogWarning($"[CommandExecutor] {error}");
                    return (false, error);
                }

                Type memberType = propertyInfo?.PropertyType ?? fieldInfo.FieldType;

                // Get target object
                var (targetObj, targetFindError) = FindGameObjectByPath(p.target_path);
                if (targetObj == null)
                {
                    string error = targetFindError ?? $"Target GameObject not found: {p.target_path}";
                    Debug.LogWarning($"[CommandExecutor] {error}");
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
                    // Reference to specific Component type
                    referenceValue = targetObj.GetComponent(memberType);
                    if (referenceValue == null)
                    {
                        string error = $"Target '{p.target_path}' does not have component '{memberType.Name}'";
                        Debug.LogWarning($"[CommandExecutor] {error}");
                        return (false, error);
                    }
                    targetTypeName = memberType.Name;
                }
                else
                {
                    string error = $"Property '{p.property}' is not a GameObject or Component reference type (type: {memberType.Name})";
                    Debug.LogWarning($"[CommandExecutor] {error}");
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
                    Debug.LogWarning($"[CommandExecutor] {error}");
                    return (false, error);
                }

                EditorUtility.SetDirty(component);
                EditorUtility.SetDirty(sourceObj);

                string result = $"Set {sourceObj.name}.{p.component}.{p.property} = {targetObj.name} ({targetTypeName})";
                Debug.Log($"[CommandExecutor] {result}");
                return (true, result);
            }
            catch (Exception e)
            {
                string error = $"Error setting object reference: {e.Message}";
                Debug.LogError($"[CommandExecutor] {error}");
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
                    // Supports JSON format {"x":1,"y":2,"z":3} or "1,2,3" or "(1, 2, 3)" format
                    if (valueString.StartsWith("{"))
                    {
                        return JsonUtility.FromJson<Vector3>(valueString);
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
                            Debug.LogWarning($"[CommandExecutor] {errorMsg}");
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
                Debug.LogError($"[CommandExecutor] Error parsing value '{valueString}' to type {targetType.Name}: {e.Message}");
                throw; // Rethrown to caller's catch block as error
            }
        }
    }
}
