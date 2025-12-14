# Animator Operations

Animator operations handle AnimatorController editing and runtime Animator parameter control.

## Operations

### create_animator_controller

Creates a new AnimatorController asset.

**Parameters:**
| Name | Type | Required | Description |
|------|------|----------|-------------|
| path | string | Yes | Asset path (e.g., "Assets/Animators/MyController.controller") |
| name | string | Yes | Controller name |

**Example:**
```json
{
  "operation": "create_animator_controller",
  "params": {
    "path": "Assets/Animators/PlayerController.controller",
    "name": "PlayerController"
  }
}
```

### create_animator_element

Creates AnimatorController elements (state, layer, parameter, transition, blend_tree).

**Common Parameters:**
| Name | Type | Required | Description |
|------|------|----------|-------------|
| controller_path | string | Yes | AnimatorController asset path |
| type | string | Yes | Element type: "state", "layer", "parameter", "transition", "blend_tree" |

**type: "state":**
```json
{
  "operation": "create_animator_element",
  "params": {
    "type": "state",
    "controller_path": "Assets/Animators/PlayerController.controller",
    "name": "Idle",
    "layer": 0,
    "motion": "Assets/Animations/Idle.anim",
    "position": [100, 50],
    "is_default": true
  }
}
```

**type: "layer":**
```json
{
  "operation": "create_animator_element",
  "params": {
    "type": "layer",
    "controller_path": "Assets/Animators/PlayerController.controller",
    "name": "UpperBody",
    "weight": 1.0,
    "blending_mode": "Override"
  }
}
```

**type: "parameter":**
```json
{
  "operation": "create_animator_element",
  "params": {
    "type": "parameter",
    "controller_path": "Assets/Animators/PlayerController.controller",
    "name": "Speed",
    "param_type": "Float",
    "default_value": 0.0
  }
}
```
- **param_type:** Float, Int, Bool, Trigger
- **default_value:** Optional initial value (not applicable for Trigger)

**type: "transition":**
```json
{
  "operation": "create_animator_element",
  "params": {
    "type": "transition",
    "controller_path": "Assets/Animators/PlayerController.controller",
    "from_state": "Idle",
    "to_state": "Walk",
    "layer": 0,
    "has_exit_time": false,
    "duration": 0.25,
    "conditions": [
      {"parameter": "Speed", "mode": "Greater", "threshold": 0.1}
    ]
  }
}
```
- **from_state special values:** "Any" (Any State), "Entry" (Entry point)
- **condition mode:** If, IfNot, Greater, Less, Equals, NotEqual

**type: "blend_tree" (1D):**
```json
{
  "operation": "create_animator_element",
  "params": {
    "type": "blend_tree",
    "controller_path": "Assets/Animators/PlayerController.controller",
    "name": "Locomotion",
    "layer": 0,
    "parameter": "Speed",
    "blend_type": "Simple1D",
    "children": [
      {"motion": "Assets/Animations/Idle.anim", "threshold": 0.0},
      {"motion": "Assets/Animations/Walk.anim", "threshold": 0.5},
      {"motion": "Assets/Animations/Run.anim", "threshold": 1.0}
    ]
  }
}
```

**type: "blend_tree" (2D):**
```json
{
  "operation": "create_animator_element",
  "params": {
    "type": "blend_tree",
    "controller_path": "Assets/Animators/PlayerController.controller",
    "name": "Movement",
    "layer": 0,
    "parameter": "VelocityX",
    "parameter_y": "VelocityZ",
    "blend_type": "FreeformDirectional2D",
    "children": [
      {"motion": "Assets/Animations/Idle.anim", "position": [0, 0]},
      {"motion": "Assets/Animations/WalkForward.anim", "position": [0, 1]},
      {"motion": "Assets/Animations/WalkBack.anim", "position": [0, -1]},
      {"motion": "Assets/Animations/WalkLeft.anim", "position": [-1, 0]},
      {"motion": "Assets/Animations/WalkRight.anim", "position": [1, 0]}
    ]
  }
}
```
- **blend_type:** Simple1D, SimpleDirectional2D, FreeformDirectional2D, FreeformCartesian2D, Direct
- **parameter_y:** Required for 2D blend types
- **children format:** Use `threshold` for 1D, use `position: [x, y]` for 2D
- **motion path formats:**
  - Direct `.anim` file: `"Assets/Animations/Walk.anim"`
  - FBX embedded clip (by name): `"Assets/Models/Character.fbx/Walk"`
  - FBX (first clip): `"Assets/Models/Character.fbx"` (returns first animation found)

### delete_animator_element

Deletes AnimatorController elements.

**Parameters:**
| Name | Type | Required | Description |
|------|------|----------|-------------|
| controller_path | string | Yes | AnimatorController asset path |
| type | string | Yes | "state", "layer", "parameter", "transition" |

**Examples:**
```json
// Delete state
{
  "operation": "delete_animator_element",
  "params": {
    "type": "state",
    "controller_path": "Assets/Animators/PlayerController.controller",
    "name": "Jump",
    "layer": 0
  }
}

// Delete transition
{
  "operation": "delete_animator_element",
  "params": {
    "type": "transition",
    "controller_path": "Assets/Animators/PlayerController.controller",
    "from_state": "Idle",
    "to_state": "Walk",
    "layer": 0
  }
}
```

### animator_element

Gets or sets AnimatorController element properties.

**Get Mode:**
```json
// Get all states
{
  "operation": "animator_element",
  "params": {
    "controller_path": "Assets/Animators/PlayerController.controller",
    "type": "state",
    "get": true
  }
}

// Get all parameters
{
  "operation": "animator_element",
  "params": {
    "controller_path": "Assets/Animators/PlayerController.controller",
    "type": "parameter",
    "get": true
  }
}
```

**Set Mode:**
```json
// Set state properties (speed, position, motion, tag)
{
  "operation": "animator_element",
  "params": {
    "controller_path": "Assets/Animators/PlayerController.controller",
    "type": "state",
    "name": "Idle",
    "layer": 0,
    "speed": 1.5,
    "position": [150, 100],
    "motion": "Assets/Animations/NewIdle.anim"
  }
}

// Set layer properties
{
  "operation": "animator_element",
  "params": {
    "controller_path": "Assets/Animators/PlayerController.controller",
    "type": "layer",
    "name": "UpperBody",
    "weight": 0.5,
    "blending_mode": "Additive"
  }
}

// Set parameter default value
{
  "operation": "animator_element",
  "params": {
    "controller_path": "Assets/Animators/PlayerController.controller",
    "type": "parameter",
    "name": "Speed",
    "default_value": 1.0
  }
}
```

### animator

Unified command for getting Animator info and setting Animator parameters (runtime).

**Parameters:**
- `path` (required): Path or name of GameObject with Animator
- `get` (optional): If true, returns Animator info
- `parameter` (optional): Name of the parameter to set
- `param_value` (optional): Value to set (for Bool/Float/Int types)
  - For Bool: 0 = false, non-zero = true
  - For Float: any float value
  - For Int: integer value
  - For Trigger: not needed (just provide `parameter`)

**Get Mode Example:**
```json
{
  "operation": "animator",
  "params": {
    "path": "unitychan",
    "get": true
  }
}
```

**Get Response Format:**
Returns a formatted string with Animator information:
```
Animator Info for 'unitychan':

Controller: UnityChanLocomotions
Controller Path: Assets/UnityChan/Animators/UnityChanLocomotions.controller
Avatar: unitychanAvatar
Apply Root Motion: False
Update Mode: Normal
Culling Mode: AlwaysAnimate

Parameters (5):
  [Bool] isWalking = False (default)
  [Bool] isRunning = False (default)
  [Float] Speed = 0.00 (default)
  [Trigger] Jump
  [Int] State = 0 (default)

Layers (1):
  [0] Base Layer (default weight: 1.00)
      States (3):
        - Idle [default]
        - Walking
        - Running
```

**Set Mode Examples:**

Set Bool parameter:
```json
{
  "operation": "animator",
  "params": {
    "path": "unitychan",
    "parameter": "isWalking",
    "param_value": 1
  }
}
```

Set Float parameter:
```json
{
  "operation": "animator",
  "params": {
    "path": "unitychan",
    "parameter": "Speed",
    "param_value": 1.5
  }
}
```

Set Int parameter:
```json
{
  "operation": "animator",
  "params": {
    "path": "unitychan",
    "parameter": "State",
    "param_value": 2
  }
}
```

Fire Trigger (no value needed):
```json
{
  "operation": "animator",
  "params": {
    "path": "unitychan",
    "parameter": "Jump"
  }
}
```

**Notes:**
- Parameter type is auto-detected from AnimatorController
- Cannot specify both `get: true` and `parameter`
- Parameter name must match exactly (case-sensitive)
- For Trigger type, just provide `parameter` without `param_value`
- Trigger automatically resets after being consumed by a transition

## Setting Avatar on Animator

To set the Avatar property on an Animator component, use `set_component_property` from [Component.md](Component.md#set_component_property):

```json
{
  "operation": "set_component_property",
  "params": {
    "path": "Character",
    "component": "Animator",
    "property": "avatar",
    "value": "Assets/Models/Character.fbx"
  }
}
```

Note: The avatar is typically extracted from the model file (FBX). The path should point to the asset containing the avatar.

## Common Unity-chan Parameters

When using Unity-chan with the UnityChanLocomotions controller, these are typical parameters:

| Parameter | Type | Description |
|-----------|------|-------------|
| `Speed` | Float | Movement speed (used in blend trees) |
| `isMoving` | Bool | Whether character is moving |
| `Jump` | Trigger | Triggers jump animation |

Note: Actual parameters depend on the specific AnimatorController used.
