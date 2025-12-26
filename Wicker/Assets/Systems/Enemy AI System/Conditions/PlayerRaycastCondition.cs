using UnityEngine;

public class PlayerRaycastCondition : AICondition
{
    [System.Serializable]
    public class Settings
    {
        [Header("Raycast Settings")]
        public LayerMask obstacleLayers = Physics2D.DefaultRaycastLayers;
        public float raycastOffsetY = 0.5f; // Vertical offset from character center
        public float maxDistance = 20f;

        [Header("Persistence Settings")]
        public bool usePersistence = false;
        public float persistenceDuration = 0.1f; // How long to stay true after losing line of sight

        [Header("Performance")]
        public float checkInterval = 0.1f; // Seconds between raycast checks

        [Header("Debug")]
        public bool drawDebug = true;
        public Color clearColor = Color.green;
        public Color blockedColor = Color.red;
    }

    public Settings settings = new Settings();

    // Runtime state
    private float lastCheckTime = 0f;
    private float lastClearTime = 0f;
    private bool cachedResult = false;

    // Default constructor
    public PlayerRaycastCondition() { }

    // Constructor with settings
    public PlayerRaycastCondition(Settings settings)
    {
        this.settings = settings;
        conditionName = "PlayerRaycast";
    }

    public override bool Evaluate(AIBlackboard blackboard)
    {
        // Check interval for performance
        if (Time.time < lastCheckTime + settings.checkInterval)
        {
            // Apply persistence if enabled
            if (settings.usePersistence && cachedResult)
            {
                if (Time.time - lastClearTime <= settings.persistenceDuration)
                {
                    return true;
                }
            }
            return cachedResult;
        }

        lastCheckTime = Time.time;
        cachedResult = CheckRaycast(blackboard);

        // Update last clear time if we have line of sight
        if (cachedResult)
        {
            lastClearTime = Time.time;
        }

        // Apply persistence check
        if (settings.usePersistence && !cachedResult)
        {
            if (Time.time - lastClearTime <= settings.persistenceDuration)
            {
                return true;
            }
        }

        return cachedResult;
    }

    private bool CheckRaycast(AIBlackboard blackboard)
    {
        Transform self = blackboard.Get<Transform>("transform");
        Transform player = blackboard.Get<Transform>("player");

        if (self == null || player == null)
            return false;

        // Calculate ray origin with vertical offset
        Vector2 rayOrigin = (Vector2)self.position + Vector2.up * settings.raycastOffsetY;
        Vector2 toPlayer = (Vector2)player.position - rayOrigin;

        // Early exit if player is too far
        if (toPlayer.magnitude > settings.maxDistance)
            return false;

        // Perform the raycast
        RaycastHit2D hit = Physics2D.Raycast(
            rayOrigin,
            toPlayer.normalized,
            toPlayer.magnitude,
            settings.obstacleLayers
        );

        // Check if we hit the player (or nothing at all)
        bool hasClearLineOfSight = hit.collider == null || hit.collider.transform == player;

        // Debug visualization
        if (settings.drawDebug)
        {
            Debug.DrawRay(rayOrigin, toPlayer,
                         hasClearLineOfSight ? settings.clearColor : settings.blockedColor,
                         settings.checkInterval);

            if (!hasClearLineOfSight && hit.collider != null)
            {
                // Draw a small marker where the ray was blocked
                Debug.DrawLine(hit.point - Vector2.up * 0.1f, hit.point + Vector2.up * 0.1f, Color.yellow, settings.checkInterval);
                Debug.DrawLine(hit.point - Vector2.right * 0.1f, hit.point + Vector2.right * 0.1f, Color.yellow, settings.checkInterval);
            }
        }

        return hasClearLineOfSight;
    }

    // Optional: Add a method to manually reset persistence
    public void ResetPersistence()
    {
        lastClearTime = 0f;
    }

    // Helper to check line of sight without persistence
    public bool CheckImmediate(AIBlackboard blackboard)
    {
        return CheckRaycast(blackboard);
    }
}
