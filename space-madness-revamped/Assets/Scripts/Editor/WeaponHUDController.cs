using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Handles weapon slot selection and the hex display transition.
///
/// Behaviour:
///   - Press 1/2/3/4 → highlights the matching slot, shows the hex display
///     for hexDisplayDuration seconds, then hides it again.
///   - The small slots container stays visible at all times except while
///     the hex is showing.
///
/// References are auto-assigned by DialogueHUDBuilder.
/// Weapon data (name, sprite) must be set via RegisterWeapon() or directly
/// in the inspector arrays before play.
/// </summary>
public class WeaponHUDController : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    //  Inspector
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Slot References (auto-assigned by builder)")]
    public GameObject[] weaponSlots = new GameObject[4];

    [Header("Hex Display (auto-assigned by builder)")]
    public GameObject      hexDisplay;
    public Image           hexWeaponIcon;
    public TextMeshProUGUI hexWeaponName;

    [Header("Weapon Data")]
    [Tooltip("Weapon name for each slot. Must match slot count.")]
    public string[] weaponNames  = { "PULSE CANNON", "SPREAD SHOT", "LASER BEAM", "MISSILE" };

    [Tooltip("Weapon icon sprite for each slot. Assign in Inspector.")]
    public Sprite[] weaponSprites = new Sprite[4];

    [Header("Timing")]
    [Tooltip("Seconds the hex display stays visible after switching weapons.")]
    [Min(0.5f)]
    public float hexDisplayDuration = 1.8f;

    // ─────────────────────────────────────────────────────────────────────────
    //  Events
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Fired when the player switches weapons. Parameter is the 0-based slot index.</summary>
    public event System.Action<int> OnWeaponSwitched;

    // ─────────────────────────────────────────────────────────────────────────
    //  Public State
    // ─────────────────────────────────────────────────────────────────────────

    public int ActiveSlotIndex { get; private set; } = 0;

    // ─────────────────────────────────────────────────────────────────────────
    //  Private
    // ─────────────────────────────────────────────────────────────────────────

    private GameObject _slotsContainer;
    private Coroutine  _hexCoroutine;

    // Cached per-slot components to avoid repeated GetComponent calls
    private Image[]            _slotBGs;
    private Outline[]          _slotOutlines;
    private Image[]            _slotWeaponIcons;
    private TextMeshProUGUI[]  _slotKeyLabels;
    private GameObject[]       _slotDots;

    private static readonly Color C_BG_ACTIVE   = Hex("#0D1A28");
    private static readonly Color C_BG_INACTIVE = new Color(0.051f, 0.102f, 0.157f, 0.55f);
    private static readonly Color C_BORDER_ACT  = Hex("#2A6EA6");
    private static readonly Color C_BORDER_DIM  = Hex("#1E3A5F");
    private static readonly Color C_ACCENT      = Hex("#4EB8E8");
    private static readonly Color C_EMPTY       = Hex("#1E3A5F");

    // ─────────────────────────────────────────────────────────────────────────
    //  Unity Lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (weaponSlots == null || weaponSlots.Length == 0)
        {
            Debug.LogError("[WeaponHUDController] No weapon slots assigned.", this);
            enabled = false;
            return;
        }

        // Cache the slots container (parent of the first slot)
        if (weaponSlots[0] != null)
            _slotsContainer = weaponSlots[0].transform.parent.gameObject;

        CacheSlotComponents();
        RefreshAllSlots();

        if (hexDisplay != null)
            hexDisplay.SetActive(false);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1)) SwitchToSlot(0);
        if (Input.GetKeyDown(KeyCode.Alpha2)) SwitchToSlot(1);
        if (Input.GetKeyDown(KeyCode.Alpha3)) SwitchToSlot(2);
        if (Input.GetKeyDown(KeyCode.Alpha4)) SwitchToSlot(3);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Public API
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Switches to the given slot index (0-based).
    /// Safe to call from external weapon systems.
    /// </summary>
    public void SwitchToSlot(int index)
    {
        if (index < 0 || index >= weaponSlots.Length) return;
        if (index == ActiveSlotIndex && hexDisplay != null && hexDisplay.activeSelf) return;

        ActiveSlotIndex = index;
        RefreshAllSlots();
        ShowHexDisplay(index);

        OnWeaponSwitched?.Invoke(index);
    }

    /// <summary>
    /// Registers weapon data at runtime (alternative to setting arrays in Inspector).
    /// </summary>
    public void RegisterWeapon(int index, string name, Sprite sprite)
    {
        if (index < 0 || index >= weaponSlots.Length) return;

        if (index < weaponNames.Length)  weaponNames[index]   = name;
        if (index < weaponSprites.Length) weaponSprites[index] = sprite;

        // Update slot icon immediately
        if (_slotWeaponIcons != null && index < _slotWeaponIcons.Length && _slotWeaponIcons[index] != null)
            _slotWeaponIcons[index].sprite = sprite;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Slot Visuals
    // ─────────────────────────────────────────────────────────────────────────

    private void RefreshAllSlots()
    {
        for (int i = 0; i < weaponSlots.Length; i++)
        {
            bool active = i == ActiveSlotIndex;

            if (_slotBGs != null && i < _slotBGs.Length && _slotBGs[i] != null)
                _slotBGs[i].color = active ? C_BG_ACTIVE : C_BG_INACTIVE;

            if (_slotOutlines != null && i < _slotOutlines.Length && _slotOutlines[i] != null)
                _slotOutlines[i].effectColor = active ? C_BORDER_ACT : C_BORDER_DIM;

            if (_slotWeaponIcons != null && i < _slotWeaponIcons.Length && _slotWeaponIcons[i] != null)
            {
                _slotWeaponIcons[i].color = active ? C_ACCENT : C_BORDER_DIM;
                if (i < weaponSprites.Length)
                    _slotWeaponIcons[i].sprite = weaponSprites[i];
            }

            if (_slotKeyLabels != null && i < _slotKeyLabels.Length && _slotKeyLabels[i] != null)
                _slotKeyLabels[i].color = active ? C_BORDER_ACT : C_EMPTY;

            if (_slotDots != null && i < _slotDots.Length && _slotDots[i] != null)
                _slotDots[i].SetActive(active);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Hex Display
    // ─────────────────────────────────────────────────────────────────────────

    private void ShowHexDisplay(int index)
    {
        if (hexDisplay == null) return;

        // Update hex content
        if (hexWeaponName != null)
            hexWeaponName.text = index < weaponNames.Length ? weaponNames[index] : "WEAPON";

        if (hexWeaponIcon != null && index < weaponSprites.Length)
            hexWeaponIcon.sprite = weaponSprites[index];

        // Restart the show/hide coroutine
        if (_hexCoroutine != null)
            StopCoroutine(_hexCoroutine);

        _hexCoroutine = StartCoroutine(HexDisplayRoutine());
    }

    private IEnumerator HexDisplayRoutine()
    {
        if (_slotsContainer != null)
            _slotsContainer.SetActive(false);

        hexDisplay.SetActive(true);

        yield return new WaitForSeconds(hexDisplayDuration);

        hexDisplay.SetActive(false);

        if (_slotsContainer != null)
            _slotsContainer.SetActive(true);

        _hexCoroutine = null;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Caching
    // ─────────────────────────────────────────────────────────────────────────

    private void CacheSlotComponents()
    {
        int count         = weaponSlots.Length;
        _slotBGs          = new Image[count];
        _slotOutlines     = new Outline[count];
        _slotWeaponIcons  = new Image[count];
        _slotKeyLabels    = new TextMeshProUGUI[count];
        _slotDots         = new GameObject[count];

        for (int i = 0; i < count; i++)
        {
            if (weaponSlots[i] == null) continue;

            _slotBGs[i]     = weaponSlots[i].GetComponent<Image>();
            _slotOutlines[i]= weaponSlots[i].GetComponent<Outline>();

            var iconT = weaponSlots[i].transform.Find("WeaponIcon");
            if (iconT != null) _slotWeaponIcons[i] = iconT.GetComponent<Image>();

            var labelT = weaponSlots[i].transform.Find("KeyLabel");
            if (labelT != null) _slotKeyLabels[i] = labelT.GetComponent<TextMeshProUGUI>();

            var dotT = weaponSlots[i].transform.Find("ActiveIndicator");
            if (dotT != null) _slotDots[i] = dotT.gameObject;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Helpers
    // ─────────────────────────────────────────────────────────────────────────

    static Color Hex(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color c);
        return c;
    }
}