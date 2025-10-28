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
    [SerializeField] [Tooltip("Distance horizontale parcourue pendant la glissade")] private float slideDistance = 3f;

    // --- NOUVEAU : Variables pour la Roulade ---
    [Header("Roll")]
    [SerializeField] [Tooltip("Hauteur minimale de chute pour déclencher la roulade")] private float minFallHeightForRoll = 2f;
    [SerializeField] [Tooltip("Durée de la roulade (doit correspondre à l'animation)")] private float rollDuration = 0.6f;
    [SerializeField] [Tooltip("Distance parcourue pendant la roulade")] private float rollDistance = 2f;
    // --- FIN NOUVEAU ---

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
    private bool IsSliding = false;
    private float originalColliderHeight;
    private Vector3 originalColliderCenter;
    private Collider currentObstacle;

    // --- NOUVEAU : Variables d'état pour la Roulade ---
    private bool isFalling = false;
    private float fallStartY;
    private bool isRolling = false;
    // --- FIN NOUVEAU ---

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
        if (playerCollider != null) { originalColliderHeight = playerCollider.height; originalColliderCenter = playerCollider.center; }
        else { originalColliderHeight = 2f; originalColliderCenter = Vector3.up; Debug.LogError("Utilisation de valeurs par défaut pour le collider!"); }
        if (runningDustEffect != null) { GameObject dust = Instantiate(runningDustEffect, transform.position, Quaternion.identity, transform); currentRunningDust = dust.GetComponent<ParticleSystem>(); if (currentRunningDust != null) currentRunningDust.Stop(); }
        if (impulseSource == null && Camera.main != null) impulseSource = Camera.main.GetComponent<CinemachineImpulseSource>();
        wasGrounded = isGrounded; // Important
    }

    void Update()
    {
        // --- MODIFIÉ : Ajouter le verrouillage 'isRolling' ---
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
        // --- MODIFIÉ : Ajouter le verrouillage 'isRolling' ---
        if (!IsSliding && !isRolling) HandleHorizontalMovement(); // La coroutine gère le mouvement pendant slide/roll
        HandleJumpExecution();
    }

    private void LateUpdate()
    {
        // --- MODIFIÉ : Détection début de chute ---
        // Si on était au sol la frame d'avant ET on ne l'est plus maintenant ET on n'est pas déjà marqué comme tombant
        // ET on ne fait pas déjà une action (roulade, slide, walljump - pour éviter faux positifs)
        if (wasGrounded && !isGrounded && !isFalling && !isRolling && !IsSliding && !isWallJumping)
        {
            isFalling = true;
            fallStartY = transform.position.y; // Enregistre la hauteur de départ
        }
        // Si on touche le sol (et qu'on ne roule pas déjà), on n'est plus en train de tomber
        // (le début de roulade mettra isFalling à false dans OnCollisionStay)
        if(isGrounded && !isRolling)
        {
            isFalling = false;
        }
        // --- FIN MODIFIÉ ---

        wasGrounded = isGrounded; // Met à jour l'état précédent pour la prochaine frame
        if(animator != null) animator.SetBool("IsGrounded", isGrounded);
    }


    private void OnCollisionStay(Collision collision)
    {
        // On vérifie si l'objet touché est bien le sol ou un obstacle franchissable
        if (collision.gameObject.CompareTag("Ground") || collision.gameObject.CompareTag("Vault"))
        {
            // --- MODIFIÉ : Logique d'atterrissage et déclenchement roulade ---
            // On détecte la transition air -> sol UNIQUEMENT
            if (!wasGrounded && !isGrounded)
            {
                // Joue les effets normaux (son, particules, shake)
                TriggerLandingEffects();

                // Vérifie si on était en train de tomber et si la chute est assez longue
                if(isFalling) // 'isFalling' est mis à true dans LateUpdate
                {
                    float fallDistance = fallStartY - transform.position.y;
                    // Déclenche la roulade si la chute est suffisante ET qu'on ne roule/glisse/saute pas déjà
                    if (fallDistance > minFallHeightForRoll && !isRolling && !IsSliding && !isWallJumping)
                    {
                        StartCoroutine(DoRoll());
                        isGrounded = true; // Force l'état au sol immédiatement
                        isFalling = false; // La chute est terminée par la roulade
                        // Ne met PAS à jour wasGrounded ici, LateUpdate s'en charge
                        return; // On sort pour ne pas écraser l'état isGrounded ci-dessous inutilement
                    }
                }
                 // Si on n'a pas roulé, on marque quand même la fin de la chute
                isFalling = false;
            }
            // --- FIN MODIFIÉ ---
            isGrounded = true; // Confirme qu'on est au sol si on reste en contact
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
        // --- MODIFIÉ : Ajouter isRolling ---
        if (isWallJumping || IsSliding || isRolling) moveInput = 0;
        else moveInput = Input.GetAxis("P1_Horizontal");
    }

    void HandleActionInput()
    {
        // --- MODIFIÉ : Ajouter !isRolling aux conditions ---
        if (Input.GetButtonDown("P1_B1") && !IsSliding && !isWallJumping && !isRolling)
            jumpBufferCounter = jumpBufferTime;

        if (Input.GetButtonDown("P1_B2") && isGrounded && !IsSliding && !isWallJumping && Mathf.Abs(moveInput) > 0.1f && !isRolling)
        {
            IsSliding = true;
            StartCoroutine(DoSlide());
        }
    }

    void CheckEnvironmentStatus()
    {
        // --- MODIFIÉ : Ajouter isRolling ---
        if (isWallJumping || IsSliding || isRolling) // Skip checks if busy
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
        // isRolling utilise le Trigger "DoRoll"
    }

    void HandleJumpStateLogic()
    {
        if (isGrounded) { coyoteTimeCounter = coyoteTime; if (isWallJumping) isWallJumping = false; }
        else { coyoteTimeCounter -= Time.deltaTime; }

        // --- MODIFIÉ : Ajouter !isRolling ---
         if (Input.GetButtonDown("P1_B1") && canVault && !isWallJumping && !IsSliding && !isRolling)
         {
             StartCoroutine(DoVault(vaultObstacle));
             jumpBufferCounter = 0f; coyoteTimeCounter = 0f;
             // Don't return, allow buffer decrement
         }
        if (jumpBufferCounter > 0f) jumpBufferCounter -= Time.deltaTime;
    }

     void HandleRotation()
     {
        // --- MODIFIÉ : Ajouter isRolling ---
        // Allow rotation only if not wall jumping or rolling
        if (!isWallJumping && !isRolling)
        {
            if (moveInput > 0.01f && !isFacingRight) Flip();
            else if (moveInput < -0.01f && isFacingRight) Flip();
        }
    }

    void Flip() { isFacingRight = !isFacingRight; transform.Rotate(0f, 180f, 0f); }

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
        // --- MODIFIÉ : Ajouter isRolling ---
        bool isRunning = isGrounded && animator.GetFloat("Speed") > 0.1f && !IsSliding && !isRolling;
        if (isRunning && !currentRunningDust.isPlaying) currentRunningDust.Play();
        else if (!isRunning && currentRunningDust.isPlaying) currentRunningDust.Stop();
    }
    #endregion

    #region Physics & Movement Execution

    void HandleHorizontalMovement()
    {
        // Cette fonction n'est appelée que si on ne glisse PAS et on ne roule PAS
        if (!isWallJumping)
        {
            if (isAgainstWall) rb.velocity = new Vector3(0, rb.velocity.y, 0);
            else rb.velocity = new Vector3(moveInput * moveSpeed, rb.velocity.y, 0);
        }
    }

    void HandleJumpExecution()
    {
       // ... (Ajoute check isRolling) ...
        if (jumpBufferCounter > 0f && coyoteTimeCounter > 0f && !(canVault && Input.GetButtonDown("P1_B1")))
        {
            // --- MODIFIÉ : Ne pas sauter si on roule ---
            if(isRolling) return;
            // --- FIN MODIFIÉ ---

            if (isAgainstWall) { /* ... Wall Jump ... */ isWallJumping = true; float d = isFacingRight?-1f:1f; rb.velocity=new Vector3(rb.velocity.x,0,0); rb.AddForce(new Vector3(d*wallJumpPushForce,jumpForce), ForceMode.Impulse); Flip(); Invoke("StopWallJump",0.3f); }
            else { /* ... Normal Jump ... */ rb.velocity = new Vector3(rb.velocity.x,0,rb.velocity.z); rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse); }
            if(animator != null) animator.SetTrigger("Jump");
            jumpBufferCounter = 0f; coyoteTimeCounter = 0f;
            if (jumpSound != null && audioSource != null) audioSource.PlayOneShot(jumpSound);
        }
    }

    private void StopWallJump() { isWallJumping = false; }
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

    private IEnumerator DoSlide()
    {
        IsSliding = true; // Déjà fait dans Update

        if (playerCollider != null)
        {
            playerCollider.height = slideColliderHeight;
            playerCollider.center = new Vector3(originalColliderCenter.x, slideColliderCenterY, originalColliderCenter.z);
        }

        yield return new WaitForSeconds(slideDuration);

        if (IsSliding && playerCollider != null)
        {
            playerCollider.height = originalColliderHeight;
            playerCollider.center = originalColliderCenter;
            IsSliding = false;

            float currentMoveInput = Input.GetAxis("P1_Horizontal");
            if (currentMoveInput > 0.01f) { transform.rotation = Quaternion.Euler(0f, 0f, 0f); isFacingRight = true; }
            else if (currentMoveInput < -0.01f) { transform.rotation = Quaternion.Euler(0f, 180f, 0f); isFacingRight = false; }
        }
        else if (playerCollider == null) { IsSliding = false; }
    }

    // --- NOUVEAU : Coroutine pour la Roulade ---
    private IEnumerator DoRoll()
    {
        isRolling = true; // Verrouille le joueur
        // On rend Kinematic pour contrôler le mouvement précisément pendant la roulade
        rb.isKinematic = true;
        rb.velocity = Vector3.zero; // Stoppe toute vélocité existante
        // Optionnel: désactiver le collider si l'animation passe à travers le sol
        // if(playerCollider != null) playerCollider.enabled = false;

        // Déclenche l'animation de roulade
        if (animator != null) animator.SetTrigger("DoRoll");

        // --- Mouvement Scripté pendant la roulade ---
        float timer = 0f;
        Vector3 startPos = transform.position;
        Vector3 rollDirection = isFacingRight ? Vector3.right : Vector3.left;
        Vector3 targetPos = startPos + rollDirection * rollDistance;

        while (timer < rollDuration)
        {
            float t = timer / rollDuration;
            // Interpole la position horizontalement, garde Y constant (au niveau de startPos)
            transform.position = Vector3.Lerp(startPos, new Vector3(targetPos.x, startPos.y, targetPos.z), t);
            timer += Time.deltaTime;
            yield return null; // Attend la frame suivante
        }
        transform.position = new Vector3(targetPos.x, startPos.y, targetPos.z); // Assure la position finale
        // --- Fin Mouvement Scripté ---

        isRolling = false; // Déverrouille
        rb.isKinematic = false; // Réactive la physique standard
        // Optionnel: réactiver le collider
        // if(playerCollider != null) playerCollider.enabled = true;

        // Force la rotation correcte à la fin, basée sur l'input actuel
        float currentMoveInput = Input.GetAxis("P1_Horizontal");
        if (currentMoveInput > 0.01f) { transform.rotation = Quaternion.Euler(0f, 0f, 0f); isFacingRight = true; }
        else if (currentMoveInput < -0.01f) { transform.rotation = Quaternion.Euler(0f, 180f, 0f); isFacingRight = false; }
    }
    // --- FIN NOUVEAU ---

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