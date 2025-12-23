using UnityEngine;

[System.Serializable]
public class CameraZoomController
{
    [Header("Zoom Settings")]
    public bool enabled = true;
    public float minOrthographicSize = 15f;
    public float maxOrthographicSize = 30f; // Double at 50 velocity
    public float velocityThreshold = 50f; // Velocity at which max zoom is reached
    public float minVelocityForZoom = 5f; // Minimum velocity before zoom starts
    public float zoomSmoothTime = 0.2f;
    public AnimationCurve zoomCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Axis Weights")]
    [Range(0f, 1f)] public float horizontalWeight = 1f;
    [Range(0f, 1f)] public float verticalWeight = 0.5f;

    // State
    private float targetSize;
    private float currentVelocity;
    private Vector3 lastVelocity;

    public void Initialize(Camera cam)
    {
        if (cam != null)
        {
            targetSize = cam.orthographicSize;
            currentVelocity = 0f;
            lastVelocity = Vector3.zero;
        }
    }

    public float CalculateTargetSize(Camera cam, Vector3 velocity)
    {
        if (!enabled || cam == null) return cam.orthographicSize;

        // Store for debugging
        lastVelocity = velocity;

        // Apply axis weights
        float effectiveVelocity = CalculateEffectiveVelocity(velocity);

        // Check if we're below the minimum threshold
        if (effectiveVelocity < minVelocityForZoom)
        {
            // Smoothly return to minimum size
            targetSize = Mathf.SmoothDamp(
                cam.orthographicSize,
                minOrthographicSize,
                ref currentVelocity,
                zoomSmoothTime,
                Mathf.Infinity,
                Time.deltaTime
            );
            return targetSize;
        }

        // Calculate zoom based on velocity
        return CalculateVelocityBasedZoom(cam, effectiveVelocity);
    }

    private float CalculateEffectiveVelocity(Vector3 velocity)
    {
        // Apply axis weights to velocity
        float weightedX = Mathf.Abs(velocity.x) * horizontalWeight;
        float weightedY = Mathf.Abs(velocity.y) * verticalWeight;

        // Combine weighted velocities (you can use different methods here)
        // Option 1: Use magnitude of weighted vector
        Vector3 weightedVelocity = new Vector3(weightedX, weightedY, 0f);
        return weightedVelocity.magnitude;

        // Option 2: Use maximum of weighted axes (uncomment to use)
        // return Mathf.Max(weightedX, weightedY);
    }

    private float CalculateVelocityBasedZoom(Camera cam, float velocity)
    {
        // Normalize velocity relative to threshold
        float normalizedVelocity = Mathf.Clamp01((velocity - minVelocityForZoom) / (velocityThreshold - minVelocityForZoom));

        // Apply curve for non-linear zoom
        float curveValue = zoomCurve.Evaluate(normalizedVelocity);

        // Calculate target size (lerp between min and max)
        float newTargetSize = Mathf.Lerp(minOrthographicSize, maxOrthographicSize, curveValue);

        // Smoothly transition to target size
        targetSize = Mathf.SmoothDamp(
            cam.orthographicSize,
            newTargetSize,
            ref currentVelocity,
            zoomSmoothTime,
            Mathf.Infinity,
            Time.deltaTime
        );

        return targetSize;
    }

    private void DrawCircle(Vector3 center, float radius, Color color, int segments = 32)
    {
        float angle = 0f;
        Vector3 lastPoint = center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * radius;

        for (int i = 1; i <= segments; i++)
        {
            angle = (float)i / (float)segments * Mathf.PI * 2f;
            Vector3 nextPoint = center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * radius;
            Debug.DrawLine(lastPoint, nextPoint, color);
            lastPoint = nextPoint;
        }
    }

    // Public API
    public void SetEnabled(bool isEnabled) => enabled = isEnabled;
    public void SetZoomRange(float minSize, float maxSize)
    {
        minOrthographicSize = minSize;
        maxOrthographicSize = maxSize;
    }
    public void SetVelocityThreshold(float threshold, float minVelocity = 5f)
    {
        velocityThreshold = threshold;
        minVelocityForZoom = minVelocity;
    }
    public float GetCurrentTargetSize() => targetSize;
    public Vector3 GetLastVelocity() => lastVelocity;
}