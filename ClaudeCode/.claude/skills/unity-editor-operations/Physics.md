# Physics Operations

Physics operations manage Unity's physics settings and layer collision matrix.

## Operations

### get_physics_settings

Gets the current physics settings.

**Parameters:** None

**Example:**
```json
{
  "operation": "get_physics_settings",
  "params": {}
}
```

**Response Format:**
Returns current physics settings including gravity, solver iterations, sleep threshold, etc.

### set_physics_settings

Modifies physics settings.

**Parameters:**
- `gravity` (optional): Gravity vector [x, y, z]
- `solver_iterations` (optional): Default solver iterations
- `solver_velocity_iterations` (optional): Default solver velocity iterations
- `sleep_threshold` (optional): Sleep threshold
- `contact_offset` (optional): Default contact offset
- `bounce_threshold` (optional): Bounce threshold
- `auto_sync_transforms` (optional): Auto sync transforms (bool)
- `queries_hit_triggers` (optional): Queries hit triggers (bool)
- `queries_hit_backfaces` (optional): Queries hit backfaces (bool)

**Example - Change gravity:**
```json
{
  "operation": "set_physics_settings",
  "params": {
    "gravity": [0, -20, 0]
  }
}
```

**Example - Multiple settings:**
```json
{
  "operation": "set_physics_settings",
  "params": {
    "gravity": [0, -9.81, 0],
    "solver_iterations": 10,
    "sleep_threshold": 0.005
  }
}
```

**Response Format:**
Returns list of updated settings.

### get_layer_collision_matrix

Gets the layer collision matrix.

**Parameters:**
- `layer_name` (optional): Get collisions for specific layer by name
- `layer` (optional): Get collisions for specific layer by index

**Example - Get all ignored pairs:**
```json
{
  "operation": "get_layer_collision_matrix",
  "params": {}
}
```

**Example - Get collisions for specific layer:**
```json
{
  "operation": "get_layer_collision_matrix",
  "params": {
    "layer_name": "Player"
  }
}
```

**Response Format:**
- Without parameters: Lists all ignored layer pairs
- With layer specified: Shows collision status with all other layers

### set_layer_collision

Sets collision behavior between two layers.

**Parameters:**
- `layer_name` (required if no layer): First layer name
- `layer` (required if no layer_name): First layer index
- `layer2_name` (required if no layer2): Second layer name
- `layer2` (required if no layer2_name): Second layer index
- `ignore` (required): true to ignore collisions, false to enable collisions

**Example - Ignore collisions between layers:**
```json
{
  "operation": "set_layer_collision",
  "params": {
    "layer_name": "Player",
    "layer2_name": "PlayerProjectiles",
    "ignore": true
  }
}
```

**Example - Enable collisions between layers:**
```json
{
  "operation": "set_layer_collision",
  "params": {
    "layer_name": "Enemies",
    "layer2_name": "EnemyProjectiles",
    "ignore": false
  }
}
```

**Example - Using layer indices:**
```json
{
  "operation": "set_layer_collision",
  "params": {
    "layer": 8,
    "layer2": 9,
    "ignore": true
  }
}
```

**Response Format:**
Returns confirmation of the collision setting change.

## Common Use Cases

### Setting up a player that doesn't collide with own projectiles
```json
// Create layers
{"operation": "create_layer", "params": {"name": "Player"}}
{"operation": "create_layer", "params": {"name": "PlayerProjectiles"}}

// Set player to not collide with own projectiles
{"operation": "set_layer_collision", "params": {"layer_name": "Player", "layer2_name": "PlayerProjectiles", "ignore": true}}

// Assign layers to objects
{"operation": "set_layer", "params": {"path": "Player", "layer_name": "Player"}}
{"operation": "set_layer", "params": {"path": "Bullet", "layer_name": "PlayerProjectiles"}}
```

### Adjusting gravity for a space game
```json
{
  "operation": "set_physics_settings",
  "params": {
    "gravity": [0, 0, 0]
  }
}
```

### Setting up platformer physics
```json
{
  "operation": "set_physics_settings",
  "params": {
    "gravity": [0, -30, 0],
    "solver_iterations": 10
  }
}
```

## Feature Permission

Physics operations that modify settings (`set_physics_settings`, `set_layer_collision`) require the **Physics** feature to be enabled in the Command Server window.

Read-only operations (`get_physics_settings`, `get_layer_collision_matrix`) are always available.
