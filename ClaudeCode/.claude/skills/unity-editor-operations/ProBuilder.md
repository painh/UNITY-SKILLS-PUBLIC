# ProBuilder Operations

ProBuilder operations for creating procedural 3D meshes in Unity Editor.

**Requirements:**
- ProBuilder package must be installed (`com.unity.probuilder`)
- Install via: Window > Package Manager > Unity Registry > ProBuilder

## Operations

### create_probuilder_shape

Creates a ProBuilder mesh with specified shape and properties.

**Common Parameters:**
| Name | Type | Required | Description |
|------|------|----------|-------------|
| shape | string | Yes | Shape type (see below) |
| name | string | No | GameObject name (default: "ProBuilder_{shape}") |
| position | array[3] | No | World position [x, y, z] |
| rotation | array[3] | No | Euler angles [x, y, z] |
| scale | array[3] | No | Scale [x, y, z] |
| color | string | No | Color name or hex code (#RRGGBB) |

**Supported Shape Types:**
- `stair` - Staircase
- `door` - Door frame
- `curved_stair` - Spiral staircase
- `arch` - Arch/arc shape
- `pipe` - Hollow cylinder
- `cone` - Cone shape
- `prism` - Triangular prism

> **Note:** For basic shapes (cube, sphere, cylinder, capsule), use `create_primitive` command instead. ProBuilder is for specialized shapes only.

---

## Shape-Specific Parameters

### stair

Creates a staircase.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| width | float | 2.0 | Stair width |
| height | float | 2.5 | Total height |
| depth | float | 4.0 | Total depth |
| steps | int | 10 | Number of steps |
| build_sides | bool | true | Generate side faces |

**Example:**
```json
{
  "operation": "create_probuilder_shape",
  "params": {
    "shape": "stair",
    "name": "MainStairs",
    "width": 2,
    "height": 3,
    "depth": 5,
    "steps": 12,
    "position": [0, 0, 0]
  }
}
```

---

### door

Creates a door frame (rectangle with opening).

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| width | float | 4.0 | Total width |
| height | float | 4.0 | Total height |
| ledge_height | float | 1.0 | Top ledge height |
| leg_width | float | 1.0 | Side pillar width |
| depth | float | 0.5 | Frame depth |

**Example:**
```json
{
  "operation": "create_probuilder_shape",
  "params": {
    "shape": "door",
    "name": "Doorway",
    "width": 3,
    "height": 4,
    "ledge_height": 0.5,
    "leg_width": 0.5,
    "depth": 0.3
  }
}
```

---

### curved_stair

Creates a spiral/curved staircase.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| width | float | 2.0 | Stair width (from inner to outer edge) |
| height | float | 2.5 | Total height |
| inner_radius | float | 2.0 | Inner radius of spiral |
| circumference | float | 90.0 | Arc angle in degrees |
| steps | int | 10 | Number of steps |
| build_sides | bool | true | Generate side faces |

**Example:**
```json
{
  "operation": "create_probuilder_shape",
  "params": {
    "shape": "curved_stair",
    "name": "SpiralStairs",
    "width": 1.5,
    "height": 4,
    "inner_radius": 1,
    "circumference": 180,
    "steps": 16,
    "position": [0, 0, 0]
  }
}
```

---

### arch

Creates an arch/arc shape.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| radius | float | 2.0 | Arch radius |
| arc_degrees | float | 180.0 | Arc angle in degrees |
| thickness | float | 0.5 | Arch thickness (radial) - alias: `width` |
| depth | float | 0.5 | Arch depth |
| sides | int | 6 | Number of segments along arc - alias: `axis_divisions` |

**Example:**
```json
{
  "operation": "create_probuilder_shape",
  "params": {
    "shape": "arch",
    "name": "Archway",
    "radius": 2.5,
    "arc_degrees": 180,
    "thickness": 0.3,
    "depth": 0.5,
    "color": "gray"
  }
}
```

---

### pipe

Creates a hollow cylinder (pipe).

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| radius | float | 1.0 | Outer radius |
| height | float | 2.0 | Height |
| thickness | float | 0.2 | Wall thickness |
| axis_divisions | int | 16 | Number of sides |
| height_cuts | int | 0 | Height segments |

**Example:**
```json
{
  "operation": "create_probuilder_shape",
  "params": {
    "shape": "pipe",
    "name": "Tube",
    "radius": 1,
    "height": 3,
    "thickness": 0.15,
    "axis_divisions": 20,
    "color": "cyan"
  }
}
```

---

### cone

Creates a cone shape.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| radius | float | 1.0 | Base radius |
| height | float | 2.0 | Height |
| sides | int | 16 | Number of sides - alias: `axis_divisions` |

**Example:**
```json
{
  "operation": "create_probuilder_shape",
  "params": {
    "shape": "cone",
    "name": "Spike",
    "radius": 1,
    "height": 3,
    "sides": 24,
    "color": "red"
  }
}
```

---

### prism

Creates a triangular prism.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| width | float | 1.0 | Width (X axis) |
| height | float | 1.0 | Height (Y axis) |
| depth | float | 1.0 | Depth (Z axis) |

**Example:**
```json
{
  "operation": "create_probuilder_shape",
  "params": {
    "shape": "prism",
    "name": "Wedge",
    "width": 2,
    "height": 1,
    "depth": 3,
    "color": "magenta"
  }
}
```

---

## Error Handling

| Error | Message |
|-------|---------|
| Missing shape | "Missing required parameter: shape" |
| Invalid shape | "Unknown shape type: '{shape}'. Valid types: stair, door, curved_stair, arch, pipe, cone, prism. For cube/cylinder, use 'create_primitive' command." |
| Duplicate name | "GameObject with name '{name}' already exists at root level..." |
| Invalid color | "Unknown color: '{color}'..." |

---

## Notes

- All shapes are created at the center pivot point
- ProBuilder meshes can be edited further in Unity's ProBuilder window
- The `build_sides` parameter only affects stair and curved_stair shapes
- Colors support both named colors (red, blue, green, etc.) and hex codes (#FF0000)
- Parameter aliases: `thickness` = `width` (for arch), `sides` = `axis_divisions` (for arch/cone)
- For basic primitives (cube, sphere, cylinder, capsule, plane, quad), use `create_primitive` command

