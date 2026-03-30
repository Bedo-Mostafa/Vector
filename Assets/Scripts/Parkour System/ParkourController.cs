using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ParkourController : MonoBehaviour
{
    [SerializeField] List<ParkourAction> parkourActions;

    [Header("UI Prompts")]
    [SerializeField] GameObject parkourPromptUI;

    [Header("Jump Settings")]
    [Tooltip("Extra forward multiplier when doing a running gap-jump")]
    [SerializeField] float gapJumpForwardMultiplier = 1.5f;

    [Header("Animation Blending (Advanced)")]
    [Tooltip("How fast the Animator blends into the parkour animation. Lower is faster.")]
    [SerializeField] float actionCrossfade = 0.15f;
    [Tooltip("How long to wait before an animation transition is allowed to end the action.")]
    [SerializeField] float transitionSafetyTimeout = 0.5f;
    [Tooltip("The Animator layer index where your parkour animations play (Base Layer is usually 0).")]
    [SerializeField] int animatorLayer = 0;

    // ── State ─────────────────────────────────────────────────────────────
    bool inAction;
    EnvironmentScanner environmentScanner;
    Animator animator;
    PlayerController playerController;

    bool isActionQueued;           // Remembers if we pressed jump early
    ParkourAction queuedAction;    // Remembers WHICH action we are waiting to perform

    // ─────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        environmentScanner = GetComponent<EnvironmentScanner>();
        animator = GetComponent<Animator>();

        playerController = GetComponent<PlayerController>()
                          ?? GetComponentInParent<PlayerController>()
                          ?? GetComponentInChildren<PlayerController>();

        if (playerController == null) Debug.LogError("[ParkourController] PlayerController not found!", this);
        if (environmentScanner == null) Debug.LogError("[ParkourController] EnvironmentScanner not found!", this);
        if (animator == null) Debug.LogError("[ParkourController] Animator not found!", this);
    }
    private void Update()
    {
        if (inAction) return;

        var lookaheadHit = environmentScanner.LookaheadObstacleCheck();
        var closeHit = environmentScanner.ObstacleCheck();

        // ── 1. Queued Action Logic (Waiting for the perfect distance) ─────
        if (isActionQueued)
        {
            // If the lookahead ray is still hitting the obstacle...
            if (lookaheadHit.forwardHitFound)
            {
                // Check if we have closed the distance!
                if (lookaheadHit.forwardHit.distance <= queuedAction.ActionTriggerDistance)
                {
                    // Target distance reached! Execute the animation!
                    isActionQueued = false;
                    StartCoroutine(DoParkourAction(queuedAction));
                }
            }
            else
            {
                // We lost the obstacle (the player turned away or stopped)
                isActionQueued = false;
                queuedAction = null;
            }

            // Stop running the rest of the Update loop while we are waiting
            return;
        }


        // ── 2. UI Prompt Logic ────────────────────────────────────────────
        bool showUI = false;
        foreach (var action in parkourActions)
        {
            if (!action.IsFallback && !action.IsPhysicsJump && action.CheckIfPossible(lookaheadHit, transform))
            {
                showUI = true;
                break;
            }
        }

        if (showUI) ShowPrompt();
        else HidePrompt();


        // ── 3. Manual Input (Jump Button) ─────────────────────────────────
        if (Input.GetButtonDown("Jump"))
        {
            foreach (var action in parkourActions)
            {
                if (!action.IsFallback && action.CheckIfPossible(lookaheadHit, transform))
                {
                    if (action.IsPhysicsJump)
                    {
                        // Open-air jump happens immediately
                        if (playerController.IsGrounded)
                        {
                            playerController.DoJump(gapJumpForwardMultiplier);
                        }
                    }
                    else
                    {
                        // It's a parkour move! QUEUE IT instead of doing it instantly.
                        queuedAction = action;
                        isActionQueued = true;
                        HidePrompt();
                    }
                    break;
                }
            }
            return;
        }


        // ── 4. Fallback Auto-Trigger (Crashed into obstacle) ──────────────
        if (closeHit.forwardHitFound && closeHit.heightHitFound)
        {
            foreach (var action in parkourActions)
            {
                if (action.IsFallback && action.CheckIfPossible(closeHit, transform))
                {
                    HidePrompt();
                    StartCoroutine(DoParkourAction(action));
                    break;
                }
            }
        }
    }

    IEnumerator DoParkourAction(ParkourAction action)
    {
        inAction = true;
        playerController.SetControl(false);

        // Tell Animator we are in an action (prevents falling transition)
        animator.SetBool("inAction", true);

        // Crossfade into the parkour animation using our new Inspector variable
        animator.SetBool("mirrorAction", action.Mirror);
        animator.CrossFade(action.AnimName, actionCrossfade);

        // Wait a couple of frames for the Animator to register the CrossFade
        yield return null;
        yield return null;

        // Get the current animation state info using our Inspector layer variable
        var animState = animator.GetNextAnimatorStateInfo(animatorLayer);
        while (animator.IsInTransition(animatorLayer)) yield return null;

        if (animState.length <= 0f)
            animState = animator.GetCurrentAnimatorStateInfo(animatorLayer);

        float timer = 0f;
        while (timer <= animState.length)
        {
            timer += Time.deltaTime;

            if (action.RotateToObstacle)
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation,
                    action.TargetRotation,
                    playerController.RotationSpeed * Time.deltaTime);

            // Use our new safety timeout variable
            if (animator.IsInTransition(animatorLayer) && timer > transitionSafetyTimeout)
                break;

            if (action.EnableTargetMatching)
                MatchTarget(action);

            yield return null;
        }

        yield return new WaitForSeconds(action.PostActionDelay);

        // Action is done, allow normal movement and physics again
        animator.SetBool("inAction", false);
        playerController.SetControl(true);
        inAction = false;
    }

    // ─────────────────────────────────────────────────────────────────────

    void MatchTarget(ParkourAction action)
    {
        if (animator.isMatchingTarget) return;
        animator.MatchTarget(
            action.MatchPos,
            transform.rotation,
            action.MatchBodyPart,
            new MatchTargetWeightMask(action.MatchPosWeight, 0),
            action.MatchStartTime,
            action.MatchTargetTime);
    }

    void ShowPrompt()
    {
        if (parkourPromptUI != null) parkourPromptUI.SetActive(true);
    }

    void HidePrompt()
    {
        if (parkourPromptUI != null) parkourPromptUI.SetActive(false);
    }
}