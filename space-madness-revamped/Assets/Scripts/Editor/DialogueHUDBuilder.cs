// ============================================================
//  DialogueHUDBuilder.cs
//  Place this file inside any folder named "Editor" in your project.
//  Menu: Tools → Build Dialogue HUD
//
//  Layout:
//    Upper-middle  — Dialogue box (compact, leaves sides free)
//    Upper-right   — Score + Health bar + Shield bar (thin)
//    Bottom-centre — Weapon panel (small slots) + Hex display (shown on switch)
//    Lower-right   — Power-up with dual-layer cooldown fill
//
//  Auto-assigns after building:
//    DialogueSystem        → all DialogueBox references
//    PlayerHUD             → health/shield fill + text references
//    WeaponHUDController   → slot and hex references
// ============================================================

#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

public static class DialogueHUDBuilder
{
    // ── Palette ──────────────────────────────────────────────
    static readonly Color C_BG_DEEP    = Hex("#080F18");
    static readonly Color C_BG_MID     = Hex("#0D1A28");
    static readonly Color C_BORDER_DIM = Hex("#1E3A5F");
    static readonly Color C_BORDER_ACT = Hex("#2A6EA6");
    static readonly Color C_ACCENT     = Hex("#4EB8E8");
    static readonly Color C_TEXT_BODY  = Hex("#A8C8E0");
    static readonly Color C_HEALTH     = Hex("#C83030");
    static readonly Color C_HEALTH_BG  = Hex("#1A0808");
    static readonly Color C_SHIELD     = Hex("#3070C8");
    static readonly Color C_SHIELD_BG  = Hex("#08101A");
    static readonly Color C_SCORE      = Hex("#E8C84E");
    static readonly Color C_LABEL_DIM  = Hex("#2A4A6A");
    static readonly Color C_EMPTY      = Hex("#1E3A5F");
    static readonly Color C_POWERUP    = Hex("#48E8A0");

    // ── References collected during build for auto-assignment ─
    static TextMeshProUGUI _actorNameTMP;
    static Image           _actorIconImg;
    static TextMeshProUGUI _dialogueTextTMP;
    static RectTransform   _cursorRT;
    static Image           _timerBarFillImg;
    static GameObject      _continuePromptGO;
    static GameObject      _dialogueBoxGO;

    static Image           _healthFillImg;
    static TextMeshProUGUI _healthValueTMP;
    static Image           _shieldFillImg;
    static TextMeshProUGUI _shieldValueTMP;

    static GameObject[]    _weaponSlotGOs = new GameObject[4];
    static GameObject      _hexDisplayGO;
    static Image           _hexWeaponIconImg;
    static TextMeshProUGUI _hexWeaponNameTMP;

    static Image           _powerupFillImg;

    // ── Entry point ──────────────────────────────────────────
    [MenuItem("Tools/Build Dialogue HUD")]
    public static void Build()
    {
        var existing = GameObject.Find("Canvas [HUD]");
        if (existing != null)
        {
            bool replace = EditorUtility.DisplayDialog(
                "Canvas already exists",
                "A 'Canvas [HUD]' already exists. Replace it?",
                "Replace", "Cancel");
            if (!replace) return;
            Undo.DestroyObjectImmediate(existing);
        }

        // ── Canvas ────────────────────────────────────────────
        var canvasGO = new GameObject("Canvas [HUD]");
        Undo.RegisterCreatedObjectUndo(canvasGO, "Build HUD");

        var canvas          = canvasGO.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 0;

        var scaler                 = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode     = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight  = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();

        if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            var esGO = new GameObject("EventSystem");
            esGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esGO.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        // ── HUD root ──────────────────────────────────────────
        var hud = MakeGO("HUD", canvasGO.transform);
        StretchFull(hud);

        BuildDialogueBox(hud.transform);
        BuildStatsPanel(hud.transform);
        BuildWeaponPanel(hud.transform);
        BuildPowerupPanel(hud.transform);

        // ── Auto-assign scripts ───────────────────────────────
        AssignDialogueSystem(_dialogueBoxGO);
        AssignPlayerHUD(hud);
        AssignWeaponHUDController(hud);

        EnsurePrefabFolder();

        Debug.Log("<color=#4EB8E8>[DialogueHUDBuilder] HUD built and scripts assigned.</color>\n" +
                  "Remaining manual steps:\n" +
                  "• Assign your TMP font to all text components.\n" +
                  "• Assign weapon sprites to each WeaponSlot/WeaponIcon and HexDisplay/WeaponIcon.\n" +
                  "• Assign power-up sprite to PowerupPanel/IconArea/PowerupIcon_Dim and PowerupIcon_Fill.\n" +
                  "• Assign a hexagonal sprite to WeaponHexDisplay/HexBG (optional, cosmetic).\n" +
                  "• Drag the Player GameObject into PlayerHUD.PlayerHealth in the Inspector.");

        Selection.activeGameObject = canvasGO;
    }

    // ══════════════════════════════════════════════════════════
    //  DIALOGUE BOX  (upper-middle, compact)
    // ══════════════════════════════════════════════════════════
    static void BuildDialogueBox(Transform parent)
    {
        _dialogueBoxGO = MakeGO("DialogueBox", parent);
        var rt         = _dialogueBoxGO.GetComponent<RectTransform>();
        // 50 % of the screen width, anchored to the top-centre
        rt.anchorMin        = new Vector2(0.25f, 1f);
        rt.anchorMax        = new Vector2(0.75f, 1f);
        rt.pivot            = new Vector2(0.5f, 1f);
        rt.offsetMin        = new Vector2(0f, -136f);
        rt.offsetMax        = Vector2.zero;

        var bgImg   = AddImage(_dialogueBoxGO, C_BG_DEEP);
        bgImg.color = Alpha(C_BG_DEEP, 0.93f);

        // Bottom border line
        var border = MakeGO("BottomBorder", _dialogueBoxGO.transform);
        var brt    = border.GetComponent<RectTransform>();
        brt.anchorMin        = new Vector2(0f, 0f);
        brt.anchorMax        = new Vector2(1f, 0f);
        brt.pivot            = new Vector2(0.5f, 0f);
        brt.sizeDelta        = new Vector2(0f, 1f);
        brt.anchoredPosition = Vector2.zero;
        AddImage(border, C_BORDER_DIM);

        // Actor icon
        var iconGO = MakeGO("ActorIcon", _dialogueBoxGO.transform);
        var irt    = iconGO.GetComponent<RectTransform>();
        irt.anchorMin        = new Vector2(0f, 0.5f);
        irt.anchorMax        = new Vector2(0f, 0.5f);
        irt.pivot            = new Vector2(0f, 0.5f);
        irt.sizeDelta        = new Vector2(56f, 56f);
        irt.anchoredPosition = new Vector2(12f, 0f);
        _actorIconImg              = AddImage(iconGO, C_BG_MID);
        _actorIconImg.preserveAspect = true;

        // Actor name
        var nameGO = MakeGO("ActorName", _dialogueBoxGO.transform);
        var nrt    = nameGO.GetComponent<RectTransform>();
        nrt.anchorMin        = new Vector2(0f, 1f);
        nrt.anchorMax        = new Vector2(0f, 1f);
        nrt.pivot            = new Vector2(0f, 1f);
        nrt.sizeDelta        = new Vector2(340f, 32f);
        nrt.anchoredPosition = new Vector2(80f, -8f);
        _actorNameTMP              = AddTMP(nameGO, "COMMAND", 24f, C_ACCENT, FontStyles.Bold);
        _actorNameTMP.characterSpacing = 4f;

        // Dialogue text
        var textGO = MakeGO("DialogueText", _dialogueBoxGO.transform);
        var trt    = textGO.GetComponent<RectTransform>();
        trt.anchorMin = new Vector2(0f, 1f);
        trt.anchorMax = new Vector2(1f, 1f);
        trt.pivot     = new Vector2(0f, 1f);
        trt.offsetMin = new Vector2(80f, -128f);
        trt.offsetMax = new Vector2(-148f, -42f);
        _dialogueTextTMP                     = AddTMP(textGO, "", 22f, C_TEXT_BODY, FontStyles.Normal);
        _dialogueTextTMP.enableWordWrapping  = true;
        _dialogueTextTMP.overflowMode        = TextOverflowModes.Overflow;
        _dialogueTextTMP.lineSpacing         = 5f;

        // Cursor (child of DialogueText)
        var cursorGO = MakeGO("Cursor", textGO.transform);
        _cursorRT    = cursorGO.GetComponent<RectTransform>();
        _cursorRT.anchorMin = Vector2.zero;
        _cursorRT.anchorMax = Vector2.zero;
        _cursorRT.pivot     = Vector2.zero;
        _cursorRT.sizeDelta = new Vector2(7f, 11f);
        AddImage(cursorGO, C_ACCENT);

        // Timer bar BG
        var timerBG = MakeGO("TimerBar", _dialogueBoxGO.transform);
        var tbrt    = timerBG.GetComponent<RectTransform>();
        tbrt.anchorMin        = new Vector2(0f, 0f);
        tbrt.anchorMax        = new Vector2(1f, 0f);
        tbrt.pivot            = new Vector2(0.5f, 0f);
        tbrt.sizeDelta        = new Vector2(0f, 2f);
        tbrt.anchoredPosition = Vector2.zero;
        AddImage(timerBG, C_BORDER_DIM);

        // Timer bar fill
        var timerFill  = MakeGO("TimerBarFill", timerBG.transform);
        var tfRt       = timerFill.GetComponent<RectTransform>();
        tfRt.pivot     = new Vector2(0f, 0.5f);
        StretchFull(timerFill);
        _timerBarFillImg            = AddImage(timerFill, C_ACCENT);
        _timerBarFillImg.type       = Image.Type.Filled;
        _timerBarFillImg.fillMethod = Image.FillMethod.Horizontal;
        _timerBarFillImg.fillOrigin = (int)Image.OriginHorizontal.Left;
        _timerBarFillImg.fillAmount = 1f;

        // Continue prompt
        _continuePromptGO  = MakeGO("ContinuePrompt", _dialogueBoxGO.transform);
        var cprt           = _continuePromptGO.GetComponent<RectTransform>();
        cprt.anchorMin        = new Vector2(1f, 0f);
        cprt.anchorMax        = new Vector2(1f, 0f);
        cprt.pivot            = new Vector2(1f, 0f);
        cprt.sizeDelta        = new Vector2(152f, 26f);
        cprt.anchoredPosition = new Vector2(-12f, 6f);
        var cpTMP             = AddTMP(_continuePromptGO, "CONTINUE ▼", 19f, C_ACCENT, FontStyles.Normal);
        cpTMP.alignment       = TextAlignmentOptions.Right;
        cpTMP.color           = Alpha(C_ACCENT, 0.7f);
    }

    // ══════════════════════════════════════════════════════════
    //  BARS PANEL   (upper-left  — health + shield)
    //  SCORE PANEL  (upper-right — score only)
    // ══════════════════════════════════════════════════════════
    static void BuildStatsPanel(Transform parent)
    {
        // ── Health + Shield — upper-left ──────────────────────
        var barsPanel = MakeGO("BarsPanel", parent);
        var brt       = barsPanel.GetComponent<RectTransform>();
        brt.anchorMin        = new Vector2(0f, 1f);
        brt.anchorMax        = new Vector2(0f, 1f);
        brt.pivot            = new Vector2(0f, 1f);
        brt.sizeDelta        = new Vector2(260f, 72f);
        brt.anchoredPosition = new Vector2(14f, -12f);

        var barsVlg = barsPanel.AddComponent<VerticalLayoutGroup>();
        barsVlg.spacing                = 5f;
        barsVlg.childAlignment         = TextAnchor.UpperLeft;
        barsVlg.childControlWidth      = true;
        barsVlg.childControlHeight     = false;
        barsVlg.childForceExpandWidth  = true;
        barsVlg.childForceExpandHeight = false;

        (_healthFillImg, _healthValueTMP) = BuildThinBar(
            barsPanel.transform, "HealthRow", "HP", C_HEALTH, C_HEALTH_BG);
        (_shieldFillImg, _shieldValueTMP) = BuildThinBar(
            barsPanel.transform, "ShieldRow", "SH", C_SHIELD, C_SHIELD_BG);

        // ── Score — upper-right ───────────────────────────────
        var scorePanel = MakeGO("ScorePanel", parent);
        var srt        = scorePanel.GetComponent<RectTransform>();
        srt.anchorMin        = new Vector2(1f, 1f);
        srt.anchorMax        = new Vector2(1f, 1f);
        srt.pivot            = new Vector2(1f, 1f);
        srt.sizeDelta        = new Vector2(230f, 30f);
        srt.anchoredPosition = new Vector2(-14f, -12f);

        var sHlg = scorePanel.AddComponent<HorizontalLayoutGroup>();
        sHlg.childAlignment        = TextAnchor.MiddleRight;
        sHlg.childControlWidth     = false;
        sHlg.childControlHeight    = false;
        sHlg.childForceExpandWidth = false;
        sHlg.spacing               = 6f;

        var scoreLabelGO = MakeGO("ScoreLabel", scorePanel.transform);
        scoreLabelGO.GetComponent<RectTransform>().sizeDelta = new Vector2(70f, 26f);
        var slTMP = AddTMP(scoreLabelGO, "SCORE", 15f, C_LABEL_DIM, FontStyles.Normal);
        slTMP.alignment        = TextAlignmentOptions.Right;
        slTMP.characterSpacing = 3f;

        var scoreValueGO = MakeGO("ScoreValue", scorePanel.transform);
        scoreValueGO.GetComponent<RectTransform>().sizeDelta = new Vector2(164f, 26f);
        var svTMP = AddTMP(scoreValueGO, "000000", 28f, C_SCORE, FontStyles.Bold);
        svTMP.alignment        = TextAlignmentOptions.Right;
        svTMP.characterSpacing = 2f;
    }

    static (Image fill, TextMeshProUGUI value) BuildThinBar(
        Transform parent, string rowName, string labelStr, Color barColor, Color bgColor)
    {
        var row = MakeGO(rowName, parent);
        row.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 26f);

        var hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing                = 5f;
        hlg.childAlignment         = TextAnchor.MiddleRight;
        hlg.childControlWidth      = false;
        hlg.childControlHeight     = false;
        hlg.childForceExpandWidth  = false;

        var labelGO = MakeGO("Label", row.transform);
        labelGO.GetComponent<RectTransform>().sizeDelta = new Vector2(28f, 20f);
        var lTMP = AddTMP(labelGO, labelStr, 15f, Alpha(barColor, 0.55f), FontStyles.Normal);
        lTMP.alignment        = TextAlignmentOptions.Right;
        lTMP.characterSpacing = 1f;

        var barBG = MakeGO("BarBG", row.transform);
        barBG.GetComponent<RectTransform>().sizeDelta = new Vector2(140f, 10f);
        AddImage(barBG, bgColor);

        var fill   = MakeGO("Fill", barBG.transform);
        StretchFull(fill);
        var fillRt   = fill.GetComponent<RectTransform>();
        fillRt.pivot = new Vector2(0f, 0.5f);
        var fillImg         = AddImage(fill, barColor);
        fillImg.type        = Image.Type.Filled;
        fillImg.fillMethod  = Image.FillMethod.Horizontal;
        fillImg.fillOrigin  = (int)Image.OriginHorizontal.Left;
        fillImg.fillAmount  = 1f;

        var valGO = MakeGO("ValueText", row.transform);
        valGO.GetComponent<RectTransform>().sizeDelta = new Vector2(70f, 20f);
        var valTMP = AddTMP(valGO, "---", 17f, barColor, FontStyles.Normal);
        valTMP.alignment = TextAlignmentOptions.Right;

        return (fillImg, valTMP);
    }

    // ══════════════════════════════════════════════════════════
    //  WEAPON PANEL  (bottom-centre)
    //
    //  SmallSlotsContainer  — 4 compact slots, always visible
    //  WeaponHexDisplay     — large hex, revealed on weapon switch
    // ══════════════════════════════════════════════════════════
    static void BuildWeaponPanel(Transform parent)
    {
        var panel = MakeGO("WeaponPanel", parent);
        var rt    = panel.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0f);
        rt.anchorMax        = new Vector2(0.5f, 0f);
        rt.pivot            = new Vector2(0.5f, 0f);
        rt.sizeDelta        = new Vector2(368f, 90f);
        rt.anchoredPosition = new Vector2(0f, 8f);

        // ── Small slots ───────────────────────────────────────
        var slotsGO = MakeGO("SmallSlotsContainer", panel.transform);
        StretchFull(slotsGO);

        var hlg = slotsGO.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing                = 8f;
        hlg.childAlignment         = TextAnchor.LowerCenter;
        hlg.childControlWidth      = false;
        hlg.childControlHeight     = false;
        hlg.childForceExpandWidth  = false;

        for (int i = 0; i < 4; i++)
            _weaponSlotGOs[i] = BuildSmallWeaponSlot(slotsGO.transform, (i + 1).ToString(), i == 0);

        // ── Hex display (hidden by default) ───────────────────
        _hexDisplayGO = MakeGO("WeaponHexDisplay", panel.transform);
        var hexRt     = _hexDisplayGO.GetComponent<RectTransform>();
        hexRt.anchorMin        = new Vector2(0.5f, 0f);
        hexRt.anchorMax        = new Vector2(0.5f, 0f);
        hexRt.pivot            = new Vector2(0.5f, 0f);
        hexRt.sizeDelta        = new Vector2(136f, 128f);
        hexRt.anchoredPosition = Vector2.zero;

        // Hex background (assign hex sprite in Inspector)
        var hexBG    = MakeGO("HexBG", _hexDisplayGO.transform);
        StretchFull(hexBG);
        AddImage(hexBG, Alpha(C_BG_MID, 0.95f));

        var hexBorder   = MakeGO("HexBorder", _hexDisplayGO.transform);
        StretchFull(hexBorder);
        AddImage(hexBorder, Color.clear);
        var hexOutline         = hexBorder.AddComponent<Outline>();
        hexOutline.effectColor    = C_BORDER_ACT;
        hexOutline.effectDistance = new Vector2(2f, -2f);

        // Weapon name (top)
        var wnGO    = MakeGO("WeaponName", _hexDisplayGO.transform);
        var wnRt    = wnGO.GetComponent<RectTransform>();
        wnRt.anchorMin        = new Vector2(0f, 1f);
        wnRt.anchorMax        = new Vector2(1f, 1f);
        wnRt.pivot            = new Vector2(0.5f, 1f);
        wnRt.sizeDelta        = new Vector2(0f, 26f);
        wnRt.anchoredPosition = new Vector2(0f, -6f);
        _hexWeaponNameTMP             = AddTMP(wnGO, "PULSE CANNON", 15f, C_ACCENT, FontStyles.Bold);
        _hexWeaponNameTMP.alignment   = TextAlignmentOptions.Center;
        _hexWeaponNameTMP.characterSpacing = 3f;

        // Weapon icon (centre)
        var wiGO    = MakeGO("WeaponIcon", _hexDisplayGO.transform);
        var wiRt    = wiGO.GetComponent<RectTransform>();
        wiRt.anchorMin        = new Vector2(0.5f, 0.5f);
        wiRt.anchorMax        = new Vector2(0.5f, 0.5f);
        wiRt.pivot            = new Vector2(0.5f, 0.5f);
        wiRt.sizeDelta        = new Vector2(70f, 54f);
        wiRt.anchoredPosition = new Vector2(0f, -6f);
        _hexWeaponIconImg              = AddImage(wiGO, C_BORDER_ACT);
        _hexWeaponIconImg.preserveAspect = true;

        _hexDisplayGO.SetActive(false);
    }

    static GameObject BuildSmallWeaponSlot(Transform parent, string keyLabel, bool active)
    {
        var slot = MakeGO($"WeaponSlot_{keyLabel}", parent);
        slot.GetComponent<RectTransform>().sizeDelta = new Vector2(82f, 82f);
        slot.AddComponent<Button>();
        AddImage(slot, active ? C_BG_MID : Alpha(C_BG_MID, 0.55f));

        var outline         = slot.AddComponent<Outline>();
        outline.effectColor    = active ? C_BORDER_ACT : C_BORDER_DIM;
        outline.effectDistance = new Vector2(1f, -1f);

        // Weapon icon
        var wGO = MakeGO("WeaponIcon", slot.transform);
        var wRt = wGO.GetComponent<RectTransform>();
        wRt.anchorMin        = new Vector2(0.5f, 0.5f);
        wRt.anchorMax        = new Vector2(0.5f, 0.5f);
        wRt.pivot            = new Vector2(0.5f, 0.5f);
        wRt.sizeDelta        = new Vector2(52f, 38f);
        wRt.anchoredPosition = new Vector2(0f, 6f);
        AddImage(wGO, active ? C_ACCENT : C_BORDER_DIM).preserveAspect = true;

        // Key label
        var lGO = MakeGO("KeyLabel", slot.transform);
        var lRt = lGO.GetComponent<RectTransform>();
        lRt.anchorMin        = new Vector2(0f, 0f);
        lRt.anchorMax        = new Vector2(1f, 0f);
        lRt.pivot            = new Vector2(0.5f, 0f);
        lRt.sizeDelta        = new Vector2(0f, 15f);
        lRt.anchoredPosition = new Vector2(0f, 4f);
        var lTMP = AddTMP(lGO, $"[{keyLabel}]", 12f, active ? C_BORDER_ACT : C_EMPTY, FontStyles.Normal);
        lTMP.alignment        = TextAlignmentOptions.Center;
        lTMP.characterSpacing = 2f;

        // Active dot
        var dot = MakeGO("ActiveIndicator", slot.transform);
        var dRt = dot.GetComponent<RectTransform>();
        dRt.anchorMin        = new Vector2(0.5f, 0f);
        dRt.anchorMax        = new Vector2(0.5f, 0f);
        dRt.pivot            = new Vector2(0.5f, 0f);
        dRt.sizeDelta        = new Vector2(4f, 4f);
        dRt.anchoredPosition = new Vector2(0f, 1f);
        AddImage(dot, C_ACCENT);
        dot.SetActive(active);

        return slot;
    }

    // ══════════════════════════════════════════════════════════
    //  POWER-UP PANEL  (lower-right)
    //
    //  Two image layers:
    //    PowerupIcon_Dim  — icon at half alpha (always shows)
    //    PowerupIcon_Fill — same icon full alpha, fills bottom-to-top
    // ══════════════════════════════════════════════════════════
    static void BuildPowerupPanel(Transform parent)
    {
        var panel = MakeGO("PowerupPanel", parent);
        var rt    = panel.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(1f, 0f);
        rt.anchorMax        = new Vector2(1f, 0f);
        rt.pivot            = new Vector2(1f, 0f);
        rt.sizeDelta        = new Vector2(78f, 96f);
        rt.anchoredPosition = new Vector2(-14f, 8f);

        var bg = MakeGO("BG", panel.transform);
        StretchFull(bg);
        AddImage(bg, Alpha(C_BG_DEEP, 0.85f));
        var bgOutline         = bg.AddComponent<Outline>();
        bgOutline.effectColor    = C_BORDER_DIM;
        bgOutline.effectDistance = new Vector2(1f, -1f);

        // "[E]" label
        var klGO = MakeGO("KeyLabel", panel.transform);
        var klRt = klGO.GetComponent<RectTransform>();
        klRt.anchorMin        = new Vector2(0f, 1f);
        klRt.anchorMax        = new Vector2(1f, 1f);
        klRt.pivot            = new Vector2(0.5f, 1f);
        klRt.sizeDelta        = new Vector2(0f, 17f);
        klRt.anchoredPosition = new Vector2(0f, -3f);
        AddTMP(klGO, "[E]", 11f, C_LABEL_DIM, FontStyles.Normal).alignment = TextAlignmentOptions.Center;

        // Icon area
        var iaGO = MakeGO("IconArea", panel.transform);
        var iaRt = iaGO.GetComponent<RectTransform>();
        iaRt.anchorMin = new Vector2(0.1f, 0.1f);
        iaRt.anchorMax = new Vector2(0.9f, 0.82f);
        iaRt.offsetMin = Vector2.zero;
        iaRt.offsetMax = Vector2.zero;

        // Dim layer (always visible at half alpha)
        var dimGO    = MakeGO("PowerupIcon_Dim", iaGO.transform);
        StretchFull(dimGO);
        var dimImg   = AddImage(dimGO, Alpha(C_POWERUP, 0.32f));
        dimImg.preserveAspect = true;

        // Fill layer (full alpha, grows bottom-to-top)
        var fillGO     = MakeGO("PowerupIcon_Fill", iaGO.transform);
        StretchFull(fillGO);
        var fillRt     = fillGO.GetComponent<RectTransform>();
        fillRt.pivot   = new Vector2(0.5f, 0f);
        _powerupFillImg               = AddImage(fillGO, C_POWERUP);
        _powerupFillImg.preserveAspect = true;
        _powerupFillImg.type          = Image.Type.Filled;
        _powerupFillImg.fillMethod    = Image.FillMethod.Vertical;
        _powerupFillImg.fillOrigin    = (int)Image.OriginVertical.Bottom;
        _powerupFillImg.fillAmount    = 1f;

        // "READY" label
        var rrGO = MakeGO("ReadyLabel", panel.transform);
        var rrRt = rrGO.GetComponent<RectTransform>();
        rrRt.anchorMin        = new Vector2(0f, 0f);
        rrRt.anchorMax        = new Vector2(1f, 0f);
        rrRt.pivot            = new Vector2(0.5f, 0f);
        rrRt.sizeDelta        = new Vector2(0f, 13f);
        rrRt.anchoredPosition = new Vector2(0f, 2f);
        var rrTMP = AddTMP(rrGO, "READY", 10f, Alpha(C_POWERUP, 0.8f), FontStyles.Normal);
        rrTMP.alignment        = TextAlignmentOptions.Center;
        rrTMP.characterSpacing = 2f;
    }

    // ══════════════════════════════════════════════════════════
    //  SCRIPT AUTO-ASSIGNMENT
    // ══════════════════════════════════════════════════════════

    static void AssignDialogueSystem(GameObject dialogueBoxGO)
    {
        var ds = dialogueBoxGO.AddComponent<DialogueSystem>();
        var so = new SerializedObject(ds);

        so.FindProperty("dialogueBox")   .objectReferenceValue = dialogueBoxGO;
        so.FindProperty("actorNameText") .objectReferenceValue = _actorNameTMP;
        so.FindProperty("actorIcon")     .objectReferenceValue = _actorIconImg;
        so.FindProperty("dialogueText")  .objectReferenceValue = _dialogueTextTMP;
        so.FindProperty("cursor")        .objectReferenceValue = _cursorRT;
        so.FindProperty("timerBarFill")  .objectReferenceValue = _timerBarFillImg;
        so.FindProperty("continuePrompt").objectReferenceValue = _continuePromptGO;

        so.ApplyModifiedProperties();
        Debug.Log("[DialogueHUDBuilder] DialogueSystem references assigned.");
    }

    static void AssignPlayerHUD(GameObject hudGO)
    {
        var hud = hudGO.AddComponent<PlayerHUD>();
        var so  = new SerializedObject(hud);

        so.FindProperty("healthFill")        .objectReferenceValue = _healthFillImg;
        so.FindProperty("healthText")        .objectReferenceValue = _healthValueTMP;
        so.FindProperty("shieldFill")        .objectReferenceValue = _shieldFillImg;
        so.FindProperty("shieldText")        .objectReferenceValue = _shieldValueTMP;
        so.FindProperty("shieldActiveColor") .colorValue           = C_SHIELD;
        so.FindProperty("shieldBrokenColor") .colorValue           = Alpha(C_SHIELD, 0.22f);
        // playerHealth is on the Player GameObject — assign in Inspector

        so.ApplyModifiedProperties();
        Debug.Log("[DialogueHUDBuilder] PlayerHUD references assigned (drag PlayerHealth manually).");
    }

    static void AssignWeaponHUDController(GameObject hudGO)
    {
        var ctrl = hudGO.AddComponent<WeaponHUDController>();
        var so   = new SerializedObject(ctrl);

        var slotsProp = so.FindProperty("weaponSlots");
        slotsProp.arraySize = 4;
        for (int i = 0; i < 4; i++)
            slotsProp.GetArrayElementAtIndex(i).objectReferenceValue = _weaponSlotGOs[i];

        so.FindProperty("hexDisplay")   .objectReferenceValue = _hexDisplayGO;
        so.FindProperty("hexWeaponIcon").objectReferenceValue = _hexWeaponIconImg;
        so.FindProperty("hexWeaponName").objectReferenceValue = _hexWeaponNameTMP;

        so.ApplyModifiedProperties();
        Debug.Log("[DialogueHUDBuilder] WeaponHUDController references assigned.");
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
        var rt       = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static Image AddImage(GameObject go, Color color)
    {
        var img   = go.AddComponent<Image>();
        img.color = color;
        return img;
    }

    static TextMeshProUGUI AddTMP(GameObject go, string text, float size, Color color, FontStyles style)
    {
        var tmp       = go.AddComponent<TextMeshProUGUI>();
        tmp.text      = text;
        tmp.fontSize  = size;
        tmp.color     = color;
        tmp.fontStyle = style;
        return tmp;
    }

    static Color Alpha(Color c, float a) { c.a = a; return c; }

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