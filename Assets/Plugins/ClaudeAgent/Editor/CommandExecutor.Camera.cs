using UnityEngine;
using UnityEditor;
using System;

namespace ClaudeAgent
{
    public partial class CommandExecutor
    {
        /// <summary>
        /// Registers camera commands
        /// </summary>
        private void RegisterCameraCommands()
        {
            RegisterCommand("create_camera", CreateCamera);
            RegisterCommand("camera", CameraCommand);
        }

        /// <summary>
        /// Unified camera command (get/set integration)
        /// Use get: true to retrieve, or specify position/fov/near/far to set
        /// </summary>
        private (bool, string) CameraCommand(CommandParams p)
        {
            try
            {
                // Get GameObject + Camera using common helper
                var (success, camera, obj, error) = GetRequiredComponent<Camera>(p);
                if (!success) return Warning(error);

                // Check for presence of set parameters
                bool hasPosition = p.position != null && p.position.Length == 3;
                bool hasFov = p.fov > 0;
                bool hasNear = p.near > 0;
                bool hasFar = p.far > 0;
                bool hasAnySetParam = hasPosition || hasFov || hasNear || hasFar;

                // If get mode
                if (p.get)
                {
                    if (hasAnySetParam)
                        return Error("Cannot specify both 'get' and property values (position, fov, near, far)");
                    return GetCameraInfo(obj, camera);
                }

                // If set mode
                if (!hasAnySetParam)
                    return Error("At least one property must be specified (position, fov, near, or far), or use 'get: true' for retrieval");

                var results = new System.Collections.Generic.List<string>();

                // Set position
                if (hasPosition)
                {
                    Undo.RecordObject(obj.transform, "Set Camera Position");
                    Vector3 newPosition = new Vector3(p.position[0], p.position[1], p.position[2]);
                    obj.transform.position = newPosition;
                    results.Add($"position={newPosition}");
                }

                // Set camera properties
                if (hasFov || hasNear || hasFar)
                {
                    Undo.RecordObject(camera, "Set Camera Properties");

                    if (hasFov)
                    {
                        camera.fieldOfView = p.fov;
                        results.Add($"fov={p.fov}");
                    }

                    if (hasNear)
                    {
                        camera.nearClipPlane = p.near;
                        results.Add($"near={p.near}");
                    }

                    if (hasFar)
                    {
                        camera.farClipPlane = p.far;
                        results.Add($"far={p.far}");
                    }

                    EditorUtility.SetDirty(camera);
                }

                return Success($"Set {obj.name} camera: {string.Join(", ", results)}");
            }
            catch (Exception e)
            {
                return Error($"Error in camera command: {e.Message}");
            }
        }

        /// <summary>
        /// Gets camera information (internal helper)
        /// </summary>
        private (bool, string) GetCameraInfo(GameObject obj, Camera camera)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Camera info for: {obj.name}");
            sb.AppendLine();
            sb.AppendLine($"Position: {obj.transform.position}");
            sb.AppendLine($"Rotation: {obj.transform.eulerAngles}");
            sb.AppendLine();
            sb.AppendLine($"Field of View: {camera.fieldOfView}");
            sb.AppendLine($"Near Clip Plane: {camera.nearClipPlane}");
            sb.AppendLine($"Far Clip Plane: {camera.farClipPlane}");
            sb.AppendLine($"Orthographic: {camera.orthographic}");
            if (camera.orthographic)
            {
                sb.AppendLine($"Orthographic Size: {camera.orthographicSize}");
            }

            string result = sb.ToString();
            Debug.Log($"[CommandExecutor] Retrieved camera info for {obj.name}");
            return (true, result);
        }

        // ====== Camera Operations ======

        /// <summary>
        /// Creates a camera
        /// </summary>
        private (bool, string) CreateCamera(CommandParams p)
        {
            try
            {
                // Smart default: with parent -> local, without parent -> world
                string defaultSpace = string.IsNullOrEmpty(p?.parent) ? "world" : "local";

                // Validate space parameter
                string posSpace = p?.position_space;
                var (posValid, posError) = ValidateSpaceParameter("position", p?.position, ref posSpace, defaultSpace);
                if (!posValid) return Error(posError);

                string desiredName = !string.IsNullOrEmpty(p?.name) ? p.name : "Camera";

                // Check for duplicates only when placing at root
                if (string.IsNullOrEmpty(p?.parent))
                {
                    string duplicateError = CheckDuplicateRootName(desiredName);
                    if (duplicateError != null) return Error(duplicateError);
                }

                // Create camera
                var cameraObj = new GameObject(desiredName);
                var camera = cameraObj.AddComponent<Camera>();

                // Set parent (before transform settings)
                if (!string.IsNullOrEmpty(p?.parent))
                {
                    var (parentObj, parentFindError) = FindGameObjectByPath(p.parent);
                    if (parentObj != null)
                    {
                        cameraObj.transform.SetParent(parentObj.transform, false);
                    }
                    else
                    {
                        UnityEngine.Object.DestroyImmediate(cameraObj);
                        return Error(parentFindError ?? $"Parent object not found: {p.parent}");
                    }
                }

                // Set position (based on space specification)
                if (p.position != null && p.position.Length == 3)
                {
                    Vector3 pos = new Vector3(p.position[0], p.position[1], p.position[2]);
                    if (posSpace == "local")
                        cameraObj.transform.localPosition = pos;
                    else
                        cameraObj.transform.position = pos;
                }

                // Set rotation
                if (p.rotation != null && p.rotation.Length == 3)
                {
                    cameraObj.transform.eulerAngles = new Vector3(p.rotation[0], p.rotation[1], p.rotation[2]);
                }

                // Set camera properties
                if (p.fov >= 0) camera.fieldOfView = p.fov;
                if (p.near >= 0) camera.nearClipPlane = p.near;
                if (p.far >= 0) camera.farClipPlane = p.far;

                // Register Undo
                Undo.RegisterCreatedObjectUndo(cameraObj, "Create Camera");

                // Result message
                string result = $"Created camera: {cameraObj.name}";
                if (p.position != null && p.position.Length == 3)
                    result += $" (position: {posSpace})";
                return Success(result);
            }
            catch (Exception e)
            {
                return Error($"Error creating camera: {e.Message}");
            }
        }
    }
}
