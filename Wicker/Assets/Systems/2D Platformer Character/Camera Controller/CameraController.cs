using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("Target")]
    public Transform target;

    [Header("Follow Settings")]
    public float smoothTime = 0.15f;
    public float maxSpeed = 15f;

    [Header("Prediction")]
    public PredictiveCameraModifier predictionModifier;

    [Header("Debug")]
    public bool showDebugVisuals = false;

    // State
    private Camera cam;
    private Vector3 currentVelocity;
    private Vector3 targetPosition;

    void Start()
    {
        cam = GetComponent<Camera>();
        
        // Initialize position
        if (target != null)
        {
            transform.position = new Vector3(target.position.x, target.position.y, transform.position.z);
            targetPosition = transform.position;
        }

        // Auto-find prediction modifier if not set
        if (predictionModifier == null)
            predictionModifier = GetComponent<PredictiveCameraModifier>();
    }

    void LateUpdate()
    {
        if (target == null) return;

        // Start with exact target position
        Vector3 basePosition = new Vector3(target.position.x, target.position.y, transform.position.z);

        // Store for debug BEFORE prediction
        Vector3 prePredictionPosition = basePosition;

        // Apply prediction if available
        if (predictionModifier != null && predictionModifier.IsActive())
        {
            basePosition = predictionModifier.ModifyPosition(basePosition, target, this);
        }

        // Smooth follow with acceleration/deceleration
        targetPosition = Vector3.SmoothDamp(
            transform.position,
            basePosition,
            ref currentVelocity,
            smoothTime,
            maxSpeed,
            Time.deltaTime
        );

        // Set final position
        transform.position = targetPosition;

        // Draw debug visuals if enabled
        if (showDebugVisuals)
        {
            DrawDebugVisuals(prePredictionPosition, basePosition);
        }
    }

    void DrawDebugVisuals(Vector3 prePredictionPos, Vector3 postPredictionPos)
    {
        // Draw green reticle at predicted target
        Debug.DrawLine(new Vector3(postPredictionPos.x, postPredictionPos.y - .5f, 0), new Vector3(postPredictionPos.x, postPredictionPos.y + .5f, 0), Color.green);
        Debug.DrawLine(new Vector3(postPredictionPos.x - .5f, postPredictionPos.y, 0), new Vector3(postPredictionPos.x + .5f, postPredictionPos.y, 0), Color.green);

        // Draw yellow line from player to predicted target
        Debug.DrawLine(new Vector3(target.position.x, target.position.y, 0), new Vector3(postPredictionPos.x, postPredictionPos.y, 0), Color.yellow);

        
    }

    // Public API
    public void SetTarget(Transform newTarget) => target = newTarget;

    // Get the current velocity (for debug)
    public Vector3 GetCurrentVelocity() => currentVelocity;

    public void Shake(float intensity, float duration = 0.3f)
    {
        StartCoroutine(DoShake(intensity, duration));
    }

    System.Collections.IEnumerator DoShake(float intensity, float duration)
    {
        Vector3 originalPos = transform.position;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float decay = 1f - (elapsed / duration);
            Vector2 offset = Random.insideUnitCircle * intensity * decay;

            transform.position = originalPos + new Vector3(offset.x, offset.y, 0);

            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.position = originalPos;
    }
}