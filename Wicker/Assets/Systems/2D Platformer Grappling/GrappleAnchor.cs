using UnityEngine;

/// <summary>
/// Marks a GameObject as a valid grapple anchor point.
/// Can be manually placed or automatically created on grapple impact.
/// </summary>
public class GrappleAnchor : MonoBehaviour
{
    [Header("Anchor Settings")]
    public Vector2 localAnchorOffset = Vector2.zero;

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

    public MovingPlatform Platform => platform;
    public bool IsOnMovingPlatform => platform != null;
    public bool IsAutoCreated { get; private set; } = false;

    // Cache for performance
    private GrappleAnchor delegatedAnchorCache;
    private bool delegatedAnchorChecked = false;

    private void Awake()
    {
        // Find parent MovingPlatform if any
        platform = GetComponentInParent<MovingPlatform>();
    }

    /// <summary>
    /// Initialize this anchor with a world position (for auto-created anchors).
    /// </summary>
    public void Initialize(Vector2 worldPosition, bool autoCreated = false)
    {
        IsAutoCreated = autoCreated;

        // Calculate dynamic local offset from this transform's position
        dynamicLocalOffset = worldPosition - (Vector2)transform.position;
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

    private void OnDrawGizmos()
    {
        if (!showGizmo) return;

        Gizmos.color = gizmoColor;
        Vector2 anchorPos = GetWorldPosition();

        // Draw anchor point
        Gizmos.DrawWireSphere(anchorPos, gizmoSize);

        // Draw X if it's a grapple point
        Gizmos.DrawLine(
            anchorPos - Vector2.one * gizmoSize * 0.7f,
            anchorPos + Vector2.one * gizmoSize * 0.7f
        );
        Gizmos.DrawLine(
            anchorPos - new Vector2(-gizmoSize, gizmoSize) * 0.7f,
            anchorPos + new Vector2(-gizmoSize, gizmoSize) * 0.7f
        );

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
    /// Get or create a grapple anchor on the target GameObject.
    /// </summary>
    public static GrappleAnchor GetOrCreateAnchor(GameObject target, Vector2 hitPoint)
    {
        // Try to get existing anchor
        GrappleAnchor existingAnchor = target.GetComponent<GrappleAnchor>();

        if (existingAnchor != null)
        {
            // Check if this anchor delegates to another anchor
            GrappleAnchor effectiveAnchor = existingAnchor.GetEffectiveAnchor();
            if (effectiveAnchor != existingAnchor)
            {
                // Return the delegated anchor instead
                return effectiveAnchor;
            }

            return existingAnchor;
        }

        // Create new anchor component
        GrappleAnchor newAnchor = target.AddComponent<GrappleAnchor>();
        newAnchor.Initialize(hitPoint, true);
        newAnchor.showGizmo = false; // Don't show gizmos for auto-created anchors

        return newAnchor;
    }

    /// <summary>
    /// Get the effective anchor for a GameObject (handles delegation and auto-creation).
    /// </summary>
    public static GrappleAnchor GetEffectiveAnchor(GameObject target, Vector2 hitPoint)
    {
        return GetOrCreateAnchor(target, hitPoint).GetEffectiveAnchor();
    }

    /// <summary>
    /// Clean up auto-created anchors when no longer needed.
    /// </summary>
    public static void CleanupAnchor(GrappleAnchor anchor)
    {
        if (anchor != null && anchor.IsAutoCreated)
        {
            // Only destroy if this was auto-created
            Object.Destroy(anchor);
        }
    }
}