# Visual Guide Modeling Operations

頂点座標からジオメトリを自動配置するコマンド。回転計算を自動化し、手計算によるミスを防止する。

## Overview

Visual Guide Modelingワークフローでは:
1. マーカー（小さな球）を配置して頂点位置を視覚化
2. 参照線（LineRenderer）で構造を確認
3. `create_fitted` コマンドでマーカー座標からジオメトリを自動生成

**使い分け:**
- 回転計算が必要な形状 → `create_fitted`
- 単純な位置配置（球など） → `create_primitive`

---

## create_fitted

頂点座標からジオメトリの位置・回転・スケールを自動計算して配置する。

### Supported Shapes

| 頂点数 | shape | 用途 | 追加パラメータ |
|--------|-------|------|---------------|
| 2点 | cylinder | 脚、ポール | radius |
| 2点 | capsule | 丸い脚 | radius |
| 2点 | cube | 角柱（正方形断面） | cross_size |
| 3点 | prism | 妻壁（三角形） | thickness |
| 4点 | cube | 板、パネル、壁（任意の四角形対応） | thickness |

### Shape Selection Guide

| 形状タイプ | 推奨 | 例 |
|-----------|------|-----|
| 柱・梁・パイプ | 2点 cube/cylinder | ベンチの脚、手すり、フレーム |
| 板・パネル・壁 | 4点 cube | 座面、背もたれ、屋根板 |

**理由:** 2点cubeは正方形断面のみ。板のような長方形断面には4点cubeを使用すること。

### Common Parameters

| Name | Type | Required | Description |
|------|------|----------|-------------|
| shape | string | Yes | Shape type: cylinder, capsule, cube, prism |
| vertices | array | Yes | Vertex positions `[[x,y,z], ...]` |
| name | string | No | GameObject name |
| parent | string | No | Parent object path |
| position_space | string | No | "local" \| "world" (default: "world" if no parent, "local" if parent specified) |
| color | string | No | Color name or hex code (#RRGGBB) |

> **Space Parameter Defaults:** Result message shows which space was used, e.g. `Created fitted cylinder: Leg (vertices: local)`

---

## 2-Point Shapes

### Vertex Definition (2点)

```
v0 (start) ●─────────────● v1 (end)
```

### cylinder (2点)

円柱を2点間に配置。脚やポールに使用。

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| radius | float | 0.05 | Cylinder radius |

**Example:**
```json
{
  "operation": "create_fitted",
  "params": {
    "shape": "cylinder",
    "vertices": [
      [-1.25, 0, 0.5],
      [-1.25, 2.2, 0.5]
    ],
    "radius": 0.04,
    "name": "Leg_FL",
    "parent": "Swing/Parts",
    "color": "#E87D8F"
  }
}
```

---

### capsule (2点)

カプセルを2点間に配置。丸い端の脚に使用。

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| radius | float | 0.05 | Capsule radius |

**Example:**
```json
{
  "operation": "create_fitted",
  "params": {
    "shape": "capsule",
    "vertices": [
      [0, 0, 0],
      [0, 1.5, 0]
    ],
    "radius": 0.03,
    "name": "RoundPost",
    "color": "brown"
  }
}
```

---

### cube (2点) - 角柱（正方形断面のみ）

正方形断面の角柱を2点間に配置。脚、梁、フレームに使用。

**注意:** 板やパネルなど長方形断面が必要な場合は、4点cubeを使用すること。

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| cross_size | float | 0.1 | Cross-section size (square) |

**Example:**
```json
{
  "operation": "create_fitted",
  "params": {
    "shape": "cube",
    "vertices": [
      [-1.25, 2.2, 0.5],
      [1.25, 2.2, 0.5]
    ],
    "cross_size": 0.08,
    "name": "TopBeam",
    "parent": "Swing/Parts",
    "color": "#E87D8F"
  }
}
```

---

## 3-Point Shapes

### Vertex Definition (3点 Prism)

```
      v2 (apex)
      /\
     /  \
    /    \
v0 ────── v1 (base)

反時計回り = 表面が手前
```

### prism (3点)

三角形プリズムを3点から配置。妻壁（gable）に使用。

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| thickness | float | 0.05 | Prism depth |

**Example:**
```json
{
  "operation": "create_fitted",
  "params": {
    "shape": "prism",
    "vertices": [
      [2.6, 0.55, 2.45],
      [3.4, 0.55, 2.45],
      [3.0, 0.85, 2.45]
    ],
    "thickness": 0.025,
    "name": "FrontGable",
    "parent": "Doghouse/Parts/Gables",
    "color": "#E8C43B"
  }
}
```

---

## 4-Point Shapes

### Vertex Definition (4点 Quad)

```
v0 ─────────── v1
 │             │
 │   (face)    │
 │             │
v3 ─────────── v2

反時計回りで法線が手前向き
```

### cube (4点) - Quad Panel

四角形パネルを4点から配置。屋根や壁パネルに使用。

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| thickness | float | 0.02 | Panel thickness |

**Example:**
```json
{
  "operation": "create_fitted",
  "params": {
    "shape": "cube",
    "vertices": [
      [-0.4, 0.6, -0.5],
      [0, 0.9, -0.5],
      [0, 0.9, 0.55],
      [-0.4, 0.6, 0.55]
    ],
    "thickness": 0.03,
    "name": "Roof_L",
    "parent": "Doghouse/Parts",
    "color": "#B22222"
  }
}
```

---

## Error Handling

| Error | Message |
|-------|---------|
| Missing shape | "Missing required parameter: shape" |
| Missing vertices | "Missing required parameter: vertices" |
| Insufficient vertices | "Need N vertices for {shape}" |
| Degenerate geometry | "Vertices are too close or collinear" |
| Invalid shape | "Shape '{shape}' is not supported. Supported: cylinder, capsule, prism, cube" |

---

## How It Works

### 2-Point (cylinder, capsule, cube beam)

1. Direction vector: `v1 - v0`
2. Center position: `(v0 + v1) / 2`
3. Rotation: `Quaternion.FromToRotation(Vector3.up, direction)`
4. Scale: Based on length and radius/cross-section parameters

### 3-Point (prism)

1. Base vector: `v1 - v0` (width direction)
2. Height vector: `v2 - midpoint(v0, v1)`
3. Normal: `Cross(base, height)`
4. Rotation: `Quaternion.LookRotation(normal, height)`
5. Position: Bounding box center

### 4-Point (cube quad) - 任意の四角形対応

`CreateShapeFromPolygon`を使用して、長方形・平行四辺形・台形など任意の四角形に対応：

1. Normal: `Cross(edge01, edge03)`
2. Center: Average of 4 vertices
3. Project vertices to 2D plane (using inverse of LookRotation)
4. Create mesh from polygon vertices (no rotation calculation needed)
5. Position and orient the mesh

---

## Cleanup: Disable References

After creating parts, disable the `_References` folder instead of deleting it:

```json
{"operation": "set_active", "params": {"path": "ObjectName/_References", "active": false}}
```

**Why disable instead of delete:**
- Markers can be re-enabled to visualize vertex positions
- Easier to adjust positions and recreate parts later
- Lines show vertex connection order (important for 4-point quads)
- Without references, all coordinates must be recalculated manually

---

## Notes

- ProBuilder package is required for prism shapes
- For basic primitives without rotation (spheres, simple cubes), use `create_primitive`
- Vertex order matters for face orientation (counter-clockwise = front-facing)
- Colors support both named colors and hex codes (#RRGGBB)