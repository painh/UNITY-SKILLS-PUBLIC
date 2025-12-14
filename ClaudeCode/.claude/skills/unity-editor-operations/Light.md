# Light Operations

Light operations for creating and controlling lights in Unity.

## Operations

### create_light
Creates a new Light GameObject with specified properties.

**Parameters:**
- `type` (required): Light type - "directional", "point", "spot", "area"
- `name` (optional): Light name
- `color` (optional): Color name (red, green, blue, yellow, cyan, magenta, white, black, gray) or #RRGGBB format
- `intensity` (optional): Light intensity (default varies by type)
- `position` (optional): [x, y, z] position
- `rotation` (optional): [x, y, z] rotation (Euler angles)

**JSON Example:**
```json
{
  "operation": "create_light",
  "params": {
    "type": "directional",
    "name": "MainLight",
    "color": "yellow",
    "intensity": 1.5,
    "position": [0, 5, 0],
    "rotation": [50, -30, 0]
  }
}
```

**Response:**
```
Created Directional light: MainLight
```

**Notes:**
- Directional lights are good for sun/moon lighting
- Point lights emit in all directions from a point
- Spot lights emit in a cone shape
- Area lights (Rectangle) are only available in baked lighting

---

### light

Unified command for getting and setting light properties.

**Parameters:**
- `path` (required): GameObject path/name containing Light component
- `get` (optional): If true, returns light info
- `color` (optional): Color name or #RRGGBB format
- `r`, `g`, `b` (optional): RGB values (0-1) as alternative to color
- `intensity` (optional): Light intensity value (typically 0-10)

**Get Mode Example:**
```json
{
  "operation": "light",
  "params": {
    "path": "MainLight",
    "get": true
  }
}
```

**Get Response Format:**
Returns detailed light information including type, color, intensity, shadows, and range.

**Set Mode Examples:**

Set color using color name:
```json
{
  "operation": "light",
  "params": {
    "path": "MainLight",
    "color": "blue"
  }
}
```

Set color using RGB:
```json
{
  "operation": "light",
  "params": {
    "path": "MainLight",
    "r": 1.0,
    "g": 0.5,
    "b": 0.0
  }
}
```

Set intensity:
```json
{
  "operation": "light",
  "params": {
    "path": "MainLight",
    "intensity": 3.0
  }
}
```

Set multiple properties:
```json
{
  "operation": "light",
  "params": {
    "path": "MainLight",
    "color": "yellow",
    "intensity": 2.0
  }
}
```

**Notes:**
- Either `color` or `r,g,b` parameters can be used
- RGB values range from 0 to 1
- Typical intensity ranges:
  - Directional: 0.5 - 2
  - Point: 1 - 10
  - Spot: 1 - 10
- Cannot specify both `get: true` and property values
- Changes are recorded in Undo history
