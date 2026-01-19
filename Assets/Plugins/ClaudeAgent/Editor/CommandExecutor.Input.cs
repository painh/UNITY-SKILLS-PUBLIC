using UnityEngine;
using UnityEditor;
using System;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json.Linq;

namespace ClaudeAgent
{
    public partial class CommandExecutor
    {
        /// <summary>
        /// Registers input simulation commands
        /// </summary>
        private void RegisterInputCommands()
        {
            RegisterCommand("simulate_key", SimulateKey);
            RegisterCommand("simulate_mouse", SimulateMouse);
            RegisterCommand("simulate_input_sequence", SimulateInputSequence);
        }

        #region Input System Detection

        private static Type _keyboardType;
        private static Type _mouseType;
        private static Type _inputStateType;
        private static Type _inputSystemType;
        private static bool _inputSystemChecked;
        private static bool _inputSystemAvailable;

        /// <summary>
        /// Check if New Input System is available
        /// </summary>
        private bool IsNewInputSystemAvailable()
        {
            if (_inputSystemChecked) return _inputSystemAvailable;

            _inputSystemChecked = true;

            // Try to load New Input System types
            _keyboardType = Type.GetType("UnityEngine.InputSystem.Keyboard, Unity.InputSystem");
            _mouseType = Type.GetType("UnityEngine.InputSystem.Mouse, Unity.InputSystem");
            _inputStateType = Type.GetType("UnityEngine.InputSystem.LowLevel.InputState, Unity.InputSystem");
            _inputSystemType = Type.GetType("UnityEngine.InputSystem.InputSystem, Unity.InputSystem");

            _inputSystemAvailable = _keyboardType != null && _mouseType != null &&
                                    _inputStateType != null && _inputSystemType != null;

            if (_inputSystemAvailable)
            {
                ConsoleLog("[CommandExecutor] New Input System detected");
            }
            else
            {
                ConsoleLog("[CommandExecutor] New Input System not available");
            }

            return _inputSystemAvailable;
        }

        /// <summary>
        /// Get the current Keyboard device
        /// </summary>
        private object GetKeyboard()
        {
            if (!IsNewInputSystemAvailable()) return null;

            var currentProperty = _keyboardType.GetProperty("current", BindingFlags.Public | BindingFlags.Static);
            return currentProperty?.GetValue(null);
        }

        /// <summary>
        /// Get the current Mouse device
        /// </summary>
        private object GetMouse()
        {
            if (!IsNewInputSystemAvailable()) return null;

            var currentProperty = _mouseType.GetProperty("current", BindingFlags.Public | BindingFlags.Static);
            return currentProperty?.GetValue(null);
        }

        #endregion

        #region Key Simulation

        /// <summary>
        /// Simulate keyboard input
        /// </summary>
        private (bool, string) SimulateKey(CommandParams p)
        {
            try
            {
                // Check play mode
                if (!EditorApplication.isPlaying)
                {
                    return Error("simulate_key requires Play mode. Enter Play mode first.");
                }

                // Validate params
                if (p == null || string.IsNullOrEmpty(p.key))
                {
                    return Error("Missing required parameter: key");
                }

                if (!IsNewInputSystemAvailable())
                {
                    return Error("New Input System is not available. Please install the Input System package.");
                }

                var keyboard = GetKeyboard();
                if (keyboard == null)
                {
                    return Error("No keyboard device found. Ensure a keyboard is connected.");
                }

                // Parse action
                string action = string.IsNullOrEmpty(p.action) ? "tap" : p.action.ToLower();
                if (action != "press" && action != "release" && action != "tap")
                {
                    return Error($"Invalid action: {p.action}. Use 'press', 'release', or 'tap'.");
                }

                // Get key control
                var keyControl = GetKeyControl(keyboard, p.key);
                if (keyControl == null)
                {
                    return Error($"Unknown key: {p.key}. Use Unity KeyCode names (e.g., 'W', 'Space', 'LeftCtrl').");
                }

                // Simulate key
                var result = new StringBuilder();

                if (action == "press" || action == "tap")
                {
                    SetKeyState(keyControl, true);
                    result.Append($"Pressed key: {p.key}");
                }

                if (action == "release" || action == "tap")
                {
                    if (action == "tap")
                    {
                        // For tap, we need to queue the release after a frame
                        // Using coroutine-like behavior through EditorApplication.delayCall
                        var releaseControl = keyControl;
                        EditorApplication.delayCall += () =>
                        {
                            if (EditorApplication.isPlaying)
                            {
                                SetKeyState(releaseControl, false);
                            }
                        };
                        result.Append(" (will release next frame)");
                    }
                    else
                    {
                        SetKeyState(keyControl, false);
                        result.Append($"Released key: {p.key}");
                    }
                }

                ConsoleLog($"[CommandExecutor] {result}");
                return Success(result.ToString());
            }
            catch (Exception e)
            {
                return Error($"Error simulating key: {e.Message}");
            }
        }

        /// <summary>
        /// Get key control from keyboard by key name
        /// </summary>
        private object GetKeyControl(object keyboard, string keyName)
        {
            // Common key name mappings
            var keyMappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                // Letters (direct mapping)
                { "A", "aKey" }, { "B", "bKey" }, { "C", "cKey" }, { "D", "dKey" },
                { "E", "eKey" }, { "F", "fKey" }, { "G", "gKey" }, { "H", "hKey" },
                { "I", "iKey" }, { "J", "jKey" }, { "K", "kKey" }, { "L", "lKey" },
                { "M", "mKey" }, { "N", "nKey" }, { "O", "oKey" }, { "P", "pKey" },
                { "Q", "qKey" }, { "R", "rKey" }, { "S", "sKey" }, { "T", "tKey" },
                { "U", "uKey" }, { "V", "vKey" }, { "W", "wKey" }, { "X", "xKey" },
                { "Y", "yKey" }, { "Z", "zKey" },

                // Numbers
                { "0", "digit0Key" }, { "1", "digit1Key" }, { "2", "digit2Key" },
                { "3", "digit3Key" }, { "4", "digit4Key" }, { "5", "digit5Key" },
                { "6", "digit6Key" }, { "7", "digit7Key" }, { "8", "digit8Key" },
                { "9", "digit9Key" },
                { "Alpha0", "digit0Key" }, { "Alpha1", "digit1Key" }, { "Alpha2", "digit2Key" },
                { "Alpha3", "digit3Key" }, { "Alpha4", "digit4Key" }, { "Alpha5", "digit5Key" },
                { "Alpha6", "digit6Key" }, { "Alpha7", "digit7Key" }, { "Alpha8", "digit8Key" },
                { "Alpha9", "digit9Key" },

                // Function keys
                { "F1", "f1Key" }, { "F2", "f2Key" }, { "F3", "f3Key" }, { "F4", "f4Key" },
                { "F5", "f5Key" }, { "F6", "f6Key" }, { "F7", "f7Key" }, { "F8", "f8Key" },
                { "F9", "f9Key" }, { "F10", "f10Key" }, { "F11", "f11Key" }, { "F12", "f12Key" },

                // Modifiers
                { "LeftShift", "leftShiftKey" }, { "RightShift", "rightShiftKey" },
                { "LeftCtrl", "leftCtrlKey" }, { "RightCtrl", "rightCtrlKey" },
                { "LeftControl", "leftCtrlKey" }, { "RightControl", "rightCtrlKey" },
                { "LeftAlt", "leftAltKey" }, { "RightAlt", "rightAltKey" },
                { "LeftCommand", "leftCommandKey" }, { "RightCommand", "rightCommandKey" },
                { "LeftMeta", "leftCommandKey" }, { "RightMeta", "rightCommandKey" },
                { "LeftWindows", "leftWindowsKey" }, { "RightWindows", "rightWindowsKey" },

                // Special keys
                { "Space", "spaceKey" },
                { "Enter", "enterKey" }, { "Return", "enterKey" },
                { "Tab", "tabKey" },
                { "Backspace", "backspaceKey" },
                { "Delete", "deleteKey" },
                { "Escape", "escapeKey" }, { "Esc", "escapeKey" },
                { "Insert", "insertKey" },
                { "Home", "homeKey" },
                { "End", "endKey" },
                { "PageUp", "pageUpKey" },
                { "PageDown", "pageDownKey" },
                { "CapsLock", "capsLockKey" },
                { "NumLock", "numLockKey" },
                { "ScrollLock", "scrollLockKey" },
                { "PrintScreen", "printScreenKey" },
                { "Pause", "pauseKey" },

                // Arrow keys
                { "UpArrow", "upArrowKey" }, { "Up", "upArrowKey" },
                { "DownArrow", "downArrowKey" }, { "Down", "downArrowKey" },
                { "LeftArrow", "leftArrowKey" }, { "Left", "leftArrowKey" },
                { "RightArrow", "rightArrowKey" }, { "Right", "rightArrowKey" },

                // Numpad
                { "Keypad0", "numpad0Key" }, { "Keypad1", "numpad1Key" }, { "Keypad2", "numpad2Key" },
                { "Keypad3", "numpad3Key" }, { "Keypad4", "numpad4Key" }, { "Keypad5", "numpad5Key" },
                { "Keypad6", "numpad6Key" }, { "Keypad7", "numpad7Key" }, { "Keypad8", "numpad8Key" },
                { "Keypad9", "numpad9Key" },
                { "KeypadPlus", "numpadPlusKey" }, { "KeypadMinus", "numpadMinusKey" },
                { "KeypadMultiply", "numpadMultiplyKey" }, { "KeypadDivide", "numpadDivideKey" },
                { "KeypadEnter", "numpadEnterKey" }, { "KeypadPeriod", "numpadPeriodKey" },

                // Symbols
                { "Minus", "minusKey" }, { "Equals", "equalsKey" },
                { "LeftBracket", "leftBracketKey" }, { "RightBracket", "rightBracketKey" },
                { "Backslash", "backslashKey" }, { "Semicolon", "semicolonKey" },
                { "Quote", "quoteKey" }, { "Comma", "commaKey" },
                { "Period", "periodKey" }, { "Slash", "slashKey" },
                { "Backquote", "backquoteKey" }, { "Tilde", "backquoteKey" }
            };

            // Try direct mapping first
            string propertyName;
            if (keyMappings.TryGetValue(keyName, out propertyName))
            {
                var property = _keyboardType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
                if (property != null)
                {
                    return property.GetValue(keyboard);
                }
            }

            // Try as-is property name (e.g., "spaceKey")
            var directProperty = _keyboardType.GetProperty(keyName, BindingFlags.Public | BindingFlags.Instance);
            if (directProperty != null)
            {
                return directProperty.GetValue(keyboard);
            }

            // Try lowercase first letter + Key suffix
            string lowerKey = keyName.ToLower();
            if (lowerKey.Length == 1)
            {
                var charProperty = _keyboardType.GetProperty(lowerKey + "Key", BindingFlags.Public | BindingFlags.Instance);
                if (charProperty != null)
                {
                    return charProperty.GetValue(keyboard);
                }
            }

            return null;
        }

        /// <summary>
        /// Set key state using InputState.Change
        /// </summary>
        private void SetKeyState(object keyControl, bool pressed)
        {
            // InputState.Change(control, value)
            var changeMethod = _inputStateType.GetMethod("Change",
                new Type[] { typeof(object).MakeByRefType(), typeof(float) });

            if (changeMethod == null)
            {
                // Try alternative signature
                var methods = _inputStateType.GetMethods(BindingFlags.Public | BindingFlags.Static);
                foreach (var m in methods)
                {
                    if (m.Name == "Change" && m.GetParameters().Length == 2)
                    {
                        var parameters = m.GetParameters();
                        // Find Change<TValue>(InputControl<TValue>, TValue) method
                        if (m.IsGenericMethod)
                        {
                            var genericMethod = m.MakeGenericMethod(typeof(float));
                            genericMethod.Invoke(null, new object[] { keyControl, pressed ? 1f : 0f });
                            return;
                        }
                    }
                }

                // Try using QueueStateEvent approach
                UseQueueStateEvent(keyControl, pressed);
            }
            else
            {
                changeMethod.Invoke(null, new object[] { keyControl, pressed ? 1f : 0f });
            }
        }

        /// <summary>
        /// Alternative method using QueueStateEvent
        /// </summary>
        private void UseQueueStateEvent(object control, bool pressed)
        {
            // Get the InputControl type
            var inputControlType = Type.GetType("UnityEngine.InputSystem.InputControl, Unity.InputSystem");
            if (inputControlType == null) return;

            // Use reflection to call InputSystem.QueueDeltaStateEvent or directly modify state
            // This is a simplified approach - for production, consider using InputTestFixture

            // Try using the InputState.Change with proper generic invocation
            var inputStateChangeMethod = _inputStateType.GetMethods(BindingFlags.Public | BindingFlags.Static);
            foreach (var method in inputStateChangeMethod)
            {
                if (method.Name == "Change" && method.IsGenericMethodDefinition)
                {
                    var genericParams = method.GetGenericArguments();
                    if (genericParams.Length == 1)
                    {
                        try
                        {
                            var genericMethod = method.MakeGenericMethod(typeof(float));
                            genericMethod.Invoke(null, new object[] { control, pressed ? 1f : 0f });
                            return;
                        }
                        catch { }
                    }
                }
            }
        }

        #endregion

        #region Mouse Simulation

        /// <summary>
        /// Simulate mouse input
        /// </summary>
        private (bool, string) SimulateMouse(CommandParams p)
        {
            try
            {
                // Check play mode
                if (!EditorApplication.isPlaying)
                {
                    return Error("simulate_mouse requires Play mode. Enter Play mode first.");
                }

                if (!IsNewInputSystemAvailable())
                {
                    return Error("New Input System is not available. Please install the Input System package.");
                }

                var mouse = GetMouse();
                if (mouse == null)
                {
                    return Error("No mouse device found.");
                }

                // Parse button (default: left)
                string button = string.IsNullOrEmpty(p.button) ? "left" : p.button.ToLower();
                if (button != "left" && button != "right" && button != "middle")
                {
                    return Error($"Invalid button: {p.button}. Use 'left', 'right', or 'middle'.");
                }

                // Parse action (default: click)
                string action = string.IsNullOrEmpty(p.action) ? "click" : p.action.ToLower();
                if (action != "click" && action != "down" && action != "up" && action != "doubleclick")
                {
                    return Error($"Invalid action: {p.action}. Use 'click', 'down', 'up', or 'doubleclick'.");
                }

                // Set mouse position if specified
                if (p.mouse_position != null && p.mouse_position.Length >= 2)
                {
                    SetMousePosition(mouse, p.mouse_position[0], p.mouse_position[1]);
                }

                // Get button control
                var buttonControl = GetMouseButtonControl(mouse, button);
                if (buttonControl == null)
                {
                    return Error($"Failed to get mouse button control: {button}");
                }

                var result = new StringBuilder();

                // Perform action
                if (action == "down" || action == "click" || action == "doubleclick")
                {
                    SetKeyState(buttonControl, true);
                    result.Append($"Mouse {button} button down");
                }

                if (action == "up" || action == "click")
                {
                    if (action == "click")
                    {
                        var releaseControl = buttonControl;
                        EditorApplication.delayCall += () =>
                        {
                            if (EditorApplication.isPlaying)
                            {
                                SetKeyState(releaseControl, false);
                            }
                        };
                        result.Append(" (will release next frame)");
                    }
                    else
                    {
                        SetKeyState(buttonControl, false);
                        result.Append($"Mouse {button} button up");
                    }
                }

                if (action == "doubleclick")
                {
                    // Schedule second click
                    var doubleClickControl = buttonControl;
                    EditorApplication.delayCall += () =>
                    {
                        if (EditorApplication.isPlaying)
                        {
                            SetKeyState(doubleClickControl, false);
                            EditorApplication.delayCall += () =>
                            {
                                if (EditorApplication.isPlaying)
                                {
                                    SetKeyState(doubleClickControl, true);
                                    EditorApplication.delayCall += () =>
                                    {
                                        if (EditorApplication.isPlaying)
                                        {
                                            SetKeyState(doubleClickControl, false);
                                        }
                                    };
                                }
                            };
                        }
                    };
                    result.Append(" (double click)");
                }

                if (p.mouse_position != null && p.mouse_position.Length >= 2)
                {
                    result.Append($" at ({p.mouse_position[0]}, {p.mouse_position[1]})");
                }

                ConsoleLog($"[CommandExecutor] {result}");
                return Success(result.ToString());
            }
            catch (Exception e)
            {
                return Error($"Error simulating mouse: {e.Message}");
            }
        }

        /// <summary>
        /// Get mouse button control
        /// </summary>
        private object GetMouseButtonControl(object mouse, string button)
        {
            string propertyName;
            switch (button.ToLower())
            {
                case "left": propertyName = "leftButton"; break;
                case "right": propertyName = "rightButton"; break;
                case "middle": propertyName = "middleButton"; break;
                default: return null;
            }

            var property = _mouseType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            return property?.GetValue(mouse);
        }

        /// <summary>
        /// Set mouse position
        /// </summary>
        private void SetMousePosition(object mouse, float x, float y)
        {
            // Get position control
            var positionProperty = _mouseType.GetProperty("position", BindingFlags.Public | BindingFlags.Instance);
            if (positionProperty == null) return;

            var positionControl = positionProperty.GetValue(mouse);
            if (positionControl == null) return;

            // Create Vector2 and set state
            var vector2Type = typeof(Vector2);
            var newPosition = new Vector2(x, y);

            // Use InputState.Change with Vector2
            var inputStateChangeMethod = _inputStateType.GetMethods(BindingFlags.Public | BindingFlags.Static);
            foreach (var method in inputStateChangeMethod)
            {
                if (method.Name == "Change" && method.IsGenericMethodDefinition)
                {
                    try
                    {
                        var genericMethod = method.MakeGenericMethod(typeof(Vector2));
                        genericMethod.Invoke(null, new object[] { positionControl, newPosition });
                        return;
                    }
                    catch { }
                }
            }
        }

        #endregion

        #region Input Sequence

        /// <summary>
        /// Simulate a sequence of inputs
        /// </summary>
        private (bool, string) SimulateInputSequence(CommandParams p)
        {
            try
            {
                // Check play mode
                if (!EditorApplication.isPlaying)
                {
                    return Error("simulate_input_sequence requires Play mode. Enter Play mode first.");
                }

                if (!IsNewInputSystemAvailable())
                {
                    return Error("New Input System is not available. Please install the Input System package.");
                }

                // Validate inputs
                if (p == null || p.inputs == null)
                {
                    return Error("Missing required parameter: inputs");
                }

                var inputsArray = p.inputs as JArray;
                if (inputsArray == null || inputsArray.Count == 0)
                {
                    return Error("Parameter 'inputs' must be a non-empty array");
                }

                // Start the sequence coroutine using EditorApplication.update
                StartInputSequence(inputsArray);

                ConsoleLog($"[CommandExecutor] Started input sequence with {inputsArray.Count} steps");
                return Success($"Started input sequence with {inputsArray.Count} steps");
            }
            catch (Exception e)
            {
                return Error($"Error starting input sequence: {e.Message}");
            }
        }

        private Queue<JToken> _pendingInputs;
        private float _waitUntilTime;
        private bool _sequenceRunning;

        /// <summary>
        /// Start processing input sequence
        /// </summary>
        private void StartInputSequence(JArray inputs)
        {
            _pendingInputs = new Queue<JToken>();
            foreach (var input in inputs)
            {
                _pendingInputs.Enqueue(input);
            }
            _waitUntilTime = 0;
            _sequenceRunning = true;

            EditorApplication.update += ProcessInputSequence;
        }

        /// <summary>
        /// Process input sequence step by step
        /// </summary>
        private void ProcessInputSequence()
        {
            if (!_sequenceRunning || !EditorApplication.isPlaying)
            {
                StopInputSequence();
                return;
            }

            // Check if waiting
            if (_waitUntilTime > 0)
            {
                if (Time.realtimeSinceStartup < _waitUntilTime)
                {
                    return; // Still waiting
                }
                _waitUntilTime = 0;
            }

            // Process next input
            if (_pendingInputs.Count == 0)
            {
                StopInputSequence();
                ConsoleLog("[CommandExecutor] Input sequence completed");
                return;
            }

            var input = _pendingInputs.Dequeue() as JObject;
            if (input == null) return;

            string inputType = input["type"]?.ToString()?.ToLower();

            try
            {
                switch (inputType)
                {
                    case "key":
                        ProcessKeyInput(input);
                        break;
                    case "mouse":
                        ProcessMouseInput(input);
                        break;
                    case "wait":
                        float duration = input["duration"]?.ToObject<float>() ?? 0.1f;
                        _waitUntilTime = Time.realtimeSinceStartup + duration;
                        ConsoleLog($"[CommandExecutor] Waiting {duration}s");
                        break;
                    default:
                        ConsoleLogWarning($"[CommandExecutor] Unknown input type: {inputType}");
                        break;
                }
            }
            catch (Exception e)
            {
                ConsoleLogError($"[CommandExecutor] Error processing input: {e.Message}");
            }
        }

        /// <summary>
        /// Process key input in sequence
        /// </summary>
        private void ProcessKeyInput(JObject input)
        {
            var keyboard = GetKeyboard();
            if (keyboard == null) return;

            string key = input["key"]?.ToString();
            string action = input["action"]?.ToString()?.ToLower() ?? "tap";

            if (string.IsNullOrEmpty(key)) return;

            var keyControl = GetKeyControl(keyboard, key);
            if (keyControl == null)
            {
                ConsoleLogWarning($"[CommandExecutor] Unknown key in sequence: {key}");
                return;
            }

            if (action == "down" || action == "press" || action == "tap")
            {
                SetKeyState(keyControl, true);
                ConsoleLog($"[CommandExecutor] Key down: {key}");
            }

            if (action == "up" || action == "release")
            {
                SetKeyState(keyControl, false);
                ConsoleLog($"[CommandExecutor] Key up: {key}");
            }

            if (action == "tap")
            {
                // Schedule release
                var releaseControl = keyControl;
                var releaseKey = key;
                EditorApplication.delayCall += () =>
                {
                    if (EditorApplication.isPlaying)
                    {
                        SetKeyState(releaseControl, false);
                        ConsoleLog($"[CommandExecutor] Key up (tap): {releaseKey}");
                    }
                };
            }
        }

        /// <summary>
        /// Process mouse input in sequence
        /// </summary>
        private void ProcessMouseInput(JObject input)
        {
            var mouse = GetMouse();
            if (mouse == null) return;

            string button = input["button"]?.ToString()?.ToLower() ?? "left";
            string action = input["action"]?.ToString()?.ToLower() ?? "click";

            // Set position if specified
            var positionToken = input["position"] as JArray;
            if (positionToken != null && positionToken.Count >= 2)
            {
                float x = positionToken[0].ToObject<float>();
                float y = positionToken[1].ToObject<float>();
                SetMousePosition(mouse, x, y);
            }

            var buttonControl = GetMouseButtonControl(mouse, button);
            if (buttonControl == null) return;

            if (action == "down" || action == "click")
            {
                SetKeyState(buttonControl, true);
                ConsoleLog($"[CommandExecutor] Mouse {button} down");
            }

            if (action == "up")
            {
                SetKeyState(buttonControl, false);
                ConsoleLog($"[CommandExecutor] Mouse {button} up");
            }

            if (action == "click")
            {
                var releaseControl = buttonControl;
                var releaseButton = button;
                EditorApplication.delayCall += () =>
                {
                    if (EditorApplication.isPlaying)
                    {
                        SetKeyState(releaseControl, false);
                        ConsoleLog($"[CommandExecutor] Mouse {releaseButton} up (click)");
                    }
                };
            }
        }

        /// <summary>
        /// Stop input sequence processing
        /// </summary>
        private void StopInputSequence()
        {
            _sequenceRunning = false;
            _pendingInputs = null;
            EditorApplication.update -= ProcessInputSequence;
        }

        #endregion
    }
}
