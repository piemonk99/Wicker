using UnityEngine;

public class DamageText : WorldSpaceText
{
    [Header("Damage Text Settings")]
    public bool useRandomOffset = true;
    public Vector2 randomOffsetRange = new Vector2(-0.5f, 0.5f);
    public float scalePunch = 1.2f;
    public float scalePunchDuration = 0.2f;

    private Vector3 originalScale;
    private float punchTime = 0f;

    protected override void OnEnable()
    {
        base.OnEnable();

        originalScale = transform.localScale;
        punchTime = 0f;

        // Apply random offset
        if (useRandomOffset)
        {
            Vector3 offset = new Vector3(
                Random.Range(randomOffsetRange.x, randomOffsetRange.y),
                0,
                Random.Range(randomOffsetRange.x, randomOffsetRange.y)
            );
            transform.position += offset;
            startPosition = transform.position;
        }

        // Initial scale punch
        transform.localScale = originalScale * scalePunch;
    }

    protected override void Update()
    {
        base.Update();

        // Handle scale punch animation
        if (punchTime < scalePunchDuration)
        {
            punchTime += Time.deltaTime;
            float t = Mathf.Clamp01(punchTime / scalePunchDuration);
            transform.localScale = Vector3.Lerp(originalScale * scalePunch, originalScale, t);
        }
    }

    public override void Initialize(string text, Color color, float? customDuration = null)
    {
        base.Initialize(text, color, customDuration);

        // Add crit indicator
        if (TextMesh != null)
        {
            // Optional: Make crits bigger/bolder
            if (color == Color.yellow) // Example crit color
            {
                TextMesh.fontSize *= 1.5f;
                TextMesh.fontStyle = TMPro.FontStyles.Bold;
            }
        }
    }
}