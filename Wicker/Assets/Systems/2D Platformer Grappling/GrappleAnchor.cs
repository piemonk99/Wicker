using UnityEngine;

/// <summary>
/// Marks a GameObject as a valid grapple anchor point.
/// Can be manually placed or automatically created on grapple impact.
/// </summary>
public class GrappleAnchor : MonoBehaviour
{
    [Header("Anchor Settings")]
    public Vector2 localAnchorOffset = Vector2.zero;

    [Tooltip("When true, uses the predefined anchor position. When false, repositions to exact grapple impact point.")]
    public bool usePredefinedPosition = true;

    [Tooltip("Whether this anchor can receive reaction forces when grappled (requires rigidbody and compatible grapple)")]
    public bool canReceiveReactionForces = false;

    [Header("Anchor Delegation")]
    [Tooltip("Optional: If set, grapples to this object will use the anchor on the referenced GameObject instead")]
    public Transform delegatedAnchorTarget;

    [Header("Visual")]
    public bool showGizmo = true;
    [SerializeField] private Color gizmoColor = Color.magenta;
    [SerializeField] private float gizmoSize = 0.25f;

    // Runtime tracking
    private MovingPlatform platform;
    private Vector3 dynamicLocalOffset; // For auto-created anchors
    private Vector3 originalDynamicOffset; // Store original for reset

    public MovingPlatform Platform => platform;
    public bool IsOnMovingPlatform => platform != null;
    public bool IsAutoCreated { get; private set; } = false;

    // Properties for external access
    public bool CanReceiveReactionForces => canReceiveReactionForces;
    public bool UsePredefinedPosition => usePredefinedPosition;

    // Cache for performance
    private GrappleAnchor delegatedAnchorCache;
    private bool delegatedAnchorChecked = false;

    // For reaction force tracking
    private Rigidbody2D anchorRigidbody;
    public Rigidbody2D AnchorRigidbody
    {
        get
        {
            if (anchorRigidbody == null)
                anchorRigidbody = GetComponent<Rigidbody2D>();
            return anchorRigidbody;
        }
    }

    private void Awake()
    {
        // Find parent MovingPlatform if any
        platform = GetComponentInParent<MovingPlatform>();

        // Store original dynamic offset for potential reset
        originalDynamicOffset = dynamicLocalOffset;
    }

    /// <summary>
    /// Initialize this anchor with a world position (for auto-created anchors).
    /// </summary>
    public void Initialize(Vector2 worldPosition, bool autoCreated = false)
    {
        IsAutoCreated = autoCreated;

        // Calculate dynamic local offset from this transform's position
        dynamicLocalOffset = worldPosition - (Vector2)transform.position;
        originalDynamicOffset = dynamicLocalOffset;
    }

    /// <summary>
    /// Reposition the anchor to a new world position.
    /// Only works if usePredefinedPosition is false.
    /// </summary>
    public void RepositionTo(Vector2 worldPosition)
    {
        if (usePredefinedPosition)
        {
            Debug.LogWarning($"Cannot reposition anchor on {gameObject.name} - usePredefinedPosition is true");
            return;
        }

        dynamicLocalOffset = worldPosition - (Vector2)transform.position;
    }

    /// <summary>
    /// Reset the anchor to its original position.
    /// </summary>
    public void ResetPosition()
    {
        dynamicLocalOffset = originalDynamicOffset;
    }

    /// <summary>
    /// Get the current world position of this anchor.
    /// </summary>
    public Vector2 GetWorldPosition()
    {
        // Use manually configured offset
        return (Vector2)transform.position + localAnchorOffset + (Vector2)dynamicLocalOffset;
    }

    /// <summary>
    /// Get the effective anchor for this grapple point (handles delegation).
    /// </summary>
    public GrappleAnchor GetEffectiveAnchor()
    {
        // Check if we have a delegated anchor target
        if (delegatedAnchorTarget != null)
        {
            // Cache the result for performance
            if (!delegatedAnchorChecked)
            {
                delegatedAnchorCache = delegatedAnchorTarget.GetComponent<GrappleAnchor>();
                delegatedAnchorChecked = true;
            }

            if (delegatedAnchorCache != null)
            {
                return delegatedAnchorCache;
            }
        }

        return this;
    }

    /// <summary>
    /// Get the world position from the effective anchor (handles delegation).
    /// </summary>
    public Vector2 GetEffectiveWorldPosition()
    {
        return GetEffectiveAnchor().GetWorldPosition();
    }

    /// <summary>
    /// Get the platform's current velocity (if on a moving platform).
    /// </summary>
    public Vector2 GetPlatformVelocity()
    {
        return platform != null ? platform.GetPlatformVelocity() : Vector2.zero;
    }

    /// <summary>
    /// Get the effective platform (handles delegation).
    /// </summary>
    public MovingPlatform GetEffectivePlatform()
    {
        return GetEffectiveAnchor().Platform;
    }

    /// <summary>
    /// Check if this anchor can receive reaction forces (handles delegation).
    /// </summary>
    public bool GetCanReceiveReactionForces()
    {
        return GetEffectiveAnchor().canReceiveReactionForces;
    }

    /// <summary>
    /// Check if this anchor should use predefined position (handles delegation).
    /// </summary>
    public bool GetUsePredefinedPosition()
    {
        return GetEffectiveAnchor().usePredefinedPosition;
    }

    private void OnDrawGizmos()
    {
        if (!showGizmo) return;

        Gizmos.color = gizmoColor;
        Vector2 anchorPos = GetWorldPosition();

        // Draw anchor point
        Gizmos.DrawWireSphere(anchorPos, gizmoSize);

        // Draw connection line to parent if offset is used
        if (localAnchorOffset != Vector2.zero)
        {
            Gizmos.DrawLine(transform.position, anchorPos);
        }

        // Draw delegation connection if applicable
        if (delegatedAnchorTarget != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, delegatedAnchorTarget.position);

            // Draw arrow
            Vector2 direction = (delegatedAnchorTarget.position - transform.position).normalized;
            Vector2 arrowStart = transform.position + (Vector3)direction * 0.5f;
            Gizmos.DrawLine(arrowStart, arrowStart + (Vector2)(Quaternion.Euler(0, 0, 135) * direction * 0.2f));
            Gizmos.DrawLine(arrowStart, arrowStart + (Vector2)(Quaternion.Euler(0, 0, 225) * direction * 0.2f));
        }
    }

    /// <summary>
    /// Reset the delegation cache (call if delegatedAnchorTarget changes at runtime).
    /// </summary>
    public void ResetDelegationCache()
    {
        delegatedAnchorChecked = false;
        delegatedAnchorCache = null;
    }
}

/// <summary>
/// Static helper for creating and managing grapple anchors.
/// </summary>
public static class GrappleAnchorSystem
{
    /// <summary>
    /// Get or create a grapple anchor on the target GameObject, respecting grapple capabilities.
    /// </summary>
    public static GrappleAnchor GetOrCreateAnchor(GameObject target, Vector2 hitPoint, bool canCreateAnchors)
    {
        // Try to get existing anchor
        GrappleAnchor existingAnchor = target.GetComponent<GrappleAnchor>();

        if (existingAnchor != null)
        {
            // Check if this anchor delegates to another anchor
            GrappleAnchor effectiveAnchor = existingAnchor.GetEffectiveAnchor();

            // Check if the effective anchor requires predefined position
            if (!effectiveAnchor.usePredefinedPosition && !canCreateAnchors)
            {
                // Grapple cannot create anchors, and this anchor doesn't have a predefined position
                Debug.Log($"Grapple failed: Cannot grapple to {target.name} - anchor requires creation but grapple lacks capability");
                return null;
            }

            // Handle position preference
            if (!effectiveAnchor.usePredefinedPosition && canCreateAnchors)
            {
                // Reposition to exact hit point (grapple can create anchors)
                effectiveAnchor.RepositionTo(hitPoint);
            }
            else if (!effectiveAnchor.usePredefinedPosition)
            {
                // Grapple cannot create anchors, use default position or fail
                Debug.Log($"Grapple failed: Cannot reposition anchor on {target.name} - grapple lacks anchor creation capability");
                return null;
            }

            if (effectiveAnchor != existingAnchor)
            {
                // Return the delegated anchor instead
                return effectiveAnchor;
            }

            return existingAnchor;
        }

        // No existing anchor found
        if (!canCreateAnchors)
        {
            // Grapple cannot create anchors and no anchor exists
            Debug.Log($"Grapple failed: No anchor on {target.name} and grapple cannot create anchors");
            return null;
        }

        // Create new anchor component (grapple can create anchors)
        GrappleAnchor newAnchor = target.AddComponent<GrappleAnchor>();
        newAnchor.Initialize(hitPoint, true);
        newAnchor.showGizmo = false; // Don't show gizmos for auto-created anchors
        newAnchor.usePredefinedPosition = false; // Auto-created anchors use hit position

        Debug.Log($"Created new anchor on {target.name} at {hitPoint}");
        return newAnchor;
    }

    /// <summary>
    /// Get the effective anchor for a GameObject (handles delegation and auto-creation).
    /// </summary>
    public static GrappleAnchor GetEffectiveAnchor(GameObject target, Vector2 hitPoint, bool canCreateAnchors)
    {
        GrappleAnchor anchor = GetOrCreateAnchor(target, hitPoint, canCreateAnchors);
        return anchor?.GetEffectiveAnchor();
    }

    /// <summary>
    /// Check if a grapple can attach to a target at a specific point.
    /// </summary>
    public static bool CanGrappleToTarget(GameObject target, Vector2 hitPoint, bool canCreateAnchors)
    {
        GrappleAnchor existingAnchor = target.GetComponent<GrappleAnchor>();

        if (existingAnchor != null)
        {
            GrappleAnchor effectiveAnchor = existingAnchor.GetEffectiveAnchor();

            // Check if anchor requires predefined position but grapple can't create/reposition
            if (!effectiveAnchor.usePredefinedPosition && !canCreateAnchors)
            {
                return false;
            }

            return true;
        }

        // No existing anchor - check if grapple can create one
        return canCreateAnchors;
    }

    /// <summary>
    /// Clean up auto-created anchors when no longer needed.
    /// </summary>
    public static void CleanupAnchor(GrappleAnchor anchor)
    {
        if (anchor != null && anchor.IsAutoCreated)
        {
            // Reset position before cleanup (optional)
            anchor.ResetPosition();

            // Only destroy if this was auto-created
            Object.Destroy(anchor);
        }
    }
}