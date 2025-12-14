# Screenshot Operations

Screenshot operations for capturing Unity scene views programmatically.

## Operations

### capture_scene_view

Captures a screenshot of the Unity scene from a specified camera position.

**Parameters:**
- `target` (optional): Path to the GameObject to focus on
- `position` (optional): [x, y, z] camera position (world coordinates)
- `distance` (optional): Distance from target (used with target, mutually exclusive with position)
- `angle` (optional): [pitch, yaw] viewing angles in degrees (default: [30, 45])
- `width` (optional): Image width in pixels (default: 800)
- `height` (optional): Image height in pixels (default: 600)
- `output_path` (optional): Output file path (default: "Temp/scene_capture.png")

**Usage Patterns:**

Pattern 1: Focus on specific object
```json
{
  "operation": "capture_scene_view",
  "params": {
    "target": "House",
    "distance": 8,
    "angle": [30, 45]
  }
}
```
Captures House from 8m distance, 30 degrees above, 45 degrees from the right.

Pattern 2: Capture from absolute position
```json
{
  "operation": "capture_scene_view",
  "params": {
    "position": [0, 10, -15],
    "target": "PlaygroundSlide"
  }
}
```
Captures from specified position looking at PlaygroundSlide.

Pattern 3: Capture entire scene (default)
```json
{
  "operation": "capture_scene_view",
  "params": {}
}
```
Automatically calculates camera position to capture all visible objects in the scene.

**Response:**
```
Screenshot saved: Temp/scene_capture.png
Resolution: 800x600
Camera: (10.00, 5.00, -10.00) looking at House
```

**Notes:**
- Default output path is `Temp/scene_capture.png` (overwrites existing file)
- Uses existing scene lighting
- Anti-aliasing is enabled (4x MSAA)
- For angle parameter:
  - pitch: Vertical angle (0 = horizontal, 90 = directly above)
  - yaw: Horizontal angle (0 = front/north, 90 = right/east)
- Camera is created temporarily and destroyed after capture
- Bounding box is calculated automatically when focusing on objects

---

## Agent Workflow Integration

After completing Visual Guide Modeling (VGM) Phase 8 (Cleanup), use this command to verify the result:

```
1. Execute capture_scene_view to capture the completed model
2. Claude can read the image to verify visual correctness
3. Report any issues or suggest fixes based on visual inspection
```

Example verification workflow:
```json
{
  "operation": "capture_scene_view",
  "params": {
    "target": "PlaygroundSlide/Parts",
    "distance": 10,
    "angle": [25, 30]
  }
}
```
