// ============================================================
//  DialogueHUDBuilder.cs
//  Place this file inside any folder named "Editor" in your project.
//  Menu: Tools → Build Dialogue HUD
//
//  Requirements:
//    - TextMeshPro package installed (Window > Package Manager)
//    - Run from an open scene where you want the Canvas created
// ============================================================

#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;
using System.IO;

public static class DialogueHUDBuilder
{
    // ── Colors ───────────────────────────────────────────────
    static readonly Color C_BG_DEEP     = Hex("#080F18");
    static readonly Color C_BG_MID      = Hex("#0D1A28");
    static readonly Color C_BORDER_DIM  = Hex("#1E3A5F");
    static readonly Color C_BORDER_ACT  = Hex("#2A6EA6");
    static readonly Color C_ACCENT      = Hex("#4EB8E8");
    static readonly Color C_TEXT_BODY   = Hex("#A8C8E0");
    static readonly Color C_HEALTH      = Hex("#C83030");
    static readonly Color C_HEALTH_BG   = Hex("#1A0808");
    static readonly Color C_SHIELD      = Hex("#3070C8");
    static readonly Color C_SHIELD_BG   = Hex("#08101A");
    static readonly Color C_THREAT      = Hex("#E84040");
    static readonly Color C_LABEL_DIM   = Hex("#2A4A6A");
    static readonly Color C_EMPTY       = Hex("#1E3A5F");

    // ── Entry point ──────────────────────────────────────────
    [MenuItem("Tools/Build Dialogue HUD")]
    public static void Build()
    {
        // Prevent duplicates
        var existing = GameObject.Find("Canvas [DialogueHUD]");
        if (existing != null)
        {
            bool replace = EditorUtility.DisplayDialog(
                "Canvas already exists",
                "A 'Canvas [DialogueHUD]' already exists in the scene. Replace it?",
                "Replace", "Cancel");
            if (!replace) return;
            Undo.DestroyObjectImmediate(existing);
        }

        // ── Canvas ───────────────────────────────────────────
        var canvasGO = new GameObject("Canvas [DialogueHUD]");
        Undo.RegisterCreatedObjectUndo(canvasGO, "Build Dialogue HUD");

        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 0;

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode     = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight  = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();

        // ── EventSystem (create if missing) ──────────────────
        if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            var esGO = new GameObject("EventSystem");
            esGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esGO.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        // ── HUD (always active) ───────────────────────────────
        var hud = MakeGO("HUD", canvasGO.transform);
        StretchFull(hud);

        BuildBottomBar(hud.transform);
        BuildSectorIndicator(hud.transform);   // top-right of screen, separate from dialogue

        // ── DialogueLayer (starts inactive) ──────────────────
        var dlgLayer = MakeGO("DialogueLayer", canvasGO.transform);
        StretchFull(dlgLayer);
        BuildDialogueBox(dlgLayer.transform);
        dlgLayer.SetActive(false);

        // ── Loadout Prefab ────────────────────────────────────
        EnsurePrefabFolder();
        BuildLoadoutSlotPrefab();

        Debug.Log("<color=#4EB8E8>[DialogueHUDBuilder] Canvas built successfully.</color>\n" +
                  "• Assign your font to all TMP components (marked with 'TODO: assign font').\n" +
                  "• Assign weapon sprites to LoadoutSlot prefabs.\n" +
                  "• Attach DialogueSystem.cs to the DialogueBox GameObject.");

        Selection.activeGameObject = canvasGO;
    }

    // ══════════════════════════════════════════════════════════
    //  DIALOGUE BOX
    // ══════════════════════════════════════════════════════════
    static void BuildDialogueBox(Transform parent)
    {
        // Container
        var box = MakeGO("DialogueBox", parent);
        var rt  = box.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot     = new Vector2(0.5f, 1f);
        rt.offsetMin = new Vector2(0, -160);
        rt.offsetMax = Vector2.zero;

        AddImage(box, C_BG_DEEP);

        // Bottom border line
        var border = MakeGO("BottomBorder", box.transform);
        var brt    = border.GetComponent<RectTransform>();
        brt.anchorMin = new Vector2(0, 0);
        brt.anchorMax = new Vector2(1, 0);
        brt.pivot     = new Vector2(0.5f, 0f);
        brt.sizeDelta = new Vector2(0, 1);
        brt.anchoredPosition = Vector2.zero;
        AddImage(border, C_BORDER_DIM);

        // Actor Icon
        var icon = MakeGO("ActorIcon", box.transform);
        var irt  = icon.GetComponent<RectTransform>();
        irt.anchorMin        = new Vector2(0, 0.5f);
        irt.anchorMax        = new Vector2(0, 0.5f);
        irt.pivot            = new Vector2(0, 0.5f);
        irt.sizeDelta        = new Vector2(64, 64);
        irt.anchoredPosition = new Vector2(16, 0);
        var iconImg          = AddImage(icon, C_BG_MID);
        iconImg.preserveAspect = true;

        // Icon border child
        var iconBorder = MakeGO("IconBorder", icon.transform);
        StretchFull(iconBorder);
        var ibImg = AddImage(iconBorder, Color.clear);
        // Outline effect via a thin child panel — simple approach
        iconBorder.GetComponent<Image>().color = C_BORDER_ACT;
        var ibOutline = iconBorder.AddComponent<Outline>();
        ibOutline.effectColor    = C_BORDER_ACT;
        ibOutline.effectDistance = new Vector2(1, -1);

        // Actor Name
        var nameGO = MakeGO("ActorName", box.transform);
        var nrt    = nameGO.GetComponent<RectTransform>();
        nrt.anchorMin        = new Vector2(0, 1);
        nrt.anchorMax        = new Vector2(0, 1);
        nrt.pivot            = new Vector2(0, 1);
        nrt.sizeDelta        = new Vector2(400, 40);
        nrt.anchoredPosition = new Vector2(96, -10);
        var nameTMP          = AddTMP(nameGO, "COMMANDER ZRIX", 32, C_ACCENT, FontStyles.Bold);
        nameTMP.characterSpacing = 4f;

        // Dialogue Text
        var textGO = MakeGO("DialogueText", box.transform);
        var trt    = textGO.GetComponent<RectTransform>();
        trt.anchorMin = new Vector2(0, 1);
        trt.anchorMax = new Vector2(1, 1);
        trt.pivot     = new Vector2(0, 1);
        trt.offsetMin = new Vector2(96, -148);
        trt.offsetMax = new Vector2(-220, -48);
        var textTMP   = AddTMP(textGO, "", 30, C_TEXT_BODY, FontStyles.Normal);
        textTMP.enableWordWrapping    = true;
        textTMP.lineSpacing           = 6f;
        textTMP.overflowMode          = TextOverflowModes.Overflow;

        // Cursor (blinking block, child of DialogueText)
        var cursor = MakeGO("Cursor", textGO.transform);
        var crt    = cursor.GetComponent<RectTransform>();
        crt.anchorMin = new Vector2(0, 0);
        crt.anchorMax = new Vector2(0, 0);
        crt.pivot     = new Vector2(0, 0);
        crt.sizeDelta = new Vector2(8, 13);
        AddImage(cursor, C_ACCENT);
        // Note: DialogueSystem.cs will position and toggle this at runtime

        // Timer Bar background
        var timerBG = MakeGO("TimerBar", box.transform);
        var tbrt    = timerBG.GetComponent<RectTransform>();
        tbrt.anchorMin = new Vector2(0, 0);
        tbrt.anchorMax = new Vector2(1, 0);
        tbrt.pivot     = new Vector2(0.5f, 0f);
        tbrt.sizeDelta = new Vector2(0, 2);
        tbrt.anchoredPosition = Vector2.zero;
        AddImage(timerBG, C_BORDER_DIM);

        // Timer Bar fill
        var timerFill = MakeGO("TimerBarFill", timerBG.transform);
        StretchFull(timerFill);
        var tfImg = AddImage(timerFill, C_ACCENT);
        tfImg.type       = Image.Type.Filled;
        tfImg.fillMethod = Image.FillMethod.Horizontal;
        tfImg.fillOrigin = (int)Image.OriginHorizontal.Left;
        tfImg.fillAmount = 1f;

        // Continue Prompt
        var continueGO = MakeGO("ContinuePrompt", box.transform);
        var cprt       = continueGO.GetComponent<RectTransform>();
        cprt.anchorMin        = new Vector2(1, 0);
        cprt.anchorMax        = new Vector2(1, 0);
        cprt.pivot            = new Vector2(1, 0);
        cprt.sizeDelta        = new Vector2(200, 30);
        cprt.anchoredPosition = new Vector2(-16, 8);
        var cpTMP             = AddTMP(continueGO, "CONTINUE ▼", 25, C_ACCENT, FontStyles.Normal);
        cpTMP.alignment       = TextAlignmentOptions.Right;
        cpTMP.characterSpacing = 3f;
        var cpColor           = cpTMP.color;
        cpColor.a             = 0.7f;
        cpTMP.color           = cpColor;
    }

    // ══════════════════════════════════════════════════════════
    //  SECTOR INDICATOR  (top-right, inside HUD)
    // ══════════════════════════════════════════════════════════
    static void BuildSectorIndicator(Transform parent)
    {
        var si  = MakeGO("SectorIndicator", parent);
        var rt  = si.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(1, 1);
        rt.anchorMax        = new Vector2(1, 1);
        rt.pivot            = new Vector2(1, 1);
        rt.sizeDelta        = new Vector2(160, 40);
        rt.anchoredPosition = new Vector2(-16, -10);

        var vlg = si.AddComponent<VerticalLayoutGroup>();
        vlg.spacing           = 2;
        vlg.childAlignment    = TextAnchor.UpperRight;
        vlg.childControlWidth = true;
        vlg.childControlHeight= false;
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;

        // Sector label
        var sectorGO  = MakeGO("SectorLabel", si.transform);
        sectorGO.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 18);
        var sectorTMP = AddTMP(sectorGO, "SECTOR 7", 10, C_ACCENT, FontStyles.Normal);
        sectorTMP.alignment       = TextAlignmentOptions.Right;
        sectorTMP.characterSpacing = 4f;

        // Threat label
        var threatGO  = MakeGO("ThreatLabel", si.transform);
        threatGO.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 18);
        var threatTMP = AddTMP(threatGO, "● HOSTILE", 10, C_THREAT, FontStyles.Bold);
        threatTMP.alignment       = TextAlignmentOptions.Right;
        threatTMP.characterSpacing = 3f;
    }

    // ══════════════════════════════════════════════════════════
    //  BOTTOM BAR
    // ══════════════════════════════════════════════════════════
    static void BuildBottomBar(Transform parent)
    {
        var bar = MakeGO("BottomBar", parent);
        var rt  = bar.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 0);
        rt.anchorMax = new Vector2(1, 0);
        rt.pivot     = new Vector2(0.5f, 0f);
        rt.sizeDelta = new Vector2(0, 160);
        rt.anchoredPosition = Vector2.zero;
        AddImage(bar, C_BG_DEEP);

        // Top border
        var topBorder = MakeGO("TopBorder", bar.transform);
        var tbrt      = topBorder.GetComponent<RectTransform>();
        tbrt.anchorMin = new Vector2(0, 1);
        tbrt.anchorMax = new Vector2(1, 1);
        tbrt.pivot     = new Vector2(0.5f, 1f);
        tbrt.sizeDelta = new Vector2(0, 1);
        tbrt.anchoredPosition = Vector2.zero;
        AddImage(topBorder, C_BORDER_DIM);

        BuildStatsPanel(bar.transform);
        BuildLoadoutPanel(bar.transform);
        BuildScavengedPanel(bar.transform);
    }

    // ── Stats Panel ──────────────────────────────────────────
    static void BuildStatsPanel(Transform parent)
    {
        var panel = MakeGO("StatsPanel", parent);
        var rt    = panel.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0, 0);
        rt.anchorMax        = new Vector2(0, 1);
        rt.pivot            = new Vector2(0, 0.5f);
        rt.sizeDelta        = new Vector2(520, 0);
        rt.anchoredPosition = new Vector2(16, 0);

        var vlg = panel.AddComponent<VerticalLayoutGroup>();
        vlg.spacing              = 16;
        vlg.childAlignment       = TextAnchor.MiddleLeft;
        vlg.childControlWidth    = true;
        vlg.childControlHeight   = false;
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;
        vlg.padding = new RectOffset(0, 0, 22, 22);

        BuildStatRow(panel.transform, "HealthRow",  "+",  C_HEALTH,  C_HEALTH_BG,  "138 / 138", C_HEALTH);
        BuildStatRow(panel.transform, "ShieldRow",  "◆",  C_SHIELD,  C_SHIELD_BG,  "30 / 30",   C_SHIELD);
    }

    static void BuildStatRow(Transform parent, string name, string icon,
                              Color barColor, Color bgColor, string valText, Color textColor)
    {
        var row = MakeGO(name, parent);
        var rt  = row.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0, 36);

        var hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing              = 6;
        hlg.childAlignment       = TextAnchor.MiddleLeft;
        hlg.childControlWidth    = false;
        hlg.childControlHeight   = false;
        hlg.childForceExpandWidth  = false;
        hlg.childForceExpandHeight = false;

        // Icon
        var iconGO = MakeGO("Icon", row.transform);
        iconGO.GetComponent<RectTransform>().sizeDelta = new Vector2(26, 26);
        var iconTMP = AddTMP(iconGO, icon, 20, barColor, FontStyles.Bold);
        iconTMP.alignment = TextAlignmentOptions.Center;

        // Bar background
        var barBG = MakeGO("BarBG", row.transform);
        barBG.GetComponent<RectTransform>().sizeDelta = new Vector2(400, 30);
        AddImage(barBG, bgColor);

        // Bar fill
        // Pivot is set to the left edge (0, 0.5) so that localScale.x shrinks the bar
        // from right to left, which is what PlayerHUD uses to drive both plain and Filled images.
        var fill   = MakeGO("Fill", barBG.transform);
        StretchFull(fill);
        var fillRt        = fill.GetComponent<RectTransform>();
        fillRt.pivot      = new Vector2(0f, 0.5f);
        var fillImg       = AddImage(fill, barColor);
        fillImg.type       = Image.Type.Filled;
        fillImg.fillMethod = Image.FillMethod.Horizontal;
        fillImg.fillOrigin = (int)Image.OriginHorizontal.Left;
        fillImg.fillAmount = 1f;

        // Value text
        var valGO = MakeGO("ValueText", row.transform);
        valGO.GetComponent<RectTransform>().sizeDelta = new Vector2(90, 26);
        var valTMP = AddTMP(valGO, valText, 18, textColor, FontStyles.Normal);
        valTMP.alignment = TextAlignmentOptions.Right;
    }

    // ── Loadout Panel ────────────────────────────────────────
    static void BuildLoadoutPanel(Transform parent)
    {
        var panel = MakeGO("LoadoutPanel", parent);
        var rt    = panel.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0);
        rt.anchorMax        = new Vector2(0.5f, 1);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.sizeDelta        = new Vector2(380, 0);
        rt.anchoredPosition = Vector2.zero;

        var hlg = panel.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing              = 6;
        hlg.childAlignment       = TextAnchor.MiddleCenter;
        hlg.childControlWidth    = false;
        hlg.childControlHeight   = false;
        hlg.childForceExpandWidth  = false;
        hlg.childForceExpandHeight = false;

        // Rotated "LOADOUT" label
        var labelGO  = MakeGO("LoadoutLabel", panel.transform);
        labelGO.GetComponent<RectTransform>().sizeDelta = new Vector2(14, 60);
        var labelTMP = AddTMP(labelGO, "LOADOUT", 8, C_LABEL_DIM, FontStyles.Normal);
        labelTMP.alignment       = TextAlignmentOptions.Center;
        labelTMP.characterSpacing = 4f;
        labelGO.transform.localRotation = Quaternion.Euler(0, 0, 90);

        // Separator
        var sep = MakeGO("Separator", panel.transform);
        sep.GetComponent<RectTransform>().sizeDelta = new Vector2(1, 50);
        AddImage(sep, C_BORDER_DIM);

        // Slots
        string[] slotNames   = { "Slot_Pulse",  "Slot_Spread", "Slot_Laser" };
        string[] slotLabels  = { "PULSE [1]",   "SPREAD [2]",  "LASER [2]"  };

        for (int i = 0; i < 3; i++)
            BuildLoadoutSlotInline(panel.transform, slotNames[i], slotLabels[i], i == 0);
    }

    static void BuildLoadoutSlotInline(Transform parent, string name, string label, bool active)
    {
        var slot = MakeGO(name, parent);
        var rt   = slot.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(100, 100);

        slot.AddComponent<Button>();
        var bg = AddImage(slot, C_BG_MID);

        // Border via Outline component
        var outline = slot.AddComponent<Outline>();
        outline.effectColor    = active ? C_BORDER_ACT : C_BORDER_DIM;
        outline.effectDistance = new Vector2(1, -1);

        // Weapon icon placeholder
        var weaponGO = MakeGO("WeaponIcon", slot.transform);
        var wrt      = weaponGO.GetComponent<RectTransform>();
        wrt.anchorMin        = new Vector2(0.5f, 0.5f);
        wrt.anchorMax        = new Vector2(0.5f, 0.5f);
        wrt.pivot            = new Vector2(0.5f, 0.5f);
        wrt.sizeDelta        = new Vector2(68, 48);
        wrt.anchoredPosition = new Vector2(0, 10);
        var wImg             = AddImage(weaponGO, active ? C_ACCENT : C_BORDER_DIM);
        wImg.preserveAspect  = true;

        // Slot label
        var labelGO  = MakeGO("SlotLabel", slot.transform);
        var lrt      = labelGO.GetComponent<RectTransform>();
        lrt.anchorMin        = new Vector2(0, 0);
        lrt.anchorMax        = new Vector2(1, 0);
        lrt.pivot            = new Vector2(0.5f, 0f);
        lrt.sizeDelta        = new Vector2(0, 18);
        lrt.anchoredPosition = new Vector2(0, 6);
        var lTMP = AddTMP(labelGO, label, 13, active ? C_BORDER_ACT : C_EMPTY, FontStyles.Normal);
        lTMP.alignment       = TextAlignmentOptions.Center;
        lTMP.characterSpacing = 2f;

        // Active indicator dot
        var dot = MakeGO("ActiveIndicator", slot.transform);
        var drt = dot.GetComponent<RectTransform>();
        drt.anchorMin        = new Vector2(0.5f, 0);
        drt.anchorMax        = new Vector2(0.5f, 0);
        drt.pivot            = new Vector2(0.5f, 0f);
        drt.sizeDelta        = new Vector2(4, 4);
        drt.anchoredPosition = new Vector2(0, 1);
        AddImage(dot, C_ACCENT);
        dot.SetActive(active);
    }

    // ── Scavenged Panel ──────────────────────────────────────
    static void BuildScavengedPanel(Transform parent)
    {
        var panel = MakeGO("ScavengedPanel", parent);
        var rt    = panel.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(1, 0);
        rt.anchorMax        = new Vector2(1, 1);
        rt.pivot            = new Vector2(1, 0.5f);
        rt.sizeDelta        = new Vector2(120, 0);
        rt.anchoredPosition = new Vector2(-16, 0);

        var vlg = panel.AddComponent<VerticalLayoutGroup>();
        vlg.spacing              = 4;
        vlg.childAlignment       = TextAnchor.UpperCenter;
        vlg.childControlWidth    = true;
        vlg.childControlHeight   = false;
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;
        vlg.padding = new RectOffset(0, 0, 8, 0);

        // "SCAVENGED" label
        var topLabel = MakeGO("ScavengedLabel", panel.transform);
        topLabel.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 18);
        var tlTMP = AddTMP(topLabel, "SCAVENGED", 11, C_LABEL_DIM, FontStyles.Normal);
        tlTMP.alignment        = TextAlignmentOptions.Center;
        tlTMP.characterSpacing = 2f;

        // Item slot
        var slotGO = MakeGO("ItemSlot", panel.transform);
        slotGO.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 72);
        AddImage(slotGO, C_BG_MID);
        var slotOutline = slotGO.AddComponent<Outline>();
        slotOutline.effectColor    = C_BORDER_DIM;
        slotOutline.effectDistance = new Vector2(1, -1);

        // "?" placeholder
        var qGO  = MakeGO("QuestionMark", slotGO.transform);
        StretchFull(qGO);
        var qTMP = AddTMP(qGO, "?", 26, C_BORDER_DIM, FontStyles.Bold);
        qTMP.alignment = TextAlignmentOptions.Center;

        // "EMPTY" label
        var emptyGO = MakeGO("EmptyLabel", panel.transform);
        emptyGO.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 16);
        var eTMP = AddTMP(emptyGO, "EMPTY", 11, C_EMPTY, FontStyles.Normal);
        eTMP.alignment        = TextAlignmentOptions.Center;
        eTMP.characterSpacing = 2f;
    }

    // ══════════════════════════════════════════════════════════
    //  LOADOUT SLOT PREFAB
    // ══════════════════════════════════════════════════════════
    static void BuildLoadoutSlotPrefab()
    {
        string prefabPath = "Assets/Prefabs/UI/LoadoutSlot.prefab";

        // Build the slot in a temp scene object, then save as prefab
        var slot = new GameObject("LoadoutSlot");
        slot.AddComponent<RectTransform>().sizeDelta = new Vector2(100, 100);
        slot.AddComponent<Button>();
        AddImage(slot, Hex("#0D1A28"));

        var outline = slot.AddComponent<Outline>();
        outline.effectColor    = Hex("#1E3A5F");
        outline.effectDistance = new Vector2(1, -1);

        var weaponGO = MakeGO("WeaponIcon", slot.transform);
        var wrt      = weaponGO.GetComponent<RectTransform>();
        wrt.anchorMin = wrt.anchorMax = new Vector2(0.5f, 0.5f);
        wrt.pivot     = new Vector2(0.5f, 0.5f);
        wrt.sizeDelta = new Vector2(68, 48);
        wrt.anchoredPosition = new Vector2(0, 10);
        AddImage(weaponGO, Hex("#1E3A5F")).preserveAspect = true;

        var labelGO = MakeGO("SlotLabel", slot.transform);
        var lrt     = labelGO.GetComponent<RectTransform>();
        lrt.anchorMin = new Vector2(0, 0); lrt.anchorMax = new Vector2(1, 0);
        lrt.pivot     = new Vector2(0.5f, 0); lrt.sizeDelta = new Vector2(0, 18);
        lrt.anchoredPosition = new Vector2(0, 6);
        var lTMP = AddTMP(labelGO, "WEAPON [1]", 13, Hex("#2A6EA6"), FontStyles.Normal);
        lTMP.alignment = TextAlignmentOptions.Center;

        var dot = MakeGO("ActiveIndicator", slot.transform);
        var drt = dot.GetComponent<RectTransform>();
        drt.anchorMin = drt.anchorMax = new Vector2(0.5f, 0);
        drt.pivot     = new Vector2(0.5f, 0);
        drt.sizeDelta = new Vector2(4, 4);
        drt.anchoredPosition = new Vector2(0, 1);
        AddImage(dot, Hex("#4EB8E8"));
        dot.SetActive(false);

        PrefabUtility.SaveAsPrefabAsset(slot, prefabPath);
        Object.DestroyImmediate(slot);

        Debug.Log($"[DialogueHUDBuilder] LoadoutSlot prefab saved to {prefabPath}");
    }

    // ══════════════════════════════════════════════════════════
    //  HELPERS
    // ══════════════════════════════════════════════════════════
    static GameObject MakeGO(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        return go;
    }

    static void StretchFull(GameObject go)
    {
        var rt        = go.GetComponent<RectTransform>();
        rt.anchorMin  = Vector2.zero;
        rt.anchorMax  = Vector2.one;
        rt.offsetMin  = Vector2.zero;
        rt.offsetMax  = Vector2.zero;
    }

    static Image AddImage(GameObject go, Color color)
    {
        var img   = go.AddComponent<Image>();
        img.color = color;
        return img;
    }

    static TextMeshProUGUI AddTMP(GameObject go, string text, float size,
                                   Color color, FontStyles style)
    {
        var tmp        = go.AddComponent<TextMeshProUGUI>();
        tmp.text       = text;
        tmp.fontSize   = size;
        tmp.color      = color;
        tmp.fontStyle  = style;
        // Font is intentionally left unassigned — assign in Inspector
        return tmp;
    }

    static void EnsurePrefabFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
            AssetDatabase.CreateFolder("Assets", "Prefabs");
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs/UI"))
            AssetDatabase.CreateFolder("Assets/Prefabs", "UI");
    }

    static Color Hex(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color c);
        return c;
    }
}
#endif