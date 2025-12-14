# Component Operations

Component operations handle adding, removing, inspecting, and modifying Unity Components attached to GameObjects.

## Operations

### add_component

Adds a Component to a GameObject.

**Parameters:**
- `path` (required): Object path or name
- `component` (required): Component type name (e.g., "Rigidbody", "BoxCollider")

**Example:**
```json
{
  "operation": "add_component",
  "params": {
    "path": "PlayerSphere",
    "component": "Rigidbody"
  }
}
```

**Response Format:**
Returns a success message with the GameObject name and added Component type.

**Note:**
- Uses `Undo.RegisterCreatedObjectUndo()` for Undo support
- Checks if Component already exists to prevent duplicates
- Supports type resolution for "Rigidbody", "UnityEngine.Rigidbody", etc.
- Supports Unity modules: CoreModule, PhysicsModule, etc.

### remove_component

Removes a Component from a GameObject.

**Parameters:**
- `path` (required): Object path or name
- `component` (required): Component type name to remove

**Example:**
```json
{
  "operation": "remove_component",
  "params": {
    "path": "PlayerSphere",
    "component": "Rigidbody"
  }
}
```

**Response Format:**
Returns a success message with the GameObject name and removed Component type.

**Note:** Uses `Undo.DestroyObjectImmediate()` for Undo support.

### get_component

Retrieves information about a specific Component on a GameObject.

**Parameters:**
- `path` (required): Object path or name
- `component` (required): Component type name to retrieve

**Example:**
```json
{
  "operation": "get_component",
  "params": {
    "path": "PlayerSphere",
    "component": "Rigidbody"
  }
}
```

**Response Format:**
Returns a formatted string with Component information in JSON format, showing all serialized properties.

### set_component_property

Sets a property or field value on a Component.

**Parameters:**
- `path` (required): Object path or name
- `component` (required): Component type name
- `property` (required): Property or field name (e.g., "mass", "isKinematic")
- `value` (required): New value as string

**Example:**
```json
{
  "operation": "set_component_property",
  "params": {
    "path": "PlayerSphere",
    "component": "Rigidbody",
    "property": "mass",
    "value": "10"
  }
}
```

**Supported Value Formats (カンマ区切り形式を使用):**
- **Primitives**: "10" (int), "5.5" (float), "true" (bool)
- **Vector2**: "x, y" (例: "1, 2")
- **Vector3**: "x, y, z" (例: "1, 2, 3")
- **Vector4**: "x, y, z, w" (例: "1, 2, 3, 4")
- **Quaternion**: "x, y, z, w" (例: "0, 0, 0, 1")
- **Rect**: "x, y, width, height" (例: "0, 0, 1, 1")
- **Bounds**: "centerX, centerY, centerZ, sizeX, sizeY, sizeZ" (例: "0, 0, 0, 1, 1, 1")
- **LayerMask**: int値のみ (例: "32")。※プロパティ型がLayerMaskの場合のみレイヤー名使用可
- **Color**: "#FF0000" (HTML形式)
- **Enums**: "Dynamic" (case-insensitive)
- **Asset References**: "Assets/path/to/asset.ext" (for UnityEngine.Object derived types)

**注意:** Camera.cullingMask等の多くのUnityプロパティは`int`型で宣言されているため、レイヤー名ではなくint値（例: UI=32, Default=1, Everything=-1）を使用すること。

**Example (Asset Reference):**
```json
{
  "operation": "set_component_property",
  "params": {
    "path": "Player",
    "component": "PlayerInput",
    "property": "actions",
    "value": "Assets/InputSystem_Actions.inputactions"
  }
}
```

**Supported Asset Types:**
- InputActionAsset
- Material
- Texture2D
- AudioClip
- ScriptableObject (and derivatives)
- Any UnityEngine.Object derived type

**Response Format:**
Returns a success message with the full property path and new value.

**Note:**
- Uses `Undo.RecordObject()` for Undo support
- Supports both properties and public fields
- Calls `EditorUtility.SetDirty()` to mark scene as modified

### get_components

Lists all Components attached to a GameObject.

**Parameters:**
- `path` (required): Object path or name

**Example:**
```json
{
  "operation": "get_components",
  "params": {
    "path": "PlayerSphere"
  }
}
```

**Response Format:**
Returns a formatted string listing all Component types on the GameObject:
```
Components on: PlayerSphere
Total: 4 component(s)

- Transform
- MeshRenderer
- MeshFilter
- SphereCollider
```

### set_object_reference

Sets a GameObject or Component reference on a Component's property or field. Use this for properties that require scene object references (not asset references).

**Parameters:**
- `path` (required): Source object path containing the Component to modify
- `component` (required): Component type name on the source object
- `property` (required): Property or field name to set the reference
- `target_path` (required): Path to the target GameObject to reference

**Example:**
```json
{
  "operation": "set_object_reference",
  "params": {
    "path": "Main Camera",
    "component": "CameraController",
    "property": "player",
    "target_path": "Player"
  }
}
```

**Response Format:**
Returns a success message with the property path and target object name.

**Note:**
- Automatically determines the reference type (GameObject, Transform, or Component)
- If the property expects a specific Component type, it will get that Component from the target GameObject
- Supports both public properties and private fields (with [SerializeField])
- Uses `Undo.RecordObject()` for Undo support
- Use `set_component_property` for asset references (like Materials, Textures) with asset paths
