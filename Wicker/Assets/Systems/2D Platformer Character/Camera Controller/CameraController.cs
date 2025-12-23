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

    [Header("Velocity-Based Zoom")]
    public CameraZoomController zoomController = new CameraZoomController();

    [Header("Debug")]
    public bool showDebugVisuals = false;

    // State
    private Camera cam;
    private Vector3 currentVelocity;
    private Vector3 targetPosition;
    private Vector3 lastTargetVelocity;

    void Start()
    {
        cam = GetComponent<Camera>();
        
        // Initialize position
        if (target != null)
        {
            transform.position = new Vector3(target.position.x, target.position.y, transform.position.z);
            targetPosition = transform.position;
        }

        // Initialize zoom controller
        zoomController.Initialize(cam);

        // Auto-find prediction modifier if not set
        if (predictionModifier == null)
            predictionModifier = GetComponent<PredictiveCameraModifier>();
    }

    void LateUpdate()
    {
        if (target == null) return;

        // Get target velocity for zoom calculations
        Vector3 targetVelocity = GetTargetVelocity();
        lastTargetVelocity = targetVelocity;

        // Apply velocity-based zoom
        if (zoomController.enabled)
        {
            float targetSize = zoomController.CalculateTargetSize(cam, targetVelocity);
            cam.orthographicSize = targetSize;
        }

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

    private Vector3 GetTargetVelocity()
    {
        // Try to get velocity from prediction modifier first
        if (predictionModifier != null)
        {
            return predictionModifier.GetVelocity();
        }
        
        // Fallback: Try to get velocity directly from Rigidbody
        Rigidbody2D rb2D = target.GetComponent<Rigidbody2D>();
        if (rb2D != null)
            return rb2D.linearVelocity;
            
        Rigidbody rb3D = target.GetComponent<Rigidbody>();
        if (rb3D != null)
            return rb3D.linearVelocity;
            
        return Vector3.zero;
    }

    void DrawDebugVisuals(Vector3 prePredictionPos, Vector3 postPredictionPos)
    {
        // Draw green reticle at predicted target
        Debug.DrawLine(new Vector3(postPredictionPos.x, postPredictionPos.y - .5f, 0), 
                      new Vector3(postPredictionPos.x, postPredictionPos.y + .5f, 0), Color.green);
        Debug.DrawLine(new Vector3(postPredictionPos.x - .5f, postPredictionPos.y, 0), 
                      new Vector3(postPredictionPos.x + .5f, postPredictionPos.y, 0), Color.green);

        // Draw yellow line from player to predicted target
        Debug.DrawLine(new Vector3(target.position.x, target.position.y, 0), 
                      new Vector3(postPredictionPos.x, postPredictionPos.y, 0), Color.yellow);
    }

    // Public API
    public void SetTarget(Transform newTarget) => target = newTarget;
    public Vector3 GetCurrentVelocity() => currentVelocity;
    
    // Zoom control methods
    public void SetZoomEnabled(bool enabled) => zoomController.SetEnabled(enabled);
    public void SetZoomParameters(float minSize, float maxSize, float velocityThreshold = 50f, float minVelocity = 5f)
    {
        zoomController.SetZoomRange(minSize, maxSize);
        zoomController.SetVelocityThreshold(velocityThreshold, minVelocity);
    }
    
    public void ForceZoom(float size, float duration = 0f)
    {
        if (duration <= 0f)
        {
            cam.orthographicSize = size;
        }
        else
        {
            StartCoroutine(AnimateZoom(size, duration));
        }
    }

    System.Collections.IEnumerator AnimateZoom(float targetSize, float duration)
    {
        float startSize = cam.orthographicSize;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float t = elapsed / duration;
            cam.orthographicSize = Mathf.Lerp(startSize, targetSize, t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        cam.orthographicSize = targetSize;
    }

    public void Shake(float intensity, float duration = 0.3f)
    {
        StartCoroutine(DoShake(intensity, duration));
    }

    System.Collections.IEnumerator DoShake(float intensity, float duration)
    {
        Vector3 originalPos = transform.position;
        float originalSize = cam.orthographicSize;
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
    
    // For debugging in OnGUI (optional)
    void OnGUI()
    {
        if (showDebugVisuals && target != null)
        {
            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.normal.textColor = Color.white;
            style.fontSize = 12;
            
            float effectiveVelocity = zoomController.GetLastVelocity().magnitude;
            string debugText = $"Camera Size: {cam.orthographicSize:F1}\n" +
                              $"Velocity: {effectiveVelocity:F1}\n" +
                              $"Min/Max: {zoomController.minOrthographicSize}/{zoomController.maxOrthographicSize}\n" +
                              $"Threshold: {zoomController.velocityThreshold}";
            
            GUI.Label(new Rect(10, 10, 300, 100), debugText, style);
        }
    }
}