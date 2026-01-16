using UnityEngine;
using UnityEditor;
using System;

namespace ClaudeAgent
{
    public partial class CommandExecutor
    {
        /// <summary>
        /// Registers light commands
        /// </summary>
        private void RegisterLightCommands()
        {
            RegisterCommand("create_light", CreateLight);
            RegisterCommand("light", LightCommand);
        }

        /// <summary>
        /// Unified light command (get/set integration)
        /// Use get: true to retrieve, or specify color/intensity/type to set
        /// </summary>
        private (bool, string) LightCommand(CommandParams p)
        {
            try
            {
                // Get GameObject + Light using common helper
                var (success, light, obj, error) = GetRequiredComponent<Light>(p);
                if (!success) return Warning(error);

                // Check for presence of set parameters
                bool hasColor = !string.IsNullOrEmpty(p.color) || (p.r >= 0 && p.g >= 0 && p.b >= 0);
                bool hasIntensity = p.intensity > 0;
                bool hasType = !string.IsNullOrEmpty(p.type);
                bool hasAnySetParam = hasColor || hasIntensity || hasType;

                // If get mode
                if (p.get)
                {
                    if (hasAnySetParam)
                        return Error("Cannot specify both 'get' and property values (color, intensity, type)");
                    return GetLightInfo(obj, light);
                }

                // If set mode
                if (!hasAnySetParam)
                    return Error("At least one property must be specified (color, intensity, or type), or use 'get: true' for retrieval");

                Undo.RecordObject(light, "Set Light Properties");

                var results = new System.Collections.Generic.List<string>();

                // Set type
                if (hasType)
                {
                    var (typeSuccess, lightType, typeError) = TryParseLightType(p.type);
                    if (!typeSuccess) return Error(typeError);
                    light.type = lightType;
                    results.Add($"type={p.type}");
                }

                // Set color
                if (!string.IsNullOrEmpty(p.color))
                {
                    var (colorSuccess, parsedColor, colorError) = TryParseColor(p.color);
                    if (!colorSuccess) return Error(colorError);
                    light.color = parsedColor;
                    results.Add($"color={p.color}");
                }
                else if (p.r >= 0 && p.g >= 0 && p.b >= 0)
                {
                    light.color = new Color(p.r, p.g, p.b);
                    results.Add($"color=({p.r}, {p.g}, {p.b})");
                }

                // Set intensity
                if (hasIntensity)
                {
                    light.intensity = p.intensity;
                    results.Add($"intensity={p.intensity}");
                }

                EditorUtility.SetDirty(light);

                return Success($"Set {obj.name} light: {string.Join(", ", results)}");
            }
            catch (Exception e)
            {
                return Error($"Error in light command: {e.Message}");
            }
        }

        /// <summary>
        /// Gets light information (internal helper)
        /// </summary>
        private (bool, string) GetLightInfo(GameObject obj, Light light)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Light info for: {obj.name}");
            sb.AppendLine();
            sb.AppendLine($"Type: {light.type}");
            sb.AppendLine($"Color: #{ColorUtility.ToHtmlStringRGBA(light.color)}");
            sb.AppendLine($"Intensity: {light.intensity}");
            sb.AppendLine($"Shadows: {light.shadows}");

            if (light.type == LightType.Point || light.type == LightType.Spot)
            {
                sb.AppendLine($"Range: {light.range}");
            }
            if (light.type == LightType.Spot)
            {
                sb.AppendLine($"Spot Angle: {light.spotAngle}");
            }

            string result = sb.ToString();
            Debug.Log($"[CommandExecutor] Retrieved light info for {obj.name}");
            return (true, result);
        }

        // ====== Light Operations ======

        /// <summary>
        /// Creates a light
        /// </summary>
        private (bool, string) CreateLight(CommandParams p)
        {
            try
            {
                if (p == null) return Error("Missing required parameters");

                // Parse light type
                var (typeSuccess, lightType, typeError) = TryParseLightType(p.type);
                if (!typeSuccess) return Error(typeError);

                // Check for name duplicates (before creation)
                string desiredName = !string.IsNullOrEmpty(p.name) ? p.name : lightType.ToString() + " Light";
                string duplicateError = CheckDuplicateRootName(desiredName);
                if (duplicateError != null) return Error(duplicateError);

                // Create light
                var lightObj = new GameObject(desiredName);
                var light = lightObj.AddComponent<Light>();
                light.type = lightType;

                // Set color
                if (!string.IsNullOrEmpty(p.color))
                {
                    var (colorSuccess, parsedColor, colorError) = TryParseColor(p.color);
                    if (!colorSuccess)
                    {
                        GameObject.DestroyImmediate(lightObj);
                        return Error(colorError);
                    }
                    light.color = parsedColor;
                }

                // Set intensity (if intensity parameter is provided)
                if (p.intensity > 0)
                {
                    light.intensity = p.intensity;
                }

                // Set position
                if (p.position != null && p.position.Length == 3)
                {
                    lightObj.transform.position = new Vector3(p.position[0], p.position[1], p.position[2]);
                }

                // Set rotation
                if (p.rotation != null && p.rotation.Length == 3)
                {
                    lightObj.transform.eulerAngles = new Vector3(p.rotation[0], p.rotation[1], p.rotation[2]);
                }

                // Register Undo
                Undo.RegisterCreatedObjectUndo(lightObj, "Create Light");

                return Success($"Created {lightType} light: {lightObj.name}");
            }
            catch (Exception e)
            {
                return Error($"Error creating light: {e.Message}");
            }
        }

        /// <summary>
        /// Converts string to LightType
        /// </summary>
        /// <returns>(success flag, LightType, error message)</returns>
        private (bool success, LightType type, string error) TryParseLightType(string type)
        {
            if (string.IsNullOrEmpty(type))
            {
                return (false, LightType.Directional, "Missing required parameter: type");
            }

            switch (type.ToLower())
            {
                case "directional":
                    return (true, LightType.Directional, null);
                case "point":
                    return (true, LightType.Point, null);
                case "spot":
                    return (true, LightType.Spot, null);
                case "area":
                    return (true, LightType.Rectangle, null);
                default:
                    return (false, LightType.Directional, $"Unknown light type: '{type}'. Valid types: directional, point, spot, area");
            }
        }
    }
}
