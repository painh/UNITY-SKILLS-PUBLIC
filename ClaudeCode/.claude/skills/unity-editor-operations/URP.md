# URP (Universal Render Pipeline) Operations

URP operations handle ScriptableRendererFeature management on UniversalRendererData assets.

**Note:** These commands require the Universal Render Pipeline package to be installed in the project. If URP is not available, commands will return an error message.

## Operations

### add_renderer_feature

Adds a ScriptableRendererFeature to a UniversalRendererData asset.

**Parameters:**

| Name | Type | Required | Description |
|------|------|----------|-------------|
| asset_path | string | Yes | Path to UniversalRendererData asset (e.g., "Assets/Settings/PC_Renderer.asset") |
| component | string | Yes | ScriptableRendererFeature type name (full class name or simple name) |
| name | string | No | Custom name for the feature (defaults to type name) |

**Example - Add custom render feature:**
```json
{
  "operation": "add_renderer_feature",
  "params": {
    "asset_path": "Assets/Settings/PC_Renderer.asset",
    "component": "MirrorRPG.Rendering.RadialBlurRenderFeature"
  }
}
```

**Example - Add built-in URP feature:**
```json
{
  "operation": "add_renderer_feature",
  "params": {
    "asset_path": "Assets/Settings/URP_Renderer.asset",
    "component": "RenderObjects",
    "name": "OutlineRenderFeature"
  }
}
```

**Response:**
```
Added renderer feature 'RadialBlurRenderFeature' (RadialBlurRenderFeature) to Assets/Settings/PC_Renderer.asset
```

---

### remove_renderer_feature

Removes a ScriptableRendererFeature from a UniversalRendererData asset.

**Parameters:**

| Name | Type | Required | Description |
|------|------|----------|-------------|
| asset_path | string | Yes | Path to UniversalRendererData asset |
| name | string | No* | Feature name to remove |
| component | string | No* | Feature type name to remove |

*At least one of `name` or `component` is required.

**Example - Remove by name:**
```json
{
  "operation": "remove_renderer_feature",
  "params": {
    "asset_path": "Assets/Settings/PC_Renderer.asset",
    "name": "RadialBlurRenderFeature"
  }
}
```

**Example - Remove by type:**
```json
{
  "operation": "remove_renderer_feature",
  "params": {
    "asset_path": "Assets/Settings/PC_Renderer.asset",
    "component": "RadialBlurRenderFeature"
  }
}
```

**Response:**
```
Removed renderer feature 'RadialBlurRenderFeature' from Assets/Settings/PC_Renderer.asset
```

---

### get_renderer_features

Gets the list of ScriptableRendererFeatures on a UniversalRendererData asset.

**Parameters:**

| Name | Type | Required | Description |
|------|------|----------|-------------|
| asset_path | string | Yes | Path to UniversalRendererData asset |

**Example:**
```json
{
  "operation": "get_renderer_features",
  "params": {
    "asset_path": "Assets/Settings/PC_Renderer.asset"
  }
}
```

**Response:**
```
Renderer Features on Assets/Settings/PC_Renderer.asset:
  [0] ScreenSpaceAmbientOcclusion (ScreenSpaceAmbientOcclusion) - active
  [1] RadialBlurRenderFeature (RadialBlurRenderFeature) - active
  [2] RenderObjects (RenderObjects) - inactive
```

---

### set_renderer_feature_property

Sets a property on a ScriptableRendererFeature.

**Parameters:**

| Name | Type | Required | Description |
|------|------|----------|-------------|
| asset_path | string | Yes | Path to UniversalRendererData asset |
| name | string | No* | Feature name to modify |
| component | string | No* | Feature type name to modify |
| property | string | Yes | Property name to set |
| value | string | Yes | Property value (as string) |

*At least one of `name` or `component` is required.

**Example - Enable/Disable feature:**
```json
{
  "operation": "set_renderer_feature_property",
  "params": {
    "asset_path": "Assets/Settings/PC_Renderer.asset",
    "name": "RadialBlurRenderFeature",
    "property": "isActive",
    "value": "true"
  }
}
```

**Example - Set custom property:**
```json
{
  "operation": "set_renderer_feature_property",
  "params": {
    "asset_path": "Assets/Settings/PC_Renderer.asset",
    "component": "RadialBlurRenderFeature",
    "property": "blurStrength",
    "value": "0.5"
  }
}
```

**Example - Set shader reference:**
```json
{
  "operation": "set_renderer_feature_property",
  "params": {
    "asset_path": "Assets/Settings/PC_Renderer.asset",
    "name": "RadialBlurRenderFeature",
    "property": "blurShader",
    "value": "Assets/Shaders/PostProcess/RadialBlur.shader"
  }
}
```

**Response:**
```
Set RadialBlurRenderFeature.blurStrength = 0.5
```

---

## Supported Property Types

The `set_renderer_feature_property` command supports the following property types:

| Type | Value Format | Example |
|------|-------------|---------|
| bool | "true" or "false" | "true" |
| int | integer string | "42" |
| float | decimal string | "0.75" |
| string | any string | "MyValue" |
| enum | enum name or index | "AfterRenderingOpaques" or "2" |
| Color | color name or #RRGGBB | "red" or "#FF5500" |
| Vector2 | "[x, y]" | "[1.5, 2.0]" |
| Vector3 | "[x, y, z]" | "[1, 2, 3]" |
| Vector4 | "[x, y, z, w]" | "[1, 2, 3, 4]" |
| Object | asset path | "Assets/Textures/Noise.png" |

---

## Common Built-in URP Renderer Features

| Feature Type | Description |
|-------------|-------------|
| RenderObjects | Renders objects with specific layer masks and overrides |
| ScreenSpaceAmbientOcclusion | SSAO post-processing effect |
| DecalRendererFeature | Renders decal projectors |
| FullScreenPassRendererFeature | Full screen post-processing pass |

---

## Notes

- **URP Required**: These commands only work when the Universal Render Pipeline package is installed
- **Asset Modification**: Commands directly modify the UniversalRendererData asset files
- **Undo Support**: All operations support Unity's Undo system
- **Type Resolution**: The `component` parameter supports:
  - Simple type name (e.g., "RadialBlurRenderFeature")
  - Full namespace path (e.g., "MirrorRPG.Rendering.RadialBlurRenderFeature")
  - URP built-in features (e.g., "RenderObjects", "ScreenSpaceAmbientOcclusion")
- **Duplicate Check**: `add_renderer_feature` will fail if a feature of the same type already exists

---

## Use Case: Adding a Custom Post-Processing Effect

1. Create your custom ScriptableRendererFeature class (e.g., `RadialBlurRenderFeature.cs`)
2. Add to renderer:
```json
{
  "operation": "add_renderer_feature",
  "params": {
    "asset_path": "Assets/Settings/PC_Renderer.asset",
    "component": "RadialBlurRenderFeature"
  }
}
```

3. Configure settings:
```json
{
  "operation": "set_renderer_feature_property",
  "params": {
    "asset_path": "Assets/Settings/PC_Renderer.asset",
    "name": "RadialBlurRenderFeature",
    "property": "settings.blurStrength",
    "value": "0.3"
  }
}
```

4. Verify:
```json
{
  "operation": "get_renderer_features",
  "params": {
    "asset_path": "Assets/Settings/PC_Renderer.asset"
  }
}
```
