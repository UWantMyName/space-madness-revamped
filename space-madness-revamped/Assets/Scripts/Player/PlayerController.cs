using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 8f;
    public float mouseFollowSpeed = 20f;

    [Header("Bounds")]
    public float xMin = -8f;
    public float xMax = 8f;
    public float yMin = -4.5f;
    public float yMax = 4.5f;

    private enum InputMode { Keyboard, Mouse }
    private InputMode currentMode = InputMode.Keyboard;

    void Update()
    {
        DetectInputMode();

        if (currentMode == InputMode.Keyboard)
            HandleKeyboard();
        else
            HandleMouse();

        ClampPosition();
    }

    void DetectInputMode()
    {
        if (Input.GetAxisRaw("Horizontal") != 0 || Input.GetAxisRaw("Vertical") != 0)
            currentMode = InputMode.Keyboard;

        if (Input.GetAxis("Mouse X") != 0 || Input.GetAxis("Mouse Y") != 0)
            currentMode = InputMode.Mouse;
    }

    void HandleKeyboard()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        Vector3 dir = new Vector3(h, v, 0f).normalized;
        transform.position += dir * moveSpeed * Time.deltaTime;
    }

    void HandleMouse()
    {
        Vector3 target = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        target.z = 0f;
        transform.position = Vector3.Lerp(transform.position, target, mouseFollowSpeed * Time.deltaTime);
    }

    void ClampPosition()
    {
        transform.position = new Vector3(
            Mathf.Clamp(transform.position.x, xMin, xMax),
            Mathf.Clamp(transform.position.y, yMin, yMax),
            0f
        );
    }
}