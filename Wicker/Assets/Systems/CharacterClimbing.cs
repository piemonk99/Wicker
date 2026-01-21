using UnityEngine;
using System.Collections.Generic;

public class CharacterClimbing : MonoBehaviour, ICharacterComponent
{
    private CharacterCore character;
    private CharacterMovement movement;
    private CharacterGrapple grapple;
    private Rigidbody2D rb;

    // State
    private bool isClimbingRope = false;
    private bool isAttemptingClimb = false;
    private ClimbableRope currentRope;
    private Transform ropeAnchor;
    private GameObject lowerRopeInstance;
    private SpringJoint2D tempJoint; // Temporary until grapple starts

    // Track triggers we're inside
    private List<Collider2D> overlappingTriggers = new List<Collider2D>();
    private Dictionary<Collider2D, ClimbableRope> triggerRopeMap = new Dictionary<Collider2D, ClimbableRope>();

    // Configuration
    [SerializeField] private LayerMask climbableLayers;

    // Visual debug
    private LineRenderer climbDebugLine;

    public void Initialize(CharacterCore core)
    {
        character = core;
        movement = core.GetComponent<CharacterMovement>();
        grapple = core.GetComponent<CharacterGrapple>();
        rb = core.GetComponent<Rigidbody2D>();

        // Create debug line renderer
        climbDebugLine = gameObject.AddComponent<LineRenderer>();
        climbDebugLine.startWidth = 0.05f;
        climbDebugLine.endWidth = 0.05f;
        climbDebugLine.material = new Material(Shader.Find("Sprites/Default"));
        climbDebugLine.startColor = Color.magenta;
        climbDebugLine.endColor = Color.magenta;
        climbDebugLine.enabled = false;

        character.OnEvent += HandleEvent;
        character.CharacterContext["isClimbingRope"] = false;
    }

    private void HandleEvent(string type, object data)
    {
        switch (type)
        {
            case "up_held":
                if (!isClimbingRope && !isAttemptingClimb)
                {
                    isAttemptingClimb = true;
                    TryStartRopeClimb();
                }
                break;

            case "up_released":
                isAttemptingClimb = false;
                break;

            case "jump_pressed":
                if (isClimbingRope)
                {
                    StopRopeClimb(true); // Jump off
                }
                break;
        }
    }

    // Track trigger overlaps
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (((1 << other.gameObject.layer) & climbableLayers) != 0 && other.isTrigger)
        {
            ClimbableRope rope = other.GetComponentInParent<ClimbableRope>();
            if (rope != null && rope.CanClimb)
            {
                overlappingTriggers.Add(other);
                triggerRopeMap[other] = rope;

                Debug.Log($"Entered climbable trigger: {other.name}, Rope: {rope.name}");
            }
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (overlappingTriggers.Contains(other))
        {
            overlappingTriggers.Remove(other);
            triggerRopeMap.Remove(other);

            Debug.Log($"Exited climbable trigger: {other.name}");
        }
    }

    private void TryStartRopeClimb()
    {
        if (isClimbingRope || overlappingTriggers.Count == 0) return;

        // Find the closest climbable trigger
        Collider2D closestTrigger = null;
        float closestDistance = float.MaxValue;
        ClimbableRope closestRope = null;
        Transform closestBone = null;

        foreach (var trigger in overlappingTriggers)
        {
            if (triggerRopeMap.TryGetValue(trigger, out var rope) && rope.CanClimb)
            {
                float distance = Vector2.Distance(transform.position, trigger.transform.position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestTrigger = trigger;
                    closestRope = rope;
                    closestBone = trigger.transform;
                }
            }
        }

        if (closestRope != null && closestBone != null)
        {
            Debug.Log($"Starting rope climb on {closestRope.name} at bone {closestBone.name}");
            StartRopeClimb(closestRope, closestBone);
        }
    }

    private void StartRopeClimb(ClimbableRope rope, Transform grabBone)
    {
        isClimbingRope = true;
        isAttemptingClimb = false;
        currentRope = rope;

        character.CharacterContext["isClimbingRope"] = true;

        // Get rope anchor (top point)
        ropeAnchor = rope.GetAnchorTransform();

        // Position player at the grab point
        Vector2 grabPoint = grabBone.position;
        rb.position = grabPoint;
        rb.linearVelocity = Vector2.zero;

        // Hide the original rope above the grab point
        rope.SetRopeVisibleAbove(grabBone, false);

        // Create a temporary anchor for the grapple
        GameObject anchorObject = CreateRopeAnchor(ropeAnchor.position, rope.gameObject);

        // Start a temporary joint to keep us in place while grapple initializes
        StartTemporaryJoint(grabPoint, anchorObject);

        // Create a new rope segment from player to original rope end
        CreateLowerRopeSegment(grabPoint, rope.GetEndTransform().position, rope);

        // Start the actual grapple using the rope's config
        StartRopeGrapple(anchorObject, rope);

        // Setup debug visualization
        climbDebugLine.enabled = true;

        Debug.Log($"Rope climb started");
        character.RaiseEvent("rope_climb_started", rope);
    }

    private GameObject CreateRopeAnchor(Vector2 position, GameObject parent)
    {
        // Create a temporary anchor object for the grapple
        GameObject anchor = new GameObject("RopeClimbAnchor");
        anchor.transform.position = position;
        anchor.transform.SetParent(parent.transform);

        // Add a collider so grapple system can detect it
        CircleCollider2D collider = anchor.AddComponent<CircleCollider2D>();
        collider.isTrigger = true;
        collider.radius = 0.1f;

        // Add a GrappleAnchor component
        GrappleAnchor grappleAnchor = anchor.AddComponent<GrappleAnchor>();
        grappleAnchor.SetAsRopeAnchor(); // Mark it as a rope anchor

        return anchor;
    }

    private void StartTemporaryJoint(Vector2 fromPosition, GameObject anchorObject)
    {
        // Create a temporary joint to hold position until grapple starts
        tempJoint = gameObject.AddComponent<SpringJoint2D>();

        // Add Rigidbody to anchor if needed
        Rigidbody2D anchorRb = anchorObject.GetComponent<Rigidbody2D>();
        if (anchorRb == null)
        {
            anchorRb = anchorObject.AddComponent<Rigidbody2D>();
            anchorRb.isKinematic = true;
        }

        tempJoint.connectedBody = anchorRb;
        tempJoint.autoConfigureDistance = false;
        tempJoint.distance = Vector2.Distance(fromPosition, anchorObject.transform.position);
        tempJoint.dampingRatio = 0.7f;
        tempJoint.frequency = 2f;
        tempJoint.enableCollision = true;

        Debug.Log($"Temporary joint created, length: {tempJoint.distance}");
    }

    private void StartRopeGrapple(GameObject anchorObject, ClimbableRope rope)
    {
        // Get the grapple config from the rope
        GrappleConfig ropeGrappleConfig = rope.GetGrappleConfig();
        if (ropeGrappleConfig == null)
        {
            Debug.LogWarning($"No grapple config found on rope {rope.name}, using fallback");
            ropeGrappleConfig = CreateDefaultRopeGrappleConfig();
        }

        // We need to temporarily equip this config to the grapple system
        // Since CharacterGrapple gets config from CharacterEquipment, we need a workaround

        // Method 1: Create a temporary equipment override
        // Method 2: Add a method to CharacterGrapple to use a temporary config
        // Method 3: Use the existing grapple but with the rope's physics settings

        // For now, let's assume we'll add this method to CharacterGrapple:
        // public void StartGrappleWithConfig(GameObject anchorObject, GrappleConfig config)

        // Send an event that CharacterGrapple can listen for
        RopeGrappleStartData startData = new RopeGrappleStartData
        {
            anchorObject = anchorObject,
            grappleConfig = ropeGrappleConfig
        };

        character.RaiseEvent("rope_grapple_start", startData);
    }

    private GrappleConfig CreateDefaultRopeGrappleConfig()
    {
        // Create a basic rope grapple config
        GrappleConfig config = ScriptableObject.CreateInstance<GrappleConfig>();
        config.name = "RopeGrapple";

        // Set reasonable defaults for rope climbing
        config.mechanicsConfig.grappleName = "Rope";
        config.mechanicsConfig.createsAnchors = true;

        config.physicsConfig.maxDistance = 20f;
        config.physicsConfig.ropeDamping = 0.1f;
        config.physicsConfig.stretchStiffness = 150f; // Ropes are stretchy
        config.physicsConfig.enableStretch = true;
        config.physicsConfig.enableSquash = false; // Can't compress ropes

        config.reelConfig.reelSpeed = 3f;
        config.reelConfig.unreelSpeed = 3f;
        config.reelConfig.minRopeLength = 0.5f;
        config.reelConfig.maxRopeLength = 20f;

        return config;
    }

    private void CreateLowerRopeSegment(Vector2 fromPosition, Vector2 toPosition, ClimbableRope originalRope)
    {
        // Create a new rope GameObject for the segment below us
        lowerRopeInstance = new GameObject("LowerRopeSegment");
        lowerRopeInstance.transform.position = fromPosition;

        // Make it a child of the player so it moves with us
        lowerRopeInstance.transform.SetParent(transform);

        // Copy the rope's grapple config for physics
        GrappleConfig ropeConfig = originalRope.GetGrappleConfig();
        if (ropeConfig != null)
        {
            // Add a config component to store the settings
            RopeSegmentConfig segmentConfig = lowerRopeInstance.AddComponent<RopeSegmentConfig>();
            segmentConfig.physicsConfig = ropeConfig.physicsConfig;
        }

        // Create end point for the rope (this will swing)
        GameObject ropeEnd = new GameObject("RopeEnd");
        ropeEnd.transform.position = toPosition;
        ropeEnd.transform.SetParent(lowerRopeInstance.transform);

        // Add physics to the end
        Rigidbody2D endRb = ropeEnd.AddComponent<Rigidbody2D>();
        endRb.mass = 0.5f;
        endRb.gravityScale = 1.2f; // Slightly heavier for good swing

        // Connect with a DistanceJoint2D that simulates rope physics
        DistanceJoint2D joint = ropeEnd.AddComponent<DistanceJoint2D>();
        joint.connectedBody = rb; // Connect to player's rigidbody
        joint.autoConfigureDistance = false;

        // Use the rope's max distance or actual distance
        float ropeLength = Vector2.Distance(fromPosition, toPosition);
        if (ropeConfig != null)
        {
            ropeLength = Mathf.Min(ropeLength, ropeConfig.physicsConfig.maxDistance);
        }

        joint.distance = ropeLength;
        joint.maxDistanceOnly = false;

        // Add rope damping
        RopeSegmentPhysics ropePhysics = ropeEnd.AddComponent<RopeSegmentPhysics>();
        ropePhysics.Initialize(rb, joint, ropeConfig?.physicsConfig);

        // Add a visual (LineRenderer)
        LineRenderer ropeVisual = lowerRopeInstance.AddComponent<LineRenderer>();
        ropeVisual.startWidth = 0.15f;
        ropeVisual.endWidth = 0.15f;
        ropeVisual.material = new Material(Shader.Find("Sprites/Default"));
        ropeVisual.startColor = Color.blue;
        ropeVisual.endColor = Color.blue;
        ropeVisual.positionCount = 2;

        // Store reference to update visuals
        RopeSegmentVisuals visuals = lowerRopeInstance.AddComponent<RopeSegmentVisuals>();
        visuals.Initialize(ropeVisual, transform, ropeEnd.transform);

        Debug.Log($"Lower rope segment created, length: {joint.distance}");
    }

    public void Tick(float deltaTime)
    {
        // Update debug visualization
        UpdateDebugVisuals();
    }

    public void PhysicsTick(float fixedDeltaTime)
    {
        // Nothing needed here - physics handled by joints and grapple system
    }

    private void UpdateDebugVisuals()
    {
        if (climbDebugLine.enabled && isClimbingRope && ropeAnchor != null)
        {
            climbDebugLine.positionCount = 2;
            climbDebugLine.SetPosition(0, transform.position);
            climbDebugLine.SetPosition(1, ropeAnchor.position);
        }
    }

    private void StopRopeClimb(bool jumpOff)
    {
        if (!isClimbingRope) return;

        Debug.Log($"Stopping rope climb, jump off: {jumpOff}");

        isClimbingRope = false;
        isAttemptingClimb = false;

        character.CharacterContext["isClimbingRope"] = false;

        // Stop the grapple if it's active
        character.RaiseEvent("rope_grapple_stop", null);

        // Remove temporary joint
        if (tempJoint != null)
        {
            Destroy(tempJoint);
            tempJoint = null;
        }

        // Show original rope again
        if (currentRope != null)
        {
            currentRope.SetRopeVisible(true);
        }

        // Remove temporary anchor
        GameObject anchor = GameObject.Find("RopeClimbAnchor");
        if (anchor != null)
        {
            Destroy(anchor);
        }

        // Remove lower rope segment
        if (lowerRopeInstance != null)
        {
            Destroy(lowerRopeInstance);
            lowerRopeInstance = null;
        }

        // Disable debug line
        climbDebugLine.enabled = false;

        // Apply jump force if jumping off
        if (jumpOff)
        {
            Vector2 jumpDirection = new Vector2(movement.GetCurrentXDirection() * 0.7f, 1f).normalized;
            rb.AddForce(jumpDirection * 12f, ForceMode2D.Impulse);
        }

        character.RaiseEvent("rope_climb_ended", jumpOff);

        currentRope = null;
        ropeAnchor = null;
    }

    private void OnDestroy()
    {
        if (character != null)
        {
            character.OnEvent -= HandleEvent;
        }

        StopRopeClimb(false);
    }

    // Getters
    public bool IsClimbingRope() => isClimbingRope;
    public ClimbableRope GetCurrentRope() => currentRope;

    // Debug visualization
    private void OnDrawGizmosSelected()
    {
        if (isAttemptingClimb && !isClimbingRope)
        {
            Gizmos.color = Color.yellow;
            foreach (var trigger in overlappingTriggers)
            {
                if (trigger != null)
                {
                    Gizmos.DrawWireSphere(trigger.transform.position, 0.2f);
                }
            }
        }

        if (isClimbingRope && ropeAnchor != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, ropeAnchor.position);
            Gizmos.DrawWireSphere(ropeAnchor.position, 0.3f);
        }
    }
}

// Data structure for starting rope grapple
public struct RopeGrappleStartData
{
    public GameObject anchorObject;
    public GrappleConfig grappleConfig;
}

// Helper class for rope segment physics
public class RopeSegmentPhysics : MonoBehaviour
{
    private Rigidbody2D playerRb;
    private DistanceJoint2D joint;
    private GrapplePhysicsConfig physicsConfig;
    private Vector2 previousPosition;

    public void Initialize(Rigidbody2D playerRb, DistanceJoint2D joint, GrapplePhysicsConfig config = null)
    {
        this.playerRb = playerRb;
        this.joint = joint;
        this.physicsConfig = config;
        previousPosition = transform.position;
    }

    void FixedUpdate()
    {
        if (joint == null) return;

        // Apply rope damping if config exists
        if (physicsConfig != null)
        {
            // Calculate velocity
            Vector2 velocity = ((Vector2)transform.position - previousPosition) / Time.fixedDeltaTime;

            // Apply damping
            velocity *= (1f - physicsConfig.ropeDamping * Time.fixedDeltaTime);

            // Update position based on damped velocity
            Rigidbody2D rb = GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.linearVelocity = velocity;
            }

            previousPosition = transform.position;
        }

        // Keep the joint distance updated based on player movement
        if (playerRb != null)
        {
            float currentDistance = Vector2.Distance(transform.position, playerRb.position);
            joint.distance = currentDistance;
        }
    }
}

// Helper class for rope segment visuals
public class RopeSegmentVisuals : MonoBehaviour
{
    private LineRenderer lineRenderer;
    private Transform startPoint;
    private Transform endPoint;

    public void Initialize(LineRenderer lineRenderer, Transform startPoint, Transform endPoint)
    {
        this.lineRenderer = lineRenderer;
        this.startPoint = startPoint;
        this.endPoint = endPoint;
    }

    void Update()
    {
        if (lineRenderer != null && startPoint != null && endPoint != null)
        {
            lineRenderer.SetPosition(0, startPoint.position);
            lineRenderer.SetPosition(1, endPoint.position);
        }
    }
}

// Simple config storage for rope segments
public class RopeSegmentConfig : MonoBehaviour
{
    public GrapplePhysicsConfig physicsConfig;
}