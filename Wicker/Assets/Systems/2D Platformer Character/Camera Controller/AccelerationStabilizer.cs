using UnityEngine;

public class AccelerationStabilizer : MonoBehaviour
{
    private Rigidbody2D rb;
    private Vector3 lastPhysicsVelocity;
    private Vector3 stableAcceleration;
    private Vector3 currentStableVelocity;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            Debug.LogError("AccelerationStabilizer requires a Rigidbody2D.");
            this.enabled = false;
            return;
        }
        lastPhysicsVelocity = rb.linearVelocity;
        currentStableVelocity = rb.linearVelocity;
    }

    void FixedUpdate()
    {
        // Sample velocity consistently in the physics loop
        Vector3 newVelocity = rb.linearVelocity;

        // Calculate acceleration using fixedDeltaTime (constant)
        stableAcceleration = (newVelocity - lastPhysicsVelocity) / Time.fixedDeltaTime;

        lastPhysicsVelocity = newVelocity;
        currentStableVelocity = newVelocity;
    }

    // Public getters for PredictiveCameraModifier
    public Vector3 GetStableVelocity() => currentStableVelocity;
    public Vector3 GetStableAcceleration() => stableAcceleration;
}