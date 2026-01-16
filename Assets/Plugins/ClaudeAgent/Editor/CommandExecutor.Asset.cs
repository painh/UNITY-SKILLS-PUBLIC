using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System;
using System.Text;
using System.Collections.Generic;

namespace ClaudeAgent
{
    public partial class CommandExecutor
    {
        /// <summary>
        /// Registers asset commands
        /// </summary>
        private void RegisterAssetCommands()
        {
            RegisterCommand("create_asset", CreateAsset);
            RegisterCommand("delete_asset", DeleteAsset);
            RegisterCommand("get_asset", GetAsset);
            RegisterCommand("import_asset", ImportAsset);
            RegisterCommand("refresh_assets", RefreshAssets);
            RegisterCommand("copy_asset", CopyAsset);
            RegisterCommand("import_package", ImportPackage);
            RegisterCommand("list_assets", ListAssets);
        }

        /// <summary>
        /// Creates a text asset
        /// </summary>
        private (bool, string) CreateAsset(CommandParams p)
        {
            try
            {
                if (p == null || string.IsNullOrEmpty(p.asset_path))
                {
                    string error = "Missing required parameter: asset_path";
                    ConsoleLogError($"[CommandExecutor] {error}");
                    return (false, error);
                }

                // Verify and create directory
                EnsureDirectoryExists(p.asset_path);

                // Asset content (default is empty string)
                string content = p.asset_content ?? "";

                // Handle based on file extension
                string extension = System.IO.Path.GetExtension(p.asset_path).ToLower();

                if (extension == ".txt" || extension == ".json" || extension == ".xml" || extension == ".md")
                {
                    // Write as text file
                    string fullPath = System.IO.Path.Combine(UnityEngine.Application.dataPath, "..", p.asset_path);
                    System.IO.File.WriteAllText(fullPath, content);
                    AssetDatabase.ImportAsset(p.asset_path);
                }
                else
                {
                    // Other asset types are not supported
                    string error = $"Unsupported asset type: {extension}. Only .txt, .json, .xml, .md are supported for create_asset.";
                    ConsoleLogWarning($"[CommandExecutor] {error}");
                    return (false, error);
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                string result = $"Created asset at: {p.asset_path}";
                ConsoleLog($"[CommandExecutor] {result}");
                return (true, result);
            }
            catch (Exception e)
            {
                string error = $"Error creating asset: {e.Message}";
                ConsoleLogError($"[CommandExecutor] {error}");
                return (false, error);
            }
        }

        /// <summary>
        /// Deletes an asset
        /// </summary>
        private (bool, string) DeleteAsset(CommandParams p)
        {
            try
            {
                if (p == null || string.IsNullOrEmpty(p.asset_path))
                {
                    string error = "Missing required parameter: asset_path";
                    ConsoleLogError($"[CommandExecutor] {error}");
                    return (false, error);
                }

                // Check if asset exists
                if (!AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(p.asset_path))
                {
                    string error = $"Asset not found: {p.asset_path}";
                    ConsoleLogWarning($"[CommandExecutor] {error}");
                    return (false, error);
                }

                // Delete asset
                if (!AssetDatabase.DeleteAsset(p.asset_path))
                {
                    string error = $"Failed to delete asset: {p.asset_path}";
                    ConsoleLogWarning($"[CommandExecutor] {error}");
                    return (false, error);
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                string result = $"Deleted asset: {p.asset_path}";
                ConsoleLog($"[CommandExecutor] {result}");
                return (true, result);
            }
            catch (Exception e)
            {
                string error = $"Error deleting asset: {e.Message}";
                ConsoleLogError($"[CommandExecutor] {error}");
                return (false, error);
            }
        }

        /// <summary>
        /// Gets asset information
        /// </summary>
        private (bool, string) GetAsset(CommandParams p)
        {
            try
            {
                if (p == null || string.IsNullOrEmpty(p.asset_path))
                {
                    string error = "Missing required parameter: asset_path";
                    ConsoleLogError($"[CommandExecutor] {error}");
                    return (false, error);
                }

                // Load asset
                var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(p.asset_path);
                if (asset == null)
                {
                    string error = $"Asset not found: {p.asset_path}";
                    ConsoleLogWarning($"[CommandExecutor] {error}");
                    return (false, error);
                }

                var sb = new StringBuilder();
                sb.AppendLine($"Asset Information:");
                sb.AppendLine();
                sb.AppendLine($"Name: {asset.name}");
                sb.AppendLine($"Type: {asset.GetType().Name}");
                sb.AppendLine($"Path: {p.asset_path}");
                sb.AppendLine($"GUID: {AssetDatabase.AssetPathToGUID(p.asset_path)}");

                // Get file size (if exists)
                string fullPath = System.IO.Path.Combine(UnityEngine.Application.dataPath, "..", p.asset_path);
                if (System.IO.File.Exists(fullPath))
                {
                    System.IO.FileInfo fileInfo = new System.IO.FileInfo(fullPath);
                    sb.AppendLine($"File Size: {fileInfo.Length} bytes");
                    sb.AppendLine($"Last Modified: {fileInfo.LastWriteTime}");
                }

                string result = sb.ToString();
                ConsoleLog($"[CommandExecutor] Retrieved asset info for: {p.asset_path}");
                return (true, result);
            }
            catch (Exception e)
            {
                string error = $"Error getting asset info: {e.Message}";
                ConsoleLogError($"[CommandExecutor] {error}");
                return (false, error);
            }
        }

        /// <summary>
        /// Imports an asset (reimport)
        /// </summary>
        private (bool, string) ImportAsset(CommandParams p)
        {
            try
            {
                if (p == null || string.IsNullOrEmpty(p.asset_path))
                {
                    string error = "Missing required parameter: asset_path";
                    ConsoleLogError($"[CommandExecutor] {error}");
                    return (false, error);
                }

                // Import asset (reimport)
                AssetDatabase.ImportAsset(p.asset_path, ImportAssetOptions.ForceUpdate);

                string result = $"Imported asset: {p.asset_path}";
                ConsoleLog($"[CommandExecutor] {result}");
                return (true, result);
            }
            catch (Exception e)
            {
                string error = $"Error importing asset: {e.Message}";
                ConsoleLogError($"[CommandExecutor] {error}");
                return (false, error);
            }
        }

        /// <summary>
        /// Refreshes the AssetDatabase
        /// </summary>
        private (bool, string) RefreshAssets(CommandParams p)
        {
            try
            {
                AssetDatabase.Refresh();

                string result = "AssetDatabase refreshed successfully";
                ConsoleLog($"[CommandExecutor] {result}");
                return (true, result);
            }
            catch (Exception e)
            {
                string error = $"Error refreshing assets: {e.Message}";
                ConsoleLogError($"[CommandExecutor] {error}");
                return (false, error);
            }
        }

        /// <summary>
        /// Copies an asset
        /// </summary>
        private (bool, string) CopyAsset(CommandParams p)
        {
            try
            {
                if (p == null || string.IsNullOrEmpty(p.from_path) || string.IsNullOrEmpty(p.to_path))
                {
                    string error = "Missing required parameters: from_path and to_path";
                    ConsoleLogError($"[CommandExecutor] {error}");
                    return (false, error);
                }

                // Check if source exists
                if (!AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(p.from_path))
                {
                    string error = $"Source asset not found: {p.from_path}";
                    ConsoleLogWarning($"[CommandExecutor] {error}");
                    return (false, error);
                }

                // Create destination directory
                EnsureDirectoryExists(p.to_path);

                // Copy asset
                if (!AssetDatabase.CopyAsset(p.from_path, p.to_path))
                {
                    string error = $"Failed to copy asset from {p.from_path} to {p.to_path}";
                    ConsoleLogWarning($"[CommandExecutor] {error}");
                    return (false, error);
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                string result = $"Copied asset from {p.from_path} to {p.to_path}";
                ConsoleLog($"[CommandExecutor] {result}");
                return (true, result);
            }
            catch (Exception e)
            {
                string error = $"Error copying asset: {e.Message}";
                ConsoleLogError($"[CommandExecutor] {error}");
                return (false, error);
            }
        }

        /// <summary>
        /// Imports a .unitypackage
        /// </summary>
        private (bool, string) ImportPackage(CommandParams p)
        {
            try
            {
                if (p == null || string.IsNullOrEmpty(p.package_path))
                {
                    string error = "Missing required parameter: package_path";
                    ConsoleLogError($"[CommandExecutor] {error}");
                    return (false, error);
                }

                // Check if package file exists
                string fullPath = p.package_path;

                // Convert relative path to absolute path from project root
                if (!System.IO.Path.IsPathRooted(fullPath))
                {
                    fullPath = System.IO.Path.Combine(UnityEngine.Application.dataPath, "..", p.package_path);
                }

                fullPath = System.IO.Path.GetFullPath(fullPath);

                if (!System.IO.File.Exists(fullPath))
                {
                    string error = $"Package file not found: {fullPath}";
                    ConsoleLogWarning($"[CommandExecutor] {error}");
                    return (false, error);
                }

                // Extension check
                if (!fullPath.EndsWith(".unitypackage", StringComparison.OrdinalIgnoreCase))
                {
                    string error = $"Invalid file type. Expected .unitypackage: {fullPath}";
                    ConsoleLogWarning($"[CommandExecutor] {error}");
                    return (false, error);
                }

                // Import package (interactive: false for automatic import)
                AssetDatabase.ImportPackage(fullPath, false);
                AssetDatabase.Refresh();

                string result = $"Imported package: {System.IO.Path.GetFileName(fullPath)}";
                ConsoleLog($"[CommandExecutor] {result}");
                return (true, result);
            }
            catch (Exception e)
            {
                string error = $"Error importing package: {e.Message}";
                ConsoleLogError($"[CommandExecutor] {error}");
                return (false, error);
            }
        }

        /// <summary>
        /// Gets list of assets in a folder
        /// </summary>
        private (bool, string) ListAssets(CommandParams p)
        {
            try
            {
                // Default is Assets folder
                string folderPath = string.IsNullOrEmpty(p?.folder_path) ? "Assets" : p.folder_path;

                // Check if folder exists
                string fullPath = System.IO.Path.Combine(UnityEngine.Application.dataPath, "..", folderPath);
                if (!System.IO.Directory.Exists(fullPath))
                {
                    string error = $"Folder not found: {folderPath}";
                    ConsoleLogWarning($"[CommandExecutor] {error}");
                    return (false, error);
                }

                // Filter setting (default is all)
                string filter = string.IsNullOrEmpty(p?.filter) ? "" : p.filter;
                bool recursive = p?.recursive ?? false;

                // Search assets
                string[] guids;
                if (string.IsNullOrEmpty(filter))
                {
                    guids = AssetDatabase.FindAssets("", new[] { folderPath });
                }
                else
                {
                    guids = AssetDatabase.FindAssets(filter, new[] { folderPath });
                }

                var sb = new StringBuilder();
                sb.AppendLine($"Assets in '{folderPath}':");
                sb.AppendLine($"Filter: {(string.IsNullOrEmpty(filter) ? "(none)" : filter)}");
                sb.AppendLine($"Recursive: {recursive}");
                sb.AppendLine();

                int count = 0;
                var listedPaths = new HashSet<string>();

                foreach (string guid in guids)
                {
                    string assetPath = AssetDatabase.GUIDToAssetPath(guid);

                    // Non-recursive: only assets directly in folder
                    if (!recursive)
                    {
                        string relativePath = assetPath.Substring(folderPath.Length).TrimStart('/');
                        if (relativePath.Contains("/"))
                        {
                            // Skip assets in subfolders, but show folder itself
                            string subFolder = folderPath + "/" + relativePath.Split('/')[0];
                            if (!listedPaths.Contains(subFolder))
                            {
                                listedPaths.Add(subFolder);
                                sb.AppendLine($"[Folder] {subFolder}/");
                                count++;
                            }
                            continue;
                        }
                    }

                    if (listedPaths.Contains(assetPath))
                        continue;

                    listedPaths.Add(assetPath);

                    // Get asset type
                    var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
                    string typeName = asset != null ? asset.GetType().Name : "Unknown";

                    // Check if it's a folder
                    if (AssetDatabase.IsValidFolder(assetPath))
                    {
                        sb.AppendLine($"[Folder] {assetPath}/");
                    }
                    else
                    {
                        sb.AppendLine($"[{typeName}] {assetPath}");
                    }
                    count++;
                }

                sb.AppendLine();
                sb.AppendLine($"Total: {count} items");

                string result = sb.ToString();
                ConsoleLog($"[CommandExecutor] Listed {count} assets in {folderPath}");
                return (true, result);
            }
            catch (Exception e)
            {
                string error = $"Error listing assets: {e.Message}";
                ConsoleLogError($"[CommandExecutor] {error}");
                return (false, error);
            }
        }
    }
}
