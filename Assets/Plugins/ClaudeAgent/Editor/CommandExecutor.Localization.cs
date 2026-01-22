using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using System.Text;
using System.Reflection;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace ClaudeAgent
{
    public partial class CommandExecutor
    {
        // Cached types from Localization package (loaded via reflection)
        private static Type _localizationSettingsType;
        private static Type _localeType;
        private static Type _stringTableCollectionType;
        private static Type _sharedTableDataType;
        private static Type _localizationEditorSettingsType;
        private static Type _localeGeneratorType;
        private static Type _stringTableType;
        private static bool _localizationTypesLoaded = false;
        private static bool _localizationAvailable = false;

        /// <summary>
        /// Registers localization commands
        /// </summary>
        private void RegisterLocalizationCommands()
        {
            RegisterCommand("create_localization_settings", CreateLocalizationSettings);
            RegisterCommand("create_string_table", CreateStringTable);
            RegisterCommand("add_string_table_entry", AddStringTableEntry);
            RegisterCommand("add_string_table_entries", AddStringTableEntries);
            RegisterCommand("import_string_table_csv", ImportStringTableCsv);
            RegisterCommand("get_localization_info", GetLocalizationInfo);
            RegisterCommand("list_string_tables", ListStringTables);
        }

        /// <summary>
        /// Loads Localization package types via reflection
        /// </summary>
        private bool EnsureLocalizationTypes()
        {
            if (_localizationTypesLoaded)
                return _localizationAvailable;

            _localizationTypesLoaded = true;

            try
            {
                // Try to load types from Localization package
                _localizationSettingsType = Type.GetType("UnityEngine.Localization.Settings.LocalizationSettings, Unity.Localization");
                _localeType = Type.GetType("UnityEngine.Localization.Locale, Unity.Localization");
                _stringTableCollectionType = Type.GetType("UnityEngine.Localization.Tables.StringTableCollection, Unity.Localization");
                _sharedTableDataType = Type.GetType("UnityEngine.Localization.Tables.SharedTableData, Unity.Localization");
                _stringTableType = Type.GetType("UnityEngine.Localization.Tables.StringTable, Unity.Localization");
                _localizationEditorSettingsType = Type.GetType("UnityEditor.Localization.LocalizationEditorSettings, Unity.Localization.Editor");
                _localeGeneratorType = Type.GetType("UnityEditor.Localization.LocaleGenerator, Unity.Localization.Editor");

                _localizationAvailable = _localizationSettingsType != null &&
                                         _localeType != null &&
                                         _stringTableCollectionType != null &&
                                         _localizationEditorSettingsType != null;

                if (_localizationAvailable)
                {
                    ConsoleLog("[CommandExecutor] Unity Localization package detected");
                }
                else
                {
                    ConsoleLogWarning("[CommandExecutor] Unity Localization package not found");
                }
            }
            catch (Exception e)
            {
                ConsoleLogWarning($"[CommandExecutor] Error loading Localization types: {e.Message}");
                _localizationAvailable = false;
            }

            return _localizationAvailable;
        }

        /// <summary>
        /// Creates LocalizationSettings asset with specified locales
        /// </summary>
        private (bool, string) CreateLocalizationSettings(CommandParams p)
        {
            try
            {
                if (!EnsureLocalizationTypes())
                {
                    return Error("Unity Localization package is not installed. Install com.unity.localization via Package Manager.");
                }

                if (p?.locales == null || p.locales.Length == 0)
                {
                    return Error("Missing required parameter: locales (array of locale codes like [\"ko\", \"en\", \"ja\"])");
                }

                string settingsPath = p.settings_path ?? "Assets/Settings/Localization/LocalizationSettings.asset";
                string defaultLocale = p.default_locale ?? p.locales[0];

                // Ensure directory exists
                EnsureDirectoryExists(settingsPath);

                // Check if settings already exist
                var existingSettings = AssetDatabase.LoadAssetAtPath(settingsPath, _localizationSettingsType);
                if (existingSettings != null)
                {
                    return Error($"LocalizationSettings already exists at: {settingsPath}");
                }

                // Create LocalizationSettings
                var settings = ScriptableObject.CreateInstance(_localizationSettingsType);
                AssetDatabase.CreateAsset(settings, settingsPath);

                // Get or create Locales
                var createdLocales = new List<object>();
                string localesDir = Path.GetDirectoryName(settingsPath) + "/Locales";
                EnsureDirectoryExists(localesDir + "/dummy.asset");

                foreach (string localeCode in p.locales)
                {
                    var locale = CreateOrGetLocale(localeCode, localesDir);
                    if (locale != null)
                    {
                        createdLocales.Add(locale);

                        // Add locale to settings
                        var addLocaleMethod = _localizationEditorSettingsType.GetMethod("AddLocale",
                            BindingFlags.Public | BindingFlags.Static, null, new[] { _localeType, typeof(bool) }, null);
                        if (addLocaleMethod != null)
                        {
                            addLocaleMethod.Invoke(null, new object[] { locale, false });
                        }
                    }
                }

                // Set the created settings as active
                var setSettingsMethod = _localizationEditorSettingsType.GetProperty("ActiveLocalizationSettings",
                    BindingFlags.Public | BindingFlags.Static);
                if (setSettingsMethod != null)
                {
                    setSettingsMethod.SetValue(null, settings);
                }

                // Set default locale
                foreach (var locale in createdLocales)
                {
                    var identifierProp = _localeType.GetProperty("Identifier");
                    var codeField = identifierProp.PropertyType.GetProperty("Code");
                    var identifier = identifierProp.GetValue(locale);
                    var code = codeField.GetValue(identifier) as string;

                    if (code == defaultLocale)
                    {
                        // Set as project locale (selected locale)
                        var selectedLocaleProp = _localizationSettingsType.GetProperty("SelectedLocale");
                        if (selectedLocaleProp != null)
                        {
                            selectedLocaleProp.SetValue(settings, locale);
                        }
                        break;
                    }
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                var sb = new StringBuilder();
                sb.AppendLine($"Created LocalizationSettings at: {settingsPath}");
                sb.AppendLine($"Locales: {string.Join(", ", p.locales)}");
                sb.AppendLine($"Default locale: {defaultLocale}");

                ConsoleLog($"[CommandExecutor] {sb}");
                return Success(sb.ToString());
            }
            catch (Exception e)
            {
                return Error($"Error creating LocalizationSettings: {e.Message}\n{e.StackTrace}");
            }
        }

        /// <summary>
        /// Creates or gets a Locale asset
        /// </summary>
        private object CreateOrGetLocale(string localeCode, string directory)
        {
            string localePath = $"{directory}/{localeCode}.asset";

            // Check if locale already exists
            var existing = AssetDatabase.LoadAssetAtPath(localePath, _localeType);
            if (existing != null)
            {
                return existing;
            }

            try
            {
                // Use LocaleGenerator to create locale
                var generateMethod = _localeGeneratorType?.GetMethod("CreateLocale",
                    BindingFlags.Public | BindingFlags.Static, null,
                    new[] { typeof(string) }, null);

                if (generateMethod != null)
                {
                    var locale = generateMethod.Invoke(null, new object[] { localeCode });
                    if (locale != null)
                    {
                        AssetDatabase.CreateAsset(locale as UnityEngine.Object, localePath);
                        return locale;
                    }
                }

                // Fallback: create locale manually using SystemLanguage
                var locale2 = ScriptableObject.CreateInstance(_localeType);

                // Set the identifier
                var identifierProp = _localeType.GetProperty("Identifier");
                if (identifierProp != null)
                {
                    var identifierType = identifierProp.PropertyType;
                    var identifier = Activator.CreateInstance(identifierType, new object[] { localeCode });
                    identifierProp.SetValue(locale2, identifier);
                }

                AssetDatabase.CreateAsset(locale2, localePath);
                return locale2;
            }
            catch (Exception e)
            {
                ConsoleLogWarning($"[CommandExecutor] Error creating locale {localeCode}: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Creates a StringTableCollection
        /// </summary>
        private (bool, string) CreateStringTable(CommandParams p)
        {
            try
            {
                if (!EnsureLocalizationTypes())
                {
                    return Error("Unity Localization package is not installed. Install com.unity.localization via Package Manager.");
                }

                if (string.IsNullOrEmpty(p?.table_name))
                {
                    return Error("Missing required parameter: table_name");
                }

                string tablePath = p.folder_path ?? "Assets/Localization/StringTables";
                string fullPath = $"{tablePath}/{p.table_name}.asset";

                EnsureDirectoryExists(fullPath);

                // Check if table already exists
                var existingTable = AssetDatabase.LoadAssetAtPath(fullPath, _stringTableCollectionType);
                if (existingTable != null)
                {
                    return Error($"StringTableCollection '{p.table_name}' already exists at: {fullPath}");
                }

                // Create StringTableCollection
                var collection = ScriptableObject.CreateInstance(_stringTableCollectionType);

                // Set table name via SharedTableData
                var sharedDataProp = _stringTableCollectionType.GetProperty("SharedData");
                if (sharedDataProp != null)
                {
                    var sharedData = ScriptableObject.CreateInstance(_sharedTableDataType);
                    var tableNameProp = _sharedTableDataType.GetProperty("TableCollectionName");
                    if (tableNameProp != null)
                    {
                        tableNameProp.SetValue(sharedData, p.table_name);
                    }

                    // Save SharedData as sub-asset
                    AssetDatabase.CreateAsset(collection, fullPath);
                    AssetDatabase.AddObjectToAsset(sharedData, collection);

                    sharedDataProp.SetValue(collection, sharedData);
                }
                else
                {
                    AssetDatabase.CreateAsset(collection, fullPath);
                }

                // Get available locales and create tables for each
                var getLocalesMethod = _localizationEditorSettingsType.GetMethod("GetLocales",
                    BindingFlags.Public | BindingFlags.Static);

                if (getLocalesMethod != null)
                {
                    var locales = getLocalesMethod.Invoke(null, null) as System.Collections.IList;
                    if (locales != null && locales.Count > 0)
                    {
                        var addTableMethod = _stringTableCollectionType.GetMethod("AddNewTable",
                            BindingFlags.Public | BindingFlags.Instance, null,
                            new[] { typeof(string) }, null);

                        foreach (var locale in locales)
                        {
                            var identifierProp = _localeType.GetProperty("Identifier");
                            var codeField = identifierProp.PropertyType.GetProperty("Code");
                            var identifier = identifierProp.GetValue(locale);
                            var code = codeField.GetValue(identifier) as string;

                            if (addTableMethod != null)
                            {
                                var table = addTableMethod.Invoke(collection, new object[] { code });
                                if (table != null)
                                {
                                    // Save table as separate asset
                                    string tableAssetPath = $"{tablePath}/{p.table_name}_{code}.asset";
                                    AssetDatabase.CreateAsset(table as UnityEngine.Object, tableAssetPath);
                                }
                            }
                        }
                    }
                }

                EditorUtility.SetDirty(collection);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                var result = $"Created StringTableCollection '{p.table_name}' at: {fullPath}";
                ConsoleLog($"[CommandExecutor] {result}");
                return Success(result);
            }
            catch (Exception e)
            {
                return Error($"Error creating StringTable: {e.Message}\n{e.StackTrace}");
            }
        }

        /// <summary>
        /// Adds an entry to a StringTable
        /// </summary>
        private (bool, string) AddStringTableEntry(CommandParams p)
        {
            try
            {
                if (!EnsureLocalizationTypes())
                {
                    return Error("Unity Localization package is not installed.");
                }

                if (string.IsNullOrEmpty(p?.table_name))
                {
                    return Error("Missing required parameter: table_name");
                }

                if (string.IsNullOrEmpty(p?.entry_key))
                {
                    return Error("Missing required parameter: entry_key");
                }

                if (p.values == null)
                {
                    return Error("Missing required parameter: values (object with locale codes as keys)");
                }

                // Find the StringTableCollection
                var collection = FindStringTableCollection(p.table_name);
                if (collection == null)
                {
                    return Error($"StringTableCollection not found: {p.table_name}");
                }

                // Parse values
                JObject valuesObj = null;
                if (p.values is JObject jo)
                {
                    valuesObj = jo;
                }
                else if (p.values is string str)
                {
                    valuesObj = JObject.Parse(str);
                }
                else
                {
                    return Error("Invalid values format. Expected JSON object like {\"ko\": \"한국어\", \"en\": \"English\"}");
                }

                // Add entry to each locale's table
                var getTableMethod = _stringTableCollectionType.GetMethod("GetTable",
                    BindingFlags.Public | BindingFlags.Instance, null,
                    new[] { typeof(string) }, null);

                int addedCount = 0;
                foreach (var prop in valuesObj.Properties())
                {
                    string localeCode = prop.Name;
                    string value = prop.Value.ToString();

                    if (getTableMethod != null)
                    {
                        var table = getTableMethod.Invoke(collection, new object[] { localeCode });
                        if (table != null)
                        {
                            // Add entry to table
                            var addEntryMethod = _stringTableType?.GetMethod("AddEntry",
                                BindingFlags.Public | BindingFlags.Instance, null,
                                new[] { typeof(string), typeof(string) }, null);

                            if (addEntryMethod != null)
                            {
                                addEntryMethod.Invoke(table, new object[] { p.entry_key, value });
                                EditorUtility.SetDirty(table as UnityEngine.Object);
                                addedCount++;
                            }
                        }
                    }
                }

                EditorUtility.SetDirty(collection);
                AssetDatabase.SaveAssets();

                var result = $"Added entry '{p.entry_key}' to {addedCount} locale(s) in table '{p.table_name}'";
                ConsoleLog($"[CommandExecutor] {result}");
                return Success(result);
            }
            catch (Exception e)
            {
                return Error($"Error adding string table entry: {e.Message}");
            }
        }

        /// <summary>
        /// Adds multiple entries to a StringTable at once
        /// </summary>
        private (bool, string) AddStringTableEntries(CommandParams p)
        {
            try
            {
                if (!EnsureLocalizationTypes())
                {
                    return Error("Unity Localization package is not installed.");
                }

                if (string.IsNullOrEmpty(p?.table_name))
                {
                    return Error("Missing required parameter: table_name");
                }

                // Parse entries from JSON - expects array of {key, values} objects
                // or children parameter with array of entry objects
                JArray entries = null;

                if (p.children != null)
                {
                    entries = new JArray();
                    foreach (var child in p.children)
                    {
                        if (child is JObject jo)
                        {
                            entries.Add(jo);
                        }
                    }
                }

                if (entries == null || entries.Count == 0)
                {
                    return Error("Missing required parameter: children (array of {key, values} objects)");
                }

                var collection = FindStringTableCollection(p.table_name);
                if (collection == null)
                {
                    return Error($"StringTableCollection not found: {p.table_name}");
                }

                var getTableMethod = _stringTableCollectionType.GetMethod("GetTable",
                    BindingFlags.Public | BindingFlags.Instance, null,
                    new[] { typeof(string) }, null);

                int totalAdded = 0;
                var errors = new List<string>();

                foreach (JObject entry in entries)
                {
                    string key = entry["key"]?.ToString();
                    var values = entry["values"] as JObject;

                    if (string.IsNullOrEmpty(key) || values == null)
                    {
                        errors.Add($"Invalid entry format: {entry}");
                        continue;
                    }

                    foreach (var prop in values.Properties())
                    {
                        string localeCode = prop.Name;
                        string value = prop.Value.ToString();

                        if (getTableMethod != null)
                        {
                            var table = getTableMethod.Invoke(collection, new object[] { localeCode });
                            if (table != null)
                            {
                                var addEntryMethod = _stringTableType?.GetMethod("AddEntry",
                                    BindingFlags.Public | BindingFlags.Instance, null,
                                    new[] { typeof(string), typeof(string) }, null);

                                if (addEntryMethod != null)
                                {
                                    addEntryMethod.Invoke(table, new object[] { key, value });
                                    EditorUtility.SetDirty(table as UnityEngine.Object);
                                    totalAdded++;
                                }
                            }
                        }
                    }
                }

                EditorUtility.SetDirty(collection);
                AssetDatabase.SaveAssets();

                var sb = new StringBuilder();
                sb.AppendLine($"Added {totalAdded} entries to table '{p.table_name}'");
                if (errors.Count > 0)
                {
                    sb.AppendLine($"Errors ({errors.Count}):");
                    foreach (var err in errors)
                    {
                        sb.AppendLine($"  - {err}");
                    }
                }

                return Success(sb.ToString());
            }
            catch (Exception e)
            {
                return Error($"Error adding string table entries: {e.Message}");
            }
        }

        /// <summary>
        /// Imports entries from a CSV file
        /// </summary>
        private (bool, string) ImportStringTableCsv(CommandParams p)
        {
            try
            {
                if (!EnsureLocalizationTypes())
                {
                    return Error("Unity Localization package is not installed.");
                }

                if (string.IsNullOrEmpty(p?.table_name))
                {
                    return Error("Missing required parameter: table_name");
                }

                if (string.IsNullOrEmpty(p?.csv_path))
                {
                    return Error("Missing required parameter: csv_path");
                }

                // Get full path
                string fullPath = p.csv_path;
                if (!Path.IsPathRooted(fullPath))
                {
                    fullPath = Path.Combine(Application.dataPath, "..", p.csv_path);
                }

                if (!File.Exists(fullPath))
                {
                    return Error($"CSV file not found: {p.csv_path}");
                }

                var collection = FindStringTableCollection(p.table_name);
                if (collection == null)
                {
                    return Error($"StringTableCollection not found: {p.table_name}");
                }

                // Read CSV
                string[] lines = File.ReadAllLines(fullPath);
                if (lines.Length < 2)
                {
                    return Error("CSV file must have at least a header row and one data row");
                }

                // Parse header - first column is key, rest are locale codes
                string[] headers = ParseCsvLine(lines[0]);
                if (headers.Length < 2)
                {
                    return Error("CSV must have at least 2 columns (key, locale)");
                }

                var getTableMethod = _stringTableCollectionType.GetMethod("GetTable",
                    BindingFlags.Public | BindingFlags.Instance, null,
                    new[] { typeof(string) }, null);

                int importedCount = 0;

                // Process data rows
                for (int i = 1; i < lines.Length; i++)
                {
                    string[] values = ParseCsvLine(lines[i]);
                    if (values.Length < 2 || string.IsNullOrEmpty(values[0]))
                        continue;

                    string key = values[0];

                    for (int j = 1; j < headers.Length && j < values.Length; j++)
                    {
                        string localeCode = headers[j];
                        string value = values[j];

                        if (getTableMethod != null)
                        {
                            var table = getTableMethod.Invoke(collection, new object[] { localeCode });
                            if (table != null)
                            {
                                var addEntryMethod = _stringTableType?.GetMethod("AddEntry",
                                    BindingFlags.Public | BindingFlags.Instance, null,
                                    new[] { typeof(string), typeof(string) }, null);

                                if (addEntryMethod != null)
                                {
                                    addEntryMethod.Invoke(table, new object[] { key, value });
                                    EditorUtility.SetDirty(table as UnityEngine.Object);
                                    importedCount++;
                                }
                            }
                        }
                    }
                }

                EditorUtility.SetDirty(collection);
                AssetDatabase.SaveAssets();

                var result = $"Imported {importedCount} entries from CSV to table '{p.table_name}'";
                ConsoleLog($"[CommandExecutor] {result}");
                return Success(result);
            }
            catch (Exception e)
            {
                return Error($"Error importing CSV: {e.Message}");
            }
        }

        /// <summary>
        /// Simple CSV line parser (handles quoted fields)
        /// </summary>
        private string[] ParseCsvLine(string line)
        {
            var result = new List<string>();
            bool inQuotes = false;
            var current = new StringBuilder();

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == ',' && !inQuotes)
                {
                    result.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }

            result.Add(current.ToString());
            return result.ToArray();
        }

        /// <summary>
        /// Gets localization system info
        /// </summary>
        private (bool, string) GetLocalizationInfo(CommandParams p)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("Localization Info:");
                sb.AppendLine();

                if (!EnsureLocalizationTypes())
                {
                    sb.AppendLine("  Status: Unity Localization package NOT installed");
                    sb.AppendLine("  Install: com.unity.localization via Package Manager");
                    return Success(sb.ToString());
                }

                sb.AppendLine("  Status: Unity Localization package installed");

                // Get active settings
                var settingsProp = _localizationEditorSettingsType.GetProperty("ActiveLocalizationSettings",
                    BindingFlags.Public | BindingFlags.Static);

                if (settingsProp != null)
                {
                    var settings = settingsProp.GetValue(null);
                    if (settings != null)
                    {
                        string settingsPath = AssetDatabase.GetAssetPath(settings as UnityEngine.Object);
                        sb.AppendLine($"  Settings: {settingsPath}");
                    }
                    else
                    {
                        sb.AppendLine("  Settings: Not configured");
                    }
                }

                // List available locales
                var getLocalesMethod = _localizationEditorSettingsType.GetMethod("GetLocales",
                    BindingFlags.Public | BindingFlags.Static);

                if (getLocalesMethod != null)
                {
                    var locales = getLocalesMethod.Invoke(null, null) as System.Collections.IList;
                    if (locales != null && locales.Count > 0)
                    {
                        sb.AppendLine($"  Locales ({locales.Count}):");
                        foreach (var locale in locales)
                        {
                            var identifierProp = _localeType.GetProperty("Identifier");
                            var identifier = identifierProp.GetValue(locale);
                            var codeProp = identifier.GetType().GetProperty("Code");
                            var code = codeProp.GetValue(identifier) as string;
                            var nameProp = locale.GetType().GetProperty("LocaleName");
                            var name = nameProp?.GetValue(locale) as string ?? code;
                            sb.AppendLine($"    - {code}: {name}");
                        }
                    }
                    else
                    {
                        sb.AppendLine("  Locales: None configured");
                    }
                }

                return Success(sb.ToString());
            }
            catch (Exception e)
            {
                return Error($"Error getting localization info: {e.Message}");
            }
        }

        /// <summary>
        /// Lists all StringTableCollections
        /// </summary>
        private (bool, string) ListStringTables(CommandParams p)
        {
            try
            {
                if (!EnsureLocalizationTypes())
                {
                    return Error("Unity Localization package is not installed.");
                }

                var guids = AssetDatabase.FindAssets($"t:{_stringTableCollectionType.Name}");
                var sb = new StringBuilder();
                sb.AppendLine($"StringTableCollections ({guids.Length}):");
                sb.AppendLine();

                foreach (var guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var collection = AssetDatabase.LoadAssetAtPath(path, _stringTableCollectionType);

                    if (collection != null)
                    {
                        // Get table name from SharedData
                        var sharedDataProp = _stringTableCollectionType.GetProperty("SharedData");
                        string tableName = "Unknown";
                        int entryCount = 0;

                        if (sharedDataProp != null)
                        {
                            var sharedData = sharedDataProp.GetValue(collection);
                            if (sharedData != null)
                            {
                                var tableNameProp = _sharedTableDataType.GetProperty("TableCollectionName");
                                tableName = tableNameProp?.GetValue(sharedData) as string ?? "Unknown";

                                var entriesProp = _sharedTableDataType.GetProperty("Entries");
                                var entries = entriesProp?.GetValue(sharedData) as System.Collections.IList;
                                entryCount = entries?.Count ?? 0;
                            }
                        }

                        sb.AppendLine($"  - {tableName}");
                        sb.AppendLine($"    Path: {path}");
                        sb.AppendLine($"    Entries: {entryCount}");
                    }
                }

                return Success(sb.ToString());
            }
            catch (Exception e)
            {
                return Error($"Error listing string tables: {e.Message}");
            }
        }

        /// <summary>
        /// Finds a StringTableCollection by name
        /// </summary>
        private UnityEngine.Object FindStringTableCollection(string tableName)
        {
            var guids = AssetDatabase.FindAssets($"t:{_stringTableCollectionType.Name}");

            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var collection = AssetDatabase.LoadAssetAtPath(path, _stringTableCollectionType);

                if (collection != null)
                {
                    var sharedDataProp = _stringTableCollectionType.GetProperty("SharedData");
                    if (sharedDataProp != null)
                    {
                        var sharedData = sharedDataProp.GetValue(collection);
                        if (sharedData != null)
                        {
                            var tableNameProp = _sharedTableDataType.GetProperty("TableCollectionName");
                            var name = tableNameProp?.GetValue(sharedData) as string;
                            if (name == tableName)
                            {
                                return collection;
                            }
                        }
                    }

                    // Also check by asset name
                    if (Path.GetFileNameWithoutExtension(path) == tableName)
                    {
                        return collection;
                    }
                }
            }

            return null;
        }
    }
}
