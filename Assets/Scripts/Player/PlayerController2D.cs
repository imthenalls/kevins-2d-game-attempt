using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(EntityStats))]
public class PlayerController2D : MonoBehaviour, IEntityController
{
    [Header("Top-Down Movement")]
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private bool lockRotation = true;
    [SerializeField] private bool forceNoGravity = true;

    private Rigidbody2D rb;
    private Vector2 moveInput;
    private bool movementEnabled = true;

    public string     DisplayName     => gameObject.name;
    public EntityStats Stats          { get; private set; }
    public Combatant   Combatant      { get; private set; }
    public bool        MovementEnabled => movementEnabled;

    private void Awake()
    {
        rb       = GetComponent<Rigidbody2D>();
        Stats    = GetComponent<EntityStats>();
        Combatant = GetComponent<Combatant>(); // may be null if Combatant is not added

        if (forceNoGravity)
        {
            rb.gravityScale = 0f;
        }

        if (lockRotation)
        {
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        }
    }

    private void Update()
    {
        if (!movementEnabled)
        {
            moveInput = Vector2.zero;
            return;
        }

#if ENABLE_INPUT_SYSTEM
        Vector2 input = Vector2.zero;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
            {
                input.x -= 1f;
            }
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
            {
                input.x += 1f;
            }
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed)
            {
                input.y -= 1f;
            }
            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed)
            {
                input.y += 1f;
            }
        }

        if (Gamepad.current != null)
        {
            input += Gamepad.current.leftStick.ReadValue();
        }

        moveInput = Vector2.ClampMagnitude(input, 1f);
#else
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");
        moveInput = new Vector2(horizontal, vertical).normalized;
#endif
    }

    private void FixedUpdate()
    {
        rb.linearVelocity = movementEnabled ? moveInput * moveSpeed : Vector2.zero;
    }

    public void SetMovementEnabled(bool enabled)
    {
        movementEnabled = enabled;
        if (!movementEnabled)
        {
            moveInput = Vector2.zero;
            rb.linearVelocity = Vector2.zero;
        }
    }
}
