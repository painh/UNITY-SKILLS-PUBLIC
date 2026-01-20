using UnityEngine;
using UnityEditor;
using System;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
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

        #region OS-Level Input (for Legacy Input System)

#if UNITY_EDITOR_OSX
        // macOS CGEvent API
        [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
        private static extern IntPtr CGEventCreateKeyboardEvent(IntPtr source, ushort virtualKey, bool keyDown);

        [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
        private static extern void CGEventPost(uint tap, IntPtr eventRef);

        [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
        private static extern void CFRelease(IntPtr cf);

        [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
        private static extern IntPtr CGEventCreateMouseEvent(IntPtr source, uint mouseType, CGPoint mouseCursorPosition, uint mouseButton);

        [StructLayout(LayoutKind.Sequential)]
        private struct CGPoint
        {
            public double x;
            public double y;
            public CGPoint(double x, double y) { this.x = x; this.y = y; }
        }

        // macOS virtual key codes
        private static readonly Dictionary<string, ushort> MacKeyMap = new Dictionary<string, ushort>(StringComparer.OrdinalIgnoreCase)
        {
            // Letters
            { "A", 0x00 }, { "S", 0x01 }, { "D", 0x02 }, { "F", 0x03 },
            { "H", 0x04 }, { "G", 0x05 }, { "Z", 0x06 }, { "X", 0x07 },
            { "C", 0x08 }, { "V", 0x09 }, { "B", 0x0B }, { "Q", 0x0C },
            { "W", 0x0D }, { "E", 0x0E }, { "R", 0x0F }, { "Y", 0x10 },
            { "T", 0x11 }, { "1", 0x12 }, { "2", 0x13 }, { "3", 0x14 },
            { "4", 0x15 }, { "6", 0x16 }, { "5", 0x17 }, { "9", 0x19 },
            { "7", 0x1A }, { "8", 0x1C }, { "0", 0x1D }, { "O", 0x1F },
            { "U", 0x20 }, { "I", 0x22 }, { "P", 0x23 }, { "L", 0x25 },
            { "J", 0x26 }, { "K", 0x28 }, { "N", 0x2D }, { "M", 0x2E },

            // Special keys
            { "Return", 0x24 }, { "Enter", 0x24 },
            { "Tab", 0x30 },
            { "Space", 0x31 },
            { "Delete", 0x33 }, { "Backspace", 0x33 },
            { "Escape", 0x35 }, { "Esc", 0x35 },
            { "LeftCommand", 0x37 }, { "LeftMeta", 0x37 },
            { "LeftShift", 0x38 },
            { "CapsLock", 0x39 },
            { "LeftAlt", 0x3A }, { "LeftOption", 0x3A },
            { "LeftControl", 0x3B }, { "LeftCtrl", 0x3B },
            { "RightShift", 0x3C },
            { "RightAlt", 0x3D }, { "RightOption", 0x3D },
            { "RightControl", 0x3E }, { "RightCtrl", 0x3E },

            // Function keys
            { "F1", 0x7A }, { "F2", 0x78 }, { "F3", 0x63 }, { "F4", 0x76 },
            { "F5", 0x60 }, { "F6", 0x61 }, { "F7", 0x62 }, { "F8", 0x64 },
            { "F9", 0x65 }, { "F10", 0x6D }, { "F11", 0x67 }, { "F12", 0x6F },

            // Arrow keys
            { "Up", 0x7E }, { "UpArrow", 0x7E },
            { "Down", 0x7D }, { "DownArrow", 0x7D },
            { "Left", 0x7B }, { "LeftArrow", 0x7B },
            { "Right", 0x7C }, { "RightArrow", 0x7C },

            // Others
            { "Home", 0x73 }, { "End", 0x77 },
            { "PageUp", 0x74 }, { "PageDown", 0x79 },
        };

        private void SendOSKeyEvent(string keyName, bool keyDown)
        {
            // Focus Game View before sending OS-level input
            FocusGameView();

            if (!MacKeyMap.TryGetValue(keyName, out ushort keyCode))
            {
                ConsoleLogWarning($"[CommandExecutor] Unknown key for macOS: {keyName}");
                return;
            }

            IntPtr eventRef = CGEventCreateKeyboardEvent(IntPtr.Zero, keyCode, keyDown);
            if (eventRef != IntPtr.Zero)
            {
                CGEventPost(0, eventRef); // 0 = kCGHIDEventTap
                CFRelease(eventRef);
            }
        }

        private void SendOSMouseEvent(string button, string action, float x, float y)
        {
            // Focus Game View before sending OS-level input
            FocusGameView();
            uint mouseType;
            uint mouseButton = 0;

            if (button == "left")
            {
                mouseType = action == "down" ? 1u : 2u; // kCGEventLeftMouseDown : kCGEventLeftMouseUp
                mouseButton = 0;
            }
            else if (button == "right")
            {
                mouseType = action == "down" ? 3u : 4u; // kCGEventRightMouseDown : kCGEventRightMouseUp
                mouseButton = 1;
            }
            else // middle
            {
                mouseType = action == "down" ? 25u : 26u; // kCGEventOtherMouseDown : kCGEventOtherMouseUp
                mouseButton = 2;
            }

            CGPoint point = new CGPoint(x, y);
            IntPtr eventRef = CGEventCreateMouseEvent(IntPtr.Zero, mouseType, point, mouseButton);
            if (eventRef != IntPtr.Zero)
            {
                CGEventPost(0, eventRef);
                CFRelease(eventRef);
            }
        }

#elif UNITY_EDITOR_WIN
        // Windows API
        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        [DllImport("user32.dll")]
        private static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, UIntPtr dwExtraInfo);

        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int X, int Y);

        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;
        private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        private const uint MOUSEEVENTF_RIGHTUP = 0x0010;
        private const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
        private const uint MOUSEEVENTF_MIDDLEUP = 0x0040;

        // Windows virtual key codes
        private static readonly Dictionary<string, byte> WinKeyMap = new Dictionary<string, byte>(StringComparer.OrdinalIgnoreCase)
        {
            // Letters
            { "A", 0x41 }, { "B", 0x42 }, { "C", 0x43 }, { "D", 0x44 },
            { "E", 0x45 }, { "F", 0x46 }, { "G", 0x47 }, { "H", 0x48 },
            { "I", 0x49 }, { "J", 0x4A }, { "K", 0x4B }, { "L", 0x4C },
            { "M", 0x4D }, { "N", 0x4E }, { "O", 0x4F }, { "P", 0x50 },
            { "Q", 0x51 }, { "R", 0x52 }, { "S", 0x53 }, { "T", 0x54 },
            { "U", 0x55 }, { "V", 0x56 }, { "W", 0x57 }, { "X", 0x58 },
            { "Y", 0x59 }, { "Z", 0x5A },

            // Numbers
            { "0", 0x30 }, { "1", 0x31 }, { "2", 0x32 }, { "3", 0x33 },
            { "4", 0x34 }, { "5", 0x35 }, { "6", 0x36 }, { "7", 0x37 },
            { "8", 0x38 }, { "9", 0x39 },

            // Special keys
            { "Return", 0x0D }, { "Enter", 0x0D },
            { "Tab", 0x09 },
            { "Space", 0x20 },
            { "Backspace", 0x08 }, { "Delete", 0x2E },
            { "Escape", 0x1B }, { "Esc", 0x1B },
            { "LeftShift", 0xA0 }, { "RightShift", 0xA1 },
            { "LeftControl", 0xA2 }, { "LeftCtrl", 0xA2 },
            { "RightControl", 0xA3 }, { "RightCtrl", 0xA3 },
            { "LeftAlt", 0xA4 }, { "RightAlt", 0xA5 },
            { "LeftWindows", 0x5B }, { "RightWindows", 0x5C },
            { "CapsLock", 0x14 },

            // Function keys
            { "F1", 0x70 }, { "F2", 0x71 }, { "F3", 0x72 }, { "F4", 0x73 },
            { "F5", 0x74 }, { "F6", 0x75 }, { "F7", 0x76 }, { "F8", 0x77 },
            { "F9", 0x78 }, { "F10", 0x79 }, { "F11", 0x7A }, { "F12", 0x7B },

            // Arrow keys
            { "Up", 0x26 }, { "UpArrow", 0x26 },
            { "Down", 0x28 }, { "DownArrow", 0x28 },
            { "Left", 0x25 }, { "LeftArrow", 0x25 },
            { "Right", 0x27 }, { "RightArrow", 0x27 },

            // Others
            { "Home", 0x24 }, { "End", 0x23 },
            { "PageUp", 0x21 }, { "PageDown", 0x22 },
            { "Insert", 0x2D },
        };

        private void SendOSKeyEvent(string keyName, bool keyDown)
        {
            // Focus Game View before sending OS-level input
            FocusGameView();

            if (!WinKeyMap.TryGetValue(keyName, out byte vk))
            {
                ConsoleLogWarning($"[CommandExecutor] Unknown key for Windows: {keyName}");
                return;
            }

            uint flags = keyDown ? 0 : KEYEVENTF_KEYUP;
            keybd_event(vk, 0, flags, UIntPtr.Zero);
        }

        private void SendOSMouseEvent(string button, string action, float x, float y)
        {
            // Focus Game View before sending OS-level input
            FocusGameView();

            // Set cursor position
            SetCursorPos((int)x, (int)y);

            uint flags = 0;
            if (button == "left")
                flags = action == "down" ? MOUSEEVENTF_LEFTDOWN : MOUSEEVENTF_LEFTUP;
            else if (button == "right")
                flags = action == "down" ? MOUSEEVENTF_RIGHTDOWN : MOUSEEVENTF_RIGHTUP;
            else // middle
                flags = action == "down" ? MOUSEEVENTF_MIDDLEDOWN : MOUSEEVENTF_MIDDLEUP;

            mouse_event(flags, 0, 0, 0, UIntPtr.Zero);
        }
#else
        // Fallback for other platforms
        private void SendOSKeyEvent(string keyName, bool keyDown)
        {
            ConsoleLogWarning($"[CommandExecutor] OS-level input not supported on this platform");
        }

        private void SendOSMouseEvent(string button, string action, float x, float y)
        {
            ConsoleLogWarning($"[CommandExecutor] OS-level input not supported on this platform");
        }
#endif

        #endregion

        #region Input System Detection

        private static Type _keyboardType;
        private static Type _mouseType;
        private static Type _inputStateType;
        private static Type _inputSystemType;
        private static bool _inputSystemChecked;
        private static bool _inputSystemAvailable;

        // Current input system mode for sequence processing
        private string _currentInputSystem = "auto";

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
                ConsoleLog("[CommandExecutor] New Input System not available, will use OS-level input");
            }

            return _inputSystemAvailable;
        }

        /// <summary>
        /// Determine which input system to use
        /// </summary>
        private string DetermineInputSystem(string requested)
        {
            if (string.IsNullOrEmpty(requested))
            {
                // Default to OS-level input (works with both Legacy and New Input System)
                return "os";
            }
            if (requested == "auto")
            {
                // Auto-detect: prefer New Input System if available
                return IsNewInputSystemAvailable() ? "new" : "os";
            }
            return requested.ToLower();
        }

        // Track last focus time to avoid redundant focus calls
        private static float _lastFocusTime = 0f;
        private static float _focusCooldown = 0.5f; // 500ms cooldown

        /// <summary>
        /// Focus the Game View window before sending OS-level input
        /// </summary>
        private void FocusGameView()
        {
            // Skip if recently focused
            if (Time.realtimeSinceStartup - _lastFocusTime < _focusCooldown)
            {
                return;
            }
            _lastFocusTime = Time.realtimeSinceStartup;

            try
            {
                ConsoleLog("[CommandExecutor] FocusGameView called");

                // Get Game View window and focus it FIRST (to switch tab if needed)
                var gameViewType = System.Type.GetType("UnityEditor.GameView, UnityEditor");
                EditorWindow gameView = null;
                Rect gameViewRect = Rect.zero;

                if (gameViewType != null)
                {
                    var getWindow = typeof(EditorWindow).GetMethod("GetWindow", new[] { typeof(System.Type), typeof(bool) });
                    if (getWindow != null)
                    {
                        gameView = getWindow.Invoke(null, new object[] { gameViewType, false }) as EditorWindow;
                        if (gameView != null)
                        {
                            // Focus Game View tab FIRST before getting position
                            EditorWindow.FocusWindowIfItsOpen(gameViewType);
                            gameView.Focus();
                            gameView.Repaint();

                            // Get position after focus (tab should now be active)
                            gameViewRect = gameView.position;
                            ConsoleLog($"[CommandExecutor] Game View focused and position: {gameViewRect}");
                        }
                    }
                }

#if UNITY_EDITOR_OSX
                // macOS: Activate Unity app using osascript with PID
                int pid = System.Diagnostics.Process.GetCurrentProcess().Id;
                ConsoleLog($"[CommandExecutor] Activating Unity (PID: {pid}) via osascript...");
                try
                {
                    var psi = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "osascript",
                        Arguments = $"-e 'tell application \"System Events\" to set frontmost of (first process whose unix id is {pid}) to true'",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    var proc = System.Diagnostics.Process.Start(psi);
                    if (proc != null)
                    {
                        proc.WaitForExit(1000);
                        if (proc.HasExited)
                        {
                            ConsoleLog($"[CommandExecutor] osascript exit code: {proc.ExitCode}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    ConsoleLogWarning($"[CommandExecutor] osascript error: {ex.Message}");
                }

                // Click on Game View center to grab keyboard focus
                if (gameViewRect.width > 0 && gameViewRect.height > 0)
                {
                    // Unity EditorWindow.position is already in screen coordinates (top-left origin)
                    // CGEvent also uses screen coordinates with top-left origin on macOS
                    // Add offset for tab bar (~20px) and toolbar area
                    float centerX = gameViewRect.x + gameViewRect.width / 2;
                    float centerY = gameViewRect.y + gameViewRect.height / 2 + 40; // Offset for tab + toolbar
                    ConsoleLog($"[CommandExecutor] Clicking Game View at ({centerX}, {centerY}) [Rect: {gameViewRect}]");

                    // Send mouse click to grab focus
                    CGPoint point = new CGPoint(centerX, centerY);
                    IntPtr mouseDown = CGEventCreateMouseEvent(IntPtr.Zero, 1u, point, 0); // kCGEventLeftMouseDown
                    if (mouseDown != IntPtr.Zero)
                    {
                        CGEventPost(0, mouseDown);
                        CFRelease(mouseDown);
                    }
                    // Small delay between down and up
                    System.Threading.Thread.Sleep(50);
                    IntPtr mouseUp = CGEventCreateMouseEvent(IntPtr.Zero, 2u, point, 0);   // kCGEventLeftMouseUp
                    if (mouseUp != IntPtr.Zero)
                    {
                        CGEventPost(0, mouseUp);
                        CFRelease(mouseUp);
                    }
                    // Wait for focus to be established
                    System.Threading.Thread.Sleep(100);
                }
#elif UNITY_EDITOR_WIN
                // Windows: Use SetForegroundWindow
                var hwnd = GetUnityWindowHandle();
                if (hwnd != IntPtr.Zero)
                {
                    SetForegroundWindow(hwnd);
                }

                // Click on Game View center
                if (gameViewRect.width > 0 && gameViewRect.height > 0)
                {
                    int centerX = (int)(gameViewRect.x + gameViewRect.width / 2);
                    int centerY = (int)(gameViewRect.y + gameViewRect.height / 2);
                    SetCursorPos(centerX, centerY);
                    mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
                    mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);
                }
#endif
            }
            catch (Exception e)
            {
                ConsoleLogWarning($"[CommandExecutor] Could not focus Game View: {e.Message}");
            }
        }

#if UNITY_EDITOR_WIN
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetActiveWindow();

        private IntPtr GetUnityWindowHandle()
        {
            // Get the main Unity window handle
            return System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
        }
#endif

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

        #region Delayed Release System

        private class DelayedRelease
        {
            public float releaseTime;
            public string key;
            public string button;
            public float mouseX, mouseY;
            public bool isNewInputSystem;
            public object control; // For new input system (key or mouse button control)
        }

        private List<DelayedRelease> _pendingReleases = new List<DelayedRelease>();
        private bool _releaseProcessorRunning = false;

        private void ScheduleDelayedRelease(DelayedRelease release)
        {
            _pendingReleases.Add(release);
            if (!_releaseProcessorRunning)
            {
                _releaseProcessorRunning = true;
                EditorApplication.update += ProcessDelayedReleases;
            }
        }

        private void ProcessDelayedReleases()
        {
            if (!EditorApplication.isPlaying || _pendingReleases.Count == 0)
            {
                StopDelayedReleaseProcessor();
                return;
            }

            float currentTime = Time.realtimeSinceStartup;
            var releasesToProcess = new List<DelayedRelease>();

            // Find all releases that are due
            for (int i = _pendingReleases.Count - 1; i >= 0; i--)
            {
                if (currentTime >= _pendingReleases[i].releaseTime)
                {
                    releasesToProcess.Add(_pendingReleases[i]);
                    _pendingReleases.RemoveAt(i);
                }
            }

            // Process releases
            foreach (var release in releasesToProcess)
            {
                if (!string.IsNullOrEmpty(release.key))
                {
                    // Key release
                    if (release.isNewInputSystem && release.control != null)
                    {
                        SetKeyState(release.control, false);
                        ConsoleLog($"[CommandExecutor] Delayed key release: {release.key}");
                    }
                    else
                    {
                        SendOSKeyEvent(release.key, false);
                        ConsoleLog($"[CommandExecutor] Delayed key release (OS): {release.key}");
                    }
                }
                else if (!string.IsNullOrEmpty(release.button))
                {
                    // Mouse button release
                    if (release.isNewInputSystem && release.control != null)
                    {
                        SetKeyState(release.control, false);
                        ConsoleLog($"[CommandExecutor] Delayed mouse release: {release.button}");
                    }
                    else
                    {
                        SendOSMouseEvent(release.button, "up", release.mouseX, release.mouseY);
                        ConsoleLog($"[CommandExecutor] Delayed mouse release (OS): {release.button}");
                    }
                }
            }

            // Stop processor if no more pending releases
            if (_pendingReleases.Count == 0)
            {
                StopDelayedReleaseProcessor();
            }
        }

        private void StopDelayedReleaseProcessor()
        {
            _releaseProcessorRunning = false;
            EditorApplication.update -= ProcessDelayedReleases;
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

                // Determine input system to use
                string inputSystem = DetermineInputSystem(p.input_system);

                // Parse action
                string action = string.IsNullOrEmpty(p.action) ? "tap" : p.action.ToLower();
                if (action != "press" && action != "release" && action != "tap")
                {
                    return Error($"Invalid action: {p.action}. Use 'press', 'release', or 'tap'.");
                }

                var result = new StringBuilder();
                result.Append($"[{inputSystem}] ");

                // Determine release delay in seconds (duration is in ms, default 100ms)
                float releaseDelay = p.duration > 0 ? p.duration / 1000f : 0.1f;

                if (inputSystem == "new")
                {
                    // Use New Input System
                    if (!IsNewInputSystemAvailable())
                    {
                        return Error("New Input System is not available. Use input_system: 'os' for Legacy Input.");
                    }

                    var keyboard = GetKeyboard();
                    if (keyboard == null)
                    {
                        return Error("No keyboard device found.");
                    }

                    var keyControl = GetKeyControl(keyboard, p.key);
                    if (keyControl == null)
                    {
                        return Error($"Unknown key: {p.key}");
                    }

                    if (action == "press" || action == "tap")
                    {
                        SetKeyState(keyControl, true);
                        result.Append($"Pressed key: {p.key}");
                    }

                    if (action == "release" || action == "tap")
                    {
                        if (action == "tap")
                        {
                            ScheduleDelayedRelease(new DelayedRelease
                            {
                                releaseTime = Time.realtimeSinceStartup + releaseDelay,
                                key = p.key,
                                isNewInputSystem = true,
                                control = keyControl
                            });
                            result.Append($" (will release after {releaseDelay * 1000f:0}ms)");
                        }
                        else
                        {
                            SetKeyState(keyControl, false);
                            result.Append($"Released key: {p.key}");
                        }
                    }
                }
                else // OS-level input
                {
                    if (action == "press" || action == "tap")
                    {
                        SendOSKeyEvent(p.key, true);
                        result.Append($"Pressed key: {p.key}");
                    }

                    if (action == "release" || action == "tap")
                    {
                        if (action == "tap")
                        {
                            ScheduleDelayedRelease(new DelayedRelease
                            {
                                releaseTime = Time.realtimeSinceStartup + releaseDelay,
                                key = p.key,
                                isNewInputSystem = false
                            });
                            result.Append($" (will release after {releaseDelay * 1000f:0}ms)");
                        }
                        else
                        {
                            SendOSKeyEvent(p.key, false);
                            result.Append($"Released key: {p.key}");
                        }
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
        /// Get key control from keyboard by key name (for New Input System)
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

                // Arrow keys
                { "UpArrow", "upArrowKey" }, { "Up", "upArrowKey" },
                { "DownArrow", "downArrowKey" }, { "Down", "downArrowKey" },
                { "LeftArrow", "leftArrowKey" }, { "Left", "leftArrowKey" },
                { "RightArrow", "rightArrowKey" }, { "Right", "rightArrowKey" },
            };

            // Try direct mapping first
            if (keyMappings.TryGetValue(keyName, out string propertyName))
            {
                var property = _keyboardType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
                if (property != null)
                    return property.GetValue(keyboard);
            }

            // Try as-is property name
            var directProperty = _keyboardType.GetProperty(keyName, BindingFlags.Public | BindingFlags.Instance);
            if (directProperty != null)
                return directProperty.GetValue(keyboard);

            // Try lowercase + Key suffix
            if (keyName.Length == 1)
            {
                var charProperty = _keyboardType.GetProperty(keyName.ToLower() + "Key", BindingFlags.Public | BindingFlags.Instance);
                if (charProperty != null)
                    return charProperty.GetValue(keyboard);
            }

            return null;
        }

        /// <summary>
        /// Set key state using InputState.Change (for New Input System)
        /// </summary>
        private void SetKeyState(object keyControl, bool pressed)
        {
            var methods = _inputStateType.GetMethods(BindingFlags.Public | BindingFlags.Static);
            foreach (var m in methods)
            {
                if (m.Name == "Change" && m.IsGenericMethod && m.GetParameters().Length == 2)
                {
                    try
                    {
                        var genericMethod = m.MakeGenericMethod(typeof(float));
                        genericMethod.Invoke(null, new object[] { keyControl, pressed ? 1f : 0f });
                        return;
                    }
                    catch { }
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

                // Determine input system to use
                string inputSystem = DetermineInputSystem(p?.input_system);

                // Parse button (default: left)
                string button = string.IsNullOrEmpty(p?.button) ? "left" : p.button.ToLower();
                if (button != "left" && button != "right" && button != "middle")
                {
                    return Error($"Invalid button: {p.button}. Use 'left', 'right', or 'middle'.");
                }

                // Parse action (default: click)
                string action = string.IsNullOrEmpty(p?.action) ? "click" : p.action.ToLower();
                if (action != "click" && action != "down" && action != "up" && action != "doubleclick")
                {
                    return Error($"Invalid action: {p.action}. Use 'click', 'down', 'up', or 'doubleclick'.");
                }

                var result = new StringBuilder();
                result.Append($"[{inputSystem}] ");

                // Determine release delay in seconds (duration is in ms, default 100ms)
                float releaseDelay = p.duration > 0 ? p.duration / 1000f : 0.1f;

                // Get mouse position - if not specified, use Game View center
                float mouseX = 0;
                float mouseY = 0;
                if (p?.mouse_position != null && p.mouse_position.Length >= 2)
                {
                    mouseX = p.mouse_position[0];
                    mouseY = p.mouse_position[1];
                }
                else
                {
                    // Get Game View center as default position
                    var gameViewType = System.Type.GetType("UnityEditor.GameView, UnityEditor");
                    if (gameViewType != null)
                    {
                        var getWindow = typeof(EditorWindow).GetMethod("GetWindow", new[] { typeof(System.Type), typeof(bool) });
                        if (getWindow != null)
                        {
                            var gameView = getWindow.Invoke(null, new object[] { gameViewType, false }) as EditorWindow;
                            if (gameView != null)
                            {
                                Rect rect = gameView.position;
                                mouseX = rect.x + rect.width / 2;
                                mouseY = rect.y + rect.height / 2 + 40; // Offset for tab + toolbar
                                ConsoleLog($"[CommandExecutor] Using Game View center: ({mouseX}, {mouseY})");
                            }
                        }
                    }
                }

                if (inputSystem == "new")
                {
                    if (!IsNewInputSystemAvailable())
                    {
                        return Error("New Input System is not available. Use input_system: 'os' for Legacy Input.");
                    }

                    var mouse = GetMouse();
                    if (mouse == null)
                    {
                        return Error("No mouse device found.");
                    }

                    if (p?.mouse_position != null && p.mouse_position.Length >= 2)
                    {
                        SetMousePosition(mouse, mouseX, mouseY);
                    }

                    var buttonControl = GetMouseButtonControl(mouse, button);
                    if (buttonControl == null)
                    {
                        return Error($"Failed to get mouse button control: {button}");
                    }

                    if (action == "down" || action == "click" || action == "doubleclick")
                    {
                        SetKeyState(buttonControl, true);
                        result.Append($"Mouse {button} button down");
                    }

                    if (action == "up" || action == "click")
                    {
                        if (action == "click")
                        {
                            ScheduleDelayedRelease(new DelayedRelease
                            {
                                releaseTime = Time.realtimeSinceStartup + releaseDelay,
                                button = button,
                                isNewInputSystem = true,
                                control = buttonControl
                            });
                            result.Append($" (will release after {releaseDelay * 1000f:0}ms)");
                        }
                        else
                        {
                            SetKeyState(buttonControl, false);
                            result.Append($"Mouse {button} button up");
                        }
                    }
                }
                else // OS-level
                {
                    if (action == "down" || action == "click" || action == "doubleclick")
                    {
                        SendOSMouseEvent(button, "down", mouseX, mouseY);
                        result.Append($"Mouse {button} button down");
                    }

                    if (action == "up" || action == "click")
                    {
                        if (action == "click")
                        {
                            ScheduleDelayedRelease(new DelayedRelease
                            {
                                releaseTime = Time.realtimeSinceStartup + releaseDelay,
                                button = button,
                                mouseX = mouseX,
                                mouseY = mouseY,
                                isNewInputSystem = false
                            });
                            result.Append($" (will release after {releaseDelay * 1000f:0}ms)");
                        }
                        else
                        {
                            SendOSMouseEvent(button, "up", mouseX, mouseY);
                            result.Append($"Mouse {button} button up");
                        }
                    }
                }

                if (p?.mouse_position != null && p.mouse_position.Length >= 2)
                {
                    result.Append($" at ({mouseX}, {mouseY})");
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
        /// Get mouse button control (for New Input System)
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
        /// Set mouse position (for New Input System)
        /// </summary>
        private void SetMousePosition(object mouse, float x, float y)
        {
            var positionProperty = _mouseType.GetProperty("position", BindingFlags.Public | BindingFlags.Instance);
            if (positionProperty == null) return;

            var positionControl = positionProperty.GetValue(mouse);
            if (positionControl == null) return;

            var newPosition = new Vector2(x, y);

            var methods = _inputStateType.GetMethods(BindingFlags.Public | BindingFlags.Static);
            foreach (var method in methods)
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

                // Store input system choice for sequence processing
                _currentInputSystem = DetermineInputSystem(p.input_system);

                // Start the sequence
                StartInputSequence(inputsArray);

                ConsoleLog($"[CommandExecutor] Started input sequence with {inputsArray.Count} steps (using {_currentInputSystem})");
                return Success($"Started input sequence with {inputsArray.Count} steps (using {_currentInputSystem})");
            }
            catch (Exception e)
            {
                return Error($"Error starting input sequence: {e.Message}");
            }
        }

        private Queue<JToken> _pendingInputs;
        private float _waitUntilTime;
        private bool _sequenceRunning;

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

        private void ProcessInputSequence()
        {
            if (!_sequenceRunning || !EditorApplication.isPlaying)
            {
                StopInputSequence();
                return;
            }

            if (_waitUntilTime > 0)
            {
                if (Time.realtimeSinceStartup < _waitUntilTime)
                    return;
                _waitUntilTime = 0;
            }

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

        private void ProcessKeyInput(JObject input)
        {
            string key = input["key"]?.ToString();
            string action = input["action"]?.ToString()?.ToLower() ?? "tap";
            float durationMs = input["duration"]?.ToObject<float>() ?? 100f;
            float releaseDelay = durationMs / 1000f;

            if (string.IsNullOrEmpty(key)) return;

            if (_currentInputSystem == "new")
            {
                var keyboard = GetKeyboard();
                if (keyboard == null) return;

                var keyControl = GetKeyControl(keyboard, key);
                if (keyControl == null)
                {
                    ConsoleLogWarning($"[CommandExecutor] Unknown key: {key}");
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
                    ScheduleDelayedRelease(new DelayedRelease
                    {
                        releaseTime = Time.realtimeSinceStartup + releaseDelay,
                        key = key,
                        isNewInputSystem = true,
                        control = keyControl
                    });
                }
            }
            else // OS-level
            {
                if (action == "down" || action == "press" || action == "tap")
                {
                    SendOSKeyEvent(key, true);
                    ConsoleLog($"[CommandExecutor] Key down: {key}");
                }

                if (action == "up" || action == "release")
                {
                    SendOSKeyEvent(key, false);
                    ConsoleLog($"[CommandExecutor] Key up: {key}");
                }

                if (action == "tap")
                {
                    ScheduleDelayedRelease(new DelayedRelease
                    {
                        releaseTime = Time.realtimeSinceStartup + releaseDelay,
                        key = key,
                        isNewInputSystem = false
                    });
                }
            }
        }

        private void ProcessMouseInput(JObject input)
        {
            string button = input["button"]?.ToString()?.ToLower() ?? "left";
            string action = input["action"]?.ToString()?.ToLower() ?? "click";
            float durationMs = input["duration"]?.ToObject<float>() ?? 100f;
            float releaseDelay = durationMs / 1000f;

            var positionToken = input["position"] as JArray;
            float x = positionToken != null && positionToken.Count >= 2 ? positionToken[0].ToObject<float>() : 0;
            float y = positionToken != null && positionToken.Count >= 2 ? positionToken[1].ToObject<float>() : 0;

            if (_currentInputSystem == "new")
            {
                var mouse = GetMouse();
                if (mouse == null) return;

                if (positionToken != null)
                    SetMousePosition(mouse, x, y);

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
                    ScheduleDelayedRelease(new DelayedRelease
                    {
                        releaseTime = Time.realtimeSinceStartup + releaseDelay,
                        button = button,
                        isNewInputSystem = true,
                        control = buttonControl
                    });
                }
            }
            else // OS-level
            {
                if (action == "down" || action == "click")
                {
                    SendOSMouseEvent(button, "down", x, y);
                    ConsoleLog($"[CommandExecutor] Mouse {button} down");
                }

                if (action == "up")
                {
                    SendOSMouseEvent(button, "up", x, y);
                    ConsoleLog($"[CommandExecutor] Mouse {button} up");
                }

                if (action == "click")
                {
                    ScheduleDelayedRelease(new DelayedRelease
                    {
                        releaseTime = Time.realtimeSinceStartup + releaseDelay,
                        button = button,
                        mouseX = x,
                        mouseY = y,
                        isNewInputSystem = false
                    });
                }
            }
        }

        private void StopInputSequence()
        {
            _sequenceRunning = false;
            _pendingInputs = null;
            EditorApplication.update -= ProcessInputSequence;
        }

        #endregion
    }
}
