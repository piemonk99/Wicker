using UnityEngine;

public class PredictiveCameraModifier : MonoBehaviour
{
    [Header("Prediction Settings")]
    public bool isActive = true;

    [Header("Velocity Prediction")]
    public float velocityLookAheadTime = 0.3f;
    [Range(0f, 1f)] public float velocityWeightX = 0.7f;
    [Range(0f, 1f)] public float velocityWeightY = 0.7f;

    [Header("Acceleration Prediction")]
    public bool useAcceleration = true;
    public float accelerationLookAheadTime = 0.2f;
    [Range(0f, 1f)] public float accelerationWeightX = 0.3f;
    [Range(0f, 1f)] public float accelerationWeightY = 0.0f; // Disable Y by default

    [Header("Direction Bias")]
    [Range(0f, 1f)] public float horizontalBias = 1f;
    [Range(0f, 1f)] public float verticalBias = 0.4f;

    [Header("Response & Limits")]
    public float responseSpeed = 8f;
    public float maxPredictionDistance = 4f;
    public float velocityDeadzone = 0.5f;

    // State
    private Vector3 previousVelocity;
    private Vector3 currentVelocity;
    private Vector3 currentAcceleration;
    private Vector3 smoothedPrediction;

    // For debugging/visualization
    private Vector3 velocityPrediction;
    private Vector3 accelerationPrediction;

    public bool IsActive() => isActive;

    public Vector3 ModifyPosition(Vector3 currentPosition, Transform target, CameraController controller)
    {
        if (target == null) return currentPosition;

        // Get velocity from Rigidbody
        Vector3 velocity = GetTargetVelocity(target);

        // Calculate acceleration (change in velocity over time)
        currentAcceleration = CalculateAcceleration(velocity);
        previousVelocity = velocity;

        // Store for external access (if other systems need it)
        currentVelocity = velocity;

        Debug.Log($"Raw Velocity: {velocity} \n" +
                  $"Acceleration: {currentAcceleration}\n" +
                  $"");

        // Apply deadzone - ignore tiny movements
        if (velocity.magnitude < velocityDeadzone)
        {
            velocity = Vector3.zero;
            currentAcceleration = Vector3.zero;
        }

        // Calculate predictions
        velocityPrediction = CalculateVelocityPrediction(velocity);
        accelerationPrediction = CalculateAccelerationPrediction(currentAcceleration);

        // Combine predictions with axis separation
        Vector3 totalPrediction = CombinePredictions(velocityPrediction, accelerationPrediction);

        // Apply direction bias
        totalPrediction = ApplyDirectionBias(totalPrediction);

        Debug.Log($"Total Prediction: {totalPrediction.magnitude}");

        // Smooth the prediction
        smoothedPrediction = Vector3.Lerp(
            smoothedPrediction,
            totalPrediction,
            responseSpeed * Time.deltaTime
        );

        // Apply to camera position
        return currentPosition + smoothedPrediction;
    }

    private Vector3 GetTargetVelocity(Transform target)
    {
        // Try 2D physics first
        Rigidbody2D rb2D = target.GetComponent<Rigidbody2D>();
        if (rb2D != null)
        {
            return rb2D.linearVelocity;
        }

        // Fallback to 3D physics
        Rigidbody rb3D = target.GetComponent<Rigidbody>();
        if (rb3D != null)
        {
            return rb3D.linearVelocity;
        }

        // Fallback: Estimate velocity from position change
        return Vector3.zero;
    }

    private Vector3 CalculateAcceleration(Vector3 currentVelocity)
    {
        // Avoid division by zero on first frame
        if (Time.deltaTime < Mathf.Epsilon) return Vector3.zero;

        return (currentVelocity - previousVelocity) / Time.deltaTime;
    }

    private Vector3 CalculateVelocityPrediction(Vector3 velocity)
    {
        if (velocity.magnitude == 0) return Vector3.zero;

        // Simple: velocity * time
        Vector3 prediction = velocity * velocityLookAheadTime;

        // Apply axis-specific weights
        prediction.x *= velocityWeightX;
        prediction.y *= velocityWeightY;
        prediction.z = 0;

        return prediction;
    }

    private Vector3 CalculateAccelerationPrediction(Vector3 acceleration)
    {
        if (!useAcceleration || acceleration.magnitude == 0) return Vector3.zero;

        // Acceleration prediction looks where the velocity WILL BE
        Vector3 prediction = acceleration * accelerationLookAheadTime;

        // Scale by axis-specific weights and normalize influence
        float accelMagnitude = acceleration.magnitude;
        float normalizedAccel = Mathf.Clamp01(accelMagnitude / 50f);

        prediction.x *= accelerationWeightX * normalizedAccel;
        prediction.y *= accelerationWeightY * normalizedAccel;
        prediction.z = 0;

        return prediction;
    }

    private Vector3 CombinePredictions(Vector3 velocityPred, Vector3 accelerationPred)
    {
        // Simply add them - axis weights already applied
        Vector3 combined = velocityPred + accelerationPred;

        // Clamp to max distance
        if (combined.magnitude > maxPredictionDistance)
        {
            combined = combined.normalized * maxPredictionDistance;
        }

        return combined;
    }

    private Vector3 ApplyDirectionBias(Vector3 prediction)
    {
        // Apply additional global bias (if needed)
        prediction.x *= horizontalBias;
        prediction.y *= verticalBias;
        prediction.z = 0;

        return prediction;
    }

    // Public API for other systems
    public Vector3 GetCurrentPrediction() => smoothedPrediction;
    public Vector3 GetVelocity() => currentVelocity;
    public Vector3 GetAcceleration() => currentAcceleration;

    // Convenience methods for common settings
    public void DisableAccelerationY()
    {
        accelerationWeightY = 0f;
    }

    public void SetVelocityWeights(float xWeight, float yWeight)
    {
        velocityWeightX = Mathf.Clamp01(xWeight);
        velocityWeightY = Mathf.Clamp01(yWeight);
    }

    public void SetAccelerationWeights(float xWeight, float yWeight)
    {
        accelerationWeightX = Mathf.Clamp01(xWeight);
        accelerationWeightY = Mathf.Clamp01(yWeight);
    }

    // Quick presets
    public void ApplyPreset(CameraPredictionPreset preset)
    {
        switch (preset)
        {
            case CameraPredictionPreset.Platformer:
                velocityWeightX = 0.8f;
                velocityWeightY = 0.3f;
                accelerationWeightX = 0.2f;
                accelerationWeightY = 0.0f;
                break;
            case CameraPredictionPreset.Racing:
                velocityWeightX = 0.9f;
                velocityWeightY = 0.1f;
                accelerationWeightX = 0.4f;
                accelerationWeightY = 0.1f;
                break;
            case CameraPredictionPreset.Grappling:
                velocityWeightX = 0.7f;
                velocityWeightY = 0.5f;
                accelerationWeightX = 0.5f;
                accelerationWeightY = 0.2f;
                break;
        }
    }
}

// Enum for quick presets
public enum CameraPredictionPreset
{
    Platformer,
    Racing,
    Grappling,
    Custom
}