using UnityEngine;
using UnityEditor;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;

namespace ClaudeAgent
{
    public partial class CommandExecutor
    {
        // Cached URP types (resolved at runtime via reflection)
        private static Type _scriptableRendererDataType;
        private static Type _scriptableRendererFeatureType;
        private static bool _urpTypesResolved = false;
        private static bool _isUrpAvailable = false;

        private void RegisterURPCommands()
        {
            RegisterCommand("add_renderer_feature", AddRendererFeature);
            RegisterCommand("remove_renderer_feature", RemoveRendererFeature);
            RegisterCommand("get_renderer_features", GetRendererFeatures);
            RegisterCommand("set_renderer_feature_property", SetRendererFeatureProperty);
        }

        /// <summary>
        /// Resolves URP types via reflection (called once, cached)
        /// </summary>
        private static bool ResolveURPTypes()
        {
            if (_urpTypesResolved)
                return _isUrpAvailable;

            _urpTypesResolved = true;

            // Try to find URP types in loaded assemblies
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                string assemblyName = assembly.GetName().Name;

                // Check for URP Runtime assembly
                if (assemblyName == "Unity.RenderPipelines.Universal.Runtime")
                {
                    _scriptableRendererDataType = assembly.GetType("UnityEngine.Rendering.Universal.ScriptableRendererData");
                    _scriptableRendererFeatureType = assembly.GetType("UnityEngine.Rendering.Universal.ScriptableRendererFeature");

                    if (_scriptableRendererDataType != null && _scriptableRendererFeatureType != null)
                    {
                        _isUrpAvailable = true;
                        return true;
                    }
                }
            }

            // Fallback: search by type name across all assemblies
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    if (_scriptableRendererDataType == null)
                    {
                        _scriptableRendererDataType = assembly.GetType("UnityEngine.Rendering.Universal.ScriptableRendererData", false);
                    }
                    if (_scriptableRendererFeatureType == null)
                    {
                        _scriptableRendererFeatureType = assembly.GetType("UnityEngine.Rendering.Universal.ScriptableRendererFeature", false);
                    }

                    if (_scriptableRendererDataType != null && _scriptableRendererFeatureType != null)
                    {
                        _isUrpAvailable = true;
                        return true;
                    }
                }
                catch (ReflectionTypeLoadException)
                {
                    // Skip problematic assemblies
                }
            }

            return false;
        }

        /// <summary>
        /// Checks if URP is available and returns error message if not
        /// </summary>
        private (bool available, string error) CheckURPAvailable()
        {
            if (!ResolveURPTypes())
            {
                return (false, "URP is not installed or not detected. Please ensure the Universal Render Pipeline package is installed and the project uses URP.");
            }
            return (true, null);
        }

        /// <summary>
        /// Adds a ScriptableRendererFeature to a UniversalRendererData asset
        /// </summary>
        private (bool, string) AddRendererFeature(CommandParams p)
        {
            try
            {
                // Check URP availability
                var (available, urpError) = CheckURPAvailable();
                if (!available)
                    return Error(urpError);

                // Validate parameters
                if (string.IsNullOrEmpty(p.asset_path))
                    return Error("Missing required parameter: asset_path (path to UniversalRendererData asset)");

                if (string.IsNullOrEmpty(p.component))
                    return Error("Missing required parameter: component (ScriptableRendererFeature type name)");

                // Load the renderer data asset
                var rendererData = AssetDatabase.LoadAssetAtPath<ScriptableObject>(p.asset_path);
                if (rendererData == null)
                    return Error($"Asset not found at: {p.asset_path}");

                // Verify it's a ScriptableRendererData
                if (!_scriptableRendererDataType.IsAssignableFrom(rendererData.GetType()))
                    return Error($"Asset at {p.asset_path} is not a UniversalRendererData (found: {rendererData.GetType().Name})");

                // Find the feature type
                var featureType = FindRendererFeatureType(p.component);
                if (featureType == null)
                    return Error($"ScriptableRendererFeature type not found: {p.component}. Make sure the class exists and inherits from ScriptableRendererFeature.");

                // Check if this feature type already exists
                var existingFeatures = GetRendererFeaturesListInternal(rendererData);
                foreach (var existing in existingFeatures)
                {
                    if (existing != null && existing.GetType() == featureType)
                    {
                        return Error($"Renderer feature of type '{p.component}' already exists on {p.asset_path}");
                    }
                }

                // Create the feature instance
                var feature = ScriptableObject.CreateInstance(featureType);
                if (feature == null)
                    return Error($"Failed to create instance of {p.component}");

                feature.name = string.IsNullOrEmpty(p.name) ? featureType.Name : p.name;

                // IMPORTANT: Set HideFlags to prevent GC and ensure proper serialization
                feature.hideFlags = HideFlags.HideInHierarchy;

                // IMPORTANT: Add as sub-asset FIRST before referencing in SerializedObject
                AssetDatabase.AddObjectToAsset(feature, rendererData);

                // Add to the renderer data using SerializedObject
                var serializedObject = new SerializedObject(rendererData);
                var featuresProperty = serializedObject.FindProperty("m_RendererFeatures");

                if (featuresProperty == null)
                {
                    // Cleanup on failure
                    AssetDatabase.RemoveObjectFromAsset(feature);
                    UnityEngine.Object.DestroyImmediate(feature, true);
                    return Error("Could not find m_RendererFeatures property on renderer data");
                }

                // Add the feature to the array
                int newIndex = featuresProperty.arraySize;
                featuresProperty.InsertArrayElementAtIndex(newIndex);
                featuresProperty.GetArrayElementAtIndex(newIndex).objectReferenceValue = feature;

                // Also add to m_RendererFeatureMap if it exists
                var featureMapProperty = serializedObject.FindProperty("m_RendererFeatureMap");
                if (featureMapProperty != null)
                {
                    featureMapProperty.InsertArrayElementAtIndex(newIndex);
                    featureMapProperty.GetArrayElementAtIndex(newIndex).longValue = feature.GetInstanceID();
                }

                serializedObject.ApplyModifiedProperties();

                // Mark dirty and save
                EditorUtility.SetDirty(feature);
                EditorUtility.SetDirty(rendererData);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                ConsoleLog($"[CommandExecutor] Added renderer feature '{feature.name}' to {p.asset_path}");
                return Success($"Added renderer feature '{feature.name}' ({featureType.Name}) to {p.asset_path}");
            }
            catch (Exception e)
            {
                return Error($"Error adding renderer feature: {e.Message}");
            }
        }

        /// <summary>
        /// Removes a ScriptableRendererFeature from a UniversalRendererData asset
        /// </summary>
        private (bool, string) RemoveRendererFeature(CommandParams p)
        {
            try
            {
                // Check URP availability
                var (available, urpError) = CheckURPAvailable();
                if (!available)
                    return Error(urpError);

                // Validate parameters
                if (string.IsNullOrEmpty(p.asset_path))
                    return Error("Missing required parameter: asset_path (path to UniversalRendererData asset)");

                if (string.IsNullOrEmpty(p.name) && string.IsNullOrEmpty(p.component))
                    return Error("Missing required parameter: name (feature name) or component (feature type name)");

                // Load the renderer data asset
                var rendererData = AssetDatabase.LoadAssetAtPath<ScriptableObject>(p.asset_path);
                if (rendererData == null)
                    return Error($"Asset not found at: {p.asset_path}");

                if (!_scriptableRendererDataType.IsAssignableFrom(rendererData.GetType()))
                    return Error($"Asset at {p.asset_path} is not a UniversalRendererData");

                var serializedObject = new SerializedObject(rendererData);
                var featuresProperty = serializedObject.FindProperty("m_RendererFeatures");

                if (featuresProperty == null)
                    return Error("Could not find m_RendererFeatures property on renderer data");

                // Find the feature to remove
                int removeIndex = -1;
                ScriptableObject featureToRemove = null;

                for (int i = 0; i < featuresProperty.arraySize; i++)
                {
                    var featureObj = featuresProperty.GetArrayElementAtIndex(i).objectReferenceValue as ScriptableObject;
                    if (featureObj == null) continue;

                    bool match = false;
                    if (!string.IsNullOrEmpty(p.name) && featureObj.name == p.name)
                        match = true;
                    else if (!string.IsNullOrEmpty(p.component) && featureObj.GetType().Name == p.component)
                        match = true;

                    if (match)
                    {
                        removeIndex = i;
                        featureToRemove = featureObj;
                        break;
                    }
                }

                if (removeIndex < 0)
                {
                    string searchTerm = !string.IsNullOrEmpty(p.name) ? p.name : p.component;
                    return Error($"Renderer feature '{searchTerm}' not found on {p.asset_path}");
                }

                string removedName = featureToRemove.name;

                // Remove from array (set to null first, then delete)
                featuresProperty.GetArrayElementAtIndex(removeIndex).objectReferenceValue = null;
                featuresProperty.DeleteArrayElementAtIndex(removeIndex);

                // Also remove from m_RendererFeatureMap if it exists
                var featureMapProperty = serializedObject.FindProperty("m_RendererFeatureMap");
                if (featureMapProperty != null && removeIndex < featureMapProperty.arraySize)
                {
                    featureMapProperty.DeleteArrayElementAtIndex(removeIndex);
                }

                serializedObject.ApplyModifiedProperties();

                // Remove the sub-asset
                AssetDatabase.RemoveObjectFromAsset(featureToRemove);
                UnityEngine.Object.DestroyImmediate(featureToRemove, true);
                AssetDatabase.SaveAssets();

                EditorUtility.SetDirty(rendererData);

                ConsoleLog($"[CommandExecutor] Removed renderer feature '{removedName}' from {p.asset_path}");
                return Success($"Removed renderer feature '{removedName}' from {p.asset_path}");
            }
            catch (Exception e)
            {
                return Error($"Error removing renderer feature: {e.Message}");
            }
        }

        /// <summary>
        /// Gets the list of ScriptableRendererFeatures on a UniversalRendererData asset
        /// </summary>
        private (bool, string) GetRendererFeatures(CommandParams p)
        {
            try
            {
                // Check URP availability
                var (available, urpError) = CheckURPAvailable();
                if (!available)
                    return Error(urpError);

                // Validate parameters
                if (string.IsNullOrEmpty(p.asset_path))
                    return Error("Missing required parameter: asset_path (path to UniversalRendererData asset)");

                // Load the renderer data asset
                var rendererData = AssetDatabase.LoadAssetAtPath<ScriptableObject>(p.asset_path);
                if (rendererData == null)
                    return Error($"Asset not found at: {p.asset_path}");

                if (!_scriptableRendererDataType.IsAssignableFrom(rendererData.GetType()))
                    return Error($"Asset at {p.asset_path} is not a UniversalRendererData");

                var features = GetRendererFeaturesListInternal(rendererData);
                var result = new System.Text.StringBuilder();
                result.AppendLine($"Renderer Features on {p.asset_path}:");

                if (features.Count == 0)
                {
                    result.AppendLine("  (none)");
                }
                else
                {
                    for (int i = 0; i < features.Count; i++)
                    {
                        var feature = features[i];
                        if (feature == null)
                        {
                            result.AppendLine($"  [{i}] (null/missing)");
                        }
                        else
                        {
                            // Get isActive via reflection
                            string activeStatus = "unknown";
                            var isActiveProp = feature.GetType().GetProperty("isActive");
                            if (isActiveProp != null)
                            {
                                bool isActive = (bool)isActiveProp.GetValue(feature);
                                activeStatus = isActive ? "active" : "inactive";
                            }
                            result.AppendLine($"  [{i}] {feature.name} ({feature.GetType().Name}) - {activeStatus}");
                        }
                    }
                }

                return Success(result.ToString().TrimEnd());
            }
            catch (Exception e)
            {
                return Error($"Error getting renderer features: {e.Message}");
            }
        }

        /// <summary>
        /// Sets a property on a ScriptableRendererFeature
        /// </summary>
        private (bool, string) SetRendererFeatureProperty(CommandParams p)
        {
            try
            {
                // Check URP availability
                var (available, urpError) = CheckURPAvailable();
                if (!available)
                    return Error(urpError);

                // Validate parameters
                if (string.IsNullOrEmpty(p.asset_path))
                    return Error("Missing required parameter: asset_path (path to UniversalRendererData asset)");

                if (string.IsNullOrEmpty(p.name) && string.IsNullOrEmpty(p.component))
                    return Error("Missing required parameter: name (feature name) or component (feature type name)");

                if (string.IsNullOrEmpty(p.property))
                    return Error("Missing required parameter: property");

                if (string.IsNullOrEmpty(p.value))
                    return Error("Missing required parameter: value");

                // Load the renderer data asset
                var rendererData = AssetDatabase.LoadAssetAtPath<ScriptableObject>(p.asset_path);
                if (rendererData == null)
                    return Error($"Asset not found at: {p.asset_path}");

                if (!_scriptableRendererDataType.IsAssignableFrom(rendererData.GetType()))
                    return Error($"Asset at {p.asset_path} is not a UniversalRendererData");

                var features = GetRendererFeaturesListInternal(rendererData);

                // Find the feature
                ScriptableObject targetFeature = null;
                foreach (var feature in features)
                {
                    if (feature == null) continue;

                    bool match = false;
                    if (!string.IsNullOrEmpty(p.name) && feature.name == p.name)
                        match = true;
                    else if (!string.IsNullOrEmpty(p.component) && feature.GetType().Name == p.component)
                        match = true;

                    if (match)
                    {
                        targetFeature = feature;
                        break;
                    }
                }

                if (targetFeature == null)
                {
                    string searchTerm = !string.IsNullOrEmpty(p.name) ? p.name : p.component;
                    return Error($"Renderer feature '{searchTerm}' not found on {p.asset_path}");
                }

                // Handle special property: isActive (enable/disable feature)
                if (p.property.ToLower() == "isactive" || p.property.ToLower() == "active")
                {
                    bool active = p.value.ToLower() == "true" || p.value == "1";

                    // Call SetActive via reflection
                    var setActiveMethod = targetFeature.GetType().GetMethod("SetActive", BindingFlags.Public | BindingFlags.Instance);
                    if (setActiveMethod != null)
                    {
                        setActiveMethod.Invoke(targetFeature, new object[] { active });
                    }
                    else
                    {
                        // Fallback: try to set m_IsActive field directly
                        var serializedFeature = new SerializedObject(targetFeature);
                        var isActiveProp = serializedFeature.FindProperty("m_IsActive");
                        if (isActiveProp != null)
                        {
                            isActiveProp.boolValue = active;
                            serializedFeature.ApplyModifiedProperties();
                        }
                    }

                    EditorUtility.SetDirty(targetFeature);
                    AssetDatabase.SaveAssets();
                    return Success($"Set {targetFeature.name}.isActive = {active}");
                }

                // Set the property using SerializedObject for proper Undo support
                var serializedObj = new SerializedObject(targetFeature);
                var prop = serializedObj.FindProperty(p.property);

                if (prop == null)
                {
                    // Try with m_ prefix (Unity convention)
                    prop = serializedObj.FindProperty("m_" + p.property);
                }

                if (prop == null)
                {
                    // Try case-insensitive search
                    var iterator = serializedObj.GetIterator();
                    while (iterator.NextVisible(true))
                    {
                        if (iterator.name.Equals(p.property, StringComparison.OrdinalIgnoreCase) ||
                            iterator.name.Equals("m_" + p.property, StringComparison.OrdinalIgnoreCase))
                        {
                            prop = serializedObj.FindProperty(iterator.name);
                            break;
                        }
                    }
                }

                if (prop == null)
                    return Error($"Property '{p.property}' not found on feature '{targetFeature.name}'");

                // Set the property value based on type
                bool success = SetSerializedPropertyValueForURP(prop, p.value);
                if (!success)
                    return Error($"Failed to set property '{p.property}' to '{p.value}'. Type mismatch or invalid value.");

                serializedObj.ApplyModifiedProperties();
                EditorUtility.SetDirty(targetFeature);
                AssetDatabase.SaveAssets();

                ConsoleLog($"[CommandExecutor] Set {targetFeature.name}.{p.property} = {p.value}");
                return Success($"Set {targetFeature.name}.{p.property} = {p.value}");
            }
            catch (Exception e)
            {
                return Error($"Error setting renderer feature property: {e.Message}");
            }
        }

        /// <summary>
        /// Helper: Gets the list of renderer features from a ScriptableRendererData (via reflection)
        /// </summary>
        private List<ScriptableObject> GetRendererFeaturesListInternal(ScriptableObject rendererData)
        {
            var features = new List<ScriptableObject>();

            var serializedObject = new SerializedObject(rendererData);
            var featuresProperty = serializedObject.FindProperty("m_RendererFeatures");

            if (featuresProperty != null)
            {
                for (int i = 0; i < featuresProperty.arraySize; i++)
                {
                    var feature = featuresProperty.GetArrayElementAtIndex(i).objectReferenceValue as ScriptableObject;
                    features.Add(feature);
                }
            }

            return features;
        }

        /// <summary>
        /// Helper: Finds a ScriptableRendererFeature type by name
        /// </summary>
        private Type FindRendererFeatureType(string typeName)
        {
            if (_scriptableRendererFeatureType == null)
                return null;

            // Search all loaded assemblies for the type
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    // Try exact name
                    var type = assembly.GetType(typeName, false, true);
                    if (type != null && _scriptableRendererFeatureType.IsAssignableFrom(type))
                        return type;

                    // Try with common namespaces
                    string[] commonNamespaces = {
                        "UnityEngine.Rendering.Universal.",
                        "UnityEngine.Experimental.Rendering.Universal."
                    };

                    foreach (var ns in commonNamespaces)
                    {
                        type = assembly.GetType(ns + typeName, false, true);
                        if (type != null && _scriptableRendererFeatureType.IsAssignableFrom(type))
                            return type;
                    }

                    // Search by simple name
                    foreach (var t in assembly.GetTypes())
                    {
                        if (t.Name == typeName && _scriptableRendererFeatureType.IsAssignableFrom(t))
                            return t;
                    }
                }
                catch (ReflectionTypeLoadException)
                {
                    // Skip assemblies that can't be loaded
                }
            }

            return null;
        }

        /// <summary>
        /// Helper: Sets a SerializedProperty value from a string (URP version)
        /// </summary>
        private bool SetSerializedPropertyValueForURP(SerializedProperty prop, string value)
        {
            try
            {
                switch (prop.propertyType)
                {
                    case SerializedPropertyType.Boolean:
                        prop.boolValue = value.ToLower() == "true" || value == "1";
                        return true;

                    case SerializedPropertyType.Integer:
                        if (int.TryParse(value, out int intVal))
                        {
                            prop.intValue = intVal;
                            return true;
                        }
                        return false;

                    case SerializedPropertyType.Float:
                        if (float.TryParse(value, out float floatVal))
                        {
                            prop.floatValue = floatVal;
                            return true;
                        }
                        return false;

                    case SerializedPropertyType.String:
                        prop.stringValue = value;
                        return true;

                    case SerializedPropertyType.Enum:
                        // Try to parse enum by name or value
                        if (int.TryParse(value, out int enumIndex))
                        {
                            prop.enumValueIndex = enumIndex;
                            return true;
                        }
                        else
                        {
                            var names = prop.enumNames;
                            for (int i = 0; i < names.Length; i++)
                            {
                                if (names[i].Equals(value, StringComparison.OrdinalIgnoreCase))
                                {
                                    prop.enumValueIndex = i;
                                    return true;
                                }
                            }
                        }
                        return false;

                    case SerializedPropertyType.Color:
                        var (colorSuccess, color, _) = TryParseColor(value);
                        if (colorSuccess)
                        {
                            prop.colorValue = color;
                            return true;
                        }
                        return false;

                    case SerializedPropertyType.Vector2:
                        if (TryParseVector2ForURP(value, out Vector2 v2))
                        {
                            prop.vector2Value = v2;
                            return true;
                        }
                        return false;

                    case SerializedPropertyType.Vector3:
                        if (TryParseVector3ForURP(value, out Vector3 v3))
                        {
                            prop.vector3Value = v3;
                            return true;
                        }
                        return false;

                    case SerializedPropertyType.Vector4:
                        if (TryParseVector4ForURP(value, out Vector4 v4))
                        {
                            prop.vector4Value = v4;
                            return true;
                        }
                        return false;

                    case SerializedPropertyType.ObjectReference:
                        // Try to load asset by path
                        var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(value);
                        if (obj != null)
                        {
                            prop.objectReferenceValue = obj;
                            return true;
                        }
                        // Allow setting to null
                        if (string.IsNullOrEmpty(value) || value.ToLower() == "null")
                        {
                            prop.objectReferenceValue = null;
                            return true;
                        }
                        return false;

                    default:
                        return false;
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Helper: Parse Vector2 from string "[x, y]" or "x, y"
        /// </summary>
        private bool TryParseVector2ForURP(string value, out Vector2 result)
        {
            result = Vector2.zero;
            try
            {
                value = value.Trim('[', ']', '(', ')');
                var parts = value.Split(',');
                if (parts.Length >= 2 &&
                    float.TryParse(parts[0].Trim(), out float x) &&
                    float.TryParse(parts[1].Trim(), out float y))
                {
                    result = new Vector2(x, y);
                    return true;
                }
            }
            catch { }
            return false;
        }

        /// <summary>
        /// Helper: Parse Vector3 from string "[x, y, z]" or "x, y, z"
        /// </summary>
        private bool TryParseVector3ForURP(string value, out Vector3 result)
        {
            result = Vector3.zero;
            try
            {
                value = value.Trim('[', ']', '(', ')');
                var parts = value.Split(',');
                if (parts.Length >= 3 &&
                    float.TryParse(parts[0].Trim(), out float x) &&
                    float.TryParse(parts[1].Trim(), out float y) &&
                    float.TryParse(parts[2].Trim(), out float z))
                {
                    result = new Vector3(x, y, z);
                    return true;
                }
            }
            catch { }
            return false;
        }

        /// <summary>
        /// Helper: Parse Vector4 from string "[x, y, z, w]" or "x, y, z, w"
        /// </summary>
        private bool TryParseVector4ForURP(string value, out Vector4 result)
        {
            result = Vector4.zero;
            try
            {
                value = value.Trim('[', ']', '(', ')');
                var parts = value.Split(',');
                if (parts.Length >= 4 &&
                    float.TryParse(parts[0].Trim(), out float x) &&
                    float.TryParse(parts[1].Trim(), out float y) &&
                    float.TryParse(parts[2].Trim(), out float z) &&
                    float.TryParse(parts[3].Trim(), out float w))
                {
                    result = new Vector4(x, y, z, w);
                    return true;
                }
            }
            catch { }
            return false;
        }
    }
}
