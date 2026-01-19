# Input Simulation Operations

Input simulation operations allow simulating keyboard and mouse input during Play mode. Supports both Unity's New Input System and Legacy Input Manager (via OS-level input).

**Requirements:**
- Unity must be in **Play mode**
- For New Input System: Input System package must be installed
- For Legacy Input: Works on macOS and Windows using OS-level events

---

## Input System Selection

All input operations support the `input_system` parameter to choose how input is delivered:

| Value | Description |
|-------|-------------|
| `auto` | (Default) Try New Input System first, fall back to OS-level input |
| `new` | Force New Input System (fails if not available) |
| `os` | Force OS-level input (uses CGEvent on macOS, user32.dll on Windows) |

**When to use each:**
- `auto` - Recommended for most cases, works with any project
- `new` - When you specifically need New Input System features
- `os` - For Legacy Input Manager projects, or when New Input System isn't working

---

## simulate_key

Simulate keyboard input (press, release, or tap a key).

### Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `key` | string | Yes | Key name (e.g., "W", "Space", "LeftCtrl") |
| `action` | string | No | "press", "release", or "tap" (default: "tap") |
| `input_system` | string | No | "auto", "new", or "os" (default: "auto") |

### Supported Keys

- **Letters:** A-Z
- **Numbers:** 0-9, Alpha0-Alpha9
- **Function keys:** F1-F12
- **Modifiers:** LeftShift, RightShift, LeftCtrl, RightCtrl, LeftAlt, RightAlt, LeftCommand, RightCommand
- **Arrow keys:** Up, Down, Left, Right (or UpArrow, DownArrow, etc.)
- **Special:** Space, Enter, Tab, Backspace, Delete, Escape, Insert, Home, End, PageUp, PageDown
- **Numpad:** Keypad0-Keypad9, KeypadPlus, KeypadMinus, KeypadMultiply, KeypadDivide, KeypadEnter

### Example

```bash
# Tap W key (press and release)
python send_message.py '{"operation":"simulate_key","params":{"key":"W","action":"tap"}}'

# Hold down Space using OS-level input (for Legacy Input projects)
python send_message.py '{"operation":"simulate_key","params":{"key":"Space","action":"press","input_system":"os"}}'

# Release Space
python send_message.py '{"operation":"simulate_key","params":{"key":"Space","action":"release","input_system":"os"}}'
```

### Response

```json
{
  "success": true,
  "result": "Pressed key: W (will release next frame) [os]"
}
```

---

## simulate_mouse

Simulate mouse button clicks and position.

### Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `button` | string | No | "left", "right", or "middle" (default: "left") |
| `action` | string | No | "click", "down", "up", or "doubleclick" (default: "click") |
| `mouse_position` | float[2] | No | Screen position [x, y] |
| `input_system` | string | No | "auto", "new", or "os" (default: "auto") |

### Example

```bash
# Left click at current position
python send_message.py '{"operation":"simulate_mouse","params":{"button":"left","action":"click"}}'

# Right click at specific position using OS-level input
python send_message.py '{"operation":"simulate_mouse","params":{"button":"right","action":"click","mouse_position":[960,540],"input_system":"os"}}'

# Double click
python send_message.py '{"operation":"simulate_mouse","params":{"button":"left","action":"doubleclick"}}'
```

### Response

```json
{
  "success": true,
  "result": "Mouse left button down (will release next frame) at (960, 540) [os]"
}
```

---

## simulate_input_sequence

Execute a sequence of inputs with timing control.

### Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `inputs` | array | Yes | Array of input steps |
| `input_system` | string | No | "auto", "new", or "os" (default: "auto") |

### Input Step Types

**Key input:**
```json
{"type": "key", "key": "W", "action": "down"}
```

**Mouse input:**
```json
{"type": "mouse", "button": "left", "action": "click", "position": [960, 540]}
```

**Wait:**
```json
{"type": "wait", "duration": 0.5}
```

### Example

```bash
# Move forward for 1 second, then attack (using OS-level for Legacy Input)
python send_message.py '{"operation":"simulate_input_sequence","params":{"input_system":"os","inputs":[
  {"type":"key","key":"W","action":"down"},
  {"type":"wait","duration":1.0},
  {"type":"key","key":"W","action":"up"},
  {"type":"mouse","button":"left","action":"click"}
]}}'
```

### Response

```json
{
  "success": true,
  "result": "Started input sequence with 4 steps [os]"
}
```

---

## Error Handling

### Not in Play Mode

```json
{
  "success": false,
  "error": "simulate_key requires Play mode. Enter Play mode first."
}
```

### Input System Not Available (when forcing 'new')

```json
{
  "success": false,
  "error": "New Input System is not available. Use input_system: 'os' for Legacy Input."
}
```

### Unknown Key

```json
{
  "success": false,
  "error": "Unknown key: InvalidKey. Use Unity KeyCode names (e.g., 'W', 'Space', 'LeftCtrl')."
}
```

### Unsupported Platform (OS-level input)

```json
{
  "success": false,
  "error": "OS-level input is only supported on macOS and Windows."
}
```

---

## Use Cases

1. **Automated Gameplay Testing**
   - Test player movement (WASD)
   - Test combat system (attack keys)
   - Test UI interactions

2. **Bug Reproduction**
   - Reproduce specific input sequences
   - Automate reproduction steps

3. **Debugging Workflow**
   - Claude modifies code
   - Enters Play mode
   - Simulates input
   - Checks console logs
   - All without user intervention

4. **Legacy Input Manager Projects**
   - Use `input_system: "os"` for projects using the old Input system
   - Works without any additional packages

---

## Best Practices

1. **Enter Play mode first** - Use `playmode` operation with `action: "play"`
2. **Use sequences for complex inputs** - Easier to manage timing
3. **Add wait steps** - Give game time to respond between inputs
4. **Check console logs** - Use `get_logs` to verify results
5. **Tap for simple presses** - Use "tap" action for quick key presses
6. **Use `auto` input_system** - Unless you need specific behavior
7. **For Legacy Input projects** - Use `input_system: "os"` explicitly
