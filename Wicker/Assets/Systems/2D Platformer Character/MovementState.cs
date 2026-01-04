using UnityEngine;

[System.Serializable]
public class MovementState
{
    public string name;
    public MovementStateType type = MovementStateType.Base;
    public int priority = 0;

    // Input control
    public bool allowMovement = true;

    // Physics control
    public bool applyGravity = true;
    public bool applyDeceleration = true;
    public bool canJump = true;

    // Multipliers
    public float gravityMultiplier = 1f;
    public float groundAccelerationMultiplier = 1f;
    public float groundDecelerationMultiplier = 1f;
    public float airAccelerationMultiplier = 1f;
    public float airDecelerationMultiplier = 1f;
    public float jumpForceMultiplier = 1f;
    public float maxSpeedMultiplier = 1f;

    public MovementState(
        string name = "Unnamed State",
        MovementStateType type = MovementStateType.Base,
        int priority = 0,
        bool allowMovement = true,
        bool applyGravity = true,
        bool applyDeceleration = true,
        bool canJump = true,
        float gravityMultiplier = 1f,
        float groundAccelerationMultiplier = 1f,
        float groundDecelerationMultiplier = 1f,
        float airAccelerationMultiplier = 1f,
        float airDecelerationMultiplier = 1f,
        float jumpForceMultiplier = 1f,
        float maxSpeedMultiplier = 1f
    )
    {
        this.name = name;
        this.type = type;
        this.priority = priority;
        this.allowMovement = allowMovement;
        this.applyGravity = applyGravity;
        this.applyDeceleration = applyDeceleration;
        this.canJump = canJump;
        this.gravityMultiplier = gravityMultiplier;
        this.groundAccelerationMultiplier = groundAccelerationMultiplier;
        this.groundDecelerationMultiplier = groundDecelerationMultiplier;
        this.airAccelerationMultiplier = airAccelerationMultiplier;
        this.airDecelerationMultiplier = airDecelerationMultiplier;
        this.jumpForceMultiplier = jumpForceMultiplier;
        this.maxSpeedMultiplier = maxSpeedMultiplier;
    }

    public MovementState CombineWith(MovementState other)
    {
        return new MovementState(
            name: $"{this.name}+{other.name}",
            type: MovementStateType.Modifier,
            priority: 0,
            allowMovement: this.allowMovement && other.allowMovement,
            applyGravity: this.applyGravity && other.applyGravity,
            applyDeceleration: this.applyDeceleration && other.applyDeceleration,
            canJump: this.canJump && other.canJump,
            gravityMultiplier: this.gravityMultiplier * other.gravityMultiplier,
            groundAccelerationMultiplier: this.groundAccelerationMultiplier * other.groundAccelerationMultiplier,
            groundDecelerationMultiplier: this.groundDecelerationMultiplier * other.groundDecelerationMultiplier,
            airAccelerationMultiplier: this.airAccelerationMultiplier * other.airAccelerationMultiplier,
            airDecelerationMultiplier: this.airDecelerationMultiplier * other.airDecelerationMultiplier,
            jumpForceMultiplier: this.jumpForceMultiplier * other.jumpForceMultiplier,
            maxSpeedMultiplier: this.maxSpeedMultiplier * other.maxSpeedMultiplier
        );
    }
}

public enum MovementStateType
{
    Base,     // Priority-based, only one active
    Modifier  // Stackable multipliers
}

// Helper class for state change events
public class BaseStateChangeData
{
    public string previousState;
    public string newState;

    public BaseStateChangeData(string previous, string current)
    {
        previousState = previous;
        newState = current;
    }
}