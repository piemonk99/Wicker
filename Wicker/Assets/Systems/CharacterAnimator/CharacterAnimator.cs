using UnityEngine;

public class CharacterAnimator : MonoBehaviour, ICharacterComponent
{
    [Header("References")]
    public SpriteRenderer spriteRenderer;
    public Animator animator;

    private CharacterCore character;

    public void Initialize(CharacterCore core)
    {
        character = core;
        core.OnEvent += HandleEvent;
    }

    public void Tick(float deltaTime) { }
    public void PhysicsTick(float fixedDeltaTime) { }

    private void HandleEvent(string type, object data)
    {
        if (type == "move_input")
        {
            Vector2 input = (Vector2)data;

            // Flip sprite based on direction
            if (Mathf.Abs(input.x) > 0.1f)
            {
                spriteRenderer.flipX = input.x < 0;
            }

            // Set animator parameters
            if (animator != null)
            {
                animator.SetFloat("speed", Mathf.Abs(input.x));
                animator.SetBool("grounded", true); // You'd get this from movement system
            }
        }
        else if (type == "jumped")
        {
            if (animator != null)
                animator.SetTrigger("jump");
        }
        else if (type == "action_started")
        {
            string action = (string)data;
            if (animator != null)
                animator.SetTrigger(action);
        }
    }
}