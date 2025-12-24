using UnityEngine;

public class SimpleEnemyAI : MonoBehaviour, ICharacterComponent
{
    [Header("Patrol Settings")]
    [SerializeField] private float patrolSpeed = 2f;
    [SerializeField] private float directionChangeDelay = 0.5f;

    [Header("Platform Detection")]
    [SerializeField] private float forwardRaycastOffset = 0.5f;
    [SerializeField] private float groundCheckDistance = 0.5f;
    [SerializeField] private LayerMask groundLayer;

    private CharacterCore character;
    private float currentDirection = 1f;
    private float directionChangeTimer = 0f;

    public void Initialize(CharacterCore core)
    {
        character = core;

        // Start moving in a random direction
        currentDirection = Random.value > 0.5f ? 1f : -1f;
        character.RaiseEvent("move_input", new Vector2(currentDirection, 0));
    }

    public void Tick(float deltaTime)
    {
        directionChangeTimer -= deltaTime;
        
    }

    public void PhysicsTick(float fixedDeltaTime)
    {
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

        // Move in current direction (scale speed appropriately)
        character.RaiseEvent("move_input", new Vector2(currentDirection, 0));

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