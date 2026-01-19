# Input Simulation Operations

Input simulation operations allow simulating keyboard and mouse input during Play mode. This uses Unity's New Input System.

**Requirements:**
- Unity must be in **Play mode**
- New Input System package must be installed

---

## simulate_key

Simulate keyboard input (press, release, or tap a key).

### Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `key` | string | Yes | Key name (e.g., "W", "Space", "LeftCtrl") |
| `action` | string | No | "press", "release", or "tap" (default: "tap") |

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

# Hold down Space
python send_message.py '{"operation":"simulate_key","params":{"key":"Space","action":"press"}}'

# Release Space
python send_message.py '{"operation":"simulate_key","params":{"key":"Space","action":"release"}}'
```

### Response

```json
{
  "success": true,
  "result": "Pressed key: W (will release next frame)"
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

### Example

```bash
# Left click at current position
python send_message.py '{"operation":"simulate_mouse","params":{"button":"left","action":"click"}}'

# Right click at specific position
python send_message.py '{"operation":"simulate_mouse","params":{"button":"right","action":"click","mouse_position":[960,540]}}'

# Double click
python send_message.py '{"operation":"simulate_mouse","params":{"button":"left","action":"doubleclick"}}'
```

### Response

```json
{
  "success": true,
  "result": "Mouse left button down (will release next frame) at (960, 540)"
}
```

---

## simulate_input_sequence

Execute a sequence of inputs with timing control.

### Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `inputs` | array | Yes | Array of input steps |

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
# Move forward for 1 second, then attack
python send_message.py '{"operation":"simulate_input_sequence","params":{"inputs":[
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
  "result": "Started input sequence with 4 steps"
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

### Input System Not Available

```json
{
  "success": false,
  "error": "New Input System is not available. Please install the Input System package."
}
```

### Unknown Key

```json
{
  "success": false,
  "error": "Unknown key: InvalidKey. Use Unity KeyCode names (e.g., 'W', 'Space', 'LeftCtrl')."
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

---

## Best Practices

1. **Enter Play mode first** - Use `playmode` operation with `action: "play"`
2. **Use sequences for complex inputs** - Easier to manage timing
3. **Add wait steps** - Give game time to respond between inputs
4. **Check console logs** - Use `get_logs` to verify results
5. **Tap for simple presses** - Use "tap" action for quick key presses
