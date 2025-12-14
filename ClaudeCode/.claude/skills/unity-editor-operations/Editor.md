# Editor Operations

Editor operations for executing menu items, querying editor state, and managing object selection.

## Operations

### execute_menu_item
Executes a Unity menu item by its path, as if the user clicked it in the Editor menu.

**Parameters:**
- `menu_path` (required): Menu item path (e.g., "GameObject/Create Empty", "Window/Package Manager")

**JSON Example:**
```json
{
  "operation": "execute_menu_item",
  "params": {
    "menu_path": "GameObject/Create Empty"
  }
}
```

**Response:**
```
Executed menu item: GameObject/Create Empty
```

**Notes:**
- Supports any valid Unity menu path
- Common paths:
  - GameObject creation: "GameObject/Create Empty", "GameObject/3D Object/Cube"
  - Window management: "Window/Package Manager", "Window/Console"
  - Assets: "Assets/Create/Folder", "Assets/Refresh"
  - Edit: "Edit/Undo", "Edit/Redo", "Edit/Play"
- Menu path must exactly match Unity's menu structure (case-sensitive)
- Useful for automating editor workflows and triggering built-in Unity commands
- Changes are automatically recorded in Undo history if the menu item supports it

---

### get_editor_state
Retrieves current Unity Editor state information including play mode, compilation status, and active scene.

**Parameters:**
- None required

**JSON Example:**
```json
{
  "operation": "get_editor_state",
  "params": {}
}
```

**Response:**
```
Unity Editor State:

  Is Playing: False
  Is Paused: False
  Is Compiling: False
  Is Updating: False
  Application Path: C:/Program Files/Unity/Hub/Editor/2021.3.0f1/Editor/Unity.exe
  Unity Version: 2021.3.0f1
  Platform: WindowsEditor
  Active Scene: SampleScene
  Scene Path: Assets/Scenes/SampleScene.unity
  Is Scene Dirty: False
```

**Notes:**
- `Is Playing`: Whether Editor is in Play mode
- `Is Paused`: Whether Play mode is paused
- `Is Compiling`: Whether scripts are currently compiling
- `Is Updating`: Whether AssetDatabase is updating
- `Is Scene Dirty`: Whether active scene has unsaved changes
- Useful for checking editor state before performing operations
- Can be used to wait for compilation to finish or check if scene needs saving

---

### get_selection
Retrieves information about currently selected objects in the Unity Editor.

**Parameters:**
- None required

**JSON Example:**
```json
{
  "operation": "get_selection",
  "params": {}
}
```

**Response (with GameObjects selected):**
```
Selected Objects (2):

  - Cube
    Type: GameObject
    Path: Environment/Cube
    Active: True

  - Sphere
    Type: GameObject
    Path: Props/Sphere
    Active: True

Active GameObject: Cube
```

**Response (with Asset selected):**
```
Selected Objects (1):

  - PlayerScript
    Type: MonoScript
    Asset Path: Assets/Scripts/PlayerScript.cs

Active GameObject: (none)
```

**Response (nothing selected):**
```
Selected Objects (0):

  No objects selected
```

**Notes:**
- Returns all selected objects, not just the active one
- For GameObjects: Shows hierarchy path and active state
- For Assets: Shows asset path in project
- `Active GameObject` is the primary selection (highlighted in Inspector)
- Selection can include mix of scene objects and project assets
- Useful for operations that should act on current selection

---

### set_selection
Selects a GameObject in the Hierarchy and highlights it ("pings" it to make it visible).

**Parameters:**
- `path` (required): GameObject path/name to select

**JSON Example:**
```json
{
  "operation": "set_selection",
  "params": {
    "path": "Environment/Props/Cube"
  }
}
```

**Response:**
```
Selected GameObject: Cube
```

**Notes:**
- Supports hierarchical paths (e.g., "Parent/Child/Object")
- Supports simple names for root objects (e.g., "Cube")
- Automatically scrolls Hierarchy to make object visible ("ping" effect)
- Selection is highlighted in yellow in Hierarchy window
- Inspector updates to show selected object's components
- Changes are recorded in Undo history
- Returns error if GameObject not found
- Only works with scene objects, not project assets (use Assets panel for that)
- Useful for programmatically focusing user attention on specific objects

---

### playmode
Controls Unity Editor play mode: play, stop, pause, resume. Without parameters, returns current state.

**Parameters:**
- `action` (optional): Action to perform - `"play"`, `"stop"`, `"pause"`, `"resume"`
- If no parameters: Returns current play mode state

**JSON Examples:**

Get current state (default):
```json
{
  "operation": "playmode",
  "params": {}
}
```

Start play mode:
```json
{
  "operation": "playmode",
  "params": {
    "action": "play"
  }
}
```

Stop play mode:
```json
{
  "operation": "playmode",
  "params": {
    "action": "stop"
  }
}
```

Pause:
```json
{
  "operation": "playmode",
  "params": {
    "action": "pause"
  }
}
```

Resume:
```json
{
  "operation": "playmode",
  "params": {
    "action": "resume"
  }
}
```

**Response (get state):**
```
Play Mode State:

  Is Playing: False
  Is Paused: False
  Is Compiling: False
  State: Edit Mode
```

**Response (action):**
```
Entered play mode
```

**Notes:**
- `play` fails if already playing (warning) or compiling (error)
- `stop` fails if not playing (warning)
- `pause` fails if not playing (error) or already paused (warning)
- `resume` fails if not playing (error) or not paused (warning)
- Play mode transitions are async - state may not change immediately
- Use `get_editor_state` for comprehensive editor info (scene, paths, version)
