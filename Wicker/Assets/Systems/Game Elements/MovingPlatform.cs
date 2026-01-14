using UnityEngine;
using System.Collections.Generic;

public enum PlatformMovementType
{
    Loop,
    PingPong
}

public enum PlatformEasingType
{
    Linear,
    SmoothStep,
    EaseInOut
}

[RequireComponent(typeof(Rigidbody2D))]
public class MovingPlatform : MonoBehaviour
{
    [Header("Waypoints")]
    [Tooltip("List of waypoints in world space the platform will move between")]
    public List<Vector3> waypoints = new List<Vector3>();

    [Header("Movement Settings")]
    [SerializeField] private PlatformMovementType movementType = PlatformMovementType.Loop;
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private float pauseDuration = 0.5f;
    [SerializeField] private PlatformEasingType easingType = PlatformEasingType.Linear;
    [SerializeField] private bool startMovingOnAwake = true;

    [Header("Debug")]
    [SerializeField] private bool showWaypoints = true;
    [SerializeField] private Color waypointColor = Color.cyan;
    [SerializeField] private bool showPath = true;
    [SerializeField] private Color pathColor = Color.yellow;

    // Runtime state
    private Rigidbody2D rb;
    private int currentWaypointIndex = 0;
    private float journeyProgress = 0f;
    private bool isMoving = true;
    private bool isPaused = false;
    private float pauseTimer = 0f;
    private bool isReversing = false;

    // For tracking movement between frames (used for passenger movement)
    private Vector3 previousPosition;
    private Vector2 platformVelocity;

    // Passengers on the platform - simplified: just track Transforms
    private List<Transform> passengers = new List<Transform>();

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        // Ensure Rigidbody2D is set up correctly for kinematic platform
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.simulated = true;

        // Set initial position
        if (waypoints.Count == 0)
        {
            waypoints.Add(transform.position);
            waypoints.Add(transform.position + Vector3.right * 3f);
        }

        transform.position = waypoints[0];
        previousPosition = transform.position;
    }

    void Start()
    {
        isMoving = startMovingOnAwake;
    }

    void FixedUpdate()
    {
        if (!isMoving || waypoints.Count < 2) return;

        // Track previous position before moving
        previousPosition = transform.position;

        if (isPaused)
        {
            pauseTimer -= Time.fixedDeltaTime;
            if (pauseTimer <= 0f)
            {
                isPaused = false;
            }
            platformVelocity = Vector2.zero;
        }
        else
        {
            MovePlatform();

            // Calculate platform velocity for this frame
            platformVelocity = (transform.position - previousPosition) / Time.fixedDeltaTime;
        }

        // Move passengers with the platform
        MovePassengersWithPlatform();
    }

    private void MovePlatform()
    {
        Vector3 startPoint = waypoints[currentWaypointIndex];
        Vector3 endPoint = GetNextWaypoint();

        float distance = Vector3.Distance(startPoint, endPoint);
        if (distance <= 0.001f)
        {
            HandleWaypointReached();
            return;
        }

        // Calculate progress based on speed and distance
        float progressDelta = (moveSpeed / distance) * Time.fixedDeltaTime;
        journeyProgress = Mathf.Clamp01(journeyProgress + progressDelta);

        // Apply easing
        float easedProgress = ApplyEasing(journeyProgress);

        // Move platform
        transform.position = Vector3.Lerp(startPoint, endPoint, easedProgress);

        // Check if we reached the waypoint
        if (journeyProgress >= 1f)
        {
            HandleWaypointReached();
        }
    }

    private Vector3 GetNextWaypoint()
    {
        if (movementType == PlatformMovementType.PingPong && isReversing)
        {
            return waypoints[currentWaypointIndex - 1];
        }
        else
        {
            int nextIndex = (currentWaypointIndex + 1) % waypoints.Count;
            return waypoints[nextIndex];
        }
    }

    private void HandleWaypointReached()
    {
        // Update current waypoint index
        if (movementType == PlatformMovementType.PingPong)
        {
            if (isReversing)
            {
                currentWaypointIndex--;
                if (currentWaypointIndex <= 0)
                {
                    currentWaypointIndex = 0;
                    isReversing = false;
                }
            }
            else
            {
                currentWaypointIndex++;
                if (currentWaypointIndex >= waypoints.Count - 1)
                {
                    currentWaypointIndex = waypoints.Count - 1;
                    isReversing = true;
                }
            }
        }
        else // Loop
        {
            currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Count;
        }

        // Reset progress and pause if needed
        journeyProgress = 0f;

        if (pauseDuration > 0f)
        {
            isPaused = true;
            pauseTimer = pauseDuration;
            platformVelocity = Vector2.zero;
        }
    }

    private float ApplyEasing(float t)
    {
        switch (easingType)
        {
            case PlatformEasingType.SmoothStep:
                return t * t * (3f - 2f * t);
            case PlatformEasingType.EaseInOut:
                return t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;
            case PlatformEasingType.Linear:
            default:
                return t;
        }
    }

    private void MovePassengersWithPlatform()
    {
        // Calculate platform movement for this frame
        Vector3 deltaPosition = transform.position - previousPosition;

        for (int i = passengers.Count - 1; i >= 0; i--)
        {
            Transform passenger = passengers[i];

            if (passenger == null)
            {
                passengers.RemoveAt(i);
                continue;
            }

            // For characters with CharacterMovement, set platform velocity for calculations
            CharacterMovement movement = passenger.GetComponent<CharacterMovement>();
            if (movement != null)
            {
                movement.SetPlatformVelocity(platformVelocity);
            }
            else
            {
                // Move non-character passengers by position delta
                passenger.position += deltaPosition;
            }
        }
    }

    private void CleanupPassengers()
    {
        // Remove any null transforms
        passengers.RemoveAll(passenger => passenger == null);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Check if this object has a Rigidbody2D and isn't already a passenger
        Rigidbody2D otherRb = other.attachedRigidbody;
        if (otherRb != null && otherRb != rb) // Don't add platform to itself
        {
            Transform passenger = otherRb.transform;
            if (!passengers.Contains(passenger))
            {
                passengers.Add(passenger);

                // If it's a character, raise event
                CharacterCore characterCore = passenger.GetComponent<CharacterCore>();
                if (characterCore != null)
                {
                    characterCore.RaiseEvent("on_moving_platform", this);
                }
            }
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        Rigidbody2D otherRb = other.attachedRigidbody;
        if (otherRb != null)
        {
            Transform passenger = otherRb.transform;
            if (passengers.Contains(passenger))
            {
                // If it's a character, raise event and clear platform velocity
                CharacterCore characterCore = passenger.GetComponent<CharacterCore>();
                if (characterCore != null)
                {
                    characterCore.RaiseEvent("off_moving_platform", this);

                    CharacterMovement movement = passenger.GetComponent<CharacterMovement>();
                    if (movement != null)
                    {
                        movement.SetPlatformVelocity(Vector2.zero);
                    }
                }

                passengers.Remove(passenger);
            }
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        // Also handle collision-based passengers (for non-trigger colliders)
        Rigidbody2D otherRb = collision.rigidbody;
        if (otherRb != null && otherRb != rb)
        {
            Transform passenger = otherRb.transform;

            // Check if passenger is above platform (so boxes don't get stuck on sides)
            bool isAbovePlatform = collision.transform.position.y > transform.position.y;

            if (isAbovePlatform && !passengers.Contains(passenger))
            {
                passengers.Add(passenger);

                // If it's a character, raise event
                CharacterCore characterCore = passenger.GetComponent<CharacterCore>();
                if (characterCore != null)
                {
                    characterCore.RaiseEvent("on_moving_platform", this);
                }
            }
        }
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        Rigidbody2D otherRb = collision.rigidbody;
        if (otherRb != null)
        {
            Transform passenger = otherRb.transform;
            if (passengers.Contains(passenger))
            {
                // If it's a character, raise event and clear platform velocity
                CharacterCore characterCore = passenger.GetComponent<CharacterCore>();
                if (characterCore != null)
                {
                    characterCore.RaiseEvent("off_moving_platform", this);

                    CharacterMovement movement = passenger.GetComponent<CharacterMovement>();
                    if (movement != null)
                    {
                        movement.SetPlatformVelocity(Vector2.zero);
                    }
                }

                passengers.Remove(passenger);
            }
        }
    }

    // Public methods for controlling the platform
    public void StartMoving()
    {
        isMoving = true;
    }

    public void StopMoving()
    {
        isMoving = false;
        platformVelocity = Vector2.zero;

        // Clear platform velocity for all character passengers when platform stops
        foreach (Transform passenger in passengers)
        {
            if (passenger != null)
            {
                CharacterMovement movement = passenger.GetComponent<CharacterMovement>();
                if (movement != null)
                {
                    movement.SetPlatformVelocity(Vector2.zero);
                }
            }
        }

        CleanupPassengers();
    }

    public void Pause(float duration = -1f)
    {
        isPaused = true;
        pauseTimer = duration > 0f ? duration : pauseDuration;
        platformVelocity = Vector2.zero;

        // Clear platform velocity for all character passengers when platform pauses
        foreach (Transform passenger in passengers)
        {
            if (passenger != null)
            {
                CharacterMovement movement = passenger.GetComponent<CharacterMovement>();
                if (movement != null)
                {
                    movement.SetPlatformVelocity(Vector2.zero);
                }
            }
        }
    }

    public void Resume()
    {
        isPaused = false;
    }

    public void SetWaypoints(List<Vector3> newWaypoints)
    {
        if (newWaypoints.Count > 0)
        {
            waypoints = newWaypoints;
            ResetToStart();
        }
    }

    public void ResetToStart()
    {
        currentWaypointIndex = 0;
        journeyProgress = 0f;
        isReversing = false;
        isPaused = false;
        transform.position = waypoints[0];
        previousPosition = transform.position;
        platformVelocity = Vector2.zero;

        // Clear all passengers
        passengers.Clear();
    }

    public Vector2 GetPlatformVelocity()
    {
        return platformVelocity;
    }

    public bool IsMoving()
    {
        return isMoving && !isPaused;
    }

    public List<Transform> GetCurrentPassengers()
    {
        CleanupPassengers();
        return new List<Transform>(passengers);
    }

    // For debugging and editor visualization
    void OnDrawGizmosSelected()
    {
        if (!showWaypoints && !showPath) return;

        // Draw waypoints
        if (showWaypoints && waypoints != null)
        {
            Gizmos.color = waypointColor;
            for (int i = 0; i < waypoints.Count; i++)
            {
                Gizmos.DrawSphere(waypoints[i], 0.2f);
                Gizmos.DrawWireSphere(waypoints[i], 0.25f);

                // Draw index number
#if UNITY_EDITOR
                UnityEditor.Handles.Label(waypoints[i] + Vector3.up * 0.3f, i.ToString());
#endif
            }
        }

        // Draw path lines
        if (showPath && waypoints.Count > 1)
        {
            Gizmos.color = pathColor;
            for (int i = 0; i < waypoints.Count; i++)
            {
                int nextIndex = (i + 1) % waypoints.Count;

                // For ping-pong mode, don't draw line from last to first
                if (movementType == PlatformMovementType.PingPong && i == waypoints.Count - 1)
                    continue;

                Gizmos.DrawLine(waypoints[i], waypoints[nextIndex]);
            }
        }
    }
}