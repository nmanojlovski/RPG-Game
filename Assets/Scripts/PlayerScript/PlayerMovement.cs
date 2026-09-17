using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class PlayerMovement : MonoBehaviour
{

    private Rigidbody2D rb;
    private Vector2 moveInput;
    private bool isKnockedback = false;

    public Animator animator;
    public PlayerCombat playerCombat;


    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
    }


    
    void FixedUpdate()
    {

        if (!isKnockedback) 
        { 
            rb.linearVelocity = moveInput * StatsManager.Instance.speed;

            animator.SetFloat("horizontal", Mathf.Abs(moveInput.x));
            animator.SetFloat("vertical", Mathf.Abs(moveInput.y));

            if (moveInput.x < 0 && transform.localScale.x > 0 || moveInput.x > 0 && transform.localScale.x < 0)
            {
                Flip();
            }
        }

        
    }

    public void Move(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
    }

    void Flip()
    {
        transform.localScale = new Vector3(transform.localScale.x * -1, transform.localScale.y, transform.localScale.z);
    }

    public void Knockback(Transform enemy, float knockforce, float stunTime)
    {
        isKnockedback = true;

        Vector2 direction = (transform.position - enemy.position).normalized;
        rb.linearVelocity = direction * knockforce;

        StartCoroutine(KnockbackCounter(stunTime));
    }

    IEnumerator KnockbackCounter(float stunTime)
    {
        yield return new WaitForSeconds(stunTime);
        rb.linearVelocity = Vector2.zero;
        isKnockedback = false;
    }



    public void Attack(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            playerCombat.Attacking();
        }
    }
}
