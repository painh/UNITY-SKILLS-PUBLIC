using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using System;

namespace ClaudeAgent
{
    public partial class CommandExecutor
    {
        /// <summary>
        /// Registers UI commands
        /// </summary>
        private void RegisterUICommands()
        {
            RegisterCommand("create_canvas", CreateCanvas);
            RegisterCommand("create_ui", CreateUIElement);  // Renamed: create_ui_element -> create_ui
            RegisterCommand("ui", UICommand);
        }

        /// <summary>
        /// Unified UI command (get/set integration)
        /// get: true for retrieval, text/color/font_size for setting
        /// </summary>
        private (bool, string) UICommand(CommandParams p)
        {
            try
            {
                if (p == null || string.IsNullOrEmpty(p.path))
                {
                    string error = "Missing required parameter: path";
                    ConsoleLogError($"[CommandExecutor] {error}");
                    return (false, error);
                }

                var (obj, findError) = FindGameObjectByPath(p.path);
                if (obj == null)
                {
                    string error = findError ?? $"GameObject not found: {p.path}";
                    ConsoleLogWarning($"[CommandExecutor] {error}");
                    return (false, error);
                }

                // Check for presence of set parameters
                bool hasText = !string.IsNullOrEmpty(p.text);
                bool hasColor = !string.IsNullOrEmpty(p.color);
                bool hasFontSize = p.font_size > 0;
                bool hasSize = p.size != null && p.size.Length >= 2;
                bool hasAnySetParam = hasText || hasColor || hasFontSize || hasSize;

                // If get mode
                if (p.get)
                {
                    // Check for simultaneous get and set parameters
                    if (hasAnySetParam)
                    {
                        string error = "Cannot specify both 'get' and property values (text, color, font_size, size)";
                        ConsoleLogError($"[CommandExecutor] {error}");
                        return (false, error);
                    }

                    return GetUIInfo(obj);
                }

                // If set mode
                if (!hasAnySetParam)
                {
                    string error = "At least one property must be specified (text, color, font_size, or size), or use 'get: true' for retrieval";
                    ConsoleLogError($"[CommandExecutor] {error}");
                    return (false, error);
                }

                var results = new System.Collections.Generic.List<string>();

                // Get Text component
                Text textComponent = obj.GetComponent<Text>();
                if (textComponent == null)
                {
                    textComponent = obj.GetComponentInChildren<Text>();
                }

                // Get TextMeshPro component
                TextMeshProUGUI tmpComponent = obj.GetComponent<TextMeshProUGUI>();
                if (tmpComponent == null)
                {
                    tmpComponent = obj.GetComponentInChildren<TextMeshProUGUI>();
                }

                // Get Image component
                Image imageComponent = obj.GetComponent<Image>();

                // Set text
                if (hasText)
                {
                    if (textComponent != null)
                    {
                        Undo.RecordObject(textComponent, "Set UI Text");
                        textComponent.text = p.text;
                        results.Add($"text=\"{p.text}\"");
                    }
                    else if (tmpComponent != null)
                    {
                        Undo.RecordObject(tmpComponent, "Set UI Text");
                        tmpComponent.text = p.text;
                        results.Add($"text=\"{p.text}\"");
                    }
                }

                // Set font size
                if (hasFontSize)
                {
                    if (textComponent != null)
                    {
                        Undo.RecordObject(textComponent, "Set UI Font Size");
                        textComponent.fontSize = p.font_size;
                        results.Add($"font_size={p.font_size}");
                    }
                    else if (tmpComponent != null)
                    {
                        Undo.RecordObject(tmpComponent, "Set UI Font Size");
                        tmpComponent.fontSize = p.font_size;
                        results.Add($"font_size={p.font_size}");
                    }
                }

                // Set color
                if (hasColor)
                {
                    var (colorSuccess, parsedColor, colorError) = TryParseColor(p.color);
                    if (!colorSuccess)
                    {
                        ConsoleLogError($"[CommandExecutor] {colorError}");
                        return (false, colorError);
                    }

                    if (textComponent != null)
                    {
                        Undo.RecordObject(textComponent, "Set UI Color");
                        textComponent.color = parsedColor;
                        results.Add($"text_color={p.color}");
                    }
                    else if (tmpComponent != null)
                    {
                        Undo.RecordObject(tmpComponent, "Set UI Color");
                        tmpComponent.color = parsedColor;
                        results.Add($"text_color={p.color}");
                    }
                    else if (imageComponent != null)
                    {
                        Undo.RecordObject(imageComponent, "Set UI Color");
                        imageComponent.color = parsedColor;
                        results.Add($"image_color={p.color}");
                    }
                }

                // Set size
                if (hasSize)
                {
                    RectTransform rectTransform = obj.GetComponent<RectTransform>();
                    if (rectTransform != null)
                    {
                        Undo.RecordObject(rectTransform, "Set UI Size");
                        rectTransform.sizeDelta = new Vector2(p.size[0], p.size[1]);
                        results.Add($"size=({p.size[0]}, {p.size[1]})");
                    }
                    else
                    {
                        ConsoleLogWarning($"[CommandExecutor] RectTransform not found on {obj.name}, size not changed");
                    }
                }

                if (results.Count == 0)
                {
                    string error = "No modifiable UI components found (Text/Image/RectTransform)";
                    ConsoleLogWarning($"[CommandExecutor] {error}");
                    return (false, error);
                }

                EditorUtility.SetDirty(obj);

                string result = $"Set {obj.name} UI: {string.Join(", ", results)}";
                ConsoleLog($"[CommandExecutor] {result}");
                return (true, result);
            }
            catch (Exception e)
            {
                string error = $"Error in ui command: {e.Message}";
                ConsoleLogError($"[CommandExecutor] {error}");
                return (false, error);
            }
        }

        /// <summary>
        /// Gets UI information (internal helper)
        /// </summary>
        private (bool, string) GetUIInfo(GameObject obj)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"UI info for: {obj.name}");
            sb.AppendLine();

            // Text component info
            Text textComponent = obj.GetComponent<Text>();
            if (textComponent == null)
            {
                textComponent = obj.GetComponentInChildren<Text>();
            }

            if (textComponent != null)
            {
                sb.AppendLine($"Text: \"{textComponent.text}\"");
                sb.AppendLine($"Font Size: {textComponent.fontSize}");
                sb.AppendLine($"Text Color: #{ColorUtility.ToHtmlStringRGBA(textComponent.color)}");
            }

            // TextMeshPro component info
            TextMeshProUGUI tmpComponent = obj.GetComponent<TextMeshProUGUI>();
            if (tmpComponent == null)
            {
                tmpComponent = obj.GetComponentInChildren<TextMeshProUGUI>();
            }

            if (tmpComponent != null)
            {
                sb.AppendLine($"Text (TMP): \"{tmpComponent.text}\"");
                sb.AppendLine($"Font Size (TMP): {tmpComponent.fontSize}");
                sb.AppendLine($"Text Color (TMP): #{ColorUtility.ToHtmlStringRGBA(tmpComponent.color)}");
            }

            // Image component info
            Image imageComponent = obj.GetComponent<Image>();
            if (imageComponent != null)
            {
                sb.AppendLine($"Image Color: #{ColorUtility.ToHtmlStringRGBA(imageComponent.color)}");
                if (imageComponent.sprite != null)
                {
                    sb.AppendLine($"Sprite: {imageComponent.sprite.name}");
                }
            }

            // Button component info
            Button buttonComponent = obj.GetComponent<Button>();
            if (buttonComponent != null)
            {
                sb.AppendLine($"Type: Button");
                sb.AppendLine($"Interactable: {buttonComponent.interactable}");
            }

            // RectTransform info
            RectTransform rectTransform = obj.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                sb.AppendLine();
                sb.AppendLine($"Position: {rectTransform.anchoredPosition}");
                sb.AppendLine($"Size: {rectTransform.sizeDelta}");
            }

            string result = sb.ToString();
            ConsoleLog($"[CommandExecutor] Retrieved UI info for {obj.name}");
            return (true, result);
        }

        // ====== UI Operations ======

        /// <summary>
        /// Creates a canvas
        /// </summary>
        private (bool, string) CreateCanvas(CommandParams p)
        {
            try
            {
                // Check for name duplicates (before creation)
                string desiredName = !string.IsNullOrEmpty(p?.name) ? p.name : "Canvas";
                string duplicateError = CheckDuplicateRootName(desiredName);
                if (duplicateError != null)
                {
                    ConsoleLogError($"[CommandExecutor] {duplicateError}");
                    return (false, duplicateError);
                }

                // Also check EventSystem for duplicates (if needed)
                if (UnityEngine.EventSystems.EventSystem.current == null)
                {
                    string eventSystemError = CheckDuplicateRootName("EventSystem");
                    if (eventSystemError != null)
                    {
                        ConsoleLogError($"[CommandExecutor] {eventSystemError}");
                        return (false, eventSystemError);
                    }
                }

                // Create Canvas
                var canvasObj = new GameObject(desiredName);
                var canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;

                // Add CanvasScaler
                var scaler = canvasObj.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);

                // Add GraphicRaycaster
                canvasObj.AddComponent<GraphicRaycaster>();

                // Create EventSystem if it doesn't exist
                if (UnityEngine.EventSystems.EventSystem.current == null)
                {
                    var eventSystemObj = new GameObject("EventSystem");
                    eventSystemObj.AddComponent<UnityEngine.EventSystems.EventSystem>();

                    // Input System detection: check if New Input System is available
                    var inputSystemUIType = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
                    if (inputSystemUIType != null)
                    {
                        eventSystemObj.AddComponent(inputSystemUIType);
                    }
                    else
                    {
                        // Legacy Input System
                        eventSystemObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
                    }

                    Undo.RegisterCreatedObjectUndo(eventSystemObj, "Create EventSystem");
                }

                // Register with Undo
                Undo.RegisterCreatedObjectUndo(canvasObj, "Create Canvas");

                string result = $"Created Canvas: {canvasObj.name}";
                ConsoleLog($"[CommandExecutor] {result}");
                return (true, result);
            }
            catch (Exception e)
            {
                string error = $"Error creating canvas: {e.Message}";
                ConsoleLogError($"[CommandExecutor] {error}");
                return (false, error);
            }
        }

        /// <summary>
        /// Creates a UI element
        /// </summary>
        private (bool, string) CreateUIElement(CommandParams p)
        {
            try
            {
                if (p == null || string.IsNullOrEmpty(p.type))
                {
                    string error = "Missing required parameter: type";
                    ConsoleLogError($"[CommandExecutor] {error}");
                    return (false, error);
                }

                // Get parent object (search for Canvas if not specified)
                GameObject parent = null;
                if (!string.IsNullOrEmpty(p.parent))
                {
                    var (parentObj, parentFindError) = FindGameObjectByPath(p.parent);
                    if (parentObj == null)
                    {
                        string error = parentFindError ?? $"Parent object not found: {p.parent}";
                        ConsoleLogWarning($"[CommandExecutor] {error}");
                        return (false, error);
                    }
                    parent = parentObj;
                }
                else
                {
                    // Search for Canvas
                    Canvas canvas = UnityEngine.Object.FindAnyObjectByType<Canvas>();
                    if (canvas != null)
                    {
                        parent = canvas.gameObject;
                    }
                    else
                    {
                        string error = "No Canvas found in scene. Create a Canvas first.";
                        ConsoleLogWarning($"[CommandExecutor] {error}");
                        return (false, error);
                    }
                }

                GameObject uiElement = null;
                string typeLower = p.type.ToLower();

                switch (typeLower)
                {
                    case "button":
                        uiElement = CreateButton(parent);
                        break;
                    case "text":
                        uiElement = CreateText(parent);
                        break;
                    case "tmpro":
                        uiElement = CreateTextMeshPro(parent);
                        break;
                    case "image":
                        uiElement = CreateImage(parent);
                        break;
                    case "panel":
                        uiElement = CreatePanel(parent);
                        break;
                    case "inputfield":
                        uiElement = CreateInputField(parent, p.placeholder);
                        break;
                    case "scrollview":
                        uiElement = CreateScrollView(parent, p.scroll_direction);
                        break;
                    default:
                        string error = $"Unknown UI element type: {p.type}. Valid types: button, text, tmpro, image, panel, inputfield, scrollview";
                        ConsoleLogWarning($"[CommandExecutor] {error}");
                        return (false, error);
                }

                // Set name
                if (!string.IsNullOrEmpty(p.name))
                {
                    uiElement.name = p.name;
                }

                // Set size
                if (p.size != null && p.size.Length >= 2)
                {
                    RectTransform rectTransform = uiElement.GetComponent<RectTransform>();
                    if (rectTransform != null)
                    {
                        rectTransform.sizeDelta = new Vector2(p.size[0], p.size[1]);
                    }
                }

                // Set position (RectTransform)
                if (p.position != null && p.position.Length >= 2)
                {
                    RectTransform rectTransform = uiElement.GetComponent<RectTransform>();
                    if (rectTransform != null)
                    {
                        rectTransform.anchoredPosition = new Vector2(p.position[0], p.position[1]);
                    }
                }

                // Set font size (Text/TMP elements)
                if (p.font_size > 0)
                {
                    var text = uiElement.GetComponent<Text>();
                    var tmpText = uiElement.GetComponent<TextMeshProUGUI>();

                    if (text != null)
                    {
                        text.fontSize = p.font_size;
                    }
                    else if (tmpText != null)
                    {
                        tmpText.fontSize = p.font_size;
                    }
                    else
                    {
                        // Find Text in child elements (for Button etc.)
                        text = uiElement.GetComponentInChildren<Text>();
                        if (text != null)
                        {
                            text.fontSize = p.font_size;
                        }
                    }
                }

                // Set text content
                if (!string.IsNullOrEmpty(p.text))
                {
                    var text = uiElement.GetComponent<Text>();
                    var tmpText = uiElement.GetComponent<TextMeshProUGUI>();

                    if (text != null)
                    {
                        text.text = p.text;
                    }
                    else if (tmpText != null)
                    {
                        tmpText.text = p.text;
                    }
                    else
                    {
                        // Find Text in child elements (for Button etc.)
                        text = uiElement.GetComponentInChildren<Text>();
                        if (text != null)
                        {
                            text.text = p.text;
                        }
                    }
                }

                // Set color
                if (!string.IsNullOrEmpty(p.color))
                {
                    var (colorSuccess, parsedColor, colorError) = TryParseColor(p.color);
                    if (colorSuccess)
                    {
                        // Set Image color (Button, Panel, Image)
                        var image = uiElement.GetComponent<Image>();
                        if (image != null)
                        {
                            image.color = parsedColor;
                        }
                        // Set Text color
                        var text = uiElement.GetComponent<Text>();
                        if (text != null)
                        {
                            text.color = parsedColor;
                        }
                        // Set TextMeshPro color
                        var tmpText = uiElement.GetComponent<TextMeshProUGUI>();
                        if (tmpText != null)
                        {
                            tmpText.color = parsedColor;
                        }
                    }
                }

                // Set anchor
                if (!string.IsNullOrEmpty(p.anchor))
                {
                    RectTransform rectTransform = uiElement.GetComponent<RectTransform>();
                    if (rectTransform != null)
                    {
                        ApplyAnchorPreset(rectTransform, p.anchor);
                    }
                }

                Undo.RegisterCreatedObjectUndo(uiElement, $"Create UI {p.type}");

                string result = $"Created UI element: {uiElement.name} ({p.type})";
                ConsoleLog($"[CommandExecutor] {result}");
                return (true, result);
            }
            catch (Exception e)
            {
                string error = $"Error creating UI element: {e.Message}";
                ConsoleLogError($"[CommandExecutor] {error}");
                return (false, error);
            }
        }

        // ====== UI Element Creation Helpers ======

        private GameObject CreateButton(GameObject parent)
        {
            var buttonObj = new GameObject("Button");
            buttonObj.transform.SetParent(parent.transform, false);

            var rectTransform = buttonObj.AddComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(200, 40);

            var image = buttonObj.AddComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 1f);

            var button = buttonObj.AddComponent<Button>();

            // Add text
            var textObj = new GameObject("Text");
            textObj.transform.SetParent(buttonObj.transform, false);
            var textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;
            var text = textObj.AddComponent<Text>();
            text.text = "Button";
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.black;

            return buttonObj;
        }

        private GameObject CreateText(GameObject parent)
        {
            var textObj = new GameObject("Text");
            textObj.transform.SetParent(parent.transform, false);

            var rectTransform = textObj.AddComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(200, 40);

            var text = textObj.AddComponent<Text>();
            text.text = "New Text";
            text.color = Color.black;

            return textObj;
        }

        private GameObject CreateTextMeshPro(GameObject parent)
        {
            var textObj = new GameObject("Text (TMP)");
            textObj.transform.SetParent(parent.transform, false);

            var rectTransform = textObj.AddComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(200, 40);

            var tmpText = textObj.AddComponent<TextMeshProUGUI>();
            tmpText.text = "New Text";
            tmpText.color = Color.white;
            tmpText.fontSize = 24;

            return textObj;
        }

        private GameObject CreateImage(GameObject parent)
        {
            var imageObj = new GameObject("Image");
            imageObj.transform.SetParent(parent.transform, false);

            var rectTransform = imageObj.AddComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(100, 100);

            var image = imageObj.AddComponent<Image>();
            image.color = Color.white;

            return imageObj;
        }

        private GameObject CreatePanel(GameObject parent)
        {
            var panelObj = new GameObject("Panel");
            panelObj.transform.SetParent(parent.transform, false);

            var rectTransform = panelObj.AddComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.sizeDelta = Vector2.zero;

            var image = panelObj.AddComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0.392f);

            return panelObj;
        }

        private GameObject CreateInputField(GameObject parent, string placeholder)
        {
            var inputFieldObj = new GameObject("InputField");
            inputFieldObj.transform.SetParent(parent.transform, false);

            var rectTransform = inputFieldObj.AddComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(250, 40);

            var image = inputFieldObj.AddComponent<Image>();
            image.color = Color.white;

            var inputField = inputFieldObj.AddComponent<UnityEngine.UI.InputField>();

            // Create Text
            var textObj = new GameObject("Text");
            textObj.transform.SetParent(inputFieldObj.transform, false);
            var textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(10, 6);
            textRect.offsetMax = new Vector2(-10, -7);
            var text = textObj.AddComponent<Text>();
            text.color = Color.black;
            text.supportRichText = false;

            // Create Placeholder
            var placeholderObj = new GameObject("Placeholder");
            placeholderObj.transform.SetParent(inputFieldObj.transform, false);
            var placeholderRect = placeholderObj.AddComponent<RectTransform>();
            placeholderRect.anchorMin = Vector2.zero;
            placeholderRect.anchorMax = Vector2.one;
            placeholderRect.offsetMin = new Vector2(10, 6);
            placeholderRect.offsetMax = new Vector2(-10, -7);
            var placeholderText = placeholderObj.AddComponent<Text>();
            placeholderText.text = !string.IsNullOrEmpty(placeholder) ? placeholder : "Enter text...";
            placeholderText.fontStyle = FontStyle.Italic;
            placeholderText.color = new Color(0, 0, 0, 0.5f);

            // Set InputField references
            inputField.textComponent = text;
            inputField.placeholder = placeholderText;

            return inputFieldObj;
        }

        private GameObject CreateScrollView(GameObject parent, string scrollDirection)
        {
            var scrollViewObj = new GameObject("ScrollView");
            scrollViewObj.transform.SetParent(parent.transform, false);

            var scrollRect = scrollViewObj.AddComponent<RectTransform>();
            scrollRect.sizeDelta = new Vector2(200, 200);

            var scrollImage = scrollViewObj.AddComponent<Image>();
            scrollImage.color = new Color(1f, 1f, 1f, 0.1f);

            var scrollRectComponent = scrollViewObj.AddComponent<ScrollRect>();

            // Create Viewport
            var viewportObj = new GameObject("Viewport");
            viewportObj.transform.SetParent(scrollViewObj.transform, false);
            var viewportRect = viewportObj.AddComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.sizeDelta = Vector2.zero;
            viewportRect.pivot = new Vector2(0, 1);
            var viewportImage = viewportObj.AddComponent<Image>();
            viewportImage.color = Color.white;
            var mask = viewportObj.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            // Create Content
            var contentObj = new GameObject("Content");
            contentObj.transform.SetParent(viewportObj.transform, false);
            var contentRect = contentObj.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0, 1);
            contentRect.sizeDelta = new Vector2(0, 300); // Default content height

            // Configure ScrollRect
            scrollRectComponent.content = contentRect;
            scrollRectComponent.viewport = viewportRect;

            // Set scroll direction
            string direction = scrollDirection?.ToLower() ?? "vertical";
            switch (direction)
            {
                case "horizontal":
                    scrollRectComponent.horizontal = true;
                    scrollRectComponent.vertical = false;
                    contentRect.anchorMin = new Vector2(0, 0);
                    contentRect.anchorMax = new Vector2(0, 1);
                    contentRect.pivot = new Vector2(0, 0.5f);
                    contentRect.sizeDelta = new Vector2(300, 0);
                    break;
                case "both":
                    scrollRectComponent.horizontal = true;
                    scrollRectComponent.vertical = true;
                    contentRect.anchorMin = new Vector2(0, 1);
                    contentRect.anchorMax = new Vector2(0, 1);
                    contentRect.pivot = new Vector2(0, 1);
                    contentRect.sizeDelta = new Vector2(300, 300);
                    break;
                default: // vertical
                    scrollRectComponent.horizontal = false;
                    scrollRectComponent.vertical = true;
                    break;
            }

            return scrollViewObj;
        }

        /// <summary>
        /// Applies an anchor preset
        /// </summary>
        private void ApplyAnchorPreset(RectTransform rectTransform, string preset)
        {
            switch (preset.ToLower())
            {
                case "top-left":
                    rectTransform.anchorMin = new Vector2(0, 1);
                    rectTransform.anchorMax = new Vector2(0, 1);
                    rectTransform.pivot = new Vector2(0, 1);
                    break;
                case "top-center":
                case "top":
                    rectTransform.anchorMin = new Vector2(0.5f, 1);
                    rectTransform.anchorMax = new Vector2(0.5f, 1);
                    rectTransform.pivot = new Vector2(0.5f, 1);
                    break;
                case "top-right":
                    rectTransform.anchorMin = new Vector2(1, 1);
                    rectTransform.anchorMax = new Vector2(1, 1);
                    rectTransform.pivot = new Vector2(1, 1);
                    break;
                case "middle-left":
                case "left":
                    rectTransform.anchorMin = new Vector2(0, 0.5f);
                    rectTransform.anchorMax = new Vector2(0, 0.5f);
                    rectTransform.pivot = new Vector2(0, 0.5f);
                    break;
                case "middle-center":
                case "center":
                    rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                    rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                    rectTransform.pivot = new Vector2(0.5f, 0.5f);
                    break;
                case "middle-right":
                case "right":
                    rectTransform.anchorMin = new Vector2(1, 0.5f);
                    rectTransform.anchorMax = new Vector2(1, 0.5f);
                    rectTransform.pivot = new Vector2(1, 0.5f);
                    break;
                case "bottom-left":
                    rectTransform.anchorMin = new Vector2(0, 0);
                    rectTransform.anchorMax = new Vector2(0, 0);
                    rectTransform.pivot = new Vector2(0, 0);
                    break;
                case "bottom-center":
                case "bottom":
                    rectTransform.anchorMin = new Vector2(0.5f, 0);
                    rectTransform.anchorMax = new Vector2(0.5f, 0);
                    rectTransform.pivot = new Vector2(0.5f, 0);
                    break;
                case "bottom-right":
                    rectTransform.anchorMin = new Vector2(1, 0);
                    rectTransform.anchorMax = new Vector2(1, 0);
                    rectTransform.pivot = new Vector2(1, 0);
                    break;
                case "stretch-top":
                    rectTransform.anchorMin = new Vector2(0, 1);
                    rectTransform.anchorMax = new Vector2(1, 1);
                    rectTransform.pivot = new Vector2(0.5f, 1);
                    break;
                case "stretch-middle":
                    rectTransform.anchorMin = new Vector2(0, 0.5f);
                    rectTransform.anchorMax = new Vector2(1, 0.5f);
                    rectTransform.pivot = new Vector2(0.5f, 0.5f);
                    break;
                case "stretch-bottom":
                    rectTransform.anchorMin = new Vector2(0, 0);
                    rectTransform.anchorMax = new Vector2(1, 0);
                    rectTransform.pivot = new Vector2(0.5f, 0);
                    break;
                case "stretch-left":
                    rectTransform.anchorMin = new Vector2(0, 0);
                    rectTransform.anchorMax = new Vector2(0, 1);
                    rectTransform.pivot = new Vector2(0, 0.5f);
                    break;
                case "stretch-center":
                    rectTransform.anchorMin = new Vector2(0.5f, 0);
                    rectTransform.anchorMax = new Vector2(0.5f, 1);
                    rectTransform.pivot = new Vector2(0.5f, 0.5f);
                    break;
                case "stretch-right":
                    rectTransform.anchorMin = new Vector2(1, 0);
                    rectTransform.anchorMax = new Vector2(1, 1);
                    rectTransform.pivot = new Vector2(1, 0.5f);
                    break;
                case "stretch":
                case "stretch-all":
                    rectTransform.anchorMin = new Vector2(0, 0);
                    rectTransform.anchorMax = new Vector2(1, 1);
                    rectTransform.pivot = new Vector2(0.5f, 0.5f);
                    break;
                default:
                    ConsoleLogWarning($"[CommandExecutor] Unknown anchor preset: {preset}. Valid values: top-left, top-center, top-right, middle-left, center, middle-right, bottom-left, bottom-center, bottom-right, stretch-top, stretch-middle, stretch-bottom, stretch-left, stretch-center, stretch-right, stretch, stretch-all");
                    break;
            }
        }
    }
}
