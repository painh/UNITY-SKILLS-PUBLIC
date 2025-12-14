# Material Operations

Material operations handle creating, applying, and inspecting Unity Materials and their properties.

## Operations

### material

Unified command for getting and setting material properties. Supports three modes:
1. **GameObject mode**: Operate on materials attached to GameObjects (via `path`)
2. **Asset mode**: Operate directly on material assets (via `material_path` without `path`)
3. **Batch mode**: Change shaders for multiple materials in a folder (via `folder_path`)

**Parameters:**

| Name | Type | Required | Description |
|------|------|----------|-------------|
| path | string | No* | GameObject path or name (for GameObject mode) |
| material_path | string | No* | Material asset path (for applying or direct asset operation) |
| folder_path | string | No* | Folder path for batch shader change |
| get | bool | No | If true, returns material info |
| shader | string | No | Target shader name (for shader change) |
| from_shader | string | No | Source shader filter (batch mode only) |
| recursive | bool | No | Process subfolders (batch mode, default: false) |
| color | string | No | Color value (color name or #RRGGBB format) |
| r, g, b | float | No | RGB color values (0.0-1.0) - alternative to `color` |
| a | float | No | Alpha value (0.0-1.0, default: 1.0) |
| metallic | float | No | Metallic value (0.0-1.0) |
| smoothness | float | No | Smoothness value (0.0-1.0) |

*One of `path`, `material_path` (without `path`), or `folder_path` is required.

---

## Mode 1: GameObject Mode (Existing)

Operate on materials attached to GameObjects via Renderer component.

**Get Material Info:**
```json
{
  "operation": "material",
  "params": {
    "path": "PlayerSphere",
    "get": true
  }
}
```

**Set Color:**
```json
{
  "operation": "material",
  "params": {
    "path": "PlayerSphere",
    "color": "red"
  }
}
```

**Apply Material Asset:**
```json
{
  "operation": "material",
  "params": {
    "path": "PlayerSphere",
    "material_path": "Assets/Materials/RedMaterial.mat"
  }
}
```

**Set Multiple Properties:**
```json
{
  "operation": "material",
  "params": {
    "path": "PlayerSphere",
    "color": "gray",
    "metallic": 1.0,
    "smoothness": 0.8
  }
}
```

---

## Mode 2: Asset Direct Mode

Operate directly on material assets without needing a GameObject.

**Get Material Asset Info:**
```json
{
  "operation": "material",
  "params": {
    "material_path": "Assets/Materials/MyMaterial.mat",
    "get": true
  }
}
```

Response:
```
Material Asset Info:

Name: MyMaterial
Shader: Standard
Asset Path: Assets/Materials/MyMaterial.mat

Color: #FFFFFFFF
Metallic: 0.00
Smoothness: 0.50
Render Queue: 2000
```

**Change Shader of Single Material:**
```json
{
  "operation": "material",
  "params": {
    "material_path": "Assets/Materials/MyMaterial.mat",
    "shader": "Universal Render Pipeline/Lit"
  }
}
```

---

## Mode 3: Batch Shader Change

Change shaders for multiple materials in a folder.

**Basic Batch Change (all materials in folder):**
```json
{
  "operation": "material",
  "params": {
    "folder_path": "Assets/Materials",
    "shader": "Universal Render Pipeline/Lit"
  }
}
```

**With Source Shader Filter:**
```json
{
  "operation": "material",
  "params": {
    "folder_path": "Assets/UnityChan/Materials",
    "shader": "Universal Render Pipeline/Lit",
    "from_shader": "Standard"
  }
}
```

**Include Subfolders:**
```json
{
  "operation": "material",
  "params": {
    "folder_path": "Assets/UnityChan",
    "shader": "Universal Render Pipeline/Lit",
    "from_shader": "Standard",
    "recursive": true
  }
}
```

Response:
```
Batch shader change completed:
  Folder: Assets/UnityChan/Materials
  Target shader: Universal Render Pipeline/Lit
  Filter (from_shader): Standard
  Recursive: True
  Changed: 5
  Skipped: 2
  Materials: Body.mat, Face.mat, Hair.mat, Skin.mat, Eyes.mat
```

---

## Common Shader Names

| Shader | Name |
|--------|------|
| Standard (Built-in) | `Standard` |
| URP Lit | `Universal Render Pipeline/Lit` |
| URP Simple Lit | `Universal Render Pipeline/Simple Lit` |
| URP Unlit | `Universal Render Pipeline/Unlit` |
| Unlit/Color | `Unlit/Color` |
| Unlit/Texture | `Unlit/Texture` |

---

## Notes

- **GameObject mode**: Creates a copy of the material when modifying properties (prevents asset modification)
- **Asset mode**: Directly modifies the material asset file
- **Batch mode**: Requires `shader` parameter; `from_shader` is optional filter
- Supports Standard and URP Lit shaders (auto-detects `_Color` vs `_BaseColor`)
- Cannot specify both `get: true` and property values/shader
- Undo is supported for all operations

---

### create_material

Creates a new Material asset and saves it to the project.

**Parameters:**
- `material_path` (required): Asset path for the new material (e.g., "Assets/Materials/NewMaterial.mat")
- `material_name` (optional): Material name
- `shader_name` (optional): Shader name (default: auto-detects URP or Standard)
- `color` (optional): Initial color value
- `metallic` (optional): Metallic value (0.0-1.0) for Standard/URP shaders
- `smoothness` (optional): Smoothness value (0.0-1.0) for Standard/URP shaders

**Example:**
```json
{
  "operation": "create_material",
  "params": {
    "material_path": "Assets/Materials/MetalMaterial.mat",
    "material_name": "MetalMaterial",
    "shader_name": "Standard",
    "color": "gray",
    "metallic": 1.0,
    "smoothness": 0.8
  }
}
```

**Note:**
- Automatically detects URP and uses "Universal Render Pipeline/Lit" shader by default
- Falls back to "Standard" shader for non-URP projects
- Automatically creates parent directories if they don't exist
