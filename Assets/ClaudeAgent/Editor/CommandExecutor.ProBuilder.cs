using UnityEngine;
using UnityEditor;
using UnityEngine.ProBuilder;
using UnityEngine.ProBuilder.MeshOperations;
using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace ClaudeAgent
{
    public partial class CommandExecutor
    {
        /// <summary>
        /// Registers ProBuilder commands
        /// </summary>
        private void RegisterProBuilderCommands()
        {
            RegisterCommand("create_probuilder_shape", CreateProBuilderShape);
            RegisterCommand("create_fitted", CreateFitted);
        }

        /// <summary>
        /// Creates a ProBuilder shape
        /// </summary>
        private (bool, string) CreateProBuilderShape(CommandParams p)
        {
            try
            {
                if (p == null)
                {
                    string error = "Missing required parameters";
                    Debug.LogError($"[CommandExecutor] {error}");
                    return (false, error);
                }

                if (string.IsNullOrEmpty(p.shape))
                {
                    string error = "Missing required parameter: shape";
                    Debug.LogError($"[CommandExecutor] {error}");
                    return (false, error);
                }

                // Check for name duplicates (before creation)
                string desiredName = !string.IsNullOrEmpty(p.name) ? p.name : $"ProBuilder_{p.shape}";
                string duplicateError = CheckDuplicateRootName(desiredName);
                if (duplicateError != null)
                {
                    Debug.LogError($"[CommandExecutor] {duplicateError}");
                    return (false, duplicateError);
                }

                // If size array is specified, convert to width/height/depth (compatibility with create_primitive)
                if (p.size != null && p.size.Length >= 3)
                {
                    if (p.width < 0) p.width = p.size[0];
                    if (p.height < 0) p.height = p.size[1];
                    if (p.depth < 0) p.depth = p.size[2];
                }

                // Generate mesh based on shape type
                // Note: cube and cylinder use Unity standard primitives (create_primitive command)
                ProBuilderMesh mesh;
                switch (p.shape.ToLower())
                {
                    case "stair":
                        mesh = CreateProBuilderStair(p);
                        break;
                    case "door":
                        mesh = CreateProBuilderDoor(p);
                        break;
                    case "curved_stair":
                        mesh = CreateProBuilderCurvedStair(p);
                        break;
                    case "arch":
                        mesh = CreateProBuilderArch(p);
                        break;
                    case "pipe":
                        mesh = CreateProBuilderPipe(p);
                        break;
                    case "cone":
                        mesh = CreateProBuilderCone(p);
                        break;
                    case "prism":
                        mesh = CreateProBuilderPrism(p);
                        break;
                    default:
                        string error = $"Unknown shape type: '{p.shape}'. Valid types: stair, door, curved_stair, arch, pipe, cone, prism. For cube/cylinder, use 'create_primitive' command.";
                        Debug.LogError($"[CommandExecutor] {error}");
                        return (false, error);
                }

                if (mesh == null)
                {
                    string error = $"Failed to create ProBuilder shape: {p.shape}";
                    Debug.LogError($"[CommandExecutor] {error}");
                    return (false, error);
                }

                // Set name
                mesh.gameObject.name = desiredName;

                // Set position
                if (p.position != null && p.position.Length == 3)
                {
                    mesh.transform.position = new Vector3(p.position[0], p.position[1], p.position[2]);
                }

                // Set rotation
                if (p.rotation != null && p.rotation.Length == 3)
                {
                    mesh.transform.eulerAngles = new Vector3(p.rotation[0], p.rotation[1], p.rotation[2]);
                }

                // Set scale
                if (p.scale != null && p.scale.Length == 3)
                {
                    mesh.transform.localScale = new Vector3(p.scale[0], p.scale[1], p.scale[2]);
                }

                // Set color (always apply URP material to avoid pink problem)
                if (!string.IsNullOrEmpty(p.color))
                {
                    var (colorSuccess, parsedColor, colorError) = TryParseColor(p.color);
                    if (!colorSuccess)
                    {
                        GameObject.DestroyImmediate(mesh.gameObject);
                        Debug.LogError($"[CommandExecutor] {colorError}");
                        return (false, colorError);
                    }
                    ApplyColorToProBuilderMesh(mesh, parsedColor);
                }
                else
                {
                    // Default is white (URP material)
                    ApplyColorToProBuilderMesh(mesh, Color.white);
                }

                // Register Undo (allows Ctrl+Z deletion)
                Undo.RegisterCreatedObjectUndo(mesh.gameObject, "Create ProBuilder Shape");

                string result = $"Created ProBuilder {p.shape}: {mesh.gameObject.name}";
                Debug.Log($"[CommandExecutor] {result}");
                return (true, result);
            }
            catch (Exception e)
            {
                string error = $"Error creating ProBuilder shape: {e.Message}";
                Debug.LogError($"[CommandExecutor] {error}");
                return (false, error);
            }
        }

        /// <summary>
        /// Creates ProBuilder Stair
        /// </summary>
        private ProBuilderMesh CreateProBuilderStair(CommandParams p)
        {
            float width = p.width > 0 ? p.width : 2f;
            float height = p.height > 0 ? p.height : 2.5f;
            float depth = p.depth > 0 ? p.depth : 4f;
            int steps = p.steps > 0 ? p.steps : 10;

            var mesh = ShapeGenerator.GenerateStair(
                PivotLocation.Center,
                new Vector3(width, height, depth),
                steps,
                p.build_sides
            );
            mesh.ToMesh();
            mesh.Refresh();
            return mesh;
        }

        /// <summary>
        /// Creates ProBuilder Door
        /// </summary>
        private ProBuilderMesh CreateProBuilderDoor(CommandParams p)
        {
            float totalWidth = p.width > 0 ? p.width : 4f;
            float totalHeight = p.height > 0 ? p.height : 4f;
            float ledgeHeight = p.ledge_height > 0 ? p.ledge_height : 1f;
            float legWidth = p.leg_width > 0 ? p.leg_width : 1f;
            float depth = p.depth > 0 ? p.depth : 0.5f;

            var mesh = ShapeGenerator.GenerateDoor(
                PivotLocation.Center,
                totalWidth,
                totalHeight,
                ledgeHeight,
                legWidth,
                depth
            );
            mesh.ToMesh();
            mesh.Refresh();
            return mesh;
        }

        /// <summary>
        /// Creates ProBuilder CurvedStair (spiral stairs)
        /// </summary>
        private ProBuilderMesh CreateProBuilderCurvedStair(CommandParams p)
        {
            float stairWidth = p.width > 0 ? p.width : 2f;
            float height = p.height > 0 ? p.height : 2.5f;
            float innerRadius = p.inner_radius > 0 ? p.inner_radius : 2f;
            float circumference = p.circumference > 0 ? p.circumference : 90f;
            int steps = p.steps > 0 ? p.steps : 10;

            var mesh = ShapeGenerator.GenerateCurvedStair(
                PivotLocation.Center,
                stairWidth,
                height,
                innerRadius,
                circumference,
                steps,
                p.build_sides
            );
            mesh.ToMesh();
            mesh.Refresh();
            return mesh;
        }

        /// <summary>
        /// Creates ProBuilder Arch
        /// </summary>
        private ProBuilderMesh CreateProBuilderArch(CommandParams p)
        {
            float angle = p.arc_degrees > 0 ? p.arc_degrees : 180f;
            float radius = p.radius > 0 ? p.radius : 2f;
            // Specify arch thickness with thickness or width (thickness takes priority)
            float archThickness = p.thickness > 0 ? p.thickness : (p.width > 0 ? p.width : 0.5f);
            float depth = p.depth > 0 ? p.depth : 0.5f;
            // Specify subdivisions with sides or axis_divisions (sides takes priority)
            int radialCuts = p.sides > 0 ? p.sides : (p.axis_divisions > 0 ? p.axis_divisions : 6);

            var mesh = ShapeGenerator.GenerateArch(
                PivotLocation.Center,
                angle,
                radius,
                archThickness,
                depth,
                radialCuts,
                true,  // insideFaces
                true,  // outsideFaces
                true,  // frontFaces
                true,  // backFaces
                true   // endCaps
            );
            mesh.ToMesh();
            mesh.Refresh();
            return mesh;
        }

        /// <summary>
        /// Creates ProBuilder Pipe
        /// </summary>
        private ProBuilderMesh CreateProBuilderPipe(CommandParams p)
        {
            float radius = p.radius > 0 ? p.radius : 1f;
            float height = p.height > 0 ? p.height : 2f;
            float thickness = p.thickness > 0 ? p.thickness : 0.2f;
            int subdivAxis = p.axis_divisions > 0 ? p.axis_divisions : 16;
            int subdivHeight = p.height_cuts >= 0 ? p.height_cuts : 0;

            var mesh = ShapeGenerator.GeneratePipe(
                PivotLocation.Center,
                radius,
                height,
                thickness,
                subdivAxis,
                subdivHeight
            );
            mesh.ToMesh();
            mesh.Refresh();
            return mesh;
        }

        /// <summary>
        /// Creates ProBuilder Cone
        /// </summary>
        private ProBuilderMesh CreateProBuilderCone(CommandParams p)
        {
            float radius = p.radius > 0 ? p.radius : 1f;
            float height = p.height > 0 ? p.height : 2f;
            // Specify subdivisions with sides or axis_divisions (sides takes priority)
            int subdivAxis = p.sides > 0 ? p.sides : (p.axis_divisions > 0 ? p.axis_divisions : 16);

            var mesh = ShapeGenerator.GenerateCone(
                PivotLocation.Center,
                radius,
                height,
                subdivAxis
            );
            mesh.ToMesh();
            mesh.Refresh();
            return mesh;
        }

        /// <summary>
        /// Creates ProBuilder Prism (triangular prism)
        /// </summary>
        private ProBuilderMesh CreateProBuilderPrism(CommandParams p)
        {
            float width = p.width > 0 ? p.width : 1f;
            float height = p.height > 0 ? p.height : 1f;
            float depth = p.depth > 0 ? p.depth : 1f;

            var mesh = ShapeGenerator.GeneratePrism(
                PivotLocation.Center,
                new Vector3(width, height, depth)
            );
            mesh.ToMesh();
            mesh.Refresh();
            return mesh;
        }

        /// <summary>
        /// Creates shape from vertex coordinates (auto-calculates rotation)
        /// 2 points: cylinder, capsule, cube(beam)
        /// 3 points: prism
        /// 4 points: cube(quad)
        /// </summary>
        private (bool, string) CreateFitted(CommandParams p)
        {
            try
            {
                if (p == null)
                {
                    string error = "Missing required parameters";
                    Debug.LogError($"[CommandExecutor] {error}");
                    return (false, error);
                }

                if (string.IsNullOrEmpty(p.shape))
                {
                    string error = "Missing required parameter: shape";
                    Debug.LogError($"[CommandExecutor] {error}");
                    return (false, error);
                }

                // Validate vertices (received as JArray)
                var verticesArray = p.vertices as JArray;
                if (verticesArray == null || verticesArray.Count < 2)
                {
                    string error = "Missing required parameter: vertices (need at least 2 points)";
                    Debug.LogError($"[CommandExecutor] {error}");
                    return (false, error);
                }

                int vertexCount = verticesArray.Count;

                // Process by shape type
                switch (p.shape.ToLower())
                {
                    case "cylinder":
                        if (vertexCount < 2) return (false, "Need 2 vertices for cylinder");
                        return CreateFittedCylinder(p);
                    case "capsule":
                        if (vertexCount < 2) return (false, "Need 2 vertices for capsule");
                        return CreateFittedCapsule(p);
                    case "prism":
                        if (vertexCount < 3) return (false, "Need 3 vertices for prism");
                        return CreateFittedPrism(p);
                    case "cube":
                        if (vertexCount == 2) return CreateFittedBeam(p);      // 2 vertices → beam
                        if (vertexCount >= 4) return CreateFittedQuad(p);      // 4 vertices → quad
                        return (false, "Need 2 or 4 vertices for cube");
                    default:
                        string error = $"Shape '{p.shape}' is not supported. Supported: cylinder, capsule, prism, cube";
                        Debug.LogError($"[CommandExecutor] {error}");
                        return (false, error);
                }
            }
            catch (Exception e)
            {
                string error = $"Error creating fitted shape: {e.Message}";
                Debug.LogError($"[CommandExecutor] {error}");
                return (false, error);
            }
        }

        /// <summary>
        /// Creates Prism from 3 vertices (auto-fit)
        /// </summary>
        private (bool, string) CreateFittedPrism(CommandParams p)
        {
            // Smart default: with parent -> local, without parent -> world
            string defaultSpace = string.IsNullOrEmpty(p.parent) ? "world" : "local";
            string posSpace = p.position_space;
            float[] dummyCoords = new float[] { 0, 0, 0 };
            var (posValid, posError) = ValidateSpaceParameter("position", dummyCoords, ref posSpace, defaultSpace);
            if (!posValid)
            {
                return (false, posError);
            }

            // Resolve parent first
            Transform parentTransform = null;
            if (!string.IsNullOrEmpty(p.parent))
            {
                var (parentObj, parentFindError) = FindGameObjectByPath(p.parent);
                if (parentObj != null)
                {
                    parentTransform = parentObj.transform;
                }
                else
                {
                    string error = parentFindError ?? $"Parent object not found: {p.parent}";
                    return (false, error);
                }
            }

            // Get 3 points from JArray
            var verticesArray = p.vertices as JArray;
            Vector3 v0 = ParseVector3FromJToken(verticesArray[0]);
            Vector3 v1 = ParseVector3FromJToken(verticesArray[1]);
            Vector3 v2 = ParseVector3FromJToken(verticesArray[2]);

            // Transform vertex coordinates (local -> world)
            Vector3 worldV0, worldV1, worldV2;
            if (posSpace == "local" && parentTransform != null)
            {
                worldV0 = parentTransform.TransformPoint(v0);
                worldV1 = parentTransform.TransformPoint(v1);
                worldV2 = parentTransform.TransformPoint(v2);
            }
            else
            {
                worldV0 = v0;
                worldV1 = v1;
                worldV2 = v2;
            }

            // Base edge vector
            Vector3 baseVec = worldV1 - worldV0;
            float width = baseVec.magnitude;

            // Midpoint of base edge
            Vector3 baseMidpoint = (worldV0 + worldV1) / 2f;

            // Vector from apex to base midpoint (height direction)
            Vector3 toApex = worldV2 - baseMidpoint;
            float height = toApex.magnitude;

            // Thickness (thickness parameter, default 0.05)
            float depth = p.thickness > 0 ? p.thickness : 0.05f;

            // Degenerate check
            if (width < 0.001f || height < 0.001f)
            {
                string error = "Degenerate triangle: vertices are too close or collinear";
                Debug.LogError($"[CommandExecutor] {error}");
                return (false, error);
            }

            // Rotation calculation
            // Base direction -> X axis, height direction -> Y axis
            Vector3 xAxis = baseVec.normalized;
            Vector3 yAxis = toApex.normalized;
            Vector3 zAxis = Vector3.Cross(xAxis, yAxis).normalized;

            // Bounding box center (matches ProBuilder's PivotLocation.Center)
            // Not centroid (height/3), but 1/2 height from base midpoint
            Vector3 center = baseMidpoint + (height / 2f) * yAxis;

            // Quaternion.LookRotation calculates rotation from forward(Z) and up(Y)
            Quaternion rotation = Quaternion.LookRotation(zAxis, yAxis);

            // Check for name duplicates (root level only)
            string desiredName = !string.IsNullOrEmpty(p.name) ? p.name : "ProBuilder_fitted_prism";
            if (string.IsNullOrEmpty(p.parent))
            {
                string duplicateError = CheckDuplicateRootName(desiredName);
                if (duplicateError != null)
                {
                    Debug.LogError($"[CommandExecutor] {duplicateError}");
                    return (false, duplicateError);
                }
            }

            // Generate ProBuilder prism
            var mesh = ShapeGenerator.GeneratePrism(
                PivotLocation.Center,
                new Vector3(width, height, depth)
            );
            mesh.ToMesh();
            mesh.Refresh();

            // Apply transform
            mesh.transform.position = center;
            mesh.transform.rotation = rotation;

            // Set name
            mesh.gameObject.name = desiredName;

            // Set parent (maintain world position)
            if (parentTransform != null)
            {
                mesh.transform.SetParent(parentTransform, true);
            }

            // Set color
            if (!string.IsNullOrEmpty(p.color))
            {
                var (colorSuccess, parsedColor, colorError) = TryParseColor(p.color);
                if (!colorSuccess)
                {
                    GameObject.DestroyImmediate(mesh.gameObject);
                    Debug.LogError($"[CommandExecutor] {colorError}");
                    return (false, colorError);
                }
                ApplyColorToProBuilderMesh(mesh, parsedColor);
            }
            else
            {
                ApplyColorToProBuilderMesh(mesh, Color.white);
            }

            // Register Undo
            Undo.RegisterCreatedObjectUndo(mesh.gameObject, "Create Fitted ProBuilder Shape");

            string result = $"Created fitted prism: {mesh.gameObject.name} (vertices: {posSpace})";
            Debug.Log($"[CommandExecutor] {result}");
            return (true, result);
        }

        /// <summary>
        /// Creates cylinder from 2 points (auto-fit)
        /// </summary>
        private (bool, string) CreateFittedCylinder(CommandParams p)
        {
            // Smart default: with parent -> local, without parent -> world
            string defaultSpace = string.IsNullOrEmpty(p.parent) ? "world" : "local";
            string posSpace = p.position_space;
            float[] dummyCoords = new float[] { 0, 0, 0 };
            var (posValid, posError) = ValidateSpaceParameter("position", dummyCoords, ref posSpace, defaultSpace);
            if (!posValid)
            {
                return (false, posError);
            }

            // Resolve parent first
            Transform parentTransform = null;
            if (!string.IsNullOrEmpty(p.parent))
            {
                var (parentObj, parentFindError) = FindGameObjectByPath(p.parent);
                if (parentObj != null)
                {
                    parentTransform = parentObj.transform;
                }
                else
                {
                    string error = parentFindError ?? $"Parent object not found: {p.parent}";
                    return (false, error);
                }
            }

            var verticesArray = p.vertices as JArray;
            Vector3 start = ParseVector3FromJToken(verticesArray[0]);
            Vector3 end = ParseVector3FromJToken(verticesArray[1]);

            // Transform vertices from local to world coordinates
            Vector3 worldStart, worldEnd;
            if (posSpace == "local" && parentTransform != null)
            {
                worldStart = parentTransform.TransformPoint(start);
                worldEnd = parentTransform.TransformPoint(end);
            }
            else
            {
                worldStart = start;
                worldEnd = end;
            }

            Vector3 direction = worldEnd - worldStart;
            float length = direction.magnitude;
            float radius = p.radius > 0 ? p.radius : 0.05f;

            if (length < 0.001f)
            {
                return (false, "Vertices are too close");
            }

            // Check for name duplicates (root level only)
            string desiredName = !string.IsNullOrEmpty(p.name) ? p.name : "Fitted_cylinder";
            if (string.IsNullOrEmpty(p.parent))
            {
                string duplicateError = CheckDuplicateRootName(desiredName);
                if (duplicateError != null)
                {
                    return (false, duplicateError);
                }
            }

            Vector3 center = (worldStart + worldEnd) / 2f;
            Quaternion rotation = Quaternion.FromToRotation(Vector3.up, direction.normalized);

            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = desiredName;
            go.transform.position = center;
            go.transform.rotation = rotation;
            go.transform.localScale = new Vector3(radius * 2, length / 2, radius * 2);

            // Set parent (maintain world position)
            if (parentTransform != null)
            {
                go.transform.SetParent(parentTransform, true);
            }

            // Set color
            if (!string.IsNullOrEmpty(p.color))
            {
                var (colorSuccess, parsedColor, colorError) = TryParseColor(p.color);
                if (!colorSuccess)
                {
                    GameObject.DestroyImmediate(go);
                    return (false, colorError);
                }
                ApplyColorToGameObject(go, parsedColor);
            }

            Undo.RegisterCreatedObjectUndo(go, "Create Fitted Cylinder");
            return (true, $"Created fitted cylinder: {go.name} (vertices: {posSpace})");
        }

        /// <summary>
        /// Creates capsule from 2 points (auto-fit)
        /// </summary>
        private (bool, string) CreateFittedCapsule(CommandParams p)
        {
            // Smart default: with parent -> local, without parent -> world
            string defaultSpace = string.IsNullOrEmpty(p.parent) ? "world" : "local";
            string posSpace = p.position_space;
            float[] dummyCoords = new float[] { 0, 0, 0 };
            var (posValid, posError) = ValidateSpaceParameter("position", dummyCoords, ref posSpace, defaultSpace);
            if (!posValid)
            {
                return (false, posError);
            }

            // Resolve parent first
            Transform parentTransform = null;
            if (!string.IsNullOrEmpty(p.parent))
            {
                var (parentObj, parentFindError) = FindGameObjectByPath(p.parent);
                if (parentObj != null)
                {
                    parentTransform = parentObj.transform;
                }
                else
                {
                    string error = parentFindError ?? $"Parent object not found: {p.parent}";
                    return (false, error);
                }
            }

            var verticesArray = p.vertices as JArray;
            Vector3 start = ParseVector3FromJToken(verticesArray[0]);
            Vector3 end = ParseVector3FromJToken(verticesArray[1]);

            // Transform vertex coordinates (local -> world)
            Vector3 worldStart, worldEnd;
            if (posSpace == "local" && parentTransform != null)
            {
                worldStart = parentTransform.TransformPoint(start);
                worldEnd = parentTransform.TransformPoint(end);
            }
            else
            {
                worldStart = start;
                worldEnd = end;
            }

            Vector3 direction = worldEnd - worldStart;
            float length = direction.magnitude;
            float radius = p.radius > 0 ? p.radius : 0.05f;

            if (length < 0.001f)
            {
                return (false, "Vertices are too close");
            }

            // Check for name duplicates (root level only)
            string desiredName = !string.IsNullOrEmpty(p.name) ? p.name : "Fitted_capsule";
            if (string.IsNullOrEmpty(p.parent))
            {
                string duplicateError = CheckDuplicateRootName(desiredName);
                if (duplicateError != null)
                {
                    return (false, duplicateError);
                }
            }

            Vector3 center = (worldStart + worldEnd) / 2f;
            Quaternion rotation = Quaternion.FromToRotation(Vector3.up, direction.normalized);

            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = desiredName;
            go.transform.position = center;
            go.transform.rotation = rotation;
            // Capsule default height is 2, radius is 0.5
            go.transform.localScale = new Vector3(radius * 2, length / 2, radius * 2);

            // Set parent (maintain world position)
            if (parentTransform != null)
            {
                go.transform.SetParent(parentTransform, true);
            }

            // Set color
            if (!string.IsNullOrEmpty(p.color))
            {
                var (colorSuccess, parsedColor, colorError) = TryParseColor(p.color);
                if (!colorSuccess)
                {
                    GameObject.DestroyImmediate(go);
                    return (false, colorError);
                }
                ApplyColorToGameObject(go, parsedColor);
            }

            Undo.RegisterCreatedObjectUndo(go, "Create Fitted Capsule");
            return (true, $"Created fitted capsule: {go.name} (vertices: {posSpace})");
        }

        /// <summary>
        /// Creates beam (cube) from 2 points (auto-fit)
        /// </summary>
        private (bool, string) CreateFittedBeam(CommandParams p)
        {
            // Smart default: with parent -> local, without parent -> world
            string defaultSpace = string.IsNullOrEmpty(p.parent) ? "world" : "local";
            string posSpace = p.position_space;
            float[] dummyCoords = new float[] { 0, 0, 0 };
            var (posValid, posError) = ValidateSpaceParameter("position", dummyCoords, ref posSpace, defaultSpace);
            if (!posValid)
            {
                return (false, posError);
            }

            // Resolve parent first
            Transform parentTransform = null;
            if (!string.IsNullOrEmpty(p.parent))
            {
                var (parentObj, parentFindError) = FindGameObjectByPath(p.parent);
                if (parentObj != null)
                {
                    parentTransform = parentObj.transform;
                }
                else
                {
                    string error = parentFindError ?? $"Parent object not found: {p.parent}";
                    return (false, error);
                }
            }

            var verticesArray = p.vertices as JArray;
            Vector3 start = ParseVector3FromJToken(verticesArray[0]);
            Vector3 end = ParseVector3FromJToken(verticesArray[1]);

            // Transform vertices from local to world coordinates
            Vector3 worldStart, worldEnd;
            if (posSpace == "local" && parentTransform != null)
            {
                worldStart = parentTransform.TransformPoint(start);
                worldEnd = parentTransform.TransformPoint(end);
            }
            else
            {
                worldStart = start;
                worldEnd = end;
            }

            Vector3 direction = worldEnd - worldStart;
            float length = direction.magnitude;
            // Square cross-section size (use 4-point cube for panels)
            float crossSize = p.cross_size > 0 ? p.cross_size : 0.1f;

            if (length < 0.001f)
            {
                return (false, "Vertices are too close");
            }

            // Check for name duplicates (root level only)
            string desiredName = !string.IsNullOrEmpty(p.name) ? p.name : "Fitted_beam";
            if (string.IsNullOrEmpty(p.parent))
            {
                string duplicateError = CheckDuplicateRootName(desiredName);
                if (duplicateError != null)
                {
                    return (false, duplicateError);
                }
            }

            Vector3 center = (worldStart + worldEnd) / 2f;
            Quaternion rotation = Quaternion.FromToRotation(Vector3.up, direction.normalized);

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = desiredName;
            go.transform.position = center;
            go.transform.rotation = rotation;
            go.transform.localScale = new Vector3(crossSize, length, crossSize);

            // Set parent (maintain world position)
            if (parentTransform != null)
            {
                go.transform.SetParent(parentTransform, true);
            }

            // Set color
            if (!string.IsNullOrEmpty(p.color))
            {
                var (colorSuccess, parsedColor, colorError) = TryParseColor(p.color);
                if (!colorSuccess)
                {
                    GameObject.DestroyImmediate(go);
                    return (false, colorError);
                }
                ApplyColorToGameObject(go, parsedColor);
            }

            Undo.RegisterCreatedObjectUndo(go, "Create Fitted Beam");
            return (true, $"Created fitted beam: {go.name} (vertices: {posSpace})");
        }

        /// <summary>
        /// Creates quad (cube panel) from 4 points (auto-fit)
        /// </summary>
        private (bool, string) CreateFittedQuad(CommandParams p)
        {
            // Smart default: with parent -> local, without parent -> world
            string defaultSpace = string.IsNullOrEmpty(p.parent) ? "world" : "local";
            string posSpace = p.position_space;
            float[] dummyCoords = new float[] { 0, 0, 0 };
            var (posValid, posError) = ValidateSpaceParameter("position", dummyCoords, ref posSpace, defaultSpace);
            if (!posValid)
            {
                return (false, posError);
            }

            // Resolve parent first
            Transform parentTransform = null;
            if (!string.IsNullOrEmpty(p.parent))
            {
                var (parentObj, parentFindError) = FindGameObjectByPath(p.parent);
                if (parentObj != null)
                {
                    parentTransform = parentObj.transform;
                }
                else
                {
                    string error = parentFindError ?? $"Parent object not found: {p.parent}";
                    return (false, error);
                }
            }

            var verticesArray = p.vertices as JArray;
            Vector3 v0 = ParseVector3FromJToken(verticesArray[0]);
            Vector3 v1 = ParseVector3FromJToken(verticesArray[1]);
            Vector3 v2 = ParseVector3FromJToken(verticesArray[2]);
            Vector3 v3 = ParseVector3FromJToken(verticesArray[3]);

            // Transform vertex coordinates (local -> world)
            Vector3 worldV0, worldV1, worldV2, worldV3;
            if (posSpace == "local" && parentTransform != null)
            {
                worldV0 = parentTransform.TransformPoint(v0);
                worldV1 = parentTransform.TransformPoint(v1);
                worldV2 = parentTransform.TransformPoint(v2);
                worldV3 = parentTransform.TransformPoint(v3);
            }
            else
            {
                worldV0 = v0;
                worldV1 = v1;
                worldV2 = v2;
                worldV3 = v3;
            }

            // Edge vectors (for normal calculation)
            Vector3 edge01 = worldV1 - worldV0;
            Vector3 edge03 = worldV3 - worldV0;

            // Degenerate check
            if (edge01.magnitude < 0.001f || edge03.magnitude < 0.001f)
            {
                return (false, "Degenerate quad: vertices are too close or collinear");
            }

            // Check for name duplicates (root level only)
            string desiredName = !string.IsNullOrEmpty(p.name) ? p.name : "Fitted_quad";
            if (string.IsNullOrEmpty(p.parent))
            {
                string duplicateError = CheckDuplicateRootName(desiredName);
                if (duplicateError != null)
                {
                    return (false, duplicateError);
                }
            }

            // Normal calculation
            Vector3 normal = Vector3.Cross(edge01, edge03).normalized;

            // Center position (average of 4 points)
            Vector3 center = (worldV0 + worldV1 + worldV2 + worldV3) / 4f;

            float thickness = p.thickness > 0 ? p.thickness : 0.02f;

            // Project 4 vertices to 2D plane (local coordinate system with normal as Z axis)
            Quaternion toLocal = Quaternion.Inverse(Quaternion.LookRotation(normal));
            IList<Vector3> points2D = new List<Vector3>();
            foreach (var worldV in new[] { worldV0, worldV1, worldV2, worldV3 })
            {
                Vector3 localPoint = toLocal * (worldV - center);
                points2D.Add(new Vector3(localPoint.x, localPoint.y, 0));
            }

            // Generate ProBuilderMesh with CreateShapeFromPolygon (supports arbitrary quadrilaterals)
            ProBuilderMesh mesh = ProBuilderMesh.Create();
            mesh.gameObject.name = desiredName;

            var createResult = mesh.CreateShapeFromPolygon(points2D, thickness, false);
            if (createResult.status != ActionResult.Status.Success)
            {
                GameObject.DestroyImmediate(mesh.gameObject);
                return (false, $"Failed to create quad from polygon: {createResult.notification}");
            }

            // Set position and orientation
            mesh.transform.position = center;
            mesh.transform.rotation = Quaternion.LookRotation(normal);

            // Set parent (maintain world position)
            if (parentTransform != null)
            {
                mesh.transform.SetParent(parentTransform, true);
            }

            // Set color
            if (!string.IsNullOrEmpty(p.color))
            {
                var (colorSuccess, parsedColor, colorError) = TryParseColor(p.color);
                if (!colorSuccess)
                {
                    GameObject.DestroyImmediate(mesh.gameObject);
                    return (false, colorError);
                }
                ApplyColorToProBuilderMesh(mesh, parsedColor);
            }
            else
            {
                ApplyColorToProBuilderMesh(mesh, Color.white);
            }

            Undo.RegisterCreatedObjectUndo(mesh.gameObject, "Create Fitted Quad");
            return (true, $"Created fitted quad: {mesh.gameObject.name} (vertices: {posSpace})");
        }

        /// <summary>
        /// Applies color to GameObject (for standard primitives)
        /// </summary>
        private void ApplyColorToGameObject(GameObject go, Color color)
        {
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                {
                    shader = Shader.Find("Standard");
                }

                if (shader != null)
                {
                    var material = new Material(shader);
                    material.color = color;
                    if (material.HasProperty("_BaseColor"))
                    {
                        material.SetColor("_BaseColor", color);
                    }
                    renderer.sharedMaterial = material;
                }
            }
        }

        /// <summary>
        /// Parses Vector3 from JToken
        /// </summary>
        private Vector3 ParseVector3FromJToken(JToken token)
        {
            var arr = token as JArray;
            if (arr != null && arr.Count >= 3)
            {
                return new Vector3(
                    arr[0].ToObject<float>(),
                    arr[1].ToObject<float>(),
                    arr[2].ToObject<float>()
                );
            }
            return Vector3.zero;
        }

        /// <summary>
        /// Applies color to ProBuilderMesh
        /// </summary>
        private void ApplyColorToProBuilderMesh(ProBuilderMesh mesh, Color color)
        {
            var renderer = mesh.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                // Use URP Lit Shader
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                {
                    // Use Standard Shader for non-URP
                    shader = Shader.Find("Standard");
                }

                if (shader != null)
                {
                    var material = new Material(shader);
                    material.color = color;
                    if (material.HasProperty("_BaseColor"))
                    {
                        material.SetColor("_BaseColor", color);
                    }
                    renderer.sharedMaterial = material;
                }
            }
        }
    }
}
