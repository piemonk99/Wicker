using UnityEngine;
using TMPro;

public abstract class WorldSpaceText : MonoBehaviour
{
    [Header("Base Settings")]
    public float duration = 2f;
    public Vector3 floatDirection = Vector3.up;
    public float floatSpeed = 1f;
    public AnimationCurve fadeCurve = AnimationCurve.Linear(0, 1, 1, 0);

    public TextMeshProUGUI TextMesh { get; protected set; }

    protected float elapsedTime = 0f;
    protected Vector3 startPosition;
    protected Color startColor;

    protected virtual void Awake()
    {
        // Get TextMeshProUGUI instead!
        TextMesh = GetComponent<TextMeshProUGUI>();
        if (TextMesh == null)
        {
            TextMesh = gameObject.AddComponent<TextMeshProUGUI>();

            // Set reasonable defaults
            TextMesh.fontSize = 24;
            TextMesh.alignment = TextAlignmentOptions.Center;
        }
    }

    protected virtual void OnEnable()
    {
        elapsedTime = 0f;
        startPosition = transform.position;
        if (TextMesh != null)
        {
            startColor = TextMesh.color;
        }
    }

    protected virtual void Update()
    {
        elapsedTime += Time.deltaTime;
        float t = Mathf.Clamp01(elapsedTime / duration);

        // Float movement
        transform.position = startPosition + floatDirection * (floatSpeed * elapsedTime);

        // Fade out
        if (TextMesh != null)
        {
            Color color = TextMesh.color;
            color.a = startColor.a * fadeCurve.Evaluate(t);
            TextMesh.color = color;
        }

        // Return to pool when done
        if (elapsedTime >= duration)
        {
            ReturnToPool();
        }
    }

    public virtual void Initialize(string text, Color color, float? customDuration = null)
    {
        if (TextMesh != null)
        {
            TextMesh.text = text;
            TextMesh.color = color;
        }

        if (customDuration.HasValue)
        {
            duration = customDuration.Value;
        }
    }

    public void SetPosition(Vector3 position)
    {
        transform.position = position;
        startPosition = position;
    }

    protected virtual void ReturnToPool()
    {
        WorldSpaceTextManager.Instance?.ReturnTextToPool(this);
    }
}