# GameObject Operations

GameObject operations include creating, deleting, finding, and manipulating Unity GameObjects in the scene.

## Operations

### create_primitive

Creates a Unity primitive GameObject with specified properties.

**Parameters:**
- `type` (required): "sphere" | "cube" | "cylinder" | "capsule" | "plane" | "quad"
- `name` (optional): GameObject name (default: PrimitiveType name)
- `color` (optional): Color name or hex code (#RRGGBB)
  - Named colors: red, green, blue, yellow, cyan, magenta, white, black, gray/grey, orange, brown
- `parent` (optional): Parent object path/name
- `position` (optional): [x, y, z] array (default: [0, 0, 0])
- `position_space` (optional): "local" | "world" (default: "world" if no parent, "local" if parent specified)
- `scale` (optional): [x, y, z] array (default: [1, 1, 1])
- `scale_space` (optional): "local" | "world" (default: "world" if no parent, "local" if parent specified)
- `rotation` (optional): [x, y, z] Euler angles (default: [0, 0, 0])
- `rotation_space` (optional): "local" | "world" (default: "world" if no parent, "local" if parent specified)

> **Space parameters:** See [SKILL.md#space-parameters](SKILL.md#space-parameters) for defaults.

**Example:**
```json
{
  "operation": "create_primitive",
  "params": {
    "type": "capsule",
    "name": "PlayerCharacter",
    "color": "#FF6B6B",
    "parent": "GameController",
    "position": [0, 1, 0],
    "position_space": "local",
    "scale": [1, 1.5, 1],
    "scale_space": "local",
    "rotation": [0, 45, 0],
    "rotation_space": "local"
  }
}
```

### create_empty

Creates an empty GameObject.

**Parameters:**
- `name` (optional): GameObject name (default: "GameObject")
- `parent` (optional): Parent object path/name
- `position` (optional): [x, y, z] array (default: [0, 0, 0])
- `position_space` (optional): "local" | "world" (default: "world" if no parent, "local" if parent specified)

> **Space parameters:** See [SKILL.md#space-parameters](SKILL.md#space-parameters) for defaults.

**Example:**
```json
{
  "operation": "create_empty",
  "params": {
    "name": "GameController",
    "parent": "Scene",
    "position": [0, 0, 0]
  }
}
```

**Response Format:**
Returns a success message with the created GameObject name.

### delete_gameobject

Deletes a GameObject by path or name.

**Parameters:**
- `path` (required): Object path (e.g., "Parent/Child") or name

**Example:**
```json
{
  "operation": "delete_gameobject",
  "params": {
    "path": "Red Sphere"
  }
}
```

**Response Format:**
Returns a success message with the deleted GameObject name.

**Note:** Uses `Undo.DestroyObjectImmediate()` for Undo support.

### set_active

Sets the active state of a GameObject.

**Parameters:**
- `path` (required): Object path or name
- `active` (required): true or false

**Example:**
```json
{
  "operation": "set_active",
  "params": {
    "path": "Red Sphere",
    "active": false
  }
}
```

**Response Format:**
Returns a success message with the GameObject name and new active state.

### tag

Unified command for getting and setting GameObject tags.

**Parameters:**
- `path` (required): Object path or name
- `get` (optional): If true, returns current tag info
- `tag` (optional): Tag name to set (must exist in project settings)

**Get Mode Example:**
```json
{
  "operation": "tag",
  "params": {
    "path": "PickupCube",
    "get": true
  }
}
```

**Get Response Format:**
Returns the GameObject name and current tag.

**Set Mode Example:**
```json
{
  "operation": "tag",
  "params": {
    "path": "PickupCube",
    "tag": "PickUp"
  }
}
```

**Set Response Format:**
Returns a success message with the GameObject name and assigned tag.

**Note:**
- Cannot specify both `get: true` and `tag`
- Returns error if tag doesn't exist (validates before assignment)
- The tag must already exist in Edit > Project Settings > Tags and Layers
- Built-in tags: Untagged, Respawn, Finish, EditorOnly, MainCamera, Player, GameController
- Use `create_tag` to add custom tags programmatically

### create_tag

Creates a new tag in Project Settings.

**Parameters:**
- `tag` (required): Tag name to create

**Example:**
```json
{
  "operation": "create_tag",
  "params": {
    "tag": "PickUp"
  }
}
```

**Response Format:**
Returns a success message with the created tag name. If the tag already exists, returns success with info message.

**Note:**
- Modifies ProjectSettings/TagManager.asset
- Tag is immediately available for use with `tag` command
- No error if tag already exists (idempotent)

### delete_tag

Deletes a custom tag from Project Settings.

**Parameters:**
- `tag` (required): Tag name to delete

**Example:**
```json
{
  "operation": "delete_tag",
  "params": {
    "tag": "PickUp"
  }
}
```

**Response Format:**
Returns a success message with the deleted tag name.

**Note:**
- Cannot delete built-in tags: Untagged, Respawn, Finish, EditorOnly, MainCamera, Player, GameController
- Returns error if tag doesn't exist
- GameObjects using the deleted tag will revert to "Untagged"

### find_gameobject

Finds GameObjects by path, name, or tag and returns detailed information.

**Parameters:**
- `path` (optional): Exact path match (e.g., "Parent/Child")
- `name` (optional): Partial name match (searches all objects)
- `tag` (optional): Tag-based search

**Example:**
```json
{
  "operation": "find_gameobject",
  "params": {
    "name": "Sphere"
  }
}
```

**Response Format:**
Returns a formatted string with found objects showing:
- Name
- Full hierarchical path
- Position
- Active state
- Tag

### set_name

Renames a GameObject.

**Parameters:**
- `path` (required): Object path or name
- `new_name` (required): New name for the GameObject

**Example:**
```json
{
  "operation": "set_name",
  "params": {
    "path": "PurpleSphere",
    "new_name": "PlayerSphere"
  }
}
```

**Response Format:**
Returns a success message with the old and new names.

**Note:** Uses `Undo.RecordObject()` for Undo support.

### set_parent

Sets or changes the parent-child relationship between GameObjects.

**Parameters:**
- `path` (required): Object path or name to move
- `parent` (optional): Parent object path or name (null/empty = move to root)
- `world_position_stays` (optional): Maintain world position (default: false)

**Example:**
```json
{
  "operation": "set_parent",
  "params": {
    "path": "PurpleSphere",
    "parent": "GameController",
    "world_position_stays": true
  }
}
```

**Response Format:**
Returns a success message with child name, parent name, and world_position_stays value.

**Note:** Uses `Undo.SetTransformParent()` for Undo support.

### duplicate_gameobject

Duplicates (clones) a GameObject.

**Parameters:**
- `path` (required): Object path or name to duplicate
- `new_name` (optional): Name for the duplicate (default: original name + " (Clone)")

**Example:**
```json
{
  "operation": "duplicate_gameobject",
  "params": {
    "path": "PurpleSphere",
    "new_name": "PurpleSphere Copy"
  }
}
```

**Response Format:**
Returns a success message with the original and duplicate names.

**Note:**
- Uses `Undo.RegisterCreatedObjectUndo()` for Undo support
- Maintains parent hierarchy of original object

### look_at

See [Transform.md#look_at](Transform.md#look_at) for details.

### create_line

Creates a line between two points using LineRenderer.

**Parameters:**
- `start` (required): Start position - either [x, y, z] array or GameObject path string
- `end` (required): End position - either [x, y, z] array or GameObject path string
- `name` (optional): Line name (default: "Line")
- `color` (optional): Color name or hex code (default: white)
- `width` (optional): Line width (default: 0.01)
- `parent` (optional): Parent object path/name
- `position_space` (optional): "local" | "world" (default: "world" if no parent, "local" if parent specified)

> **Space parameters:** See [SKILL.md#space-parameters](SKILL.md#space-parameters) for defaults.

**Position Types:**
- Coordinate array: `[0, 1, 2]` - world or local position depending on position_space
- GameObject path: `"Parent/Child"` - uses the GameObject's transform.position

**Example (coordinates with parent - local coords):**
```json
{
  "operation": "create_line",
  "params": {
    "name": "LegEdge",
    "start": [0, 0, 0],
    "end": [0, 2, 0],
    "parent": "Swing/_References",
    "color": "cyan",
    "width": 0.02
  }
}
```

**Example (world coordinates):**
```json
{
  "operation": "create_line",
  "params": {
    "name": "FloorEdge",
    "start": [-0.4, 0, 0.5],
    "end": [0.4, 0, 0.5],
    "color": "yellow",
    "width": 0.02
  }
}
```

**Example (GameObject paths):**
```json
{
  "operation": "create_line",
  "params": {
    "name": "MarkerConnection",
    "start": "Markers/V_Start",
    "end": "Markers/V_End",
    "color": "cyan"
  }
}
```

**Response Format:**
Returns a success message with the line name and space used.

**Note:**
- Automatically detects URP vs Built-in render pipeline for shader selection
- Uses `Undo.RegisterCreatedObjectUndo()` for Undo support
