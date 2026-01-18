using UnityEngine;
using UnityEditor;
using System;
using System.Text;

namespace ClaudeAgent
{
    public partial class CommandExecutor
    {
        /// <summary>
        /// Registers physics commands
        /// </summary>
        private void RegisterPhysicsCommands()
        {
            RegisterCommand("get_physics_settings", GetPhysicsSettings);
            RegisterCommand("set_physics_settings", SetPhysicsSettings);
            RegisterCommand("get_layer_collision_matrix", GetLayerCollisionMatrix);
            RegisterCommand("set_layer_collision", SetLayerCollision);
        }

        /// <summary>
        /// Get physics settings
        /// </summary>
        private (bool, string) GetPhysicsSettings(CommandParams p)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("Physics Settings:");
                sb.AppendLine();
                sb.AppendLine($"Gravity: {Physics.gravity}");
                sb.AppendLine($"Default Solver Iterations: {Physics.defaultSolverIterations}");
                sb.AppendLine($"Default Solver Velocity Iterations: {Physics.defaultSolverVelocityIterations}");
                sb.AppendLine($"Sleep Threshold: {Physics.sleepThreshold}");
                sb.AppendLine($"Default Contact Offset: {Physics.defaultContactOffset}");
                sb.AppendLine($"Bounce Threshold: {Physics.bounceThreshold}");
                sb.AppendLine($"Default Max Angular Speed: {Physics.defaultMaxAngularSpeed}");
                sb.AppendLine($"Auto Sync Transforms: {Physics.autoSyncTransforms}");
                sb.AppendLine($"Auto Simulation: {Physics.autoSimulation}");
                sb.AppendLine($"Queries Hit Triggers: {Physics.queriesHitTriggers}");
                sb.AppendLine($"Queries Hit Backfaces: {Physics.queriesHitBackfaces}");

                string result = sb.ToString().TrimEnd();
                ConsoleLog("[CommandExecutor] Retrieved physics settings");
                return Success(result);
            }
            catch (Exception e)
            {
                string error = $"Error getting physics settings: {e.Message}";
                ConsoleLogError($"[CommandExecutor] {error}");
                return Error(error);
            }
        }

        /// <summary>
        /// Set physics settings
        /// </summary>
        private (bool, string) SetPhysicsSettings(CommandParams p)
        {
            try
            {
                if (p == null)
                {
                    return Error("Missing parameters");
                }

                var sb = new StringBuilder();
                sb.AppendLine("Updated physics settings:");

                // Gravity
                if (p.gravity != null && p.gravity.Length == 3)
                {
                    Physics.gravity = new Vector3(p.gravity[0], p.gravity[1], p.gravity[2]);
                    sb.AppendLine($"  Gravity: {Physics.gravity}");
                }

                // Default Solver Iterations
                if (p.solver_iterations > 0)
                {
                    Physics.defaultSolverIterations = p.solver_iterations;
                    sb.AppendLine($"  Default Solver Iterations: {p.solver_iterations}");
                }

                // Default Solver Velocity Iterations
                if (p.solver_velocity_iterations > 0)
                {
                    Physics.defaultSolverVelocityIterations = p.solver_velocity_iterations;
                    sb.AppendLine($"  Default Solver Velocity Iterations: {p.solver_velocity_iterations}");
                }

                // Sleep Threshold
                if (p.sleep_threshold >= 0)
                {
                    Physics.sleepThreshold = p.sleep_threshold;
                    sb.AppendLine($"  Sleep Threshold: {p.sleep_threshold}");
                }

                // Default Contact Offset
                if (p.contact_offset > 0)
                {
                    Physics.defaultContactOffset = p.contact_offset;
                    sb.AppendLine($"  Default Contact Offset: {p.contact_offset}");
                }

                // Bounce Threshold
                if (p.bounce_threshold >= 0)
                {
                    Physics.bounceThreshold = p.bounce_threshold;
                    sb.AppendLine($"  Bounce Threshold: {p.bounce_threshold}");
                }

                // Auto Sync Transforms
                if (p.auto_sync_transforms_set)
                {
                    Physics.autoSyncTransforms = p.auto_sync_transforms;
                    sb.AppendLine($"  Auto Sync Transforms: {p.auto_sync_transforms}");
                }

                // Queries Hit Triggers
                if (p.queries_hit_triggers_set)
                {
                    Physics.queriesHitTriggers = p.queries_hit_triggers;
                    sb.AppendLine($"  Queries Hit Triggers: {p.queries_hit_triggers}");
                }

                // Queries Hit Backfaces
                if (p.queries_hit_backfaces_set)
                {
                    Physics.queriesHitBackfaces = p.queries_hit_backfaces;
                    sb.AppendLine($"  Queries Hit Backfaces: {p.queries_hit_backfaces}");
                }

                string result = sb.ToString().TrimEnd();
                ConsoleLog($"[CommandExecutor] {result}");
                return Success(result);
            }
            catch (Exception e)
            {
                string error = $"Error setting physics settings: {e.Message}";
                ConsoleLogError($"[CommandExecutor] {error}");
                return Error(error);
            }
        }

        /// <summary>
        /// Get layer collision matrix
        /// </summary>
        private (bool, string) GetLayerCollisionMatrix(CommandParams p)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("Layer Collision Matrix:");
                sb.AppendLine();

                // Check if specific layers requested
                if (!string.IsNullOrEmpty(p?.layer_name) || p?.layer >= 0)
                {
                    int layerIndex = -1;
                    string layerName = "";

                    if (!string.IsNullOrEmpty(p.layer_name))
                    {
                        layerIndex = LayerMask.NameToLayer(p.layer_name);
                        if (layerIndex == -1)
                        {
                            return Error($"Layer '{p.layer_name}' not found");
                        }
                        layerName = p.layer_name;
                    }
                    else
                    {
                        layerIndex = p.layer;
                        layerName = LayerMask.LayerToName(layerIndex);
                        if (string.IsNullOrEmpty(layerName))
                        {
                            layerName = $"Layer {layerIndex}";
                        }
                    }

                    sb.AppendLine($"Collisions for layer '{layerName}' ({layerIndex}):");
                    sb.AppendLine();

                    for (int i = 0; i <= 31; i++)
                    {
                        string otherLayerName = LayerMask.LayerToName(i);
                        if (string.IsNullOrEmpty(otherLayerName)) continue;

                        bool ignores = Physics.GetIgnoreLayerCollision(layerIndex, i);
                        string status = ignores ? "IGNORED" : "COLLIDES";
                        sb.AppendLine($"  [{i}] {otherLayerName}: {status}");
                    }
                }
                else
                {
                    // Show full matrix (only defined layers)
                    sb.AppendLine("Ignored layer pairs:");
                    sb.AppendLine();

                    int ignoredCount = 0;
                    for (int i = 0; i <= 31; i++)
                    {
                        string layer1Name = LayerMask.LayerToName(i);
                        if (string.IsNullOrEmpty(layer1Name)) continue;

                        for (int j = i; j <= 31; j++)
                        {
                            string layer2Name = LayerMask.LayerToName(j);
                            if (string.IsNullOrEmpty(layer2Name)) continue;

                            if (Physics.GetIgnoreLayerCollision(i, j))
                            {
                                sb.AppendLine($"  [{i}] {layer1Name} <-> [{j}] {layer2Name}");
                                ignoredCount++;
                            }
                        }
                    }

                    if (ignoredCount == 0)
                    {
                        sb.AppendLine("  (no ignored layer pairs - all layers collide with each other)");
                    }
                    else
                    {
                        sb.AppendLine();
                        sb.AppendLine($"Total: {ignoredCount} ignored layer pairs");
                    }
                }

                string result = sb.ToString().TrimEnd();
                ConsoleLog("[CommandExecutor] Retrieved layer collision matrix");
                return Success(result);
            }
            catch (Exception e)
            {
                string error = $"Error getting layer collision matrix: {e.Message}";
                ConsoleLogError($"[CommandExecutor] {error}");
                return Error(error);
            }
        }

        /// <summary>
        /// Set layer collision between two layers
        /// </summary>
        private (bool, string) SetLayerCollision(CommandParams p)
        {
            try
            {
                if (p == null)
                {
                    return Error("Missing parameters");
                }

                // Get layer 1
                int layer1Index = -1;
                string layer1Name = "";

                if (!string.IsNullOrEmpty(p.layer_name))
                {
                    layer1Index = LayerMask.NameToLayer(p.layer_name);
                    if (layer1Index == -1)
                    {
                        return Error($"Layer '{p.layer_name}' not found");
                    }
                    layer1Name = p.layer_name;
                }
                else if (p.layer >= 0)
                {
                    layer1Index = p.layer;
                    layer1Name = LayerMask.LayerToName(layer1Index);
                    if (string.IsNullOrEmpty(layer1Name)) layer1Name = $"Layer {layer1Index}";
                }
                else
                {
                    return Error("Missing required parameter: layer_name or layer (first layer)");
                }

                // Get layer 2
                int layer2Index = -1;
                string layer2Name = "";

                if (!string.IsNullOrEmpty(p.layer2_name))
                {
                    layer2Index = LayerMask.NameToLayer(p.layer2_name);
                    if (layer2Index == -1)
                    {
                        return Error($"Layer '{p.layer2_name}' not found");
                    }
                    layer2Name = p.layer2_name;
                }
                else if (p.layer2 >= 0)
                {
                    layer2Index = p.layer2;
                    layer2Name = LayerMask.LayerToName(layer2Index);
                    if (string.IsNullOrEmpty(layer2Name)) layer2Name = $"Layer {layer2Index}";
                }
                else
                {
                    return Error("Missing required parameter: layer2_name or layer2 (second layer)");
                }

                // Validate layer indices
                if (layer1Index < 0 || layer1Index > 31)
                {
                    return Error($"Invalid layer index: {layer1Index}. Valid range is 0-31.");
                }
                if (layer2Index < 0 || layer2Index > 31)
                {
                    return Error($"Invalid layer index: {layer2Index}. Valid range is 0-31.");
                }

                // Set collision
                bool ignore = p.ignore;
                Physics.IgnoreLayerCollision(layer1Index, layer2Index, ignore);

                string action = ignore ? "now IGNORING" : "now COLLIDING";
                string result = $"Layers '{layer1Name}' ({layer1Index}) and '{layer2Name}' ({layer2Index}) are {action}";
                ConsoleLog($"[CommandExecutor] {result}");
                return Success(result);
            }
            catch (Exception e)
            {
                string error = $"Error setting layer collision: {e.Message}";
                ConsoleLogError($"[CommandExecutor] {error}");
                return Error(error);
            }
        }
    }
}
