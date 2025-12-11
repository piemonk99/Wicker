using UnityEngine;

[CreateAssetMenu(fileName = "GrappleType", menuName = "Scriptable Objects/GrappleType")]
public class GrappleType : ScriptableObject
{
    public string grappleName = "Basic";

    [Header("Physics")]
    public float maxDistance = 20f;
    public float launchSpeed = 25f;
    public float retractSpeed = 15f;
    public float swingForce = 50f;
    public float dampening = 0.95f;

    [Header("Movement Overrides")]
    public bool allowHorizontalControl = true;
    public bool applyGravity = true;
    public float gravityMultiplier = 1f;
    public float airControlMultiplier = 0.5f;

    [Header("Visuals")]
    public Material ropeMaterial;
    public float ropeWidth = 0.1f;
    public Color ropeColor = Color.white;

    [Header("Audio")]
    public AudioClip fireSound;
    public AudioClip retractSound;
    public AudioClip attachSound;
}
