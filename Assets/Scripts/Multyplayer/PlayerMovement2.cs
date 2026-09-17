using UnityEngine;
using UnityEngine.InputSystem;
using Photon.Pun;
using System.Collections;

public class PlayerMovement2 : MonoBehaviourPun
{
    [Header("Movement")]
    public float speed = 5f;

    private Rigidbody2D rb;
    private Vector2 moveInput;
    private bool isKnockedback = false;

    public Animator animator;
    public PlayerCombat2 playerCombat;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void FixedUpdate()
    {

        if (!NetworkManager.GameStarted) return;
        // Only the owner controls movement
        if (!photonView.IsMine) return;

        if (!isKnockedback)
        {
            rb.linearVelocity = moveInput * speed;

            animator.SetFloat("horizontal", Mathf.Abs(moveInput.x));
            animator.SetFloat("vertical", Mathf.Abs(moveInput.y));

            // Flip
            if ((moveInput.x < 0 && transform.localScale.x > 0) ||
                (moveInput.x > 0 && transform.localScale.x < 0))
            {
                Flip();
            }
        }
    }

    public void Move(InputAction.CallbackContext context)
    {
        if (!NetworkManager.GameStarted) return;

        if (!photonView.IsMine) return;
        moveInput = context.ReadValue<Vector2>();
    }

    public void Attack(InputAction.CallbackContext context)
    {
        if (!NetworkManager.GameStarted) return;

        if (!photonView.IsMine) return;

        if (context.performed)
        {
            playerCombat.Attacking();
        }
    }

    void Flip()
    {
        transform.localScale = new Vector3(
            transform.localScale.x * -1f,
            transform.localScale.y,
            transform.localScale.z
        );
    }

    // Called by PlayerHealth when knockback starts/ends
    public void SetKnockback(bool value)
    {
        isKnockedback = value;
    }
}