using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages all visual aspects of the grapple system.
/// Handles hook/rope instantiation, procedural rope rendering, and visual effects.
/// </summary>
public class GrappleVisualManager
{
    private GrappleVisualConfig visualConfig;
    private Transform grappleOrigin;
    private LineRenderer grappleLine;

    private GameObject currentHookInstance;
    private GameObject currentRopeInstance;
    private bool showPhysicsDebug;

    /// <summary>
    /// Initializes a new instance of GrappleVisualManager with visual configuration.
    /// </summary>
    /// <param name="visualConfig">Visual configuration containing prefabs and settings.</param>
    /// <param name="grappleOrigin">Transform representing the grapple origin (player attachment point).</param>
    /// <param name="grappleLine">LineRenderer for debug/fallback rope visualization.</param>
    /// <param name="showPhysicsDebug">Whether to show physics debug visuals.</param>
    public GrappleVisualManager(GrappleVisualConfig visualConfig, Transform grappleOrigin, LineRenderer grappleLine, bool showPhysicsDebug = false)
    {
        this.visualConfig = visualConfig;
        this.grappleOrigin = grappleOrigin;
        this.grappleLine = grappleLine;
        this.showPhysicsDebug = showPhysicsDebug;

        if (grappleLine != null)
        {
            grappleLine.enabled = false;
            grappleLine.positionCount = 2;
        }
    }

    /// <summary>
    /// Instantiates visual elements for the grapple (hook and rope).
    /// Called when grapple starts to create visual representation.
    /// </summary>
    /// <param name="grapplePoint">World position where the grapple attaches.</param>
    public void InstantiateGrappleVisuals(Vector2 grapplePoint)
    {
        CleanupGrappleVisuals();

        // Instantiate hook at grapple point
        if (visualConfig.hookPrefab != null)
        {
            currentHookInstance = GameObject.Instantiate(
                visualConfig.hookPrefab,
                grapplePoint,
                Quaternion.identity
            );

            currentHookInstance.transform.localScale = new Vector3(
                visualConfig.hookScale.x,
                visualConfig.hookScale.y,
                1f
            );
        }

        // Instantiate rope between grapple origin and hook
        if (visualConfig.ropePrefab != null)
        {
            currentRopeInstance = GameObject.Instantiate(
                visualConfig.ropePrefab,
                Vector3.zero,
                Quaternion.identity
            );
        }

        if (grappleLine != null)
            grappleLine.enabled = true;
    }

    /// <summary>
    /// Cleans up all visual elements associated with the grapple.
    /// Called when grapple ends to remove visual representation.
    /// </summary>
    public void CleanupGrappleVisuals()
    {
        if (currentHookInstance != null)
        {
            GameObject.Destroy(currentHookInstance);
            currentHookInstance = null;
        }

        if (currentRopeInstance != null)
        {
            GameObject.Destroy(currentRopeInstance);
            currentRopeInstance = null;
        }

        if (grappleLine != null)
            grappleLine.enabled = false;
    }

    /// <summary>
    /// Updates all grapple visual elements based on current state.
    /// Positions hook and rope, updates LineRenderer colors, and handles debug visuals.
    /// </summary>
    /// <param name="grapplePoint">Current grapple point position.</param>
    /// <param name="currentRopeLength">Current rope length.</param>
    /// <param name="isGrappling">Whether grappling is currently active.</param>
    /// <param name="shouldReel">Whether the rope should be reeling in.</param>
    /// <param name="shouldUnreel">Whether the rope should be unreeling out.</param>
    /// <param name="ropeState">Current rope state (stretch/squash information).</param>
    /// <param name="swingArc">Current swing arc for debug visualization (optional).</param>
    public void UpdateGrappleVisuals(Vector2 grapplePoint, float currentRopeLength, bool isGrappling,
        bool shouldReel, bool shouldUnreel, RopeState ropeState, SwingArc swingArc = null)
    {
        if (!isGrappling) return;

        // Update rope start anchor to follow player
        if (currentRopeInstance != null)
        {
            // Find the start anchor (player side) by name
            Transform startAnchor = FindTransformInChildren(currentRopeInstance.transform, visualConfig.ropeStartAnchorName);

            if (startAnchor != null)
            {
                startAnchor.position = grappleOrigin.position;
            }

            // Update end anchor (hook side)
            Transform endAnchor = FindTransformInChildren(currentRopeInstance.transform, visualConfig.ropeEndAnchorName);
            if (endAnchor != null && currentHookInstance != null)
            {
                endAnchor.position = currentHookInstance.transform.position;
            }

            UpdateRopeVisualsProcedural(grapplePoint, currentRopeLength);
        }

        // Update debug line renderer with state-based colors
        UpdateLineRendererVisuals(grapplePoint, shouldReel, shouldUnreel, ropeState);

        // Physics debug visualization
        if (showPhysicsDebug && isGrappling)
        {
            DrawSwingCircle(grapplePoint, currentRopeLength);

            if (swingArc != null)
            {
                Debug.DrawRay(grappleOrigin.position, swingArc.tangentDirection * 3f, Color.green);
                Debug.DrawRay(grappleOrigin.position, -swingArc.tangentDirection * 3f, Color.green);
            }
        }
    }

    /// <summary>
    /// Updates the procedural rope visual by positioning bone transforms.
    /// Creates either a straight line or sagging curve based on rope slack.
    /// </summary>
    /// <param name="grapplePoint">Grapple point position.</param>
    /// <param name="currentRopeLength">Current rope length.</param>
    private void UpdateRopeVisualsProcedural(Vector2 grapplePoint, float currentRopeLength)
    {
        if (currentRopeInstance == null) return;

        // Get all bones
        List<Transform> boneTransforms = new List<Transform>();
        foreach (Transform child in currentRopeInstance.transform)
        {
            if (child.name.ToLower().Contains("bone"))
            {
                boneTransforms.Add(child);
            }
        }

        if (boneTransforms.Count == 0) return;

        // Sort bones numerically
        boneTransforms.Sort((a, b) => {
            int aNum = ExtractBoneNumber(a.name);
            int bNum = ExtractBoneNumber(b.name);
            return aNum.CompareTo(bNum);
        });

        Vector2 playerPos = grappleOrigin.position;
        Vector2 hookPos = grapplePoint;
        float currentDistance = Vector2.Distance(playerPos, hookPos);

        // Calculate slack (positive when player is inside the rope circle)
        float slack = Mathf.Max(0, currentRopeLength - currentDistance);

        // Determine if rope should be straight
        bool shouldBeStraight = slack < 0.01f || Mathf.Abs(currentDistance - currentRopeLength) < 0.05f;

        // Position each bone
        for (int i = 0; i < boneTransforms.Count; i++)
        {
            float t = i / (float)(boneTransforms.Count - 1);

            Vector2 bonePosition;

            if (shouldBeStraight)
            {
                // Straight line from player to hook
                bonePosition = Vector2.Lerp(playerPos, hookPos, t);
            }
            else
            {
                // Use the gravity-based curve
                bonePosition = CalculateProperSag(playerPos, hookPos, t, slack);
            }

            // Set position
            boneTransforms[i].position = bonePosition;

            // Set rotation to point toward next bone
            if (i < boneTransforms.Count - 1)
            {
                Vector2 nextPos;
                if (shouldBeStraight)
                {
                    nextPos = Vector2.Lerp(playerPos, hookPos, (i + 1) / (float)(boneTransforms.Count - 1));
                }
                else
                {
                    nextPos = CalculateProperSag(playerPos, hookPos, (i + 1) / (float)(boneTransforms.Count - 1), slack);
                }

                Vector2 direction = (nextPos - bonePosition).normalized;
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                boneTransforms[i].rotation = Quaternion.Euler(0, 0, angle);
            }
            else
            {
                // Last bone points toward hook
                Vector2 direction = (hookPos - bonePosition).normalized;
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                boneTransforms[i].rotation = Quaternion.Euler(0, 0, angle);
            }
        }
    }

    /// <summary>
    /// Calculates rope sag position based on a catenary-like curve.
    /// Simulates realistic rope hanging under gravity with proper tension distribution.
    /// 
    /// The calculation uses a piecewise quadratic Bézier curve:
    /// 1. Determines which end point is lower (startIsLower)
    /// 2. Calculates maximum sag height (h) using rope length constraint
    /// 3. Creates two quadratic curves meeting at the lowest point
    /// 
    /// This creates a natural-looking rope curve that accounts for gravity and tension.
    /// </summary>
    /// <param name="start">Starting point of the rope (player position).</param>
    /// <param name="end">End point of the rope (hook position).</param>
    /// <param name="t">Normalized position along the rope (0 at start, 1 at end).</param>
    /// <param name="slack">Amount of slack in the rope (rope length - straight distance).</param>
    /// <returns>Position along the sagging rope curve at parameter t.</returns>
    private Vector2 CalculateProperSag(Vector2 start, Vector2 end, float t, float slack)
    {
        // Straight line if no significant slack
        if (slack < 0.01f)
        {
            return Vector2.Lerp(start, end, t);
        }

        float straightDistance = Vector2.Distance(start, end);

        // Determine which point is lower to find natural sag direction
        bool startIsLower = start.y < end.y;
        float lowestT = startIsLower ? 0.25f : 0.75f; // Sag point parameter

        // Calculate segment lengths for sag formula
        float segmentA = straightDistance * lowestT;
        float segmentB = straightDistance * (1f - lowestT);
        float ropeLength = straightDistance + slack;

        // Calculate maximum sag height using rope length constraint
        // This formula ensures the curved rope length matches the actual rope length
        float heightSquared = ((ropeLength * ropeLength) - (segmentA + segmentB) * (segmentA + segmentB)) *
                             ((ropeLength * ropeLength) - (segmentA - segmentB) * (segmentA - segmentB)) /
                             (4f * ropeLength * ropeLength);
        float maxSagHeight = Mathf.Sqrt(Mathf.Max(0f, heightSquared));

        // Gravity direction (normalized)
        Vector2 gravityDir = Physics2D.gravity.normalized;

        // Calculate lowest point position
        Vector2 lowestPointStraightLine = Vector2.Lerp(start, end, lowestT);
        Vector2 lowestPoint = lowestPointStraightLine + gravityDir * maxSagHeight;

        // Create piecewise quadratic Bézier curve for smooth rope shape

        if (t <= lowestT)
        {
            // First curve: start to lowest point
            float segmentT = t / lowestT;

            // Control point is halfway between start and lowest point, pulled down slightly
            Vector2 midpoint = Vector2.Lerp(start, lowestPointStraightLine, 0.5f);
            Vector2 controlPoint = midpoint + gravityDir * maxSagHeight * 0.7f;

            // Quadratic Bézier formula: (1-u)² * P0 + 2(1-u)u * P1 + u² * P2
            float u = segmentT;
            float oneMinusU = 1f - u;

            return oneMinusU * oneMinusU * start +
                   2f * oneMinusU * u * controlPoint +
                   u * u * lowestPoint;
        }
        else
        {
            // Second curve: lowest point to end
            float segmentT = (t - lowestT) / (1f - lowestT);

            // Control point is halfway between lowest point and end, pulled down slightly
            Vector2 midpoint = Vector2.Lerp(lowestPointStraightLine, end, 0.5f);
            Vector2 controlPoint = midpoint + gravityDir * maxSagHeight * 0.7f;

            // Quadratic Bézier formula
            float u = segmentT;
            float oneMinusU = 1f - u;

            return oneMinusU * oneMinusU * lowestPoint +
                   2f * oneMinusU * u * controlPoint +
                   u * u * end;
        }
    }

    /// <summary>
    /// Extracts numerical value from bone name for sorting.
    /// Handles various naming conventions like "bone_1", "bone1", etc.
    /// </summary>
    /// <param name="boneName">Name of the bone GameObject.</param>
    /// <returns>Extracted number, or 9999 if no number found.</returns>
    private int ExtractBoneNumber(string boneName)
    {
        string lowerName = boneName.ToLower();

        // Remove "bone_" prefix if present
        if (lowerName.StartsWith("bone_"))
        {
            string numberPart = lowerName.Substring(5);
            if (int.TryParse(numberPart, out int result))
            {
                return result;
            }
        }

        // Extract any digits from the name
        string digits = "";
        foreach (char c in boneName)
        {
            if (char.IsDigit(c))
            {
                digits += c;
            }
        }

        if (int.TryParse(digits, out int digitResult))
        {
            return digitResult;
        }

        // Default for bones without numbers
        return 9999;
    }

    /// <summary>
    /// Recursively searches for a transform by name in the hierarchy.
    /// </summary>
    /// <param name="parent">Root transform to search from.</param>
    /// <param name="name">Name of the transform to find.</param>
    /// <returns>Found transform, or null if not found.</returns>
    private Transform FindTransformInChildren(Transform parent, string name)
    {
        if (parent.name == name) return parent;

        foreach (Transform child in parent)
        {
            Transform result = FindTransformInChildren(child, name);
            if (result != null) return result;
        }

        return null;
    }

    /// <summary>
    /// Updates the LineRenderer with state-based colors and width.
    /// Provides visual feedback for reeling, stretch, and squash states.
    /// </summary>
    private void UpdateLineRendererVisuals(Vector2 grapplePoint, bool shouldReel, bool shouldUnreel, RopeState ropeState)
    {
        if (grappleLine == null) return;

        grappleLine.SetPosition(0, grappleOrigin.position);
        grappleLine.SetPosition(1, grapplePoint);

        // Visual feedback based on rope state
        if (shouldReel)
        {
            grappleLine.startColor = Color.yellow;
            grappleLine.endColor = Color.yellow;
            grappleLine.widthMultiplier = 0.15f;
        }
        else if (shouldUnreel)
        {
            grappleLine.startColor = Color.green;
            grappleLine.endColor = Color.green;
            grappleLine.widthMultiplier = 0.15f;
        }
        else if (ropeState.isStretch && ropeState.ratio > 0)
        {
            // Stretching - red with intensity based on ratio
            float intensity = Mathf.Clamp01(ropeState.ratio * 5f);
            grappleLine.startColor = Color.Lerp(Color.white, Color.red, intensity);
            grappleLine.endColor = Color.Lerp(Color.white, Color.red, intensity);
            grappleLine.widthMultiplier = 0.1f + (0.1f * intensity);
        }
        else if (ropeState.isSquash)
        {
            // Squashing - blue with intensity based on ratio
            float intensity = Mathf.Clamp01(Mathf.Abs(ropeState.ratio) * 5f);
            grappleLine.startColor = Color.Lerp(Color.white, Color.blue, intensity);
            grappleLine.endColor = Color.Lerp(Color.white, Color.blue, intensity);
            grappleLine.widthMultiplier = 0.1f + (0.1f * intensity);
        }
        else
        {
            // Normal or no stretch/squash
            grappleLine.startColor = Color.white;
            grappleLine.endColor = Color.white;
            grappleLine.widthMultiplier = 0.1f;
        }
    }

    /// <summary>
    /// Draws debug visualization of the swing circle.
    /// Shows the theoretical maximum swing radius.
    /// </summary>
    /// <param name="grapplePoint">Center of the swing circle.</param>
    /// <param name="ropeLength">Radius of the swing circle.</param>
    private void DrawSwingCircle(Vector2 grapplePoint, float ropeLength)
    {
        const int segments = 32;
        float angleStep = 360f / segments;

        for (int i = 0; i < segments; i++)
        {
            float angle1 = i * angleStep * Mathf.Deg2Rad;
            float angle2 = (i + 1) * angleStep * Mathf.Deg2Rad;

            Vector2 point1 = grapplePoint + new Vector2(Mathf.Cos(angle1), Mathf.Sin(angle1)) * ropeLength;
            Vector2 point2 = grapplePoint + new Vector2(Mathf.Cos(angle2), Mathf.Sin(angle2)) * ropeLength;

            Debug.DrawLine(point1, point2, new Color(1, 1, 1, 0.3f));
        }
    }

    /// <summary>
    /// Draws debug visualization for grapple raycast targeting.
    /// Shows aim direction and hit detection in the editor.
    /// </summary>
    /// <param name="origin">Raycast origin point.</param>
    /// <param name="direction">Raycast direction.</param>
    /// <param name="layers">Layer mask for raycast.</param>
    /// <param name="maxDistance">Maximum raycast distance.</param>
    /// <param name="hitColor">Color for successful hits.</param>
    /// <param name="missColor">Color for missed raycasts.</param>
    public void DrawRaycastDebug(Vector2 origin, Vector2 direction, LayerMask layers, float maxDistance,
        Color hitColor, Color missColor)
    {
        if (origin == Vector2.zero) return;

        RaycastHit2D debugHit = Physics2D.Raycast(
            origin,
            direction,
            maxDistance,
            layers
        );

        bool hit = debugHit.collider != null;
        float rayLength = hit ? debugHit.distance : maxDistance;
        Color rayColor = hit ? hitColor : missColor;

        Vector3 start = origin;
        Vector3 end = start + (Vector3)direction * rayLength;

        Debug.DrawLine(start, end, rayColor);

        if (hit)
        {
            Debug.DrawRay(debugHit.point, Vector2.up * 0.2f, Color.yellow);
            Debug.DrawRay(debugHit.point, Vector2.down * 0.2f, Color.yellow);
            Debug.DrawRay(debugHit.point, Vector2.left * 0.2f, Color.yellow);
            Debug.DrawRay(debugHit.point, Vector2.right * 0.2f, Color.yellow);
        }
    }
}