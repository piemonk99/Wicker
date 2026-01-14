using UnityEngine;

/// <summary>
/// Marks a GameObject as a valid grapple anchor point.
/// Can be manually placed or automatically created on grapple impact.
/// </summary>
public class GrappleAnchor : MonoBehaviour
{
    [Header("Anchor Settings")]
    public Vector2 localAnchorOffset = Vector2.zero;

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
    /// Get the platform's current velocity (if on a moving platform).
    /// </summary>
    public Vector2 GetPlatformVelocity()
    {
        return platform != null ? platform.GetPlatformVelocity() : Vector2.zero;
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
            return existingAnchor;
        }

        // Create new anchor component
        GrappleAnchor newAnchor = target.AddComponent<GrappleAnchor>();
        newAnchor.Initialize(hitPoint, true);
        newAnchor.showGizmo = false; // Don't show gizmos for auto-created anchors

        return newAnchor;
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