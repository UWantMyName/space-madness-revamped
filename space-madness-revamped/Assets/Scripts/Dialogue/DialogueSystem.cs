// ============================================================
//  DialogueSystem.cs
//  Attach to the DialogueBox GameObject inside DialogueLayer.
//
//  Inspector wiring (drag from your Canvas hierarchy):
//    - dialogueLayer      → DialogueLayer
//    - actorNameText      → DialogueBox/ActorName
//    - actorIcon          → DialogueBox/ActorIcon
//    - dialogueText       → DialogueBox/DialogueText
//    - cursor             → DialogueBox/DialogueText/Cursor
//    - timerBarFill       → DialogueBox/TimerBar/TimerBarFill
//    - continuePrompt     → DialogueBox/ContinuePrompt
// ============================================================

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// ── Data ──────────────────────────────────────────────────────────────────────

[Serializable]
public class DialogueLine
{
    public string actorName;
    public Sprite actorIcon;
    public Color  accentColor = Color.white;
    [TextArea(2, 5)]
    public string text;
}

[CreateAssetMenu(fileName = "DialogueSequence", menuName = "Dialogue/Sequence")]
public class DialogueSequence : ScriptableObject
{
    public List<DialogueLine> lines = new();
}

// ── System ────────────────────────────────────────────────────────────────────

public class DialogueSystem : MonoBehaviour
{
    // ── Inspector refs ───────────────────────────────────────
    [Header("UI References")]
    [Tooltip("The DialogueBox GameObject (child of DialogueLayer — this gets toggled on/off)")]
    public GameObject        dialogueBox;

    [Tooltip("TMP text showing the actor's name")]
    public TextMeshProUGUI   actorNameText;

    [Tooltip("Image showing the actor's portrait/icon")]
    public Image             actorIcon;

    [Tooltip("TMP text where dialogue is typed out")]
    public TextMeshProUGUI   dialogueText;

    [Tooltip("Small blinking cursor block (child of DialogueText)")]
    public RectTransform     cursor;

    [Tooltip("The filled Image inside TimerBar — set Fill Method: Horizontal")]
    public Image             timerBarFill;

    [Tooltip("'CONTINUE ▼' label shown after typing finishes")]
    public GameObject        continuePrompt;

    [Header("Timing")]
    [Tooltip("Characters revealed per second")]
    public float typingSpeed     = 35f;

    [Tooltip("Seconds to wait after typing before auto-advancing")]
    public float autoAdvanceDelay = 3f;

    [Tooltip("Cursor blink interval in seconds")]
    public float cursorBlinkRate = 0.5f;

    // ── Events ───────────────────────────────────────────────
    /// <summary>Fired when the last line has been shown and dismissed.</summary>
    public event Action OnSequenceComplete;

    /// <summary>Fired every time a new line starts (index of that line passed in).</summary>
    public event Action<int> OnLineStart;

    // ── Private state ────────────────────────────────────────
    private DialogueSequence _sequence;
    private int              _lineIndex;
    private bool             _isTyping;
    private bool             _isActive;

    private Coroutine        _typingCoroutine;
    private Coroutine        _timerCoroutine;
    private Coroutine        _cursorCoroutine;

    // ── Unity ────────────────────────────────────────────────
    private void Awake()
    {
        // Safety: hide everything on startup
        if (dialogueBox != null)
            dialogueBox.SetActive(false);
    }

    private void Update()
    {
        if (!_isActive) return;

        if (Input.GetKeyDown(KeyCode.Space)   ||
            Input.GetKeyDown(KeyCode.Escape)   ||
            Input.GetMouseButtonDown(0))
        {
            Skip();
        }
    }

    // ── Public API ───────────────────────────────────────────

    /// <summary>
    /// Start playing a list of DialogueLines built at runtime.
    /// Called by DialogueAdapter — no ScriptableObject asset needed.
    /// </summary>
    public void Play(List<DialogueLine> lines)
    {
        if (lines == null || lines.Count == 0)
        {
            Debug.LogWarning("[DialogueSystem] Tried to Play a null or empty line list.");
            return;
        }

        var runtimeSequence       = ScriptableObject.CreateInstance<DialogueSequence>();
        runtimeSequence.lines     = lines;
        Play(runtimeSequence);
    }

    /// <summary>Start playing a DialogueSequence.</summary>
    public void Play(DialogueSequence sequence)
    {
        if (sequence == null || sequence.lines.Count == 0)
        {
            Debug.LogWarning("[DialogueSystem] Tried to Play a null or empty sequence.");
            return;
        }

        StopAllDialogueCoroutines();

        _sequence  = sequence;
        _lineIndex = 0;
        _isActive  = true;

        dialogueBox.SetActive(true);
        ShowLine(_lineIndex);
    }

    /// <summary>
    /// Skip logic:
    ///   - If typing  → complete text immediately, start the auto-advance timer.
    ///   - If waiting → advance to next line immediately.
    /// </summary>
    public void Skip()
    {
        if (_isTyping)
            CompleteCurrentLine();
        else
            AdvanceLine();
    }

    /// <summary>Hide the dialogue box and stop everything.</summary>
    public void Hide()
    {
        StopAllDialogueCoroutines();
        _isActive = false;
        dialogueBox.SetActive(false);
    }

    // ── Core flow ────────────────────────────────────────────

    private void ShowLine(int index)
    {
        DialogueLine line = _sequence.lines[index];
        OnLineStart?.Invoke(index);

        // ── Apply actor data ──────────────────────────────
        actorNameText.text  = line.actorName;
        actorNameText.color = line.accentColor;

        if (line.actorIcon != null)
        {
            actorIcon.sprite  = line.actorIcon;
            actorIcon.color   = Color.white;
        }
        else
        {
            actorIcon.color = new Color(0, 0, 0, 0); // hide if no sprite
        }

        // ── Reset text ────────────────────────────────────
        dialogueText.text = string.Empty;

        // ── Reset timer bar ───────────────────────────────
        SetTimerBar(1f);

        // ── Reset UI state ────────────────────────────────
        SetCursor(visible: true, color: line.accentColor);
        SetContinuePrompt(false);

        // ── Start typewriter ──────────────────────────────
        _typingCoroutine = StartCoroutine(TypeLine(line));
    }

    private IEnumerator TypeLine(DialogueLine line)
    {
        _isTyping = true;

        float interval = 1f / Mathf.Max(1f, typingSpeed);
        int   length   = line.text.Length;

        for (int i = 1; i <= length; i++)
        {
            dialogueText.text = line.text[..i];
            PositionCursor();
            yield return new WaitForSeconds(interval);
        }

        _isTyping = false;
        OnTypingComplete(line);
    }

    private void OnTypingComplete(DialogueLine line)
    {
        // Freeze cursor (stop blink, hide it)
        StopCursorBlink();
        SetCursor(visible: false, color: line.accentColor);

        SetContinuePrompt(true);

        // Start countdown to auto-advance
        _timerCoroutine = StartCoroutine(AutoAdvanceCountdown());
    }

    private IEnumerator AutoAdvanceCountdown()
    {
        float elapsed = 0f;

        while (elapsed < autoAdvanceDelay)
        {
            elapsed += Time.deltaTime;
            SetTimerBar(1f - (elapsed / autoAdvanceDelay));
            yield return null;
        }

        AdvanceLine();
    }

    private void CompleteCurrentLine()
    {
        // Stop the typewriter mid-way
        if (_typingCoroutine != null)
        {
            StopCoroutine(_typingCoroutine);
            _typingCoroutine = null;
        }

        _isTyping = false;

        // Show full text immediately
        dialogueText.text = _sequence.lines[_lineIndex].text;
        PositionCursor();

        OnTypingComplete(_sequence.lines[_lineIndex]);
    }

    private void AdvanceLine()
    {
        // Cancel any running timer
        if (_timerCoroutine != null)
        {
            StopCoroutine(_timerCoroutine);
            _timerCoroutine = null;
        }

        _lineIndex++;

        if (_lineIndex >= _sequence.lines.Count)
            FinishSequence();
        else
            ShowLine(_lineIndex);
    }

    private void FinishSequence()
    {
        Hide();
        OnSequenceComplete?.Invoke();
    }

    // ── Cursor helpers ───────────────────────────────────────

    private void PositionCursor()
    {
        if (cursor == null) return;

        // Place cursor right after the last character using TMP's text info
        dialogueText.ForceMeshUpdate();

        TMP_TextInfo info = dialogueText.textInfo;
        if (info.characterCount == 0)
        {
            cursor.anchoredPosition = Vector2.zero;
            return;
        }

        int lastCharIndex = info.characterCount - 1;
        TMP_CharacterInfo lastChar = info.characterInfo[lastCharIndex];

        // topRight of the last visible character
        Vector3 worldPos = lastChar.topRight;
        Vector2 localPos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            dialogueText.rectTransform,
            RectTransformUtility.WorldToScreenPoint(null, worldPos),
            null,
            out localPos
        );

        cursor.anchoredPosition = new Vector2(localPos.x + 2f, localPos.y);
    }

    private void SetCursor(bool visible, Color color)
    {
        if (cursor == null) return;
        cursor.gameObject.SetActive(visible);
        var img = cursor.GetComponent<Image>();
        if (img != null) img.color = color;

        if (visible)
        {
            StopCursorBlink();
            _cursorCoroutine = StartCoroutine(BlinkCursor());
        }
    }

    private IEnumerator BlinkCursor()
    {
        var img = cursor.GetComponent<Image>();
        while (true)
        {
            if (img != null) img.enabled = !img.enabled;
            yield return new WaitForSeconds(cursorBlinkRate);
        }
    }

    private void StopCursorBlink()
    {
        if (_cursorCoroutine != null)
        {
            StopCoroutine(_cursorCoroutine);
            _cursorCoroutine = null;
        }

        // Make sure the image is left in a visible state when blink stops
        if (cursor != null)
        {
            var img = cursor.GetComponent<Image>();
            if (img != null) img.enabled = true;
        }
    }

    // ── Timer bar ────────────────────────────────────────────

    private void SetTimerBar(float t)
    {
        if (timerBarFill != null)
            timerBarFill.fillAmount = Mathf.Clamp01(t);
    }

    // ── Continue prompt ──────────────────────────────────────

    private void SetContinuePrompt(bool show)
    {
        if (continuePrompt != null)
            continuePrompt.SetActive(show);
    }

    // ── Cleanup ──────────────────────────────────────────────

    private void StopAllDialogueCoroutines()
    {
        if (_typingCoroutine  != null) { StopCoroutine(_typingCoroutine);  _typingCoroutine  = null; }
        if (_timerCoroutine   != null) { StopCoroutine(_timerCoroutine);   _timerCoroutine   = null; }
        if (_cursorCoroutine  != null) { StopCoroutine(_cursorCoroutine);  _cursorCoroutine  = null; }
        _isTyping = false;
    }
}