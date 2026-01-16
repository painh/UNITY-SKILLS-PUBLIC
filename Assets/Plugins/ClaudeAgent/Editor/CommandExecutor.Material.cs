using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

namespace ClaudeAgent
{
    public partial class CommandExecutor
    {
        /// <summary>
        /// Registers material commands
        /// </summary>
        private void RegisterMaterialCommands()
        {
            RegisterCommand("material", MaterialCommand);
            RegisterCommand("create_material", CreateMaterial);
        }

        /// <summary>
        /// Unified material command (get/set integration)
        /// get: true for retrieval, material_path/color/metallic/smoothness for setting
        /// shader for shader change, folder_path for batch processing
        /// </summary>
        private (bool, string) MaterialCommand(CommandParams p)
        {
            try
            {
                if (p == null)
                {
                    string error = "Missing parameters";
                    ConsoleLogError($"[CommandExecutor] {error}");
                    return (false, error);
                }

                // Folder batch processing mode
                if (!string.IsNullOrEmpty(p.folder_path))
                {
                    return BatchChangeMaterialShaders(p);
                }

                // Material asset direct operation mode (no path, has material_path)
                if (string.IsNullOrEmpty(p.path) && !string.IsNullOrEmpty(p.material_path))
                {
                    return MaterialAssetOperation(p);
                }

                // Existing GameObject-based operations below
                if (string.IsNullOrEmpty(p.path))
                {
                    string error = "Missing required parameter: path (or use folder_path for batch, material_path for asset operation)";
                    ConsoleLogError($"[CommandExecutor] {error}");
                    return (false, error);
                }

                var (obj, findError) = FindGameObjectByPath(p.path);
                if (obj == null)
                {
                    string error = findError ?? $"GameObject not found: {p.path}";
                    ConsoleLogWarning($"[CommandExecutor] {error}");
                    return (false, error);
                }

                // Get Renderer component
                Renderer renderer = obj.GetComponent<Renderer>();
                if (renderer == null)
                {
                    string error = $"Renderer component not found on {obj.name}";
                    ConsoleLogWarning($"[CommandExecutor] {error}");
                    return (false, error);
                }

                // Check for presence of set parameters
                bool hasMaterialPath = !string.IsNullOrEmpty(p.material_path);
                bool hasColorString = !string.IsNullOrEmpty(p.color);
                bool hasColorRGB = p.r >= 0 && p.g >= 0 && p.b >= 0;
                bool hasColor = hasColorString || hasColorRGB;
                bool hasMetallic = p.metallic >= 0f;
                bool hasSmoothness = p.smoothness >= 0f;
                bool hasAnySetParam = hasMaterialPath || hasColor || hasMetallic || hasSmoothness;

                // If get mode
                if (p.get)
                {
                    // Check for simultaneous get and set parameters
                    if (hasAnySetParam)
                    {
                        string error = "Cannot specify both 'get' and property values (material_path, color, metallic, smoothness)";
                        ConsoleLogError($"[CommandExecutor] {error}");
                        return (false, error);
                    }

                    return GetMaterialInfo(obj, renderer);
                }

                // If set mode
                if (!hasAnySetParam)
                {
                    string error = "At least one property must be specified (material_path, color, metallic, or smoothness), or use 'get: true' for retrieval";
                    ConsoleLogError($"[CommandExecutor] {error}");
                    return (false, error);
                }

                var results = new List<string>();
                Material material = renderer.sharedMaterial;

                // If material_path is specified, apply material first
                if (hasMaterialPath)
                {
                    Material newMaterial = AssetDatabase.LoadAssetAtPath<Material>(p.material_path);
                    if (newMaterial == null)
                    {
                        string error = $"Material not found at path: {p.material_path}";
                        ConsoleLogWarning($"[CommandExecutor] {error}");
                        return (false, error);
                    }
                    Undo.RecordObject(renderer, "Set Material");
                    renderer.sharedMaterial = newMaterial;
                    material = newMaterial;
                    results.Add($"material={p.material_path}");
                }

                // Create material copy when changing properties
                if ((hasColor || hasMetallic || hasSmoothness) && material != null)
                {
                    // Instantiate asset material for modification (to not modify original asset)
                    string assetPath = AssetDatabase.GetAssetPath(material);
                    bool isAssetMaterial = !string.IsNullOrEmpty(assetPath);

                    if (isAssetMaterial)
                    {
                        // Create copy if asset material
                        material = new Material(material);
                        renderer.sharedMaterial = material;
                    }
                    else if (material == null)
                    {
                        material = new Material(Shader.Find("Standard"));
                        renderer.sharedMaterial = material;
                    }

                    Undo.RecordObject(renderer, "Set Material Properties");

                    if (hasColor)
                    {
                        Color finalColor;
                        string colorResult;

                        if (hasColorString)
                        {
                            var (colorSuccess, parsedColor, colorError) = TryParseColor(p.color);
                            if (!colorSuccess)
                            {
                                ConsoleLogError($"[CommandExecutor] {colorError}");
                                return (false, colorError);
                            }
                            finalColor = parsedColor;
                            colorResult = p.color;
                        }
                        else
                        {
                            // Create color from r, g, b parameters
                            float alpha = p.a > 0 ? p.a : 1f;
                            finalColor = new Color(p.r, p.g, p.b, alpha);
                            colorResult = $"({p.r}, {p.g}, {p.b}, {alpha})";
                        }

                        // Set to appropriate property based on shader
                        if (material.HasProperty("_BaseColor"))
                        {
                            material.SetColor("_BaseColor", finalColor);  // URP Lit
                        }
                        else if (material.HasProperty("_Color"))
                        {
                            material.SetColor("_Color", finalColor);  // Standard
                        }
                        else
                        {
                            material.color = finalColor;  // Fallback
                        }
                        results.Add($"color={colorResult}");
                    }

                    if (hasMetallic && material.HasProperty("_Metallic"))
                    {
                        material.SetFloat("_Metallic", Mathf.Clamp01(p.metallic));
                        results.Add($"metallic={p.metallic}");
                    }

                    if (hasSmoothness)
                    {
                        // Standard: _Glossiness, URP: _Smoothness
                        if (material.HasProperty("_Glossiness"))
                        {
                            material.SetFloat("_Glossiness", Mathf.Clamp01(p.smoothness));
                            results.Add($"smoothness={p.smoothness}");
                        }
                        else if (material.HasProperty("_Smoothness"))
                        {
                            material.SetFloat("_Smoothness", Mathf.Clamp01(p.smoothness));
                            results.Add($"smoothness={p.smoothness}");
                        }
                    }
                }

                EditorUtility.SetDirty(renderer);

                string result = $"Set {obj.name} material: {string.Join(", ", results)}";
                ConsoleLog($"[CommandExecutor] {result}");
                return (true, result);
            }
            catch (Exception e)
            {
                string error = $"Error in material command: {e.Message}";
                ConsoleLogError($"[CommandExecutor] {error}");
                return (false, error);
            }
        }

        /// <summary>
        /// Gets material information (internal helper)
        /// </summary>
        private (bool, string) GetMaterialInfo(GameObject obj, Renderer renderer)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Material info for: {obj.name}");
            sb.AppendLine();

            Material material = renderer.sharedMaterial;
            if (material == null)
            {
                sb.AppendLine("No material assigned");
            }
            else
            {
                sb.AppendLine($"Material Name: {material.name}");
                sb.AppendLine($"Shader: {material.shader.name}");
                sb.AppendLine();

                // Color info
                if (material.HasProperty("_Color"))
                {
                    Color color = material.color;
                    sb.AppendLine($"Color: #{ColorUtility.ToHtmlStringRGBA(color)}");
                }
                else if (material.HasProperty("_BaseColor"))
                {
                    Color color = material.GetColor("_BaseColor");
                    sb.AppendLine($"Color: #{ColorUtility.ToHtmlStringRGBA(color)}");
                }

                // Metallic/Smoothness
                if (material.HasProperty("_Metallic"))
                {
                    sb.AppendLine($"Metallic: {material.GetFloat("_Metallic"):F2}");
                }
                if (material.HasProperty("_Glossiness"))
                {
                    sb.AppendLine($"Smoothness: {material.GetFloat("_Glossiness"):F2}");
                }
                else if (material.HasProperty("_Smoothness"))
                {
                    sb.AppendLine($"Smoothness: {material.GetFloat("_Smoothness"):F2}");
                }

                sb.AppendLine();

                // Asset info
                string assetPath = AssetDatabase.GetAssetPath(material);
                if (!string.IsNullOrEmpty(assetPath))
                {
                    sb.AppendLine($"Asset Path: {assetPath}");
                }
                else
                {
                    sb.AppendLine("Asset Path: <instance material>");
                }
            }

            string result = sb.ToString();
            ConsoleLog($"[CommandExecutor] Retrieved material info for {obj.name}");
            return (true, result);
        }

        /// <summary>
        /// Creates a new material
        /// </summary>
        private (bool, string) CreateMaterial(CommandParams p)
        {
            try
            {
                if (p == null || string.IsNullOrEmpty(p.material_path))
                {
                    string error = "Missing required parameter: material_path";
                    ConsoleLogError($"[CommandExecutor] {error}");
                    return (false, error);
                }

                // Get shader (auto-detect URP and select default)
                string shaderName;
                if (string.IsNullOrEmpty(p.shader_name))
                {
                    // Check if using URP
                    var currentRP = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
                    if (currentRP != null && currentRP.GetType().Name.Contains("Universal"))
                    {
                        shaderName = "Universal Render Pipeline/Lit";
                    }
                    else
                    {
                        shaderName = "Standard";
                    }
                }
                else
                {
                    shaderName = p.shader_name;
                }
                Shader shader = Shader.Find(shaderName);
                if (shader == null)
                {
                    string error = $"Shader not found: {shaderName}";
                    ConsoleLogWarning($"[CommandExecutor] {error}");
                    return (false, error);
                }

                // Create new material
                Material material = new Material(shader);

                // Set name
                if (!string.IsNullOrEmpty(p.material_name))
                {
                    material.name = p.material_name;
                }

                // Set color
                if (!string.IsNullOrEmpty(p.color))
                {
                    var (colorSuccess, parsedColor, colorError) = TryParseColor(p.color);
                    if (!colorSuccess)
                    {
                        ConsoleLogError($"[CommandExecutor] {colorError}");
                        return (false, colorError);
                    }
                    // Set to appropriate property based on shader
                    if (material.HasProperty("_BaseColor"))
                    {
                        material.SetColor("_BaseColor", parsedColor);  // URP Lit
                    }
                    else if (material.HasProperty("_Color"))
                    {
                        material.SetColor("_Color", parsedColor);  // Standard
                    }
                    else
                    {
                        material.color = parsedColor;  // Fallback
                    }
                }

                // Set Metallic/Smoothness (for Standard/URP shaders)
                if (p.metallic >= 0f)
                {
                    if (material.HasProperty("_Metallic"))
                        material.SetFloat("_Metallic", Mathf.Clamp01(p.metallic));
                }
                if (p.smoothness >= 0f)
                {
                    // Standard: _Glossiness, URP: _Smoothness
                    if (material.HasProperty("_Glossiness"))
                        material.SetFloat("_Glossiness", Mathf.Clamp01(p.smoothness));
                    else if (material.HasProperty("_Smoothness"))
                        material.SetFloat("_Smoothness", Mathf.Clamp01(p.smoothness));
                }

                // Check if directory exists
                EnsureDirectoryExists(p.material_path);

                // Save material as asset
                AssetDatabase.CreateAsset(material, p.material_path);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                string result = $"Created material at: {p.material_path} (Shader: {shaderName})";
                ConsoleLog($"[CommandExecutor] {result}");
                return (true, result);
            }
            catch (Exception e)
            {
                string error = $"Error creating material: {e.Message}";
                ConsoleLogError($"[CommandExecutor] {error}");
                return (false, error);
            }
        }

        /// <summary>
        /// Direct operation on material asset (no path, has material_path)
        /// get: true for info retrieval, shader to change shader
        /// </summary>
        private (bool, string) MaterialAssetOperation(CommandParams p)
        {
            try
            {
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(p.material_path);
                if (mat == null)
                {
                    string error = $"Material not found at path: {p.material_path}";
                    ConsoleLogWarning($"[CommandExecutor] {error}");
                    return (false, error);
                }

                // If get mode
                if (p.get)
                {
                    // Check for simultaneous get and shader specification
                    if (!string.IsNullOrEmpty(p.shader))
                    {
                        string error = "Cannot specify both 'get' and 'shader'";
                        ConsoleLogError($"[CommandExecutor] {error}");
                        return (false, error);
                    }
                    return GetMaterialAssetInfo(mat, p.material_path);
                }

                // Change shader
                if (!string.IsNullOrEmpty(p.shader))
                {
                    return ChangeMaterialShader(mat, p.shader, p.material_path);
                }

                string err = "No operation specified. Use 'get: true' for info or 'shader' to change shader";
                ConsoleLogError($"[CommandExecutor] {err}");
                return (false, err);
            }
            catch (Exception e)
            {
                string error = $"Error in material asset operation: {e.Message}";
                ConsoleLogError($"[CommandExecutor] {error}");
                return (false, error);
            }
        }

        /// <summary>
        /// Batch change material shaders in a folder
        /// </summary>
        private (bool, string) BatchChangeMaterialShaders(CommandParams p)
        {
            try
            {
                if (string.IsNullOrEmpty(p.shader))
                {
                    string error = "shader parameter is required for batch operation";
                    ConsoleLogError($"[CommandExecutor] {error}");
                    return (false, error);
                }

                // Check if folder exists
                if (!AssetDatabase.IsValidFolder(p.folder_path))
                {
                    string error = $"Folder not found: {p.folder_path}";
                    ConsoleLogWarning($"[CommandExecutor] {error}");
                    return (false, error);
                }

                // Check target shader
                Shader newShader = Shader.Find(p.shader);
                if (newShader == null)
                {
                    string error = $"Shader not found: {p.shader}";
                    ConsoleLogWarning($"[CommandExecutor] {error}");
                    return (false, error);
                }

                // Get materials in folder
                string[] guids = AssetDatabase.FindAssets("t:Material", new[] { p.folder_path });

                int changed = 0;
                int skipped = 0;
                int errors = 0;
                var changedMaterials = new List<string>();

                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);

                    // If recursive: false, skip subfolders
                    if (!p.recursive)
                    {
                        string parentFolder = Path.GetDirectoryName(path).Replace("\\", "/");
                        if (parentFolder != p.folder_path)
                        {
                            continue;
                        }
                    }

                    Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (mat == null)
                    {
                        errors++;
                        continue;
                    }

                    // from_shader filter
                    if (!string.IsNullOrEmpty(p.from_shader))
                    {
                        if (mat.shader.name != p.from_shader)
                        {
                            skipped++;
                            continue;
                        }
                    }

                    // Change shader
                    Undo.RecordObject(mat, "Batch Change Shader");
                    mat.shader = newShader;
                    EditorUtility.SetDirty(mat);
                    changed++;
                    changedMaterials.Add(Path.GetFileName(path));
                }

                AssetDatabase.SaveAssets();

                var sb = new StringBuilder();
                sb.AppendLine($"Batch shader change completed:");
                sb.AppendLine($"  Folder: {p.folder_path}");
                sb.AppendLine($"  Target shader: {p.shader}");
                if (!string.IsNullOrEmpty(p.from_shader))
                {
                    sb.AppendLine($"  Filter (from_shader): {p.from_shader}");
                }
                sb.AppendLine($"  Recursive: {p.recursive}");
                sb.AppendLine($"  Changed: {changed}");
                sb.AppendLine($"  Skipped: {skipped}");
                if (errors > 0)
                {
                    sb.AppendLine($"  Errors: {errors}");
                }
                if (changed > 0 && changed <= 10)
                {
                    sb.AppendLine($"  Materials: {string.Join(", ", changedMaterials)}");
                }

                string result = sb.ToString();
                ConsoleLog($"[CommandExecutor] {result}");
                return (true, result);
            }
            catch (Exception e)
            {
                string error = $"Error in batch shader change: {e.Message}";
                ConsoleLogError($"[CommandExecutor] {error}");
                return (false, error);
            }
        }

        /// <summary>
        /// Gets material asset information (helper)
        /// </summary>
        private (bool, string) GetMaterialAssetInfo(Material mat, string assetPath)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Material Asset Info:");
            sb.AppendLine();
            sb.AppendLine($"Name: {mat.name}");
            sb.AppendLine($"Shader: {mat.shader.name}");
            sb.AppendLine($"Asset Path: {assetPath}");
            sb.AppendLine();

            // Color info
            if (mat.HasProperty("_Color"))
            {
                Color color = mat.color;
                sb.AppendLine($"Color: #{ColorUtility.ToHtmlStringRGBA(color)}");
            }
            else if (mat.HasProperty("_BaseColor"))
            {
                Color color = mat.GetColor("_BaseColor");
                sb.AppendLine($"Color: #{ColorUtility.ToHtmlStringRGBA(color)}");
            }

            // Metallic/Smoothness
            if (mat.HasProperty("_Metallic"))
            {
                sb.AppendLine($"Metallic: {mat.GetFloat("_Metallic"):F2}");
            }
            if (mat.HasProperty("_Glossiness"))
            {
                sb.AppendLine($"Smoothness: {mat.GetFloat("_Glossiness"):F2}");
            }
            else if (mat.HasProperty("_Smoothness"))
            {
                sb.AppendLine($"Smoothness: {mat.GetFloat("_Smoothness"):F2}");
            }

            // Render Queue
            sb.AppendLine($"Render Queue: {mat.renderQueue}");

            string result = sb.ToString();
            ConsoleLog($"[CommandExecutor] Retrieved material asset info for {mat.name}");
            return (true, result);
        }

        /// <summary>
        /// Changes material shader (helper)
        /// </summary>
        private (bool, string) ChangeMaterialShader(Material mat, string shaderName, string assetPath)
        {
            Shader newShader = Shader.Find(shaderName);
            if (newShader == null)
            {
                string error = $"Shader not found: {shaderName}";
                ConsoleLogWarning($"[CommandExecutor] {error}");
                return (false, error);
            }

            string oldShaderName = mat.shader.name;
            Undo.RecordObject(mat, "Change Material Shader");
            mat.shader = newShader;
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();

            string result = $"Changed shader of '{mat.name}' from '{oldShaderName}' to '{shaderName}'";
            ConsoleLog($"[CommandExecutor] {result}");
            return (true, result);
        }

    }
}
