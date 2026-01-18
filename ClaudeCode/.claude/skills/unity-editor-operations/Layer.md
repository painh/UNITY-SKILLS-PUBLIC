# Layer Operations

Layer operations manage Unity's layer system for organizing GameObjects and controlling physics/rendering interactions.

## Overview

Unity supports 32 layers (0-31):
- **Layers 0-7**: Builtin layers (Default, TransparentFX, Ignore Raycast, Water, UI, etc.) - cannot be modified
- **Layers 8-31**: User-definable layers (24 available slots)

## Operations

### list_layers

Lists all layers in the project.

**Parameters:** None

**Example:**
```json
{
  "operation": "list_layers",
  "params": {}
}
```

**Response Format:**
Returns a formatted list of all builtin and user layers with their indices.

### create_layer

Creates a new layer in an available slot.

**Parameters:**
- `name` (required): Name for the new layer
- `index` (optional): Specific layer index (8-31). If not specified, uses first available slot.

**Example:**
```json
{
  "operation": "create_layer",
  "params": {
    "name": "Enemies"
  }
}
```

**Example with specific index:**
```json
{
  "operation": "create_layer",
  "params": {
    "name": "Projectiles",
    "index": 10
  }
}
```

**Response Format:**
Returns success message with the layer name and assigned index.

**Note:**
- Cannot create layers with duplicate names
- Only indices 8-31 are available for user layers
- Modifies ProjectSettings/TagManager.asset

### delete_layer

Deletes a user layer (clears its name from the slot).

**Parameters:**
- `name` (required if no index): Name of the layer to delete
- `index` (required if no name): Index of the layer to delete (8-31)

**Example by name:**
```json
{
  "operation": "delete_layer",
  "params": {
    "name": "Enemies"
  }
}
```

**Example by index:**
```json
{
  "operation": "delete_layer",
  "params": {
    "index": 10
  }
}
```

**Response Format:**
Returns success message with the deleted layer name and index.

**Note:**
- Cannot delete builtin layers (0-7)
- GameObjects using deleted layer will remain on that layer index
- Consider reassigning GameObjects before deleting their layer

### set_layer

Sets the layer of a GameObject.

**Parameters:**
- `path` (required): Path or name of the GameObject
- `layer` (required if no index): Layer name
- `index` (required if no layer): Layer index (0-31)
- `recursive` (optional): Apply to all children (default: false)

**Example by layer name:**
```json
{
  "operation": "set_layer",
  "params": {
    "path": "Enemy_01",
    "layer_name": "Enemies"
  }
}
```

**Example with recursive:**
```json
{
  "operation": "set_layer",
  "params": {
    "path": "Player",
    "layer_name": "Player",
    "recursive": true
  }
}
```

**Example by index:**
```json
{
  "operation": "set_layer",
  "params": {
    "path": "Bullet",
    "index": 10
  }
}
```

**Response Format:**
Returns success message with the GameObject name and new layer.

### get_layer

Gets the current layer of a GameObject.

**Parameters:**
- `path` (required): Path or name of the GameObject

**Example:**
```json
{
  "operation": "get_layer",
  "params": {
    "path": "Player"
  }
}
```

**Response Format:**
Returns the layer name and index for the specified GameObject.

## Common Use Cases

### Setting up collision layers
```json
// Create layers
{"operation": "create_layer", "params": {"name": "Player"}}
{"operation": "create_layer", "params": {"name": "Enemies"}}
{"operation": "create_layer", "params": {"name": "Projectiles"}}

// Assign GameObjects
{"operation": "set_layer", "params": {"path": "Player", "layer_name": "Player", "recursive": true}}
{"operation": "set_layer", "params": {"path": "Enemy_01", "layer_name": "Enemies"}}
```

### Organizing scene objects
```json
// Create organizational layers
{"operation": "create_layer", "params": {"name": "Environment"}}
{"operation": "create_layer", "params": {"name": "Interactables"}}

// Assign objects
{"operation": "set_layer", "params": {"path": "Ground", "layer_name": "Environment"}}
{"operation": "set_layer", "params": {"path": "Door", "layer_name": "Interactables"}}
```

## Feature Permission

Layer operations that modify project settings (`create_layer`, `delete_layer`, `set_layer`) require the **Layer** feature to be enabled in the Command Server window.

Read-only operations (`list_layers`, `get_layer`) are always available.
