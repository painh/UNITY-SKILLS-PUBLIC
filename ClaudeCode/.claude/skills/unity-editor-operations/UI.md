# UI Operations

UI operations for creating and manipulating Unity UI (uGUI) elements.

## Operations

### create_canvas
Creates a new Canvas with CanvasScaler, GraphicRaycaster, and EventSystem.

**Parameters:**
- `name` (optional): Canvas name (default: "Canvas")

**JSON Example:**
```json
{
  "operation": "create_canvas",
  "params": {
    "name": "MainCanvas"
  }
}
```

**Response:**
```
Created Canvas: MainCanvas
```

**Notes:**
- Canvas uses ScreenSpaceOverlay render mode by default
- CanvasScaler is configured with ScaleWithScreenSize mode (1920x1080 reference resolution)
- GraphicRaycaster is automatically added for UI event handling
- EventSystem is automatically created if not already present in the scene
- Changes are recorded in Undo history
- **New Input System**: If using New Input System, create EventSystem first with `InputSystemUIInputModule` instead of `StandaloneInputModule`

---

### create_ui
Creates a UI element (Button, Text, Image, Panel, InputField, ScrollView) as a child of Canvas or specified parent.

**Parameters:**
- `type` (required): UI element type - "button", "text", "tmpro", "image", "panel", "inputfield", "scrollview"
- `name` (optional): Element name
- `parent` (optional): Parent GameObject path/name (defaults to first Canvas found)
- `position` (optional): [x, y] anchored position in RectTransform
- `size` (optional): [width, height] size in pixels
- `text` (optional): Text content for Text elements (also applies to Button's child Text)
- `font_size` (optional): Font size for Text elements (also applies to Button's child Text)
- `color` (optional): Color name or #RRGGBB format (applies to Image background or Text color)
- `anchor` (optional): Anchor preset - "top-left", "top", "top-right", "left", "center", "right", "bottom-left", "bottom", "bottom-right", "stretch-top", "stretch-middle", "stretch-bottom", "stretch-left", "stretch-center", "stretch-right", "stretch"
- `placeholder` (optional): Placeholder text for InputField
- `scroll_direction` (optional): Scroll direction for ScrollView - "vertical" (default), "horizontal", "both"

**JSON Example (Button):**
```json
{
  "operation": "create_ui",
  "params": {
    "type": "button",
    "name": "StartButton",
    "position": [0, -100]
  }
}
```

**JSON Example (InputField):**
```json
{
  "operation": "create_ui",
  "params": {
    "type": "inputfield",
    "name": "ChatInput",
    "placeholder": "Enter message...",
    "size": [300, 40],
    "position": [0, -200]
  }
}
```

**JSON Example (ScrollView):**
```json
{
  "operation": "create_ui",
  "params": {
    "type": "scrollview",
    "name": "MessageList",
    "scroll_direction": "vertical",
    "size": [400, 300]
  }
}
```

**JSON Example (Text with text, color, font_size):**
```json
{
  "operation": "create_ui",
  "params": {
    "type": "text",
    "name": "TitleText",
    "text": "Game Title",
    "color": "blue",
    "font_size": 36,
    "anchor": "top-center",
    "position": [0, -50]
  }
}
```

**JSON Example (TextMeshPro):**
```json
{
  "operation": "create_ui",
  "params": {
    "type": "tmpro",
    "name": "ScoreText",
    "text": "Score: 0",
    "color": "white",
    "font_size": 24,
    "position": [10, -10],
    "anchor": "top-left"
  }
}
```

**JSON Example (Button with text and color):**
```json
{
  "operation": "create_ui",
  "params": {
    "type": "button",
    "name": "PlayButton",
    "text": "Play",
    "color": "#4CAF50",
    "size": [200, 50]
  }
}
```

**Response:**
```
Created UI element: StartButton (button)
```

**Notes:**
- **Button**: Creates button with Image component and Text child (default text: "Button", size: 160x30)
- **Text**: Creates text element using legacy Unity UI Text (default text: "New Text", size: 200x40, color: black)
- **TextMeshPro (tmpro)**: Creates text element using TextMeshProUGUI (default text: "New Text", size: 200x40, color: white, fontSize: 24). Recommended for better text rendering quality.
- **Image**: Creates image element (default color: white, size: 100x100)
- **Panel**: Creates panel with anchors stretched to fill parent (semi-transparent white background)
- **InputField**: Creates input field with Text and Placeholder children (default size: 200x30)
- **ScrollView**: Creates scroll view with Viewport, Content, and Mask (default size: 200x200)
- If no parent is specified, searches for Canvas in scene
- Returns error if Canvas not found and no parent specified
- Position uses RectTransform.anchoredPosition (2D coordinate system)

---

### ui

Unified command for getting and setting UI element properties.

**Parameters:**
- `path` (required): GameObject path/name containing UI component
- `get` (optional): If true, returns UI element info
- `text` (optional): Text content to set
- `color` (optional): Color name or #RRGGBB format
- `r`, `g`, `b` (optional): RGB values (0-1) as alternative to color
- `a` (optional): Alpha value (0-1, default: 1.0 for full opacity)
- `font_size` (optional): Font size for Text component
- `size` (optional): [width, height] for RectTransform size

**Get Mode Example:**
```json
{
  "operation": "ui",
  "params": {
    "path": "Canvas/ScoreText",
    "get": true
  }
}
```

**Get Response Format:**
Returns UI element information including RectTransform properties, component types, and current values.

**Set Mode Examples:**

Set text:
```json
{
  "operation": "ui",
  "params": {
    "path": "Canvas/ScoreText",
    "text": "Score: 1000"
  }
}
```

Set color (using color name):
```json
{
  "operation": "ui",
  "params": {
    "path": "Canvas/Panel",
    "color": "blue",
    "a": 0.5
  }
}
```

Set color (using RGB):
```json
{
  "operation": "ui",
  "params": {
    "path": "Canvas/StartButton",
    "r": 1.0,
    "g": 0.8,
    "b": 0.0,
    "a": 1.0
  }
}
```

Set both text and color:
```json
{
  "operation": "ui",
  "params": {
    "path": "Canvas/Label",
    "text": "Hello World",
    "color": "green"
  }
}
```

Set size:
```json
{
  "operation": "ui",
  "params": {
    "path": "Canvas/ScoreText",
    "size": [300, 50]
  }
}
```

**Notes:**
- Cannot specify both `get: true` and property values
- Works with any component derived from Graphic (Image, Text, RawImage, etc.)
- `size` modifies the RectTransform.sizeDelta property
- Either `color` or `r,g,b` parameters must be provided for color changes
- RGB values range from 0 to 1
- Alpha (a) controls transparency: 0 = fully transparent, 1 = fully opaque
- Changes are recorded in Undo history
- To change Button text color, target the Text child: "Canvas/Button/Text"
- Works with Unity's legacy Text component and TextMeshProUGUI. For TextMeshPro, use `create_ui` with `type: "tmpro"`

---

## Advanced Features

以下の高度なUI機能は `set_component_property` 操作で対応できます。専用操作を追加する前に、まずこの方法で実現可能か確認してください。

### World Space Canvas (VR対応)

Canvas作成後に `set_component_property` でWorld Spaceに変更：

```json
// 1. Canvas作成
{"operation": "create_canvas", "params": {"name": "VRCanvas"}}

// 2. Render ModeをWorld Spaceに変更
{"operation": "set_component_property", "params": {
  "path": "VRCanvas",
  "component_type": "Canvas",
  "property_name": "renderMode",
  "property_value": "WorldSpace"
}}

// 3. 位置・スケール調整 (unified transform command)
{"operation": "transform", "params": {"path": "VRCanvas", "scale": [0.01, 0.01, 0.01], "position": [0, 1.5, 2], "space": "world"}}
```

### LayoutGroup (自動配置)

`add_component` + `set_component_property` でLayoutGroupを追加：

```json
// VerticalLayoutGroup追加
{"operation": "add_component", "params": {"path": "Canvas/Panel", "component_type": "VerticalLayoutGroup"}}

// spacing設定
{"operation": "set_component_property", "params": {
  "path": "Canvas/Panel",
  "component_type": "VerticalLayoutGroup",
  "property_name": "spacing",
  "property_value": "10"
}}
```

### Text プロパティ

フォントサイズ、アライメントなどは `set_component_property` で設定：

```json
{"operation": "set_component_property", "params": {
  "path": "Canvas/Text",
  "component_type": "Text",
  "property_name": "fontSize",
  "property_value": "24"
}}
```

> **Note**: これらの機能が頻繁に必要になる場合は、専用操作の追加を検討してください。
