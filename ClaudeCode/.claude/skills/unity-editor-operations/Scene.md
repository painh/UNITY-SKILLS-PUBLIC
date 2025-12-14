# Scene Operations

Scene operations handle loading, saving, creating, and inspecting Unity Scenes.

## Operations

### open_scene

Opens an existing Scene file.

**Parameters:**
- `scene_path` (required): Scene asset path (e.g., "Assets/Scenes/MyScene.unity")
- `save_current` (optional): Save current scene before opening (default: false)

**Example:**
```json
{
  "operation": "open_scene",
  "params": {
    "scene_path": "Assets/Scenes/Level1.unity",
    "save_current": true
  }
}
```

**Response Format:**
Returns a success message with the opened scene name and path.

**Note:**
- Automatically saves the current scene if it has unsaved changes and save_current is true
- Uses `EditorSceneManager.OpenScene()` with Single mode

### save_scene

Saves the current Scene or saves it as a new file.

**Parameters:**
- `scene_path` (optional): Save as new file path (if not specified, saves to current path)

**Example:**
```json
{
  "operation": "save_scene",
  "params": {
    "scene_path": "Assets/Scenes/MyLevel_Backup.unity"
  }
}
```

**Response Format:**
Returns a success message with the save path.

**Note:**
- Automatically creates parent directories if they don't exist
- If scene_path is not specified, saves to the current scene's path
- Returns error if current scene has no path and scene_path is not provided

### create_scene

Creates a new Scene with default GameObjects (Camera, Directional Light).

**Parameters:**
- `scene_path` (required): Path for the new scene (e.g., "Assets/Scenes/NewScene.unity")
- `save_current` (optional): Save current scene before creating new (default: false)

**Example:**
```json
{
  "operation": "create_scene",
  "params": {
    "scene_path": "Assets/Scenes/TestScene.unity",
    "save_current": true
  }
}
```

**Response Format:**
Returns a success message with the created scene path.

**Note:**
- Creates new scene with `DefaultGameObjects` setup (Camera + Directional Light)
- Automatically creates parent directories if they don't exist
- Uses `EditorSceneManager.NewScene()` and saves immediately

### get_scene_hierarchy

Retrieves the hierarchy structure of the active Scene.

**Parameters:**
- `max_depth` (optional): Maximum depth for hierarchy traversal (default: -1 = unlimited)

**Example:**
```json
{
  "operation": "get_scene_hierarchy",
  "params": {
    "max_depth": 3
  }
}
```

**Response Format:**
Returns a formatted string showing the scene's hierarchy tree:
```
Scene Hierarchy: SampleScene
Path: Assets/Scenes/SampleScene.unity
Root GameObjects: 3

- Main Camera (Active: True, Components: 3)
- Directional Light (Active: True, Components: 2)
- GameController (Active: True, Components: 1)
  - PlayerSphere (Active: True, Components: 4)
  - EnemySpawner (Active: True, Components: 2)
```

**Note:**
- Shows GameObject name, active state, and component count
- Uses indentation to show parent-child relationships
- max_depth limits how deep the traversal goes (useful for large scenes)

### get_active_scene

Retrieves detailed information about the currently active Scene.

**Parameters:** None

**Example:**
```json
{
  "operation": "get_active_scene",
  "params": {}
}
```

**Response Format:**
Returns a formatted string with scene information:
```
Active Scene Information:

Name: SampleScene
Path: Assets/Scenes/SampleScene.unity
Build Index: 0
Is Loaded: True
Is Dirty: False
Root GameObject Count: 3
Is Valid: True
```

**Note:**
- "Is Dirty" indicates if the scene has unsaved changes
- "Build Index" shows the scene's position in build settings (-1 if not in build)
