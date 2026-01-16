using UnityEngine;
using UnityEditor;
using System;

namespace ClaudeAgent
{
    public partial class CommandExecutor
    {
        /// <summary>
        /// Registers terrain commands
        /// </summary>
        private void RegisterTerrainCommands()
        {
            RegisterCommand("create_terrain", CreateTerrain);
            RegisterCommand("add_terrain_layer", AddTerrainLayer);
            RegisterCommand("terrain_height", TerrainHeightCommand);
            RegisterCommand("terrain_texture", TerrainTextureCommand);
        }

        // ====== Terrain Operations ======

        /// <summary>
        /// Creates a terrain
        /// </summary>
        private (bool, string) CreateTerrain(CommandParams p)
        {
            try
            {
                if (p == null)
                {
                    string error = "Missing required parameters";
                    ConsoleLogError($"[CommandExecutor] {error}");
                    return (false, error);
                }

                // Smart default: with parent -> local, without parent -> world
                string defaultSpace = string.IsNullOrEmpty(p.parent) ? "world" : "local";

                // Validate space parameter (before object creation)
                string posSpace = p.position_space;
                var (posValid, posError) = ValidateSpaceParameter("position", p.position, ref posSpace, defaultSpace);
                if (!posValid)
                {
                    ConsoleLogError($"[CommandExecutor] {posError}");
                    return (false, posError);
                }

                // Default values
                float width = p.terrain_width > 0 ? p.terrain_width : 100f;
                float length = p.terrain_length > 0 ? p.terrain_length : 100f;
                float height = p.terrain_height > 0 ? p.terrain_height : 50f;
                int resolution = p.heightmap_resolution > 0 ? p.heightmap_resolution : 513;

                // Validate resolution (must be 2^n + 1)
                if (!IsValidHeightmapResolution(resolution))
                {
                    string error = $"Invalid heightmap_resolution: {resolution}. Must be 2^n + 1 (e.g., 33, 65, 129, 257, 513, 1025)";
                    ConsoleLogError($"[CommandExecutor] {error}");
                    return (false, error);
                }

                // Check for name duplicates
                string desiredName = !string.IsNullOrEmpty(p.name) ? p.name : "Terrain";
                string duplicateError = CheckDuplicateRootName(desiredName);
                if (duplicateError != null)
                {
                    ConsoleLogError($"[CommandExecutor] {duplicateError}");
                    return (false, duplicateError);
                }

                // Create TerrainData
                TerrainData terrainData = new TerrainData();
                terrainData.heightmapResolution = resolution;
                terrainData.size = new Vector3(width, height, length);

                // Save TerrainData as asset
                string assetPath = $"Assets/ClaudeAgent/Generated/Terrains/{desiredName}_Data.asset";
                EnsureDirectoryExists(assetPath);
                AssetDatabase.CreateAsset(terrainData, assetPath);

                // Create Terrain object
                GameObject terrainObj = Terrain.CreateTerrainGameObject(terrainData);
                terrainObj.name = desiredName;

                // Set parent (before transform settings)
                string parentInfo = "";
                if (!string.IsNullOrEmpty(p.parent))
                {
                    var (parentObj, parentFindError) = FindGameObjectByPath(p.parent);
                    if (parentObj == null)
                    {
                        // Error if parent not found (no fallback)
                        GameObject.DestroyImmediate(terrainObj);
                        AssetDatabase.DeleteAsset(assetPath);
                        string error = parentFindError ?? $"Parent GameObject not found: {p.parent}";
                        ConsoleLogError($"[CommandExecutor] {error}");
                        return (false, error);
                    }
                    terrainObj.transform.SetParent(parentObj.transform, false);
                    parentInfo = $", Parent: {p.parent}";
                }

                // Set position (based on space specification)
                if (p.position != null && p.position.Length == 3)
                {
                    Vector3 pos = new Vector3(p.position[0], p.position[1], p.position[2]);
                    if (posSpace == "local")
                    {
                        terrainObj.transform.localPosition = pos;
                    }
                    else // "world"
                    {
                        terrainObj.transform.position = pos;
                    }
                }

                // Register Undo
                Undo.RegisterCreatedObjectUndo(terrainObj, "Create Terrain");

                // Used space info
                string spaceInfo = "";
                if (p.position != null && p.position.Length == 3)
                {
                    spaceInfo = $" (position: {posSpace})";
                }

                string result = $"Created terrain: {desiredName} (Size: {width}x{length}x{height}, Resolution: {resolution}{parentInfo}){spaceInfo}";
                ConsoleLog($"[CommandExecutor] {result}");
                return (true, result);
            }
            catch (Exception e)
            {
                string error = $"Error creating terrain: {e.Message}";
                ConsoleLogError($"[CommandExecutor] {error}");
                return (false, error);
            }
        }

        /// <summary>
        /// Unified terrain height command (get/set/paint integration)
        /// Use get: true for info, flatten_height/heights for setting, center/radius/height_delta for painting
        /// </summary>
        private (bool, string) TerrainHeightCommand(CommandParams p)
        {
            try
            {
                if (p == null || string.IsNullOrEmpty(p.path))
                {
                    string error = "Missing required parameter: path";
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

                Terrain terrain = obj.GetComponent<Terrain>();
                if (terrain == null)
                {
                    string error = $"Terrain component not found on: {p.path}";
                    ConsoleLogWarning($"[CommandExecutor] {error}");
                    return (false, error);
                }

                TerrainData terrainData = terrain.terrainData;
                if (terrainData == null)
                {
                    string error = $"TerrainData not found for: {p.path}";
                    ConsoleLogWarning($"[CommandExecutor] {error}");
                    return (false, error);
                }

                // Check for presence of set parameters
                bool hasHeights = p.heights != null && p.heights.Length > 0;
                bool hasFlattenHeight = p.flatten_height >= 0;
                bool hasCenter = p.center != null && p.center.Length == 2;
                bool hasRadius = p.radius > 0;
                bool hasHeightDelta = p.height_delta != 0;
                bool hasPaintParams = hasCenter && hasRadius && hasHeightDelta;
                bool hasAnySetParam = hasHeights || hasFlattenHeight || hasPaintParams;

                // If get mode
                if (p.get)
                {
                    // Check for simultaneous specification of get and set parameters
                    if (hasAnySetParam)
                    {
                        string error = "Cannot specify both 'get' and height modification parameters";
                        ConsoleLogError($"[CommandExecutor] {error}");
                        return (false, error);
                    }

                    return GetTerrainInfo(obj, terrain, terrainData);
                }

                // If set mode
                if (!hasAnySetParam)
                {
                    string error = "At least one parameter must be specified (heights, flatten_height, or center+radius+height_delta), or use 'get: true' for retrieval";
                    ConsoleLogError($"[CommandExecutor] {error}");
                    return (false, error);
                }

                // Paint mode (center, radius, height_delta)
                if (hasPaintParams)
                {
                    return PaintTerrainHeightInternal(obj, terrain, terrainData, p);
                }

                // Set mode (heights or flatten_height)
                Undo.RecordObject(terrainData, "Set Terrain Height");
                int resolution = terrainData.heightmapResolution;

                // If heights array is specified
                if (hasHeights)
                {
                    // Convert 1D array to 2D
                    int expectedLength = resolution * resolution;
                    if (p.heights.Length != expectedLength)
                    {
                        string error = $"Invalid heights array length: {p.heights.Length}. Expected: {expectedLength} ({resolution}x{resolution})";
                        ConsoleLogError($"[CommandExecutor] {error}");
                        return (false, error);
                    }

                    float[,] heights2D = new float[resolution, resolution];
                    for (int y = 0; y < resolution; y++)
                    {
                        for (int x = 0; x < resolution; x++)
                        {
                            heights2D[y, x] = p.heights[y * resolution + x];
                        }
                    }
                    terrainData.SetHeights(0, 0, heights2D);

                    string result = $"Set terrain heights for {obj.name} from array";
                    ConsoleLog($"[CommandExecutor] {result}");
                    return (true, result);
                }
                // Flatten with single height value
                else if (hasFlattenHeight)
                {
                    float normalizedHeight = p.flatten_height / terrainData.size.y;
                    normalizedHeight = Mathf.Clamp01(normalizedHeight);

                    float[,] heights2D = new float[resolution, resolution];
                    for (int y = 0; y < resolution; y++)
                    {
                        for (int x = 0; x < resolution; x++)
                        {
                            heights2D[y, x] = normalizedHeight;
                        }
                    }
                    terrainData.SetHeights(0, 0, heights2D);

                    EditorUtility.SetDirty(terrainData);

                    string result = $"Flattened terrain {obj.name} to height: {p.flatten_height}";
                    ConsoleLog($"[CommandExecutor] {result}");
                    return (true, result);
                }

                string fallbackError = "No valid height parameters provided";
                ConsoleLogError($"[CommandExecutor] {fallbackError}");
                return (false, fallbackError);
            }
            catch (Exception e)
            {
                string error = $"Error in terrain_height command: {e.Message}";
                ConsoleLogError($"[CommandExecutor] {error}");
                return (false, error);
            }
        }

        /// <summary>
        /// Gets terrain information (internal helper)
        /// </summary>
        private (bool, string) GetTerrainInfo(GameObject obj, Terrain terrain, TerrainData terrainData)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Terrain Info for '{obj.name}':");
            sb.AppendLine();
            sb.AppendLine($"Size: {terrainData.size.x} x {terrainData.size.z} (Width x Length)");
            sb.AppendLine($"Max Height: {terrainData.size.y}");
            sb.AppendLine($"Heightmap Resolution: {terrainData.heightmapResolution}");
            sb.AppendLine($"Position: {obj.transform.position}");
            sb.AppendLine();

            // TerrainLayers info
            if (terrainData.terrainLayers != null && terrainData.terrainLayers.Length > 0)
            {
                sb.AppendLine($"Terrain Layers ({terrainData.terrainLayers.Length}):");
                for (int i = 0; i < terrainData.terrainLayers.Length; i++)
                {
                    var layer = terrainData.terrainLayers[i];
                    string textureName = layer.diffuseTexture != null ? layer.diffuseTexture.name : "(none)";
                    sb.AppendLine($"  [{i}] {textureName} (Tile: {layer.tileSize.x}x{layer.tileSize.y})");
                }
            }
            else
            {
                sb.AppendLine("Terrain Layers: (none)");
            }

            // TerrainData asset path
            string assetPath = AssetDatabase.GetAssetPath(terrainData);
            if (!string.IsNullOrEmpty(assetPath))
            {
                sb.AppendLine();
                sb.AppendLine($"TerrainData Asset: {assetPath}");
            }

            string result = sb.ToString();
            ConsoleLog($"[CommandExecutor] Retrieved terrain info for: {obj.name}");
            return (true, result);
        }

        /// <summary>
        /// Modifies part of terrain height (internal helper)
        /// </summary>
        private (bool, string) PaintTerrainHeightInternal(GameObject obj, Terrain terrain, TerrainData terrainData, CommandParams p)
        {
            Undo.RecordObject(terrainData, "Paint Terrain Height");

            int resolution = terrainData.heightmapResolution;
            Vector3 terrainSize = terrainData.size;
            Vector3 terrainPos = obj.transform.position;

            // Convert world coordinates to normalized coordinates within terrain
            float centerX = (p.center[0] - terrainPos.x) / terrainSize.x;
            float centerZ = (p.center[1] - terrainPos.z) / terrainSize.z;

            // Normalized radius
            float radiusX = p.radius / terrainSize.x;
            float radiusZ = p.radius / terrainSize.z;

            // Height delta (normalized)
            float heightDelta = p.height_delta / terrainSize.y;

            // Get current heights
            float[,] heights = terrainData.GetHeights(0, 0, resolution, resolution);

            // Calculate affected area
            int minX = Mathf.Max(0, Mathf.FloorToInt((centerX - radiusX) * (resolution - 1)));
            int maxX = Mathf.Min(resolution - 1, Mathf.CeilToInt((centerX + radiusX) * (resolution - 1)));
            int minZ = Mathf.Max(0, Mathf.FloorToInt((centerZ - radiusZ) * (resolution - 1)));
            int maxZ = Mathf.Min(resolution - 1, Mathf.CeilToInt((centerZ + radiusZ) * (resolution - 1)));

            // Falloff type
            string falloff = !string.IsNullOrEmpty(p.falloff) ? p.falloff.ToLower() : "smooth";

            int modifiedCount = 0;
            for (int z = minZ; z <= maxZ; z++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    // Normalized coordinates
                    float nx = (float)x / (resolution - 1);
                    float nz = (float)z / (resolution - 1);

                    // Distance from center
                    float distX = (nx - centerX) / radiusX;
                    float distZ = (nz - centerZ) / radiusZ;
                    float dist = Mathf.Sqrt(distX * distX + distZ * distZ);

                    if (dist <= 1.0f)
                    {
                        float factor = 1.0f;
                        switch (falloff)
                        {
                            case "linear":
                                factor = 1.0f - dist;
                                break;
                            case "flat":
                                factor = 1.0f;
                                break;
                            case "smooth":
                            default:
                                // Smooth falloff (cosine interpolation)
                                factor = (Mathf.Cos(dist * Mathf.PI) + 1.0f) * 0.5f;
                                break;
                        }

                        heights[z, x] = Mathf.Clamp01(heights[z, x] + heightDelta * factor);
                        modifiedCount++;
                    }
                }
            }

            terrainData.SetHeights(0, 0, heights);
            EditorUtility.SetDirty(terrainData);

            string action = p.height_delta >= 0 ? "raised" : "lowered";
            string result = $"Painted terrain height: {action} area at ({p.center[0]}, {p.center[1]}) with radius {p.radius}, delta {p.height_delta} ({modifiedCount} points modified)";
            ConsoleLog($"[CommandExecutor] {result}");
            return (true, result);
        }

        /// <summary>
        /// Adds a texture layer to terrain
        /// </summary>
        private (bool, string) AddTerrainLayer(CommandParams p)
        {
            try
            {
                if (p == null || string.IsNullOrEmpty(p.path))
                {
                    string error = "Missing required parameter: path";
                    ConsoleLogError($"[CommandExecutor] {error}");
                    return (false, error);
                }

                if (string.IsNullOrEmpty(p.texture_path))
                {
                    string error = "Missing required parameter: texture_path";
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

                Terrain terrain = obj.GetComponent<Terrain>();
                if (terrain == null)
                {
                    string error = $"Terrain component not found on: {p.path}";
                    ConsoleLogWarning($"[CommandExecutor] {error}");
                    return (false, error);
                }

                TerrainData terrainData = terrain.terrainData;
                if (terrainData == null)
                {
                    string error = $"TerrainData not found for: {p.path}";
                    ConsoleLogWarning($"[CommandExecutor] {error}");
                    return (false, error);
                }

                // Load texture
                Texture2D diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(p.texture_path);
                if (diffuseTexture == null)
                {
                    string error = $"Texture not found at path: {p.texture_path}";
                    ConsoleLogError($"[CommandExecutor] {error}");
                    return (false, error);
                }

                // Load normal map (optional)
                Texture2D normalTexture = null;
                if (!string.IsNullOrEmpty(p.normal_path))
                {
                    normalTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(p.normal_path);
                    if (normalTexture == null)
                    {
                        ConsoleLogWarning($"[CommandExecutor] Normal map not found at path: {p.normal_path}");
                    }
                }

                Undo.RecordObject(terrainData, "Add Terrain Layer");

                // Create TerrainLayer
                TerrainLayer newLayer = new TerrainLayer();
                newLayer.diffuseTexture = diffuseTexture;
                if (normalTexture != null)
                {
                    newLayer.normalMapTexture = normalTexture;
                }

                // Set tile size
                float tileSize = p.tile_size > 0 ? p.tile_size : 10f;
                newLayer.tileSize = new Vector2(tileSize, tileSize);

                // Save TerrainLayer as asset
                string layerName = System.IO.Path.GetFileNameWithoutExtension(p.texture_path);
                string layerPath = $"Assets/ClaudeAgent/Generated/Terrains/{obj.name}_{layerName}_Layer.asset";
                EnsureDirectoryExists(layerPath);
                AssetDatabase.CreateAsset(newLayer, layerPath);

                // Add to existing layers
                TerrainLayer[] existingLayers = terrainData.terrainLayers;
                TerrainLayer[] newLayers = new TerrainLayer[existingLayers.Length + 1];
                existingLayers.CopyTo(newLayers, 0);
                newLayers[newLayers.Length - 1] = newLayer;
                terrainData.terrainLayers = newLayers;

                EditorUtility.SetDirty(terrainData);

                string result = $"Added terrain layer '{layerName}' to {obj.name} (Total layers: {newLayers.Length})";
                ConsoleLog($"[CommandExecutor] {result}");
                return (true, result);
            }
            catch (Exception e)
            {
                string error = $"Error adding terrain layer: {e.Message}";
                ConsoleLogError($"[CommandExecutor] {error}");
                return (false, error);
            }
        }

        /// <summary>
        /// Unified terrain texture command (paint/fill integration)
        /// Use fill: true for full fill, center/radius for painting
        /// </summary>
        private (bool, string) TerrainTextureCommand(CommandParams p)
        {
            try
            {
                if (p == null || string.IsNullOrEmpty(p.path))
                {
                    string error = "Missing required parameter: path";
                    ConsoleLogError($"[CommandExecutor] {error}");
                    return (false, error);
                }

                if (p.layer_index < 0)
                {
                    string error = "Missing or invalid parameter: layer_index (must be >= 0)";
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

                Terrain terrain = obj.GetComponent<Terrain>();
                if (terrain == null)
                {
                    string error = $"Terrain component not found on: {p.path}";
                    ConsoleLogWarning($"[CommandExecutor] {error}");
                    return (false, error);
                }

                TerrainData terrainData = terrain.terrainData;
                if (terrainData == null)
                {
                    string error = $"TerrainData not found for: {p.path}";
                    ConsoleLogWarning($"[CommandExecutor] {error}");
                    return (false, error);
                }

                int layerCount = terrainData.terrainLayers.Length;
                if (layerCount == 0)
                {
                    string error = "Terrain has no layers. Add layers first using add_terrain_layer";
                    ConsoleLogError($"[CommandExecutor] {error}");
                    return (false, error);
                }

                if (p.layer_index >= layerCount)
                {
                    string error = $"Invalid layer_index: {p.layer_index}. Terrain has {layerCount} layers (0-{layerCount - 1})";
                    ConsoleLogError($"[CommandExecutor] {error}");
                    return (false, error);
                }

                // Check for presence of paint parameters
                bool hasCenter = p.center != null && p.center.Length == 2;
                bool hasRadius = p.radius > 0;
                bool hasPaintParams = hasCenter && hasRadius;

                // If fill mode (fill: true or no center/radius)
                if (p.fill || !hasPaintParams)
                {
                    // Error if fill is specified together with paint parameters
                    if (p.fill && hasPaintParams)
                    {
                        string error = "Cannot specify both 'fill' and paint parameters (center, radius)";
                        ConsoleLogError($"[CommandExecutor] {error}");
                        return (false, error);
                    }

                    // Error if only center or radius is specified partially
                    if (hasCenter != hasRadius)
                    {
                        string error = "Paint mode requires both 'center' and 'radius' parameters";
                        ConsoleLogError($"[CommandExecutor] {error}");
                        return (false, error);
                    }

                    return FillTerrainTextureInternal(obj, terrainData, p, layerCount);
                }

                // Paint mode (center, radius)
                return PaintTerrainTextureInternal(obj, terrainData, p, layerCount);
            }
            catch (Exception e)
            {
                string error = $"Error in terrain_texture command: {e.Message}";
                ConsoleLogError($"[CommandExecutor] {error}");
                return (false, error);
            }
        }

        /// <summary>
        /// Fills terrain with specified layer (internal helper)
        /// </summary>
        private (bool, string) FillTerrainTextureInternal(GameObject obj, TerrainData terrainData, CommandParams p, int layerCount)
        {
            Undo.RecordObject(terrainData, "Fill Terrain Layer");

            // Get strength (default 1.0)
            float strength = p.strength > 0 ? Mathf.Clamp01(p.strength) : 1.0f;

            int alphamapWidth = terrainData.alphamapWidth;
            int alphamapHeight = terrainData.alphamapHeight;
            float[,,] alphamaps = terrainData.GetAlphamaps(0, 0, alphamapWidth, alphamapHeight);

            for (int y = 0; y < alphamapHeight; y++)
            {
                for (int x = 0; x < alphamapWidth; x++)
                {
                    // Set strength to specified layer
                    alphamaps[y, x, p.layer_index] = strength;

                    // Normalize other layers (so sum equals 1)
                    float remaining = 1f - strength;
                    float otherSum = 0f;
                    for (int i = 0; i < layerCount; i++)
                    {
                        if (i != p.layer_index) otherSum += alphamaps[y, x, i];
                    }

                    if (otherSum > 0f)
                    {
                        for (int i = 0; i < layerCount; i++)
                        {
                            if (i != p.layer_index)
                            {
                                alphamaps[y, x, i] = alphamaps[y, x, i] / otherSum * remaining;
                            }
                        }
                    }
                    else if (remaining > 0f && layerCount > 1)
                    {
                        // If other layers are 0, distribute evenly
                        float perLayer = remaining / (layerCount - 1);
                        for (int i = 0; i < layerCount; i++)
                        {
                            if (i != p.layer_index)
                            {
                                alphamaps[y, x, i] = perLayer;
                            }
                        }
                    }
                }
            }

            terrainData.SetAlphamaps(0, 0, alphamaps);
            EditorUtility.SetDirty(terrainData);

            string layerName = terrainData.terrainLayers[p.layer_index].diffuseTexture?.name ?? $"Layer{p.layer_index}";
            string result = $"Filled terrain with layer [{p.layer_index}] '{layerName}' at strength {strength:F2}";
            ConsoleLog($"[CommandExecutor] {result}");
            return (true, result);
        }

        /// <summary>
        /// Paints terrain texture in circular area (internal helper)
        /// </summary>
        private (bool, string) PaintTerrainTextureInternal(GameObject obj, TerrainData terrainData, CommandParams p, int layerCount)
        {
            Undo.RecordObject(terrainData, "Paint Terrain Texture");

            // Get strength (default 1.0)
            float strength = p.strength > 0 ? Mathf.Clamp01(p.strength) : 1.0f;

            int alphamapWidth = terrainData.alphamapWidth;
            int alphamapHeight = terrainData.alphamapHeight;
            Vector3 terrainSize = terrainData.size;
            Vector3 terrainPos = obj.transform.position;

            // Convert world coordinates to normalized coordinates within terrain
            float centerX = (p.center[0] - terrainPos.x) / terrainSize.x;
            float centerZ = (p.center[1] - terrainPos.z) / terrainSize.z;

            // Normalized radius
            float radiusX = p.radius / terrainSize.x;
            float radiusZ = p.radius / terrainSize.z;

            float[,,] alphamaps = terrainData.GetAlphamaps(0, 0, alphamapWidth, alphamapHeight);

            // Calculate affected area
            int minX = Mathf.Max(0, Mathf.FloorToInt((centerX - radiusX) * alphamapWidth));
            int maxX = Mathf.Min(alphamapWidth - 1, Mathf.CeilToInt((centerX + radiusX) * alphamapWidth));
            int minZ = Mathf.Max(0, Mathf.FloorToInt((centerZ - radiusZ) * alphamapHeight));
            int maxZ = Mathf.Min(alphamapHeight - 1, Mathf.CeilToInt((centerZ + radiusZ) * alphamapHeight));

            // Falloff type
            string falloff = !string.IsNullOrEmpty(p.falloff) ? p.falloff.ToLower() : "smooth";

            int modifiedCount = 0;
            for (int z = minZ; z <= maxZ; z++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    // Normalized coordinates
                    float nx = (float)x / alphamapWidth;
                    float nz = (float)z / alphamapHeight;

                    // Distance from center
                    float distX = (nx - centerX) / radiusX;
                    float distZ = (nz - centerZ) / radiusZ;
                    float dist = Mathf.Sqrt(distX * distX + distZ * distZ);

                    if (dist <= 1.0f)
                    {
                        float factor = 1.0f;
                        switch (falloff)
                        {
                            case "linear":
                                factor = 1.0f - dist;
                                break;
                            case "flat":
                                factor = 1.0f;
                                break;
                            case "smooth":
                            default:
                                factor = (Mathf.Cos(dist * Mathf.PI) + 1.0f) * 0.5f;
                                break;
                        }

                        float targetStrength = strength * factor;

                        // Add strength to specified layer (max 1.0)
                        float newValue = Mathf.Clamp01(alphamaps[z, x, p.layer_index] + targetStrength);
                        alphamaps[z, x, p.layer_index] = newValue;

                        // Normalize (so sum equals 1)
                        float sum = 0f;
                        for (int i = 0; i < layerCount; i++) sum += alphamaps[z, x, i];
                        if (sum > 0f)
                        {
                            for (int i = 0; i < layerCount; i++) alphamaps[z, x, i] /= sum;
                        }

                        modifiedCount++;
                    }
                }
            }

            terrainData.SetAlphamaps(0, 0, alphamaps);
            EditorUtility.SetDirty(terrainData);

            string layerName = terrainData.terrainLayers[p.layer_index].diffuseTexture?.name ?? $"Layer{p.layer_index}";
            string result = $"Painted terrain texture: layer [{p.layer_index}] '{layerName}' at ({p.center[0]}, {p.center[1]}) with radius {p.radius} ({modifiedCount} points modified)";
            ConsoleLog($"[CommandExecutor] {result}");
            return (true, result);
        }

        /// <summary>
        /// Checks if heightmap resolution is valid (2^n + 1)
        /// </summary>
        private bool IsValidHeightmapResolution(int resolution)
        {
            // Valid values: 33, 65, 129, 257, 513, 1025, 2049, 4097
            int[] validResolutions = { 33, 65, 129, 257, 513, 1025, 2049, 4097 };
            return Array.IndexOf(validResolutions, resolution) >= 0;
        }
    }
}
