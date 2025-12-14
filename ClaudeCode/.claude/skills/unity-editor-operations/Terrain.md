# Terrain Operations

Terrain operations for creating and controlling Unity Terrain objects.

## Operations

### create_terrain
Creates a new Terrain GameObject with specified properties.

**Parameters:**
- `name` (optional): Terrain name (default: "Terrain")
- `terrain_width` (optional): Width in X axis (default: 100)
- `terrain_length` (optional): Length in Z axis (default: 100)
- `terrain_height` (optional): Maximum height in Y axis (default: 50)
- `heightmap_resolution` (optional): Heightmap resolution, must be 2^n + 1 (default: 513)
- `position` (optional): [x, y, z] position
- `position_space` (optional): "local" | "world" (default: "world" if no parent, "local" if parent specified)
- `parent` (optional): Parent GameObject path/name

> **Space parameters:** See [SKILL.md#space-parameters](SKILL.md#space-parameters) for defaults.

**Valid heightmap_resolution values:** 33, 65, 129, 257, 513, 1025, 2049, 4097

**JSON Example:**
```json
{
  "operation": "create_terrain",
  "params": {
    "name": "MyTerrain",
    "terrain_width": 200,
    "terrain_length": 200,
    "terrain_height": 100,
    "heightmap_resolution": 257,
    "position": [0, 0, 0]
  }
}
```

**Response:**
```
Created terrain: MyTerrain (Size: 200x200x100, Resolution: 257) (position: world)
```

**Notes:**
- TerrainData is automatically saved to `Assets/ClaudeAgent/Generated/Terrains/`
- Higher resolution provides more detail but uses more memory
- Common resolutions: 257 (low), 513 (medium), 1025 (high)

---

### add_terrain_layer
Adds a texture layer to a Terrain for painting.

**Parameters:**
- `path` (required): GameObject path/name containing Terrain component
- `texture_path` (required): Path to diffuse texture asset
- `normal_path` (optional): Path to normal map texture
- `tile_size` (optional): Texture tile size in world units (default: 10)

**JSON Example:**
```json
{
  "operation": "add_terrain_layer",
  "params": {
    "path": "MyTerrain",
    "texture_path": "Assets/Textures/Grass_Diffuse.png",
    "normal_path": "Assets/Textures/Grass_Normal.png",
    "tile_size": 5
  }
}
```

**Response:**
```
Added terrain layer 'Grass_Diffuse' to MyTerrain (Total layers: 1)
```

---

### terrain_height

Unified command for getting terrain info and modifying terrain height.

**Parameters:**
- `path` (required): GameObject path/name containing Terrain component
- `get` (optional): If true, returns terrain info
- `flatten_height` (optional): Height value to flatten entire terrain (world units)
- `heights` (optional): 1D array of height values (normalized 0-1)
- `center` (optional): [x, z] center position for paint mode
- `radius` (optional): Radius of effect for paint mode
- `height_delta` (optional): Height change amount for paint mode
- `falloff` (optional): Falloff type - "smooth" (default), "linear", or "flat"

**Get Mode Example:**
```json
{
  "operation": "terrain_height",
  "params": {
    "path": "MyTerrain",
    "get": true
  }
}
```

**Get Response Format:**
```
Terrain Info for 'MyTerrain':

Size: 100 x 100 (Width x Length)
Max Height: 50
Heightmap Resolution: 513
Position: (0.0, 0.0, 0.0)

Terrain Layers (2):
  [0] Grass_Diffuse (Tile: 5x5)
  [1] Rock_Diffuse (Tile: 10x10)

TerrainData Asset: Assets/ClaudeAgent/Generated/Terrains/MyTerrain_Data.asset
```

**Flatten Mode Example:**
```json
{
  "operation": "terrain_height",
  "params": {
    "path": "MyTerrain",
    "flatten_height": 10
  }
}
```

**Paint Mode Examples:**

Create hill:
```json
{
  "operation": "terrain_height",
  "params": {
    "path": "MyTerrain",
    "center": [5, 5],
    "radius": 3,
    "height_delta": 2,
    "falloff": "smooth"
  }
}
```

Create valley:
```json
{
  "operation": "terrain_height",
  "params": {
    "path": "MyTerrain",
    "center": [10, 10],
    "radius": 2,
    "height_delta": -1.5,
    "falloff": "linear"
  }
}
```

**Falloff Types:**
| Type | Description |
|------|-------------|
| `smooth` | Cosine interpolation - natural looking hills |
| `linear` | Linear falloff from center to edge |
| `flat` | No falloff - creates plateau/crater with sharp edges |

**Notes:**
- Cannot specify both `get: true` and modification parameters
- `center` uses world coordinates
- `height_delta` positive = raise, negative = lower

---

### terrain_texture

Unified command for painting and filling terrain textures.

**Parameters:**
- `path` (required): GameObject path/name containing Terrain component
- `layer_index` (required): Index of the layer to paint (0-based)
- `fill` (optional): If true, fills entire terrain with layer
- `center` (optional): [x, z] center position for paint mode
- `radius` (optional): Radius of effect for paint mode
- `strength` (optional): Paint/fill strength 0-1 (default: 1.0)
- `falloff` (optional): Falloff type - "smooth" (default), "linear", or "flat"

**Fill Mode Example:**
```json
{
  "operation": "terrain_texture",
  "params": {
    "path": "MyTerrain",
    "layer_index": 1,
    "fill": true,
    "strength": 1.0
  }
}
```

**Response:**
```
Filled terrain with layer [1] 'Rock_Diffuse' at strength 1.00
```

**Paint Mode Example:**
```json
{
  "operation": "terrain_texture",
  "params": {
    "path": "MyTerrain",
    "layer_index": 1,
    "center": [50, 50],
    "radius": 10,
    "strength": 0.8,
    "falloff": "smooth"
  }
}
```

**Response:**
```
Painted terrain texture: layer [1] 'Rock_Diffuse' at (50, 50) with radius 10 (314 points modified)
```

**Notes:**
- Requires at least one layer added via `add_terrain_layer`
- Use `terrain_height` with `get: true` to check available layer indices
- Other layers are automatically normalized (sum = 1.0)
- Cannot specify both `fill: true` and paint parameters (center, radius)
- Changes are recorded in Undo history
