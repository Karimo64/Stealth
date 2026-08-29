using UnityEngine;
using UnityEngine.InputSystem;

// This script goes on the Player GameObject.
// It reads keyboard input every frame and moves the player using physics
// (Rigidbody2D), which is the correct way to move something that should
// also be able to collide with walls/enemies later.
//
// NOTE: Unity 6's default 2D template comes set up to use the NEW Input
// System package exclusively (not the old Input.GetAxis API), so this
// version uses UnityEngine.InputSystem's Keyboard class instead.
public class PlayerMovement : MonoBehaviour
{
    // [SerializeField] makes this visible/editable in the Inspector panel
    // in Unity, even though the variable itself is private. That way you
    // (or we) can tweak the speed without touching code.
    [SerializeField] private float moveSpeed = 5f;

    private Rigidbody2D rb;
    private Vector2 moveInput;

    // Awake() runs once, right when the object is created/scene loads.
    // We grab a reference to the Rigidbody2D component here so we don't
    // have to look it up again every frame (that would be wasteful).
    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    // Update() runs once per rendered frame. Input should always be read
    // here (not in FixedUpdate) so we never miss a key press.
    void Update()
    {
        moveInput = Vector2.zero;

        // Keyboard.current is the New Input System's way of asking "what
        // keyboard is connected right now". It can be null in rare cases
        // (no keyboard hardware), so we guard against that.
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) moveInput.x -= 1f;
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) moveInput.x += 1f;
        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) moveInput.y += 1f;
        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) moveInput.y -= 1f;

        // Without this, moving diagonally (two keys at once) would be
        // faster than moving straight, because we'd be adding a full
        // speed in both X and Y. Normalize() caps the total to speed 1.
        moveInput.Normalize();
    }

    // FixedUpdate() runs at a fixed time interval, independent of frame
    // rate. All physics changes (moving a Rigidbody2D) should happen here
    // for consistent, smooth movement on any computer.
    void FixedUpdate()
    {
        rb.MovePosition(rb.position + moveInput * moveSpeed * Time.fixedDeltaTime);
    }
}
