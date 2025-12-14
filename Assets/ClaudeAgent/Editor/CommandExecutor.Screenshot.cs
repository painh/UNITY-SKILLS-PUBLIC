using UnityEngine;
using UnityEditor;
using System;
using System.IO;

namespace ClaudeAgent
{
    public partial class CommandExecutor
    {
        /// <summary>
        /// Registers screenshot commands
        /// </summary>
        private void RegisterScreenshotCommands()
        {
            RegisterCommand("capture_scene_view", CaptureSceneViewCommand);
        }

        /// <summary>
        /// Captures a screenshot of the scene view
        /// </summary>
        private (bool, string) CaptureSceneViewCommand(CommandParams p)
        {
            GameObject cameraObj = null;
            RenderTexture rt = null;
            RenderTexture prevActive = null;

            try
            {
                // Default values
                int imageWidth = (p.width > 0) ? (int)p.width : 800;
                int imageHeight = (p.height > 0) ? (int)p.height : 600;
                string outputPath = !string.IsNullOrEmpty(p.output_path)
                    ? p.output_path
                    : "Temp/scene_capture.png";

                // Convert relative path to absolute path
                if (!Path.IsPathRooted(outputPath))
                {
                    outputPath = Path.Combine(Application.dataPath, "..", outputPath);
                }
                outputPath = Path.GetFullPath(outputPath);

                // Create output directory
                string outputDir = Path.GetDirectoryName(outputPath);
                if (!Directory.Exists(outputDir))
                {
                    Directory.CreateDirectory(outputDir);
                }

                // Create temporary camera
                cameraObj = new GameObject("TempScreenshotCamera");
                cameraObj.hideFlags = HideFlags.HideAndDontSave;
                var camera = cameraObj.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.Skybox;
                camera.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 1f);

                Vector3 cameraPosition;
                Vector3 lookAtPoint;

                // If target is specified
                if (!string.IsNullOrEmpty(p.target))
                {
                    var (targetObj, findError) = FindGameObjectByPath(p.target);
                    if (targetObj == null)
                    {
                        return Error(findError ?? $"Target not found: {p.target}");
                    }

                    // Calculate target's bounding box
                    Bounds bounds = CalculateObjectBounds(targetObj);
                    lookAtPoint = bounds.center;

                    // If camera position is directly specified
                    if (p.position != null && p.position.Length >= 3)
                    {
                        cameraPosition = new Vector3(p.position[0], p.position[1], p.position[2]);
                    }
                    // Calculate from distance + angle
                    else
                    {
                        float distance = (p.distance > 0) ? p.distance : CalculateIdealDistance(bounds, camera.fieldOfView);
                        float pitch = 30f;  // Default: 30 degrees from above
                        float yaw = 45f;    // Default: 45 degrees from right

                        if (p.angle != null && p.angle.Length >= 2)
                        {
                            pitch = p.angle[0];
                            yaw = p.angle[1];
                        }

                        cameraPosition = CalculateCameraPosition(lookAtPoint, distance, pitch, yaw);
                    }
                }
                // If no target but position is specified
                else if (p.position != null && p.position.Length >= 3)
                {
                    cameraPosition = new Vector3(p.position[0], p.position[1], p.position[2]);
                    lookAtPoint = Vector3.zero;  // Look at origin
                }
                // If nothing specified: capture entire scene
                else
                {
                    Bounds sceneBounds = CalculateSceneBounds();
                    lookAtPoint = sceneBounds.center;
                    float distance = CalculateIdealDistance(sceneBounds, camera.fieldOfView);
                    cameraPosition = CalculateCameraPosition(lookAtPoint, distance, 30f, 45f);
                }

                // Position camera
                cameraObj.transform.position = cameraPosition;
                cameraObj.transform.LookAt(lookAtPoint);

                // Create RenderTexture and render
                rt = new RenderTexture(imageWidth, imageHeight, 24, RenderTextureFormat.ARGB32);
                rt.antiAliasing = 4;
                camera.targetTexture = rt;

                // Create texture for saving
                prevActive = RenderTexture.active;
                RenderTexture.active = rt;

                camera.Render();

                var tex = new Texture2D(imageWidth, imageHeight, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, imageWidth, imageHeight), 0, 0);
                tex.Apply();

                // Convert to PNG and save
                byte[] pngData = tex.EncodeToPNG();
                File.WriteAllBytes(outputPath, pngData);

                // Cleanup Texture2D
                UnityEngine.Object.DestroyImmediate(tex);

                string targetInfo = !string.IsNullOrEmpty(p.target) ? $" looking at {p.target}" : "";
                return Success($"Screenshot saved: {outputPath}\nResolution: {imageWidth}x{imageHeight}\nCamera: ({cameraPosition.x:F2}, {cameraPosition.y:F2}, {cameraPosition.z:F2}){targetInfo}");
            }
            catch (Exception e)
            {
                return Error($"Error capturing screenshot: {e.Message}");
            }
            finally
            {
                // Cleanup
                RenderTexture.active = prevActive;

                if (rt != null)
                {
                    rt.Release();
                    UnityEngine.Object.DestroyImmediate(rt);
                }

                if (cameraObj != null)
                {
                    UnityEngine.Object.DestroyImmediate(cameraObj);
                }
            }
        }

        /// <summary>
        /// Calculates the bounding box of an object and all its children
        /// </summary>
        private Bounds CalculateObjectBounds(GameObject obj)
        {
            Bounds bounds = new Bounds(obj.transform.position, Vector3.zero);
            bool hasBounds = false;

            // Get bounding box from all Renderers
            var renderers = obj.GetComponentsInChildren<Renderer>();
            foreach (var renderer in renderers)
            {
                if (hasBounds)
                {
                    bounds.Encapsulate(renderer.bounds);
                }
                else
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
            }

            // Use Transform position if no Renderer exists
            if (!hasBounds)
            {
                bounds = new Bounds(obj.transform.position, Vector3.one);
            }

            return bounds;
        }

        /// <summary>
        /// Calculates the bounding box of the entire scene
        /// </summary>
        private Bounds CalculateSceneBounds()
        {
            var allRenderers = UnityEngine.Object.FindObjectsOfType<Renderer>();

            if (allRenderers.Length == 0)
            {
                return new Bounds(Vector3.zero, Vector3.one * 10);
            }

            Bounds bounds = allRenderers[0].bounds;
            for (int i = 1; i < allRenderers.Length; i++)
            {
                bounds.Encapsulate(allRenderers[i].bounds);
            }

            return bounds;
        }

        /// <summary>
        /// Calculates the ideal camera distance to fit the bounding box
        /// </summary>
        private float CalculateIdealDistance(Bounds bounds, float fov)
        {
            float maxExtent = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z);
            float minDistance = maxExtent / Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
            return minDistance * 2f;  // Add margin
        }

        /// <summary>
        /// Calculates camera position from look-at point, distance, pitch, and yaw
        /// </summary>
        private Vector3 CalculateCameraPosition(Vector3 lookAt, float distance, float pitch, float yaw)
        {
            // Convert pitch and yaw to radians
            float pitchRad = pitch * Mathf.Deg2Rad;
            float yawRad = yaw * Mathf.Deg2Rad;

            // Convert from spherical to Cartesian coordinates
            float y = distance * Mathf.Sin(pitchRad);
            float horizontalDist = distance * Mathf.Cos(pitchRad);
            float x = horizontalDist * Mathf.Sin(yawRad);
            float z = -horizontalDist * Mathf.Cos(yawRad);

            return lookAt + new Vector3(x, y, z);
        }
    }
}
