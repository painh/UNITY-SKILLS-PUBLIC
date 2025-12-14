# Camera Operations

Camera operations for creating and controlling cameras in Unity.

## Operations

### create_camera
Creates a new Camera GameObject with specified properties.

**Parameters:**
- `name` (optional): Camera name
- `parent` (optional): Parent GameObject path
- `position` (optional): [x, y, z] position
- `position_space` (optional): "world" or "local" (default: "world" if no parent, "local" if parent specified)
- `rotation` (optional): [x, y, z] rotation (Euler angles)
- `fov` (optional): Field of view in degrees (default: 60)
- `near` (optional): Near clip plane distance (default: 0.3)
- `far` (optional): Far clip plane distance (default: 1000)

**JSON Example (Root):**
```json
{
  "operation": "create_camera",
  "params": {
    "name": "MainCamera",
    "position": [0, 3, -10],
    "fov": 75,
    "near": 0.5,
    "far": 200
  }
}
```

**JSON Example (Child of Parent):**
```json
{
  "operation": "create_camera",
  "params": {
    "name": "FirstPersonCamera",
    "parent": "Player/Head",
    "position": [0, 0.1, 0],
    "fov": 90
  }
}
```

**Response:**
```
Created camera: FirstPersonCamera (position: local)
```

**Notes:**
- Default FOV is 60 degrees
- Default near clip plane is 0.3
- Default far clip plane is 1000
- Cameras automatically receive an Audio Listener component
- When `parent` is specified, position defaults to local coordinates
- Root level cameras check for duplicate names

---

### camera

Unified command for getting and setting camera properties.

**Parameters:**
- `path` (required): GameObject path/name containing Camera component
- `get` (optional): If true, returns camera info
- `position` (optional): [x, y, z] position (world space)
- `fov` (optional): Field of view in degrees
- `near` (optional): Near clip plane distance
- `far` (optional): Far clip plane distance

**Get Mode Example:**
```json
{
  "operation": "camera",
  "params": {
    "path": "MainCamera",
    "get": true
  }
}
```

**Get Response Format:**
Returns camera information including position, rotation, FOV, clip planes, and orthographic settings.

**Set Mode Examples:**

Set position:
```json
{
  "operation": "camera",
  "params": {
    "path": "MainCamera",
    "position": [5, 5, -15]
  }
}
```

Set camera properties:
```json
{
  "operation": "camera",
  "params": {
    "path": "MainCamera",
    "fov": 60,
    "near": 0.3,
    "far": 500
  }
}
```

Set all properties at once:
```json
{
  "operation": "camera",
  "params": {
    "path": "MainCamera",
    "position": [0, 5, -10],
    "fov": 75,
    "near": 0.5,
    "far": 200
  }
}
```

**Notes:**
- Cannot specify both `get: true` and property values
- Common FOV ranges:
  - First-person: 70-90
  - Third-person: 50-70
  - Cinematic: 35-50
- Changes are recorded in Undo history
- For rotation, use the `transform` command
