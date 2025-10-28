using UnityEngine;
using Cinemachine;
using System.Collections;

public class PlayerMovement : MonoBehaviour
{
    #region Variables

    // Serialized Fields (Visible in Inspector)
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float jumpForce = 7f;
    [SerializeField] [Tooltip("Horizontal push force during a wall jump")] private float wallJumpPushForce = 5f;
    [SerializeField] private Transform characterModel;

    [Header("Game Feel Settings")]
    [SerializeField] [Tooltip("Multiplier reducing jump height when jump button is released")] private float jumpCutMultiplier = 0.5f;
    [SerializeField] [Tooltip("Time in seconds the player can still jump after leaving ground")] private float coyoteTime = 0.1f;
    [SerializeField] [Tooltip("Time in seconds a jump input is buffered before landing")] private float jumpBufferTime = 0.1f;

    [Header("Juice Effects")]
    [SerializeField] private GameObject landingDustEffect;
    [SerializeField] private GameObject runningDustEffect;
    [SerializeField] private AudioClip jumpSound;
    [SerializeField] private AudioClip landSound;
    [SerializeField] private CinemachineImpulseSource impulseSource;

    [Header("Parkour")]
    [SerializeField] private float parkourDetectionRange = 1f;
    [SerializeField] private float parkourDetectionHeight = 1.0f;
    [SerializeField] private float parkourMinDistance = 0.5f;
    [SerializeField] private LayerMask obstacleLayer;

    [Header("Vault Scripted Move")]
    [SerializeField] [Tooltip("Distance BEFORE obstacle center to start vault")] private float vaultStartOffset = 0.8f;
    [SerializeField] [Tooltip("Distance AFTER obstacle center to end vault")] private float vaultEndOffset = 0.8f;
    [SerializeField] [Tooltip("Vertical hop height during vault")] private float vaultHopHeight = 0.5f;
    [SerializeField] [Tooltip("Total duration of the vault (MUST match animation speed)")] private float vaultDuration = 0.8f;

    [Header("Slide")]
    [SerializeField] [Tooltip("Collider height during slide")] private float slideColliderHeight = 1f;
    [SerializeField] [Tooltip("Collider center Y during slide")] private float slideColliderCenterY = 0.5f;
    [SerializeField] [Tooltip("Duration of the slide in seconds")] private float slideDuration = 0.7f;

    // Private Components
    private Rigidbody rb;
    private Animator animator;
    private CapsuleCollider playerCollider;
    private AudioSource audioSource;
    private ParticleSystem currentRunningDust;

    // State Variables
    private bool isGrounded;
    private float moveInput;
    private bool isFacingRight = true;
    private float coyoteTimeCounter;
    private float jumpBufferCounter;
    private bool wasGrounded;
    private bool isAgainstWall = false;
    private bool canVault = false;
    private Collider vaultObstacle = null;
    private bool isWallJumping = false;
    private bool IsSliding = false; // Note: 'I' majuscule to match Animator param
    private float originalColliderHeight;
    private Vector3 originalColliderCenter;
    private Collider currentObstacle; // Used by vault coroutine

    #endregion

    #region Unity Lifecycle Methods

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        playerCollider = GetComponent<CapsuleCollider>();
        if (playerCollider == null) Debug.LogError("ERREUR : CapsuleCollider introuvable sur le Player !");

        audioSource = GetComponent<AudioSource>();
        if(audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;

        if (characterModel == null && transform.childCount > 0) characterModel = transform.GetChild(0);
        if (characterModel != null) animator = characterModel.GetComponent<Animator>();

        if (animator != null) animator.applyRootMotion = false;

        if (playerCollider != null)
        {
            originalColliderHeight = playerCollider.height;
            originalColliderCenter = playerCollider.center;
        }
        else
        {
             originalColliderHeight = 2f;
             originalColliderCenter = Vector3.up;
             Debug.LogError("Utilisation de valeurs par défaut pour le collider car il n'a pas été trouvé!");
        }


        if (runningDustEffect != null)
        {
            GameObject dust = Instantiate(runningDustEffect, transform.position, Quaternion.identity, transform);
            currentRunningDust = dust.GetComponent<ParticleSystem>();
            if (currentRunningDust != null) currentRunningDust.Stop();
        }
        if (impulseSource == null && Camera.main != null) impulseSource = Camera.main.GetComponent<CinemachineImpulseSource>();

        wasGrounded = isGrounded;
    }

    void Update()
    {
        HandleRawInput();
        HandleRotation();
        HandleActionInput();
        CheckEnvironmentStatus();
        UpdateAnimatorParameters();
        HandleJumpStateLogic();
        HandleVariableJumpCut();
        HandleRunningDust();
    }


    void FixedUpdate()
    {
        HandleHorizontalMovement();
        HandleJumpExecution();
    }

    private void LateUpdate()
    {
        wasGrounded = isGrounded;
        if(animator != null) animator.SetBool("IsGrounded", isGrounded);
    }

    private void OnCollisionStay(Collision collision)
    {
        if (collision.gameObject.CompareTag("Ground") || collision.gameObject.CompareTag("Vault"))
        {
            if (!wasGrounded && !isGrounded) TriggerLandingEffects();
            isGrounded = true;
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        if (collision.gameObject.CompareTag("Ground") || collision.gameObject.CompareTag("Vault"))
        {
            isGrounded = false;
        }
    }
    #endregion

    #region Input & State Handling

    void HandleRawInput()
    {
        if (isWallJumping) moveInput = 0;
        else moveInput = Input.GetAxis("P1_Horizontal");
    }


    void HandleActionInput()
    {
        if (Input.GetButtonDown("P1_B1") && !IsSliding && !isWallJumping)
            jumpBufferCounter = jumpBufferTime;

        if (Input.GetButtonDown("P1_B2") && isGrounded && !IsSliding && !isWallJumping && Mathf.Abs(moveInput) > 0.1f)
        {
            IsSliding = true;
            StartCoroutine(DoSlide());
        }
    }


    void CheckEnvironmentStatus()
    {
        if (isWallJumping || IsSliding) // Skip checks if busy
        {
            isAgainstWall = false; canVault = false; vaultObstacle = null; return;
        }
        CheckEnvironment(); // Perform raycasts
    }

     void UpdateAnimatorParameters()
    {
        if(animator == null) return;
        if (isAgainstWall && !isWallJumping) animator.SetFloat("Speed", 0);
        else animator.SetFloat("Speed", Mathf.Abs(moveInput));
        animator.SetBool("IsSliding", IsSliding);
    }


    void HandleJumpStateLogic()
    {
        if (isGrounded) { coyoteTimeCounter = coyoteTime; if (isWallJumping) isWallJumping = false; }
        else { coyoteTimeCounter -= Time.deltaTime; }

         if (Input.GetButtonDown("P1_B1") && canVault && !isWallJumping && !IsSliding)
         {
             StartCoroutine(DoVault(vaultObstacle));
             jumpBufferCounter = 0f; coyoteTimeCounter = 0f;
             // Don't return, allow buffer decrement
         }
        if (jumpBufferCounter > 0f) jumpBufferCounter -= Time.deltaTime;
    }

     void HandleRotation()
     {
        // Allow rotation even when sliding now
        if (!isWallJumping)
        {
            if (moveInput > 0.01f && !isFacingRight) Flip();
            else if (moveInput < -0.01f && isFacingRight) Flip();
        }
    }

    void Flip()
    {
        isFacingRight = !isFacingRight;
        transform.Rotate(0f, 180f, 0f);
    }


    void HandleVariableJumpCut()
    {
        if (Input.GetButtonUp("P1_B1") && rb.velocity.y > 0)
        {
            rb.velocity = new Vector3(rb.velocity.x, rb.velocity.y * jumpCutMultiplier, rb.velocity.z);
            coyoteTimeCounter = 0f;
        }
    }

    void HandleRunningDust()
    {
        if (currentRunningDust == null || animator == null) return;
        bool isRunning = isGrounded && animator.GetFloat("Speed") > 0.1f && !IsSliding;
        if (isRunning && !currentRunningDust.isPlaying) currentRunningDust.Play();
        else if (!isRunning && currentRunningDust.isPlaying) currentRunningDust.Stop();
    }


    #endregion

    #region Physics & Movement Execution

    void HandleHorizontalMovement()
    {
        if (!isWallJumping && !IsSliding) // Apply velocity only if not wall jumping OR sliding
        {
            if (isAgainstWall) rb.velocity = new Vector3(0, rb.velocity.y, 0);
            else rb.velocity = new Vector3(moveInput * moveSpeed, rb.velocity.y, 0);
        }
        // If sliding, Drag slows player
    }


    void HandleJumpExecution()
    {
        if (jumpBufferCounter > 0f && coyoteTimeCounter > 0f && !(canVault && Input.GetButtonDown("P1_B1")))
        {
            if (isAgainstWall) // Wall Jump
            {
                isWallJumping = true;
                float jumpDirection = isFacingRight ? -1f : 1f;
                rb.velocity = new Vector3(rb.velocity.x, 0, 0);
                rb.AddForce(new Vector3(jumpDirection * wallJumpPushForce, jumpForce), ForceMode.Impulse);
                Flip();
                Invoke("StopWallJump", 0.3f);
            }
            else // Normal Jump
            {
                rb.velocity = new Vector3(rb.velocity.x, 0, rb.velocity.z);
                rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            }
            if(animator != null) animator.SetTrigger("Jump");
            jumpBufferCounter = 0f; coyoteTimeCounter = 0f;
            if (jumpSound != null && audioSource != null) audioSource.PlayOneShot(jumpSound);
        }
    }

    private void StopWallJump()
    {
        isWallJumping = false;
    }

    #endregion

    #region Environment Checks
    private void CheckEnvironment()
    {
        isAgainstWall = false; canVault = false; vaultObstacle = null;
        RaycastHit hit;
        Vector3 rayStart = transform.position + Vector3.up * parkourDetectionHeight;
        Vector3 rayDirection = isFacingRight ? Vector3.right : Vector3.left;
        Debug.DrawRay(rayStart, rayDirection * parkourDetectionRange, Color.red);
        Debug.DrawRay(rayStart, rayDirection * parkourMinDistance, Color.yellow);
        if (Physics.Raycast(rayStart, rayDirection, out hit, parkourMinDistance, obstacleLayer))
        { isAgainstWall = true; return; }
        if (Physics.Raycast(rayStart, rayDirection, out hit, parkourDetectionRange, obstacleLayer))
        { if (hit.collider.CompareTag("Vault")) { canVault = true; vaultObstacle = hit.collider; } }
    }
    #endregion

    #region Action Coroutines

    private IEnumerator DoVault(Collider obstacleCollider)
    {
        this.enabled = false; rb.isKinematic = true;
        if(playerCollider != null) playerCollider.enabled = false;
        if(obstacleCollider != null) obstacleCollider.enabled = false;

        if(animator != null) animator.SetTrigger("DoVault");

        float timer = 0f;
        Vector3 direction = isFacingRight ? Vector3.right : Vector3.left;
        Vector3 boxCenter = obstacleCollider != null ? obstacleCollider.transform.position : transform.position;
        float startY = transform.position.y;
        Vector3 startPos = new Vector3(boxCenter.x, startY, transform.position.z) - (direction * vaultStartOffset);
        Vector3 targetPos = new Vector3(boxCenter.x, startY, transform.position.z) + (direction * vaultEndOffset);
        transform.position = startPos;

        while (timer < vaultDuration)
        {
            float t = timer / vaultDuration;
            Vector3 newPos = Vector3.Lerp(startPos, targetPos, t);
            newPos.y = startY + Mathf.Sin(t * Mathf.PI) * vaultHopHeight;
            transform.position = newPos;
            timer += Time.deltaTime;
            yield return null;
        }
        transform.position = targetPos;

        if(playerCollider != null) playerCollider.enabled = true;
        rb.isKinematic = false; this.enabled = true;
        if(obstacleCollider != null) obstacleCollider.enabled = true;
    }


    // Coroutine managing the slide state and collider changes
    private IEnumerator DoSlide()
    {
        // IsSliding is set true immediately in HandleActionInput

        if (playerCollider != null)
        {
            // Change collider
            playerCollider.height = slideColliderHeight;
            playerCollider.center = new Vector3(originalColliderCenter.x, slideColliderCenterY, originalColliderCenter.z);
        }

        yield return new WaitForSeconds(slideDuration); // Wait

        // Restore collider ONLY if we are still sliding
        if (IsSliding && playerCollider != null)
        {
            playerCollider.height = originalColliderHeight;
            playerCollider.center = originalColliderCenter;

            IsSliding = false; // Unlock state AFTER restoring collider

            // --- NOUVEAU : Forcer la rotation à la fin ---
            float currentMoveInput = Input.GetAxis("P1_Horizontal"); // Lire l'input actuel
            if (currentMoveInput > 0.01f)
            {
                transform.rotation = Quaternion.Euler(0f, 0f, 0f); // Force vers la droite
                isFacingRight = true;
            }
            else if (currentMoveInput < -0.01f)
            {
                transform.rotation = Quaternion.Euler(0f, 180f, 0f); // Force vers la gauche
                isFacingRight = false;
            }
             // Si l'input est neutre, on garde la direction d'avant la glissade (implicite)
            // --- FIN NOUVEAU ---
        }
        else if (playerCollider == null) // Safety check
        {
             IsSliding = false;
        }
    }


    #endregion

    #region Helper Methods
    private void TriggerLandingEffects()
    {
        if (landingDustEffect != null) Instantiate(landingDustEffect, transform.position, Quaternion.identity);
        if (landSound != null && audioSource != null) audioSource.PlayOneShot(landSound);
        if (impulseSource != null) impulseSource.GenerateImpulse();
    }
    #endregion
}