using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System;
using System.Text;
using System.Collections.Generic;

namespace ClaudeAgent
{
    public partial class CommandExecutor
    {
        /// <summary>
        /// Registers animator commands
        /// </summary>
        private void RegisterAnimatorCommands()
        {
            RegisterCommand("animator", AnimatorCommand);
            RegisterCommand("create_animator_controller", CreateAnimatorController);
            RegisterCommand("create_animator_element", CreateAnimatorElement);
            RegisterCommand("delete_animator_element", DeleteAnimatorElement);
            RegisterCommand("animator_element", AnimatorElement);
        }

        /// <summary>
        /// Unified animator command (get/set integration)
        /// Use get: true to retrieve all info, parameter+value to set parameter, parameter only for Trigger
        /// </summary>
        private (bool, string) AnimatorCommand(CommandParams p)
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

                Animator animator = obj.GetComponent<Animator>();
                if (animator == null)
                {
                    string error = $"Animator component not found on: {p.path}";
                    ConsoleLogWarning($"[CommandExecutor] {error}");
                    return (false, error);
                }

                // Check for parameter name
                bool hasParameter = !string.IsNullOrEmpty(p.parameter);

                // If get mode
                if (p.get)
                {
                    // Check for simultaneous specification of get and set parameters
                    if (hasParameter)
                    {
                        string error = "Cannot specify both 'get' and 'parameter'";
                        ConsoleLogError($"[CommandExecutor] {error}");
                        return (false, error);
                    }

                    return GetAnimatorInfo(obj, animator);
                }

                // If parameter set mode
                if (!hasParameter)
                {
                    string error = "Must specify 'parameter' or use 'get: true' for retrieval";
                    ConsoleLogError($"[CommandExecutor] {error}");
                    return (false, error);
                }

                // Get parameter info from AnimatorController
                AnimatorController animatorController = animator.runtimeAnimatorController as AnimatorController;
                if (animatorController == null)
                {
                    var overrideController = animator.runtimeAnimatorController as AnimatorOverrideController;
                    if (overrideController != null)
                    {
                        animatorController = overrideController.runtimeAnimatorController as AnimatorController;
                    }
                }

                if (animatorController == null)
                {
                    string error = "No AnimatorController found";
                    ConsoleLogWarning($"[CommandExecutor] {error}");
                    return (false, error);
                }

                // Find parameter
                AnimatorControllerParameter foundParam = null;
                foreach (var param in animatorController.parameters)
                {
                    if (param.name == p.parameter)
                    {
                        foundParam = param;
                        break;
                    }
                }

                if (foundParam == null)
                {
                    string error = $"Parameter not found: {p.parameter}";
                    ConsoleLogWarning($"[CommandExecutor] {error}");
                    return (false, error);
                }

                // Set parameter
                string result;
                switch (foundParam.type)
                {
                    case AnimatorControllerParameterType.Bool:
                        bool boolValue = p.param_value != 0;
                        animator.SetBool(p.parameter, boolValue);
                        result = $"Set Animator bool '{p.parameter}' = {boolValue} on {obj.name}";
                        break;

                    case AnimatorControllerParameterType.Float:
                        animator.SetFloat(p.parameter, p.param_value);
                        result = $"Set Animator float '{p.parameter}' = {p.param_value} on {obj.name}";
                        break;

                    case AnimatorControllerParameterType.Int:
                        int intValue = (int)p.param_value;
                        animator.SetInteger(p.parameter, intValue);
                        result = $"Set Animator int '{p.parameter}' = {intValue} on {obj.name}";
                        break;

                    case AnimatorControllerParameterType.Trigger:
                        animator.SetTrigger(p.parameter);
                        result = $"Triggered '{p.parameter}' on {obj.name}";
                        break;

                    default:
                        string error = $"Unknown parameter type: {foundParam.type}";
                        ConsoleLogError($"[CommandExecutor] {error}");
                        return (false, error);
                }

                ConsoleLog($"[CommandExecutor] {result}");
                return (true, result);
            }
            catch (Exception e)
            {
                string error = $"Error in animator command: {e.Message}";
                ConsoleLogError($"[CommandExecutor] {error}");
                return (false, error);
            }
        }

        /// <summary>
        /// Gets animator information (internal helper)
        /// </summary>
        private (bool, string) GetAnimatorInfo(GameObject obj, Animator animator)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Animator Info for '{obj.name}':");
            sb.AppendLine();

            // RuntimeAnimatorController info
            AnimatorController animatorController = null;
            if (animator.runtimeAnimatorController != null)
            {
                sb.AppendLine($"Controller: {animator.runtimeAnimatorController.name}");
                string controllerPath = AssetDatabase.GetAssetPath(animator.runtimeAnimatorController);
                if (!string.IsNullOrEmpty(controllerPath))
                {
                    sb.AppendLine($"Controller Path: {controllerPath}");
                    // Get AnimatorController (for Edit mode)
                    animatorController = animator.runtimeAnimatorController as AnimatorController;
                    if (animatorController == null)
                    {
                        // If AnimatorOverrideController
                        var overrideController = animator.runtimeAnimatorController as AnimatorOverrideController;
                        if (overrideController != null)
                        {
                            animatorController = overrideController.runtimeAnimatorController as AnimatorController;
                        }
                    }
                }
            }
            else
            {
                sb.AppendLine("Controller: (none)");
            }

            // Avatar info
            if (animator.avatar != null)
            {
                sb.AppendLine($"Avatar: {animator.avatar.name}");
            }

            // Basic state
            sb.AppendLine($"Apply Root Motion: {animator.applyRootMotion}");
            sb.AppendLine($"Update Mode: {animator.updateMode}");
            sb.AppendLine($"Culling Mode: {animator.cullingMode}");
            sb.AppendLine();

            // Parameter list (retrieved from AnimatorController in Edit mode)
            bool isPlaying = Application.isPlaying;
            AnimatorControllerParameter[] parameters = null;

            if (isPlaying && animator.parameterCount > 0)
            {
                // Play mode: get from Animator
                parameters = animator.parameters;
            }
            else if (animatorController != null && animatorController.parameters.Length > 0)
            {
                // Edit mode: get from AnimatorController
                parameters = animatorController.parameters;
            }

            if (parameters != null && parameters.Length > 0)
            {
                sb.AppendLine($"Parameters ({parameters.Length}):");
                foreach (var param in parameters)
                {
                    string value = "";
                    if (isPlaying)
                    {
                        // Get current value in Play mode
                        switch (param.type)
                        {
                            case AnimatorControllerParameterType.Bool:
                                value = animator.GetBool(param.name).ToString();
                                break;
                            case AnimatorControllerParameterType.Float:
                                value = animator.GetFloat(param.name).ToString("F2");
                                break;
                            case AnimatorControllerParameterType.Int:
                                value = animator.GetInteger(param.name).ToString();
                                break;
                            case AnimatorControllerParameterType.Trigger:
                                value = "(trigger)";
                                break;
                        }
                    }
                    else
                    {
                        // Show default value in Edit mode
                        switch (param.type)
                        {
                            case AnimatorControllerParameterType.Bool:
                                value = $"{param.defaultBool} (default)";
                                break;
                            case AnimatorControllerParameterType.Float:
                                value = $"{param.defaultFloat:F2} (default)";
                                break;
                            case AnimatorControllerParameterType.Int:
                                value = $"{param.defaultInt} (default)";
                                break;
                            case AnimatorControllerParameterType.Trigger:
                                value = "(trigger)";
                                break;
                        }
                    }
                    sb.AppendLine($"  [{param.type}] {param.name} = {value}");
                }
            }
            else
            {
                sb.AppendLine("Parameters: (none)");
            }

            // Layer info (retrieved from AnimatorController in Edit mode)
            sb.AppendLine();
            if (isPlaying && animator.layerCount > 0)
            {
                // Play mode: get from Animator
                sb.AppendLine($"Layers ({animator.layerCount}):");
                for (int i = 0; i < animator.layerCount; i++)
                {
                    string layerName = animator.GetLayerName(i);
                    float weight = animator.GetLayerWeight(i);
                    sb.AppendLine($"  [{i}] {layerName} (weight: {weight:F2})");
                }
            }
            else if (animatorController != null && animatorController.layers.Length > 0)
            {
                // Edit mode: get from AnimatorController (including state info)
                sb.AppendLine($"Layers ({animatorController.layers.Length}):");
                for (int i = 0; i < animatorController.layers.Length; i++)
                {
                    var layer = animatorController.layers[i];
                    sb.AppendLine($"  [{i}] {layer.name} (default weight: {layer.defaultWeight:F2})");

                    // Show state info
                    if (layer.stateMachine != null)
                    {
                        var states = layer.stateMachine.states;
                        if (states.Length > 0)
                        {
                            sb.AppendLine($"      States ({states.Length}):");
                            foreach (var childState in states)
                            {
                                string defaultMarker = (childState.state == layer.stateMachine.defaultState) ? " [default]" : "";
                                sb.AppendLine($"        - {childState.state.name}{defaultMarker}");
                            }
                        }
                    }
                }
            }
            else
            {
                sb.AppendLine("Layers: (none)");
            }

            string result = sb.ToString();
            ConsoleLog($"[CommandExecutor] Retrieved Animator info for: {obj.name}");
            return (true, result);
        }

        #region AnimatorController edit operations

        /// <summary>
        /// Creates an AnimatorController asset
        /// </summary>
        private (bool, string) CreateAnimatorController(CommandParams p)
        {
            try
            {
                if (p == null || string.IsNullOrEmpty(p.path))
                {
                    return (false, "Missing required parameter: path");
                }

                if (string.IsNullOrEmpty(p.name))
                {
                    return (false, "Missing required parameter: name");
                }

                // Check path extension
                string assetPath = p.path;
                if (!assetPath.EndsWith(".controller"))
                {
                    assetPath += ".controller";
                }

                // Check for existing file
                if (AssetDatabase.LoadAssetAtPath<AnimatorController>(assetPath) != null)
                {
                    return (false, $"AnimatorController already exists at: {assetPath}");
                }

                // Create directory
                EnsureDirectoryExists(assetPath);

                // Create AnimatorController
                var controller = AnimatorController.CreateAnimatorControllerAtPath(assetPath);
                controller.name = p.name;

                EditorUtility.SetDirty(controller);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                string result = $"Created AnimatorController: {p.name} at {assetPath}";
                ConsoleLog($"[CommandExecutor] {result}");
                return (true, result);
            }
            catch (Exception e)
            {
                string error = $"Error creating AnimatorController: {e.Message}";
                ConsoleLogError($"[CommandExecutor] {error}");
                return (false, error);
            }
        }

        /// <summary>
        /// Creates an AnimatorController element (state/layer/parameter/transition/blend_tree)
        /// </summary>
        private (bool, string) CreateAnimatorElement(CommandParams p)
        {
            try
            {
                if (p == null || string.IsNullOrEmpty(p.controller_path))
                {
                    return (false, "Missing required parameter: controller_path");
                }

                if (string.IsNullOrEmpty(p.type))
                {
                    return (false, "Missing required parameter: type");
                }

                var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(p.controller_path);
                if (controller == null)
                {
                    return (false, $"AnimatorController not found: {p.controller_path}");
                }

                Undo.RecordObject(controller, $"Create Animator {p.type}");

                switch (p.type.ToLower())
                {
                    case "state":
                        return CreateAnimatorState(controller, p);
                    case "layer":
                        return CreateAnimatorLayer(controller, p);
                    case "parameter":
                        return CreateAnimatorParameter(controller, p);
                    case "transition":
                        return CreateAnimatorTransition(controller, p);
                    case "blend_tree":
                        return CreateBlendTree(controller, p);
                    default:
                        return (false, $"Invalid type: '{p.type}'. Must be one of: state, layer, parameter, transition, blend_tree");
                }
            }
            catch (Exception e)
            {
                string error = $"Error creating animator element: {e.Message}";
                ConsoleLogError($"[CommandExecutor] {error}");
                return (false, error);
            }
        }

        /// <summary>
        /// Deletes an AnimatorController element (state/layer/parameter/transition)
        /// </summary>
        private (bool, string) DeleteAnimatorElement(CommandParams p)
        {
            try
            {
                if (p == null || string.IsNullOrEmpty(p.controller_path))
                {
                    return (false, "Missing required parameter: controller_path");
                }

                if (string.IsNullOrEmpty(p.type))
                {
                    return (false, "Missing required parameter: type");
                }

                var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(p.controller_path);
                if (controller == null)
                {
                    return (false, $"AnimatorController not found: {p.controller_path}");
                }

                Undo.RecordObject(controller, $"Delete Animator {p.type}");

                switch (p.type.ToLower())
                {
                    case "state":
                        return DeleteAnimatorState(controller, p);
                    case "layer":
                        return DeleteAnimatorLayer(controller, p);
                    case "parameter":
                        return DeleteAnimatorParameter(controller, p);
                    case "transition":
                        return DeleteAnimatorTransition(controller, p);
                    default:
                        return (false, $"Invalid type: '{p.type}'. Must be one of: state, layer, parameter, transition");
                }
            }
            catch (Exception e)
            {
                string error = $"Error deleting animator element: {e.Message}";
                ConsoleLogError($"[CommandExecutor] {error}");
                return (false, error);
            }
        }

        /// <summary>
        /// Get/set AnimatorController element
        /// </summary>
        private (bool, string) AnimatorElement(CommandParams p)
        {
            try
            {
                if (p == null || string.IsNullOrEmpty(p.controller_path))
                {
                    return (false, "Missing required parameter: controller_path");
                }

                if (string.IsNullOrEmpty(p.type))
                {
                    return (false, "Missing required parameter: type");
                }

                var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(p.controller_path);
                if (controller == null)
                {
                    return (false, $"AnimatorController not found: {p.controller_path}");
                }

                // Get mode
                if (p.get)
                {
                    return GetAnimatorElement(controller, p);
                }

                // Set mode
                Undo.RecordObject(controller, $"Set Animator {p.type}");
                return SetAnimatorElement(controller, p);
            }
            catch (Exception e)
            {
                string error = $"Error in animator_element: {e.Message}";
                ConsoleLogError($"[CommandExecutor] {error}");
                return (false, error);
            }
        }

        #endregion

        #region Create operations implementation

        private (bool, string) CreateAnimatorState(AnimatorController controller, CommandParams p)
        {
            if (string.IsNullOrEmpty(p.name))
            {
                return (false, "Missing required parameter: name");
            }

            int layerIndex = p.layer >= 0 ? p.layer : 0;
            if (layerIndex >= controller.layers.Length)
            {
                return (false, $"Layer index {layerIndex} out of range (0-{controller.layers.Length - 1})");
            }

            var layer = controller.layers[layerIndex];
            var stateMachine = layer.stateMachine;

            // Check for duplicate state name
            foreach (var childState in stateMachine.states)
            {
                if (childState.state.name == p.name)
                {
                    return (false, $"State '{p.name}' already exists in layer {layerIndex}");
                }
            }

            // Create state
            Vector3 statePosition = Vector3.zero;
            if (p.position != null && p.position.Length >= 2)
            {
                statePosition = new Vector3(p.position[0], p.position[1], 0);
            }

            var state = stateMachine.AddState(p.name, statePosition);

            // Set motion
            if (!string.IsNullOrEmpty(p.motion))
            {
                var clip = LoadAnimationClip(p.motion);
                if (clip != null)
                {
                    state.motion = clip;
                }
                else
                {
                    ConsoleLogWarning($"[CommandExecutor] Animation clip not found: {p.motion}");
                }
            }

            // Set default state
            if (p.is_default)
            {
                stateMachine.defaultState = state;
            }

            // Set speed
            if (p.speed >= 0)
            {
                state.speed = p.speed;
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            string result = $"Created state '{p.name}' in layer {layerIndex}";
            ConsoleLog($"[CommandExecutor] {result}");
            return (true, result);
        }

        private (bool, string) CreateAnimatorLayer(AnimatorController controller, CommandParams p)
        {
            if (string.IsNullOrEmpty(p.name))
            {
                return (false, "Missing required parameter: name");
            }

            // Check for duplicate layer name
            foreach (var existingLayer in controller.layers)
            {
                if (existingLayer.name == p.name)
                {
                    return (false, $"Layer '{p.name}' already exists");
                }
            }

            // Create layer
            var newLayer = new AnimatorControllerLayer
            {
                name = p.name,
                stateMachine = new AnimatorStateMachine(),
                defaultWeight = p.weight >= 0 ? p.weight : 1f
            };
            newLayer.stateMachine.name = p.name;
            newLayer.stateMachine.hideFlags = UnityEngine.HideFlags.HideInHierarchy;

            // Set blending mode
            if (!string.IsNullOrEmpty(p.blending_mode))
            {
                switch (p.blending_mode.ToLower())
                {
                    case "override":
                        newLayer.blendingMode = AnimatorLayerBlendingMode.Override;
                        break;
                    case "additive":
                        newLayer.blendingMode = AnimatorLayerBlendingMode.Additive;
                        break;
                    default:
                        return (false, $"Invalid blending_mode: '{p.blending_mode}'. Must be 'Override' or 'Additive'");
                }
            }

            // Set AvatarMask
            if (!string.IsNullOrEmpty(p.avatar_mask))
            {
                var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(p.avatar_mask);
                if (mask != null)
                {
                    newLayer.avatarMask = mask;
                }
                else
                {
                    ConsoleLogWarning($"[CommandExecutor] AvatarMask not found: {p.avatar_mask}");
                }
            }

            // Add layer
            var layers = new List<AnimatorControllerLayer>(controller.layers);
            layers.Add(newLayer);
            controller.layers = layers.ToArray();

            // Add StateMachine to asset
            AssetDatabase.AddObjectToAsset(newLayer.stateMachine, controller);

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            string result = $"Created layer '{p.name}'";
            ConsoleLog($"[CommandExecutor] {result}");
            return (true, result);
        }

        private (bool, string) CreateAnimatorParameter(AnimatorController controller, CommandParams p)
        {
            if (string.IsNullOrEmpty(p.name))
            {
                return (false, "Missing required parameter: name");
            }

            if (string.IsNullOrEmpty(p.param_type))
            {
                return (false, "Missing required parameter: param_type");
            }

            // Check for duplicate parameter name
            foreach (var existingParam in controller.parameters)
            {
                if (existingParam.name == p.name)
                {
                    return (false, $"Parameter '{p.name}' already exists");
                }
            }

            // Determine parameter type
            AnimatorControllerParameterType paramType;
            switch (p.param_type.ToLower())
            {
                case "float":
                    paramType = AnimatorControllerParameterType.Float;
                    break;
                case "int":
                    paramType = AnimatorControllerParameterType.Int;
                    break;
                case "bool":
                    paramType = AnimatorControllerParameterType.Bool;
                    break;
                case "trigger":
                    paramType = AnimatorControllerParameterType.Trigger;
                    break;
                default:
                    return (false, $"Invalid param_type: '{p.param_type}'. Must be 'Float', 'Int', 'Bool', or 'Trigger'");
            }

            // Create parameter with default value
            string defaultValueInfo = "";
            var newParam = new AnimatorControllerParameter
            {
                name = p.name,
                type = paramType
            };

            // Set default value
            if (p.default_value != null && paramType != AnimatorControllerParameterType.Trigger)
            {
                ConsoleLog($"[CommandExecutor] Setting default_value: {p.default_value} (type: {p.default_value.GetType()})");

                switch (paramType)
                {
                    case AnimatorControllerParameterType.Float:
                        float floatVal = Convert.ToSingle(p.default_value);
                        newParam.defaultFloat = floatVal;
                        defaultValueInfo = $" (default: {floatVal})";
                        ConsoleLog($"[CommandExecutor] Set defaultFloat = {floatVal}");
                        break;
                    case AnimatorControllerParameterType.Int:
                        int intVal = Convert.ToInt32(p.default_value);
                        newParam.defaultInt = intVal;
                        defaultValueInfo = $" (default: {intVal})";
                        ConsoleLog($"[CommandExecutor] Set defaultInt = {intVal}");
                        break;
                    case AnimatorControllerParameterType.Bool:
                        bool boolVal = Convert.ToBoolean(p.default_value);
                        newParam.defaultBool = boolVal;
                        defaultValueInfo = $" (default: {boolVal})";
                        ConsoleLog($"[CommandExecutor] Set defaultBool = {boolVal}");
                        break;
                }
            }

            // Add to parameter array
            var paramList = new List<AnimatorControllerParameter>(controller.parameters);
            paramList.Add(newParam);
            controller.parameters = paramList.ToArray();

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            string result = $"Created parameter '{p.name}' of type {paramType}{defaultValueInfo}";
            ConsoleLog($"[CommandExecutor] {result}");
            return (true, result);
        }

        private (bool, string) CreateAnimatorTransition(AnimatorController controller, CommandParams p)
        {
            if (string.IsNullOrEmpty(p.from_state))
            {
                return (false, "Missing required parameter: from_state");
            }

            if (string.IsNullOrEmpty(p.to_state))
            {
                return (false, "Missing required parameter: to_state");
            }

            int layerIndex = p.layer >= 0 ? p.layer : 0;
            if (layerIndex >= controller.layers.Length)
            {
                return (false, $"Layer index {layerIndex} out of range (0-{controller.layers.Length - 1})");
            }

            var layer = controller.layers[layerIndex];
            var stateMachine = layer.stateMachine;

            // Search for from_state
            AnimatorState fromState = null;
            bool isFromAnyState = p.from_state.ToLower() == "any";
            bool isFromEntry = p.from_state.ToLower() == "entry";

            if (!isFromAnyState && !isFromEntry)
            {
                foreach (var childState in stateMachine.states)
                {
                    if (childState.state.name == p.from_state)
                    {
                        fromState = childState.state;
                        break;
                    }
                }

                if (fromState == null)
                {
                    return (false, $"Source state '{p.from_state}' not found in layer {layerIndex}");
                }
            }

            // Search for to_state
            AnimatorState toState = null;
            foreach (var childState in stateMachine.states)
            {
                if (childState.state.name == p.to_state)
                {
                    toState = childState.state;
                    break;
                }
            }

            if (toState == null)
            {
                return (false, $"Destination state '{p.to_state}' not found in layer {layerIndex}");
            }

            // Create transition
            AnimatorStateTransition transition;
            if (isFromAnyState)
            {
                transition = stateMachine.AddAnyStateTransition(toState);
            }
            else if (isFromEntry)
            {
                stateMachine.AddEntryTransition(toState);
                EditorUtility.SetDirty(controller);
                AssetDatabase.SaveAssets();
                string entryResult = $"Created entry transition to '{p.to_state}' in layer {layerIndex}";
                ConsoleLog($"[CommandExecutor] {entryResult}");
                return (true, entryResult);
            }
            else
            {
                transition = fromState.AddTransition(toState);
            }

            // Configure transition
            transition.hasExitTime = p.has_exit_time;
            if (p.exit_time >= 0)
            {
                transition.exitTime = p.exit_time;
            }
            if (p.duration >= 0)
            {
                transition.duration = p.duration;
            }

            // Add conditions
            if (p.conditions != null && p.conditions.Length > 0)
            {
                foreach (var condObj in p.conditions)
                {
                    try
                    {
                        var condDict = condObj as Dictionary<string, object>;
                        if (condDict == null)
                        {
                            // Handle conversion from Newtonsoft.Json
                            var jObj = condObj as Newtonsoft.Json.Linq.JObject;
                            if (jObj != null)
                            {
                                condDict = jObj.ToObject<Dictionary<string, object>>();
                            }
                        }

                        if (condDict != null)
                        {
                            string paramName = condDict.ContainsKey("parameter") ? condDict["parameter"].ToString() : "";
                            string modeStr = condDict.ContainsKey("mode") ? condDict["mode"].ToString() : "Greater";
                            float thresholdVal = condDict.ContainsKey("threshold") ? Convert.ToSingle(condDict["threshold"]) : 0f;

                            AnimatorConditionMode condMode;
                            switch (modeStr.ToLower())
                            {
                                case "if":
                                    condMode = AnimatorConditionMode.If;
                                    break;
                                case "ifnot":
                                    condMode = AnimatorConditionMode.IfNot;
                                    break;
                                case "greater":
                                    condMode = AnimatorConditionMode.Greater;
                                    break;
                                case "less":
                                    condMode = AnimatorConditionMode.Less;
                                    break;
                                case "equals":
                                    condMode = AnimatorConditionMode.Equals;
                                    break;
                                case "notequal":
                                    condMode = AnimatorConditionMode.NotEqual;
                                    break;
                                default:
                                    condMode = AnimatorConditionMode.Greater;
                                    break;
                            }

                            transition.AddCondition(condMode, thresholdVal, paramName);
                        }
                    }
                    catch (Exception e)
                    {
                        ConsoleLogWarning($"[CommandExecutor] Failed to parse condition: {e.Message}");
                    }
                }
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            string fromName = isFromAnyState ? "Any State" : p.from_state;
            string result = $"Created transition from '{fromName}' to '{p.to_state}' in layer {layerIndex}";
            ConsoleLog($"[CommandExecutor] {result}");
            return (true, result);
        }

        private (bool, string) CreateBlendTree(AnimatorController controller, CommandParams p)
        {
            if (string.IsNullOrEmpty(p.name))
            {
                return (false, "Missing required parameter: name");
            }

            if (string.IsNullOrEmpty(p.parameter))
            {
                return (false, "Missing required parameter: parameter");
            }

            int layerIndex = p.layer >= 0 ? p.layer : 0;
            if (layerIndex >= controller.layers.Length)
            {
                return (false, $"Layer index {layerIndex} out of range (0-{controller.layers.Length - 1})");
            }

            var layer = controller.layers[layerIndex];
            var stateMachine = layer.stateMachine;

            // Check for duplicate state name
            foreach (var childState in stateMachine.states)
            {
                if (childState.state.name == p.name)
                {
                    return (false, $"State '{p.name}' already exists in layer {layerIndex}");
                }
            }

            // Create BlendTree
            BlendTree blendTree;
            Vector3 statePosition = Vector3.zero;
            if (p.position != null && p.position.Length >= 2)
            {
                statePosition = new Vector3(p.position[0], p.position[1], 0);
            }

            var state = controller.CreateBlendTreeInController(p.name, out blendTree, layerIndex);

            // Set state position
            var states = stateMachine.states;
            for (int i = 0; i < states.Length; i++)
            {
                if (states[i].state == state)
                {
                    states[i].position = statePosition;
                    break;
                }
            }
            stateMachine.states = states;

            // Configure BlendTree
            blendTree.blendParameter = p.parameter;

            // Set Y-axis parameter for 2D blend tree
            if (!string.IsNullOrEmpty(p.parameter_y))
            {
                blendTree.blendParameterY = p.parameter_y;
            }

            // Set blend type
            if (!string.IsNullOrEmpty(p.blend_type))
            {
                switch (p.blend_type.ToLower())
                {
                    case "1d":
                    case "simple1d":
                        blendTree.blendType = BlendTreeType.Simple1D;
                        break;
                    case "2d":
                    case "simpledirectional2d":
                        blendTree.blendType = BlendTreeType.SimpleDirectional2D;
                        break;
                    case "freeformdirectional2d":
                        blendTree.blendType = BlendTreeType.FreeformDirectional2D;
                        break;
                    case "freeformcartesian2d":
                        blendTree.blendType = BlendTreeType.FreeformCartesian2D;
                        break;
                    case "direct":
                        blendTree.blendType = BlendTreeType.Direct;
                        break;
                    default:
                        blendTree.blendType = BlendTreeType.Simple1D;
                        break;
                }
            }

            // Add child elements
            int addedChildren = 0;
            if (p.children != null && p.children.Length > 0)
            {
                ConsoleLog($"[CommandExecutor] Processing {p.children.Length} children for BlendTree");
                bool is2D = blendTree.blendType != BlendTreeType.Simple1D && blendTree.blendType != BlendTreeType.Direct;

                foreach (var childObj in p.children)
                {
                    try
                    {
                        ConsoleLog($"[CommandExecutor] Child object type: {childObj?.GetType().Name ?? "null"}");
                        var childDict = childObj as Dictionary<string, object>;
                        if (childDict == null)
                        {
                            var jObj = childObj as Newtonsoft.Json.Linq.JObject;
                            if (jObj != null)
                            {
                                childDict = jObj.ToObject<Dictionary<string, object>>();
                                ConsoleLog($"[CommandExecutor] Converted JObject to Dictionary");
                            }
                        }

                        if (childDict != null)
                        {
                            string motionPath = childDict.ContainsKey("motion") ? childDict["motion"].ToString() : "";
                            ConsoleLog($"[CommandExecutor] Motion path: {motionPath}");

                            if (!string.IsNullOrEmpty(motionPath))
                            {
                                var clip = LoadAnimationClip(motionPath);
                                if (clip != null)
                                {
                                    if (is2D && childDict.ContainsKey("position"))
                                    {
                                        // 2D BlendTree: use position [x, y]
                                        Vector2 pos = ParseVector2FromChild(childDict, "position");
                                        blendTree.AddChild(clip, pos);
                                        ConsoleLog($"[CommandExecutor] Added 2D child: {clip.name} at ({pos.x}, {pos.y})");
                                    }
                                    else
                                    {
                                        // 1D BlendTree: use threshold
                                        float thresholdVal = childDict.ContainsKey("threshold") ? Convert.ToSingle(childDict["threshold"]) : 0f;
                                        blendTree.AddChild(clip, thresholdVal);
                                        ConsoleLog($"[CommandExecutor] Added 1D child: {clip.name} at threshold {thresholdVal}");
                                    }
                                    addedChildren++;
                                }
                                else
                                {
                                    ConsoleLogWarning($"[CommandExecutor] Animation clip not found: {motionPath}");
                                }
                            }
                        }
                        else
                        {
                            ConsoleLogWarning($"[CommandExecutor] Could not parse child as Dictionary");
                        }
                    }
                    catch (Exception e)
                    {
                        ConsoleLogWarning($"[CommandExecutor] Failed to parse child: {e.Message}\n{e.StackTrace}");
                    }
                }

                ConsoleLog($"[CommandExecutor] BlendTree children count after adding: {blendTree.children.Length}");
            }

            // Mark both BlendTree and Controller as dirty
            EditorUtility.SetDirty(blendTree);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            string result = $"Created BlendTree '{p.name}' with parameter '{p.parameter}' in layer {layerIndex} (children: {addedChildren})";
            ConsoleLog($"[CommandExecutor] {result}");
            return (true, result);
        }

        #endregion

        /// <summary>
        /// Loads an animation clip (.anim files and FBX embedded clips supported)
        /// </summary>
        private AnimationClip LoadAnimationClip(string motionPath)
        {
            ConsoleLog($"[CommandExecutor] LoadAnimationClip: {motionPath}");

            // First try direct load (.anim file)
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(motionPath);
            if (clip != null)
            {
                ConsoleLog($"[CommandExecutor] Loaded directly: {clip.name}");
                return clip;
            }

            // Search for embedded clips from model files like FBX
            string extension = System.IO.Path.GetExtension(motionPath).ToLower();
            ConsoleLog($"[CommandExecutor] Extension: '{extension}'");

            if (extension == ".fbx" || extension == ".dae" || extension == ".obj")
            {
                // Extract clip name from path (Assets/Models/Char.fbx/Walk -> Walk)
                // or load all clips
                var allAssets = AssetDatabase.LoadAllAssetsAtPath(motionPath);
                ConsoleLog($"[CommandExecutor] LoadAllAssetsAtPath returned {allAssets?.Length ?? 0} assets");
                foreach (var asset in allAssets)
                {
                    ConsoleLog($"[CommandExecutor]   Asset: {asset?.name} ({asset?.GetType().Name})");
                    if (asset is AnimationClip animClip && !animClip.name.StartsWith("__preview__"))
                    {
                        ConsoleLog($"[CommandExecutor] Found clip in FBX: {animClip.name}");
                        return animClip; // Return first found clip
                    }
                }
            }

            // If path contains embedded clip name
            // Example: "Assets/UnityChan/Animations/unitychan.fbx/WAIT00"
            int lastSlashIndex = motionPath.LastIndexOf('/');
            if (lastSlashIndex > 0)
            {
                string parentPath = motionPath.Substring(0, lastSlashIndex);
                string clipName = motionPath.Substring(lastSlashIndex + 1);
                ConsoleLog($"[CommandExecutor] Trying parent path: {parentPath}, clip name: {clipName}");

                // Check if parent path is an FBX or similar file
                string parentExtension = System.IO.Path.GetExtension(parentPath).ToLower();
                if (parentExtension == ".fbx" || parentExtension == ".dae" || parentExtension == ".obj")
                {
                    var allAssets = AssetDatabase.LoadAllAssetsAtPath(parentPath);
                    ConsoleLog($"[CommandExecutor] LoadAllAssetsAtPath (parent) returned {allAssets?.Length ?? 0} assets");
                    foreach (var asset in allAssets)
                    {
                        ConsoleLog($"[CommandExecutor]   Asset: {asset?.name} ({asset?.GetType().Name})");
                        if (asset is AnimationClip animClip && animClip.name == clipName)
                        {
                            ConsoleLog($"[CommandExecutor] Found clip by name: {animClip.name}");
                            return animClip;
                        }
                    }
                }
            }

            ConsoleLogWarning($"[CommandExecutor] Animation clip not found: {motionPath}");
            return null;
        }

        /// <summary>
        /// Parses Vector2 from child element dictionary
        /// </summary>
        private Vector2 ParseVector2FromChild(Dictionary<string, object> childDict, string key)
        {
            if (!childDict.ContainsKey(key))
            {
                return Vector2.zero;
            }

            var posObj = childDict[key];

            // If JArray
            var jArray = posObj as Newtonsoft.Json.Linq.JArray;
            if (jArray != null && jArray.Count >= 2)
            {
                return new Vector2(jArray[0].ToObject<float>(), jArray[1].ToObject<float>());
            }

            // If object[]
            var objArray = posObj as object[];
            if (objArray != null && objArray.Length >= 2)
            {
                return new Vector2(Convert.ToSingle(objArray[0]), Convert.ToSingle(objArray[1]));
            }

            // If List<object>
            var listObj = posObj as System.Collections.IList;
            if (listObj != null && listObj.Count >= 2)
            {
                return new Vector2(Convert.ToSingle(listObj[0]), Convert.ToSingle(listObj[1]));
            }

            return Vector2.zero;
        }

        #region Delete operations implementation

        private (bool, string) DeleteAnimatorState(AnimatorController controller, CommandParams p)
        {
            if (string.IsNullOrEmpty(p.name))
            {
                return (false, "Missing required parameter: name");
            }

            int layerIndex = p.layer >= 0 ? p.layer : 0;
            if (layerIndex >= controller.layers.Length)
            {
                return (false, $"Layer index {layerIndex} out of range (0-{controller.layers.Length - 1})");
            }

            var layer = controller.layers[layerIndex];
            var stateMachine = layer.stateMachine;

            // Search for state
            AnimatorState targetState = null;
            foreach (var childState in stateMachine.states)
            {
                if (childState.state.name == p.name)
                {
                    targetState = childState.state;
                    break;
                }
            }

            if (targetState == null)
            {
                return (false, $"State '{p.name}' not found in layer {layerIndex}");
            }

            // Delete state
            stateMachine.RemoveState(targetState);

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            string result = $"Deleted state '{p.name}' from layer {layerIndex}";
            ConsoleLog($"[CommandExecutor] {result}");
            return (true, result);
        }

        private (bool, string) DeleteAnimatorLayer(AnimatorController controller, CommandParams p)
        {
            if (string.IsNullOrEmpty(p.name))
            {
                return (false, "Missing required parameter: name");
            }

            // Cannot delete Base Layer
            if (controller.layers.Length <= 1)
            {
                return (false, "Cannot delete the only layer");
            }

            // Search for layer
            int targetIndex = -1;
            for (int i = 0; i < controller.layers.Length; i++)
            {
                if (controller.layers[i].name == p.name)
                {
                    targetIndex = i;
                    break;
                }
            }

            if (targetIndex < 0)
            {
                return (false, $"Layer '{p.name}' not found");
            }

            // Delete layer
            controller.RemoveLayer(targetIndex);

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            string result = $"Deleted layer '{p.name}'";
            ConsoleLog($"[CommandExecutor] {result}");
            return (true, result);
        }

        private (bool, string) DeleteAnimatorParameter(AnimatorController controller, CommandParams p)
        {
            if (string.IsNullOrEmpty(p.name))
            {
                return (false, "Missing required parameter: name");
            }

            // Search for parameter
            int targetIndex = -1;
            for (int i = 0; i < controller.parameters.Length; i++)
            {
                if (controller.parameters[i].name == p.name)
                {
                    targetIndex = i;
                    break;
                }
            }

            if (targetIndex < 0)
            {
                return (false, $"Parameter '{p.name}' not found");
            }

            // Delete parameter
            controller.RemoveParameter(targetIndex);

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            string result = $"Deleted parameter '{p.name}'";
            ConsoleLog($"[CommandExecutor] {result}");
            return (true, result);
        }

        private (bool, string) DeleteAnimatorTransition(AnimatorController controller, CommandParams p)
        {
            if (string.IsNullOrEmpty(p.from_state))
            {
                return (false, "Missing required parameter: from_state");
            }

            if (string.IsNullOrEmpty(p.to_state))
            {
                return (false, "Missing required parameter: to_state");
            }

            int layerIndex = p.layer >= 0 ? p.layer : 0;
            if (layerIndex >= controller.layers.Length)
            {
                return (false, $"Layer index {layerIndex} out of range (0-{controller.layers.Length - 1})");
            }

            var layer = controller.layers[layerIndex];
            var stateMachine = layer.stateMachine;

            bool isFromAnyState = p.from_state.ToLower() == "any";

            if (isFromAnyState)
            {
                // Transition from Any State
                foreach (var transition in stateMachine.anyStateTransitions)
                {
                    if (transition.destinationState != null && transition.destinationState.name == p.to_state)
                    {
                        stateMachine.RemoveAnyStateTransition(transition);
                        EditorUtility.SetDirty(controller);
                        AssetDatabase.SaveAssets();
                        string result = $"Deleted transition from Any State to '{p.to_state}' in layer {layerIndex}";
                        ConsoleLog($"[CommandExecutor] {result}");
                        return (true, result);
                    }
                }
                return (false, $"Transition from Any State to '{p.to_state}' not found in layer {layerIndex}");
            }
            else
            {
                // Transition from normal state
                AnimatorState fromState = null;
                foreach (var childState in stateMachine.states)
                {
                    if (childState.state.name == p.from_state)
                    {
                        fromState = childState.state;
                        break;
                    }
                }

                if (fromState == null)
                {
                    return (false, $"Source state '{p.from_state}' not found in layer {layerIndex}");
                }

                foreach (var transition in fromState.transitions)
                {
                    if (transition.destinationState != null && transition.destinationState.name == p.to_state)
                    {
                        fromState.RemoveTransition(transition);
                        EditorUtility.SetDirty(controller);
                        AssetDatabase.SaveAssets();
                        string result = $"Deleted transition from '{p.from_state}' to '{p.to_state}' in layer {layerIndex}";
                        ConsoleLog($"[CommandExecutor] {result}");
                        return (true, result);
                    }
                }
                return (false, $"Transition from '{p.from_state}' to '{p.to_state}' not found in layer {layerIndex}");
            }
        }

        #endregion

        #region Get/Set operations implementation

        private (bool, string) GetAnimatorElement(AnimatorController controller, CommandParams p)
        {
            var sb = new StringBuilder();

            switch (p.type.ToLower())
            {
                case "state":
                    return GetStates(controller, p, sb);
                case "layer":
                    return GetLayers(controller, p, sb);
                case "parameter":
                    return GetParameters(controller, p, sb);
                case "blend_tree":
                    return GetBlendTree(controller, p, sb);
                default:
                    return (false, $"Invalid type for get: '{p.type}'. Must be one of: state, layer, parameter, blend_tree");
            }
        }

        private (bool, string) GetStates(AnimatorController controller, CommandParams p, StringBuilder sb)
        {
            int layerIndex = p.layer >= 0 ? p.layer : -1; // -1 = all layers

            sb.AppendLine($"States in AnimatorController '{controller.name}':");
            sb.AppendLine();

            for (int i = 0; i < controller.layers.Length; i++)
            {
                if (layerIndex >= 0 && i != layerIndex) continue;

                var layer = controller.layers[i];
                var stateMachine = layer.stateMachine;

                sb.AppendLine($"Layer [{i}] {layer.name}:");

                if (!string.IsNullOrEmpty(p.name))
                {
                    // Specific state only
                    foreach (var childState in stateMachine.states)
                    {
                        if (childState.state.name == p.name)
                        {
                            AppendStateInfo(sb, childState, stateMachine.defaultState);
                            break;
                        }
                    }
                }
                else
                {
                    // All states
                    foreach (var childState in stateMachine.states)
                    {
                        AppendStateInfo(sb, childState, stateMachine.defaultState);
                    }
                }
                sb.AppendLine();
            }

            return (true, sb.ToString());
        }

        private void AppendStateInfo(StringBuilder sb, ChildAnimatorState childState, AnimatorState defaultState)
        {
            var state = childState.state;
            string defaultMarker = (state == defaultState) ? " [default]" : "";
            sb.AppendLine($"  - {state.name}{defaultMarker}");
            sb.AppendLine($"      Position: ({childState.position.x}, {childState.position.y})");
            sb.AppendLine($"      Speed: {state.speed}");
            if (state.motion != null)
            {
                sb.AppendLine($"      Motion: {AssetDatabase.GetAssetPath(state.motion)}");
            }
            if (!string.IsNullOrEmpty(state.tag))
            {
                sb.AppendLine($"      Tag: {state.tag}");
            }
        }

        private (bool, string) GetLayers(AnimatorController controller, CommandParams p, StringBuilder sb)
        {
            sb.AppendLine($"Layers in AnimatorController '{controller.name}':");
            sb.AppendLine();

            for (int i = 0; i < controller.layers.Length; i++)
            {
                var layer = controller.layers[i];

                if (!string.IsNullOrEmpty(p.name) && layer.name != p.name) continue;

                sb.AppendLine($"[{i}] {layer.name}");
                sb.AppendLine($"    Weight: {layer.defaultWeight}");
                sb.AppendLine($"    Blending Mode: {layer.blendingMode}");
                if (layer.avatarMask != null)
                {
                    sb.AppendLine($"    Avatar Mask: {AssetDatabase.GetAssetPath(layer.avatarMask)}");
                }
                sb.AppendLine($"    States: {layer.stateMachine.states.Length}");
                sb.AppendLine();
            }

            return (true, sb.ToString());
        }

        private (bool, string) GetParameters(AnimatorController controller, CommandParams p, StringBuilder sb)
        {
            sb.AppendLine($"Parameters in AnimatorController '{controller.name}':");
            sb.AppendLine();

            foreach (var param in controller.parameters)
            {
                if (!string.IsNullOrEmpty(p.name) && param.name != p.name) continue;

                string defaultValue = "";
                switch (param.type)
                {
                    case AnimatorControllerParameterType.Bool:
                        defaultValue = $"= {param.defaultBool}";
                        break;
                    case AnimatorControllerParameterType.Float:
                        defaultValue = $"= {param.defaultFloat}";
                        break;
                    case AnimatorControllerParameterType.Int:
                        defaultValue = $"= {param.defaultInt}";
                        break;
                    case AnimatorControllerParameterType.Trigger:
                        defaultValue = "(trigger)";
                        break;
                }
                sb.AppendLine($"  [{param.type}] {param.name} {defaultValue}");
            }

            return (true, sb.ToString());
        }

        private (bool, string) GetBlendTree(AnimatorController controller, CommandParams p, StringBuilder sb)
        {
            if (string.IsNullOrEmpty(p.name))
            {
                return (false, "Missing required parameter: name (blend tree state name)");
            }

            int layerIndex = p.layer >= 0 ? p.layer : 0;
            if (layerIndex >= controller.layers.Length)
            {
                return (false, $"Layer index {layerIndex} out of range (0-{controller.layers.Length - 1})");
            }

            var layer = controller.layers[layerIndex];
            var stateMachine = layer.stateMachine;

            foreach (var childState in stateMachine.states)
            {
                if (childState.state.name == p.name)
                {
                    var blendTree = childState.state.motion as BlendTree;
                    if (blendTree == null)
                    {
                        return (false, $"State '{p.name}' does not contain a BlendTree");
                    }

                    sb.AppendLine($"BlendTree '{p.name}' in layer {layerIndex}:");
                    sb.AppendLine();
                    sb.AppendLine($"  Blend Type: {blendTree.blendType}");
                    sb.AppendLine($"  Blend Parameter: {blendTree.blendParameter}");
                    if (!string.IsNullOrEmpty(blendTree.blendParameterY))
                    {
                        sb.AppendLine($"  Blend Parameter Y: {blendTree.blendParameterY}");
                    }
                    sb.AppendLine($"  Children: {blendTree.children.Length}");

                    bool is2D = blendTree.blendType != BlendTreeType.Simple1D && blendTree.blendType != BlendTreeType.Direct;
                    foreach (var child in blendTree.children)
                    {
                        string motionPath = child.motion != null ? AssetDatabase.GetAssetPath(child.motion) : "(none)";
                        if (is2D)
                        {
                            sb.AppendLine($"    - Position: ({child.position.x}, {child.position.y}), Motion: {motionPath}");
                        }
                        else
                        {
                            sb.AppendLine($"    - Threshold: {child.threshold}, Motion: {motionPath}");
                        }
                    }

                    return (true, sb.ToString());
                }
            }

            return (false, $"State '{p.name}' not found in layer {layerIndex}");
        }

        private (bool, string) SetAnimatorElement(AnimatorController controller, CommandParams p)
        {
            switch (p.type.ToLower())
            {
                case "state":
                    return SetState(controller, p);
                case "layer":
                    return SetLayer(controller, p);
                case "parameter":
                    return SetParameter(controller, p);
                case "blend_tree":
                    return SetBlendTree(controller, p);
                default:
                    return (false, $"Invalid type for set: '{p.type}'. Must be one of: state, layer, parameter, blend_tree");
            }
        }

        private (bool, string) SetState(AnimatorController controller, CommandParams p)
        {
            if (string.IsNullOrEmpty(p.name))
            {
                return (false, "Missing required parameter: name");
            }

            int layerIndex = p.layer >= 0 ? p.layer : 0;
            if (layerIndex >= controller.layers.Length)
            {
                return (false, $"Layer index {layerIndex} out of range (0-{controller.layers.Length - 1})");
            }

            var layer = controller.layers[layerIndex];
            var stateMachine = layer.stateMachine;
            var states = stateMachine.states;

            for (int i = 0; i < states.Length; i++)
            {
                if (states[i].state.name == p.name)
                {
                    var state = states[i].state;
                    var changes = new List<string>();

                    // Set position
                    if (p.position != null && p.position.Length >= 2)
                    {
                        states[i].position = new Vector3(p.position[0], p.position[1], 0);
                        stateMachine.states = states;
                        changes.Add($"position=({p.position[0]}, {p.position[1]})");
                    }

                    // Set speed
                    if (p.speed >= 0)
                    {
                        state.speed = p.speed;
                        changes.Add($"speed={p.speed}");
                    }

                    // Set motion
                    if (!string.IsNullOrEmpty(p.motion))
                    {
                        var clip = LoadAnimationClip(p.motion);
                        if (clip != null)
                        {
                            state.motion = clip;
                            changes.Add($"motion={p.motion}");
                        }
                        else
                        {
                            ConsoleLogWarning($"[CommandExecutor] Animation clip not found: {p.motion}");
                        }
                    }

                    // Set tag
                    if (!string.IsNullOrEmpty(p.tag))
                    {
                        state.tag = p.tag;
                        changes.Add($"tag={p.tag}");
                    }

                    EditorUtility.SetDirty(controller);
                    AssetDatabase.SaveAssets();

                    string result = $"Updated state '{p.name}': {string.Join(", ", changes)}";
                    ConsoleLog($"[CommandExecutor] {result}");
                    return (true, result);
                }
            }

            return (false, $"State '{p.name}' not found in layer {layerIndex}");
        }

        private (bool, string) SetLayer(AnimatorController controller, CommandParams p)
        {
            if (string.IsNullOrEmpty(p.name))
            {
                return (false, "Missing required parameter: name");
            }

            var layers = controller.layers;
            for (int i = 0; i < layers.Length; i++)
            {
                if (layers[i].name == p.name)
                {
                    var changes = new List<string>();

                    // Set weight
                    if (p.weight >= 0)
                    {
                        layers[i].defaultWeight = p.weight;
                        changes.Add($"weight={p.weight}");
                    }

                    // Set blending mode
                    if (!string.IsNullOrEmpty(p.blending_mode))
                    {
                        switch (p.blending_mode.ToLower())
                        {
                            case "override":
                                layers[i].blendingMode = AnimatorLayerBlendingMode.Override;
                                changes.Add("blending_mode=Override");
                                break;
                            case "additive":
                                layers[i].blendingMode = AnimatorLayerBlendingMode.Additive;
                                changes.Add("blending_mode=Additive");
                                break;
                        }
                    }

                    controller.layers = layers;
                    EditorUtility.SetDirty(controller);
                    AssetDatabase.SaveAssets();

                    string result = $"Updated layer '{p.name}': {string.Join(", ", changes)}";
                    ConsoleLog($"[CommandExecutor] {result}");
                    return (true, result);
                }
            }

            return (false, $"Layer '{p.name}' not found");
        }

        private (bool, string) SetParameter(AnimatorController controller, CommandParams p)
        {
            if (string.IsNullOrEmpty(p.name))
            {
                return (false, "Missing required parameter: name");
            }

            if (p.default_value == null)
            {
                return (false, "Missing required parameter: default_value");
            }

            // Search for parameter
            var parameters = controller.parameters;
            int targetIndex = -1;
            AnimatorControllerParameterType targetType = AnimatorControllerParameterType.Float;

            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].name == p.name)
                {
                    targetIndex = i;
                    targetType = parameters[i].type;
                    break;
                }
            }

            if (targetIndex < 0)
            {
                return (false, $"Parameter '{p.name}' not found");
            }

            if (targetType == AnimatorControllerParameterType.Trigger)
            {
                return (false, "Trigger parameters do not have a default value");
            }

            // Create new parameter and set default value
            var newParam = new AnimatorControllerParameter
            {
                name = p.name,
                type = targetType
            };

            string changeInfo = "";
            switch (targetType)
            {
                case AnimatorControllerParameterType.Float:
                    newParam.defaultFloat = Convert.ToSingle(p.default_value);
                    changeInfo = $"default_value={newParam.defaultFloat}";
                    break;
                case AnimatorControllerParameterType.Int:
                    newParam.defaultInt = Convert.ToInt32(p.default_value);
                    changeInfo = $"default_value={newParam.defaultInt}";
                    break;
                case AnimatorControllerParameterType.Bool:
                    newParam.defaultBool = Convert.ToBoolean(p.default_value);
                    changeInfo = $"default_value={newParam.defaultBool}";
                    break;
            }

            // Replace parameter
            var paramList = new List<AnimatorControllerParameter>(parameters);
            paramList[targetIndex] = newParam;
            controller.parameters = paramList.ToArray();

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            string result = $"Updated parameter '{p.name}': {changeInfo}";
            ConsoleLog($"[CommandExecutor] {result}");
            return (true, result);
        }

        private (bool, string) SetBlendTree(AnimatorController controller, CommandParams p)
        {
            if (string.IsNullOrEmpty(p.name))
            {
                return (false, "Missing required parameter: name (blend tree state name)");
            }

            int layerIndex = p.layer >= 0 ? p.layer : 0;
            if (layerIndex >= controller.layers.Length)
            {
                return (false, $"Layer index {layerIndex} out of range (0-{controller.layers.Length - 1})");
            }

            var layer = controller.layers[layerIndex];
            var stateMachine = layer.stateMachine;

            foreach (var childState in stateMachine.states)
            {
                if (childState.state.name == p.name)
                {
                    var blendTree = childState.state.motion as BlendTree;
                    if (blendTree == null)
                    {
                        return (false, $"State '{p.name}' does not contain a BlendTree");
                    }

                    var changes = new List<string>();

                    // Set parameter
                    if (!string.IsNullOrEmpty(p.parameter))
                    {
                        blendTree.blendParameter = p.parameter;
                        changes.Add($"parameter={p.parameter}");
                    }

                    // Set Y-axis parameter (for 2D blend tree)
                    if (!string.IsNullOrEmpty(p.parameter_y))
                    {
                        blendTree.blendParameterY = p.parameter_y;
                        changes.Add($"parameter_y={p.parameter_y}");
                    }

                    // Set blend type
                    if (!string.IsNullOrEmpty(p.blend_type))
                    {
                        switch (p.blend_type.ToLower())
                        {
                            case "1d":
                            case "simple1d":
                                blendTree.blendType = BlendTreeType.Simple1D;
                                changes.Add("blend_type=Simple1D");
                                break;
                            case "2d":
                            case "simpledirectional2d":
                                blendTree.blendType = BlendTreeType.SimpleDirectional2D;
                                changes.Add("blend_type=SimpleDirectional2D");
                                break;
                            case "freeformdirectional2d":
                                blendTree.blendType = BlendTreeType.FreeformDirectional2D;
                                changes.Add("blend_type=FreeformDirectional2D");
                                break;
                            case "freeformcartesian2d":
                                blendTree.blendType = BlendTreeType.FreeformCartesian2D;
                                changes.Add("blend_type=FreeformCartesian2D");
                                break;
                            case "direct":
                                blendTree.blendType = BlendTreeType.Direct;
                                changes.Add("blend_type=Direct");
                                break;
                        }
                    }

                    // Update child elements (clear existing and re-add)
                    if (p.children != null && p.children.Length > 0)
                    {
                        // Clear existing children
                        while (blendTree.children.Length > 0)
                        {
                            blendTree.RemoveChild(0);
                        }

                        bool is2D = blendTree.blendType != BlendTreeType.Simple1D && blendTree.blendType != BlendTreeType.Direct;
                        int addedCount = 0;

                        // Add new children
                        foreach (var childObj in p.children)
                        {
                            try
                            {
                                var childDict = childObj as Dictionary<string, object>;
                                if (childDict == null)
                                {
                                    var jObj = childObj as Newtonsoft.Json.Linq.JObject;
                                    if (jObj != null)
                                    {
                                        childDict = jObj.ToObject<Dictionary<string, object>>();
                                    }
                                }

                                if (childDict != null)
                                {
                                    string motionPath = childDict.ContainsKey("motion") ? childDict["motion"].ToString() : "";

                                    if (!string.IsNullOrEmpty(motionPath))
                                    {
                                        var clip = LoadAnimationClip(motionPath);
                                        if (clip != null)
                                        {
                                            if (is2D && childDict.ContainsKey("position"))
                                            {
                                                // 2D BlendTree: use position [x, y]
                                                Vector2 pos = ParseVector2FromChild(childDict, "position");
                                                blendTree.AddChild(clip, pos);
                                            }
                                            else
                                            {
                                                // 1D BlendTree: use threshold
                                                float thresholdVal = childDict.ContainsKey("threshold") ? Convert.ToSingle(childDict["threshold"]) : 0f;
                                                blendTree.AddChild(clip, thresholdVal);
                                            }
                                            addedCount++;
                                        }
                                        else
                                        {
                                            ConsoleLogWarning($"[CommandExecutor] Animation clip not found: {motionPath}");
                                        }
                                    }
                                }
                            }
                            catch (Exception e)
                            {
                                ConsoleLogWarning($"[CommandExecutor] Failed to parse child: {e.Message}");
                            }
                        }
                        changes.Add($"children={addedCount} items added");
                    }

                    // Mark both BlendTree and Controller as Dirty
                    EditorUtility.SetDirty(blendTree);
                    EditorUtility.SetDirty(controller);
                    AssetDatabase.SaveAssets();

                    string result = $"Updated BlendTree '{p.name}': {string.Join(", ", changes)}";
                    ConsoleLog($"[CommandExecutor] {result}");
                    return (true, result);
                }
            }

            return (false, $"State '{p.name}' not found in layer {layerIndex}");
        }

        #endregion

    }
}
