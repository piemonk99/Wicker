using UnityEngine;
using System.Collections.Generic;

public class ClimbableRope : MonoBehaviour
{
    [Header("Rope Settings")]
    public bool canClimb = true;
    public Transform ropeAnchor;
    public Transform ropeEnd;

    [Header("Grapple Configuration")]
    public GrappleConfig grappleConfig; // Custom physics for this rope

    [Header("Visuals")]
    public Renderer ropeRenderer;
    public LineRenderer lineRenderer;

    private List<Transform> ropeBones = new List<Transform>();
    private bool bonesInitialized = false;

    public bool CanClimb => canClimb;

    public GrappleConfig GetGrappleConfig()
    {
        return grappleConfig;
    }

    public List<Transform> GetBones()
    {
        if (!bonesInitialized)
        {
            ropeBones.Clear();

            // Find all child transforms
            foreach (Transform child in transform)
            {
                // Look for bones or segments
                if (child.name.ToLower().Contains("bone") ||
                    child.name.ToLower().Contains("segment") ||
                    child.GetComponent<Collider2D>() != null)
                {
                    ropeBones.Add(child);
                }
            }

            // Sort by position (top to bottom)
            ropeBones.Sort((a, b) => b.position.y.CompareTo(a.position.y));

            bonesInitialized = true;
        }

        return ropeBones;
    }

    public Transform GetAnchorTransform()
    {
        if (ropeAnchor != null) return ropeAnchor;

        // Find highest bone
        List<Transform> bones = GetBones();
        if (bones.Count > 0) return bones[0];

        return transform;
    }

    public Transform GetEndTransform()
    {
        if (ropeEnd != null) return ropeEnd;

        // Find lowest bone
        List<Transform> bones = GetBones();
        if (bones.Count > 0) return bones[bones.Count - 1];

        return transform;
    }

    public void SetRopeVisible(bool visible)
    {
        if (ropeRenderer != null)
        {
            ropeRenderer.enabled = visible;
        }

        if (lineRenderer != null)
        {
            lineRenderer.enabled = visible;
        }

        foreach (var bone in GetBones())
        {
            Renderer boneRenderer = bone.GetComponent<Renderer>();
            if (boneRenderer != null)
            {
                boneRenderer.enabled = visible;
            }
        }
    }

    public void SetRopeVisibleAbove(Transform referenceBone, bool visible)
    {
        List<Transform> bones = GetBones();
        int referenceIndex = bones.IndexOf(referenceBone);

        if (referenceIndex >= 0)
        {
            // Hide/show bones above the reference
            for (int i = 0; i <= referenceIndex; i++)
            {
                Renderer renderer = bones[i].GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.enabled = visible;
                }
            }
        }
    }

    void Start()
    {
        // Initialize
        GetBones();

        // Auto-find renderer if not set
        if (ropeRenderer == null)
        {
            ropeRenderer = GetComponent<Renderer>();
        }

        if (lineRenderer == null)
        {
            lineRenderer = GetComponent<LineRenderer>();
        }

        // Create default config if none assigned
        if (grappleConfig == null)
        {
            grappleConfig = CreateDefaultGrappleConfig();
            Debug.LogWarning($"No grapple config assigned to rope {name}, created default");
        }
    }

    private GrappleConfig CreateDefaultGrappleConfig()
    {
        GrappleConfig config = ScriptableObject.CreateInstance<GrappleConfig>();
        config.name = $"{name}_RopeConfig";

        // Set reasonable defaults for ropes
        config.mechanicsConfig.grappleName = "Rope";
        config.mechanicsConfig.createsAnchors = true;

        // Calculate max distance from rope length
        float ropeLength = CalculateRopeLength();
        config.physicsConfig.maxDistance = ropeLength;

        config.physicsConfig.ropeDamping = 0.1f;
        config.physicsConfig.stretchStiffness = 100f;
        config.physicsConfig.enableStretch = true;
        config.physicsConfig.enableSquash = false;

        config.reelConfig.reelSpeed = 3f;
        config.reelConfig.unreelSpeed = 3f;
        config.reelConfig.minRopeLength = 0.5f;
        config.reelConfig.maxRopeLength = ropeLength;

        return config;
    }

    private float CalculateRopeLength()
    {
        List<Transform> bones = GetBones();
        if (bones.Count < 2) return 10f; // Default

        float length = 0f;
        for (int i = 0; i < bones.Count - 1; i++)
        {
            length += Vector2.Distance(bones[i].position, bones[i + 1].position);
        }

        return length;
    }
}