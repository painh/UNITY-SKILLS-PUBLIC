# Prefab Operations

Prefab operations handle creating, instantiating, editing, and saving Unity Prefab assets.

## Operations

### create_prefab

Creates a Prefab asset from an existing GameObject in the scene.

**Parameters:**
- `path` (required): Path or name of GameObject to convert to Prefab
- `prefab_path` (required): Destination path for the Prefab (e.g., "Assets/Prefabs/Player.prefab")

**Example:**
```json
{
  "operation": "create_prefab",
  "params": {
    "path": "PlayerCharacter",
    "prefab_path": "Assets/Prefabs/Player.prefab"
  }
}
```

**Response Format:**
Returns a success message with the GameObject name and created Prefab path.

**Note:**
- Automatically creates parent directories if they don't exist
- Uses `PrefabUtility.SaveAsPrefabAsset()`
- Original GameObject remains in the scene (not converted to prefab instance)

### instantiate_prefab

Instantiates a Prefab into the active scene.

**Parameters:**
- `prefab_path` (required): Path to the Prefab asset
- `name` (optional): Name for the instantiated GameObject
- `parent` (optional): Parent GameObject path or name
- `position` (optional): [x, y, z] position array
- `position_space` (optional): "local" | "world" (default: "world" if no parent, "local" if parent specified)
- `rotation` (optional): [x, y, z] Euler angles
- `rotation_space` (optional): "local" | "world" (default: "world" if no parent, "local" if parent specified)
- `scale` (optional): [x, y, z] scale array
- `scale_space` (optional): "local" | "world" (default: "world" if no parent, "local" if parent specified)

> **Space parameters:** See [SKILL.md#space-parameters](SKILL.md#space-parameters) for defaults.

**Example:**
```json
{
  "operation": "instantiate_prefab",
  "params": {
    "prefab_path": "Assets/Prefabs/Enemy.prefab",
    "name": "Enemy_01",
    "parent": "Enemies",
    "position": [5, 0, 10]
  }
}
```

**Response Format:**
Returns a success message with the instantiated GameObject name.

**Note:**
- Uses `PrefabUtility.InstantiatePrefab()` to maintain prefab connection
- Uses `Undo.RegisterCreatedObjectUndo()` for Undo support
- Parent is set during instantiation, then transform values are applied based on space

### open_prefab

Opens a Prefab in Prefab Edit Mode.

**Parameters:**
- `prefab_path` (required): Path to the Prefab asset to edit

**Example:**
```json
{
  "operation": "open_prefab",
  "params": {
    "prefab_path": "Assets/Prefabs/Player.prefab"
  }
}
```

**Response Format:**
Returns a success message with the opened Prefab path.

**Note:**
- Uses `PrefabStageUtility.OpenPrefab()` to open in edit mode
- Switches Unity Editor to Prefab editing context
- Requires `using UnityEditor.SceneManagement;`

### save_prefab

Saves the currently open Prefab in Prefab Edit Mode.

**Parameters:** None

**Example:**
```json
{
  "operation": "save_prefab",
  "params": {}
  }
}
```

**Response Format:**
Returns a success message with the saved Prefab path.

**Note:**
- Only works when a Prefab is currently open in edit mode
- Uses `PrefabStageUtility.GetCurrentPrefabStage()` to get current stage
- Uses `PrefabUtility.SaveAsPrefabAsset()` to save changes
- Returns error if no Prefab is currently being edited
