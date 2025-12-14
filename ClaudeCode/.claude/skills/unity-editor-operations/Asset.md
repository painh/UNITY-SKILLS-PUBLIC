# Asset Operations

Asset operations handle creating, deleting, importing, and managing Unity asset files in the project.

## Operations

### create_asset

Creates a text-based asset file (.txt, .json, .xml, .md).

**Parameters:**
- `asset_path` (required): Asset path (e.g., "Assets/Data/config.json")
- `asset_content` (optional): Content to write to the file (default: empty string)

**Example:**
```json
{
  "operation": "create_asset",
  "params": {
    "asset_path": "Assets/Data/config.json",
    "asset_content": "{\"version\": \"1.0\", \"enabled\": true}"
  }
}
```

**Response Format:**
Returns a success message with the created asset path.

**Note:**
- Automatically creates parent directories if they don't exist
- Supports file types: .txt, .json, .xml, .md
- Uses `System.IO.File.WriteAllText()` for content
- Calls `AssetDatabase.ImportAsset()`, `SaveAssets()`, and `Refresh()`

### delete_asset

Deletes an asset file from the project.

**Parameters:**
- `asset_path` (required): Asset path to delete

**Example:**
```json
{
  "operation": "delete_asset",
  "params": {
    "asset_path": "Assets/Data/old_config.json"
  }
}
```

**Response Format:**
Returns a success message with the deleted asset path.

**Note:**
- Validates asset exists before deletion
- Uses `AssetDatabase.DeleteAsset()`
- Calls `AssetDatabase.Refresh()` after deletion

### get_asset

Retrieves detailed information about an asset.

**Parameters:**
- `asset_path` (required): Asset path to query

**Example:**
```json
{
  "operation": "get_asset",
  "params": {
    "asset_path": "Assets/Textures/player_icon.png"
  }
}
```

**Response Format:**
Returns a formatted string with asset information:
```
Asset Info:
Name: player_icon
Type: Texture2D
Path: Assets/Textures/player_icon.png
GUID: a1b2c3d4e5f6g7h8i9j0
File Size: 24.5 KB
Last Modified: 2025-11-24 10:30:15
```

**Note:**
- Uses `AssetDatabase.LoadAssetAtPath()` to load the asset
- Returns asset name, type, path, GUID, file size, and last modified date
- Uses `AssetDatabase.AssetPathToGUID()` for GUID
- Uses `System.IO.FileInfo` for file metadata

### import_asset

Re-imports an asset, forcing Unity to update it.

**Parameters:**
- `asset_path` (required): Asset path to re-import

**Example:**
```json
{
  "operation": "import_asset",
  "params": {
    "asset_path": "Assets/Models/character.fbx"
  }
}
```

**Response Format:**
Returns a success message with the imported asset path.

**Note:**
- Useful when asset files are modified externally
- Uses `AssetDatabase.ImportAsset()` with `ImportAssetOptions.ForceUpdate`

### refresh_assets

Refreshes the AssetDatabase to detect external file changes.

**Parameters:** None

**Example:**
```json
{
  "operation": "refresh_assets",
  "params": {}
}
```

**Response Format:**
Returns a success message indicating AssetDatabase was refreshed.

**Note:**
- Scans the entire project for file changes
- Uses `AssetDatabase.Refresh()`
- Useful after batch file operations

### copy_asset

Copies an asset to a new location.

**Parameters:**
- `from_path` (required): Source asset path
- `to_path` (required): Destination asset path

**Example:**
```json
{
  "operation": "copy_asset",
  "params": {
    "from_path": "Assets/Materials/BlueMaterial.mat",
    "to_path": "Assets/Materials/Variants/BlueMaterial_V2.mat"
  }
}
```

**Response Format:**
Returns a success message with source and destination paths.

**Note:**
- Validates source asset exists
- Automatically creates destination directories if they don't exist
- Uses `AssetDatabase.CopyAsset()`
- Calls `AssetDatabase.Refresh()` after copy

### import_package

Imports a .unitypackage file into the project.

**Parameters:**
- `package_path` (required): Path to the .unitypackage file (absolute or relative to project root)

**Example:**
```json
{
  "operation": "import_package",
  "params": {
    "package_path": "Assets/UnityChan_v1.4.0.unitypackage"
  }
}
```

**Response Format:**
Returns a success message with the imported package name.

**Note:**
- Supports both absolute paths and paths relative to project root
- Uses `AssetDatabase.ImportPackage()` with `interactive: false` (no dialog)
- Validates file exists and has .unitypackage extension
- Calls `AssetDatabase.Refresh()` after import

### list_assets

Lists assets in a specified folder.

**Parameters:**
- `folder_path` (optional): Folder path to list (default: "Assets")
- `filter` (optional): Search filter (e.g., "t:Prefab", "t:Material", "player")
- `recursive` (optional): Include subfolders (default: false)

**Example:**
```json
{
  "operation": "list_assets",
  "params": {
    "folder_path": "Assets/UnityChan",
    "filter": "t:Prefab",
    "recursive": true
  }
}
```

**Response Format:**
Returns a formatted list of assets:
```
Assets in 'Assets/UnityChan':
Filter: t:Prefab
Recursive: true

[Folder] Assets/UnityChan/Prefabs/
[GameObject] Assets/UnityChan/Prefabs/unitychan.prefab
[GameObject] Assets/UnityChan/Prefabs/unitychan_dynamic.prefab

Total: 3 items
```

**Filter Examples:**
- `t:Prefab` - Prefab files only
- `t:Material` - Material files only
- `t:Script` - C# scripts only
- `t:AnimationClip` - Animation clips only
- `t:AnimatorController` - Animator controllers only
- `player` - Assets with "player" in name

**Note:**
- Uses `AssetDatabase.FindAssets()` for searching
- When `recursive: false`, shows subfolders but not their contents
- Returns asset type and full path for each item
