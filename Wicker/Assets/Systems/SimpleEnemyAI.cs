using UnityEngine;

public class SimpleEnemyAI : MonoBehaviour, ICharacterController
{
    [Header("Patrol Settings")]
    [SerializeField] private float directionChangeDelay = 0.5f;

    [Header("Platform Detection")]
    [SerializeField] private float forwardRaycastOffset = 0.5f;
    [SerializeField] private float groundCheckDistance = 0.5f;
    [SerializeField] private LayerMask groundLayer;

    // Controller state
    private CharacterCore character;
    private bool isEnabled = true;
    public bool IsEnabled => isEnabled;

    // AI state
    private float currentDirection = 1f;
    private float directionChangeTimer = 0f;

    // Reusable vector to avoid allocations
    private Vector2 moveInputVector = new Vector2();

    public void Initialize(CharacterCore characterCore)
    {
        this.character = characterCore;

        // Start moving in a random direction
        currentDirection = Random.value > 0.5f ? 1f : -1f;

        // Send initial movement
        moveInputVector.x = currentDirection;
        moveInputVector.y = 0;
        character.RaiseEvent("move_input", moveInputVector);
    }

    public void Enable() => isEnabled = true;
    public void Disable() => isEnabled = false;

    public void UpdateController(float deltaTime)
    {
        if (!isEnabled || character == null) return;

        // Timer updates in Update for accuracy
        directionChangeTimer -= deltaTime;
    }

    public void FixedUpdateController(float fixedDeltaTime)
    {
        if (!isEnabled || character == null) return;

        // Do patrol logic in physics tick for consistency
        Patrol();
    }

    private void Patrol()
    {
        // Check for platform edge
        bool hasGroundAhead = CheckGroundAhead();
        bool hasWallAhead = CheckWallAhead();

        // If no ground ahead or wall ahead, change direction
        if (!hasGroundAhead || hasWallAhead)
        {
            if (directionChangeTimer <= 0)
            {
                currentDirection *= -1;
                directionChangeTimer = directionChangeDelay;
            }
        }

        // Move in current direction
        moveInputVector.x = currentDirection;
        moveInputVector.y = 0;
        character.RaiseEvent("move_input", moveInputVector);

        // Update facing direction
        transform.localScale = new Vector3(
            Mathf.Sign(currentDirection) * Mathf.Abs(transform.localScale.x),
            transform.localScale.y,
            transform.localScale.z
        );
    }

    private bool CheckGroundAhead()
    {
        // Raycast forward and down to check for ground ahead
        Vector2 rayOrigin = (Vector2)transform.position +
            Vector2.right * currentDirection * forwardRaycastOffset;

        RaycastHit2D hit = Physics2D.Raycast(
            rayOrigin,
            Vector2.down,
            groundCheckDistance,
            groundLayer
        );

        return hit.collider != null;
    }

    private bool CheckWallAhead()
    {
        // Raycast forward to check for walls
        Vector2 rayOrigin = (Vector2)transform.position +
            Vector2.right * currentDirection * 0.2f;

        RaycastHit2D hit = Physics2D.Raycast(
            rayOrigin,
            Vector2.right * currentDirection,
            0.3f,
            groundLayer
        );

        return hit.collider != null;
    }

    private void OnDrawGizmos()
    {
        if (!isEnabled) return;

        // Draw ground check ray
        Gizmos.color = Color.red;
        Vector3 rayStart = transform.position + Vector3.right * currentDirection * forwardRaycastOffset;
        Gizmos.DrawLine(rayStart, rayStart + Vector3.down * groundCheckDistance);

        // Draw wall check ray
        Gizmos.color = Color.blue;
        Vector3 wallRayStart = transform.position + Vector3.right * currentDirection * 0.2f;
        Gizmos.DrawLine(wallRayStart, wallRayStart + Vector3.right * currentDirection * 0.3f);
    }
}