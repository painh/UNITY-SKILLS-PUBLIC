# Transform Operations

Transform operations handle GameObject positioning, rotation, scaling, and orientation in Unity's 3D space.

## Operations

### transform

Unified command for getting and setting transform properties (position, rotation, scale).

**Parameters:**
- `path` (required): Object path or name
- `get` (optional): If true, returns transform info
- `position` (optional): [x, y, z] array
- `rotation` (optional): [x, y, z] Euler angles
- `scale` (optional): [x, y, z] array
- `space` (optional): `"local"` (default) or `"world"` for position/rotation

**Get Mode Example:**
```json
{
  "operation": "transform",
  "params": {
    "path": "PurpleSphere",
    "get": true
  }
}
```

**Get Response Format:**
Returns a formatted string with complete Transform data:
- Full hierarchical path
- World position
- Local position
- World rotation (Euler angles)
- Local rotation (Euler angles)
- Local scale
- Parent object name
- Child count

**Set Mode Examples:**

Set position (local space, default):
```json
{
  "operation": "transform",
  "params": {
    "path": "PurpleSphere",
    "position": [5, 2, 0]
  }
}
```

Set position in world space:
```json
{
  "operation": "transform",
  "params": {
    "path": "PurpleSphere",
    "position": [5, 2, 0],
    "space": "world"
  }
}
```

Set rotation:
```json
{
  "operation": "transform",
  "params": {
    "path": "PurpleSphere",
    "rotation": [0, 45, 0]
  }
}
```

Set multiple properties at once:
```json
{
  "operation": "transform",
  "params": {
    "path": "PurpleSphere",
    "position": [5, 2, 0],
    "rotation": [0, 45, 0],
    "scale": [2, 2, 2]
  }
}
```

**Note:**
- Scale is always local scale (Unity limitation)
- Cannot specify both `get: true` and property values

### look_at

Makes a GameObject look at (face towards) another GameObject.

**Parameters:**
- `path` (required): Object path or name (the object that will rotate)
- `target_path` (required): Target object path or name (what to look at)

**Example:**
```json
{
  "operation": "look_at",
  "params": {
    "path": "Main Camera",
    "target_path": "PurpleSphere"
  }
}
```

**Response Format:**
Returns a success message with the object name and target name.

**Note:** Uses `Undo.RecordObject()` for Undo support.
