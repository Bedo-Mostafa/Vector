using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ParkourController : MonoBehaviour
{
    [SerializeField] List<ParkourAction> parkourActions;

    [Header("Timing Window")]
    [SerializeField] float parkourTimingWindow = 0.8f;
    [SerializeField] GameObject parkourPromptUI;

    [Header("Gap Jump")]
    [Tooltip("Extra forward multiplier when doing a running gap-jump")]
    [SerializeField] float gapJumpForwardMultiplier = 1.5f;

    // ── State ─────────────────────────────────────────────────────────────
    bool inAction;
    bool promptActive;
    float promptTimer;
    ParkourAction pendingAction;

    EnvironmentScanner environmentScanner;
    Animator animator;
    PlayerController playerController;

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

        // ── Active parkour prompt window ──────────────────────────────────
        if (promptActive)
        {
            promptTimer -= Time.deltaTime;

            if (Input.GetButtonDown("Jump"))
            {
                var action = pendingAction;
                HidePrompt();
                StartCoroutine(DoParkourAction(action));
                return;
            }

            if (promptTimer <= 0f)
                HidePrompt();

            return;
        }

        // ── Free jump — no obstacle ahead ────────────────────────────────
        if (Input.GetButtonDown("Jump") && playerController.IsGrounded)
        {
            // Pass a higher forward multiplier for that Vector-style gap leap
            playerController.DoJump(gapJumpForwardMultiplier);
            StartCoroutine(WaitForLanding());
            return;
        }

        // ── Lookahead obstacle detection ──────────────────────────────────
        var lookahead = environmentScanner.LookaheadObstacleCheck();
        if (lookahead.forwardHitFound && lookahead.heightHitFound)
        {
            foreach (var action in parkourActions)
            {
                if (action.CheckIfPossible(lookahead, transform))
                {
                    pendingAction = action;
                    ShowPrompt();
                    break;
                }
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Free jump — just block new actions until the player lands
    // ─────────────────────────────────────────────────────────────────────

    IEnumerator WaitForLanding()
    {
        inAction = true;

        // Wait a tiny bit so IsGrounded doesn't immediately fire on the same frame
        yield return new WaitForSeconds(0.1f);

        // Wait until landed
        while (!playerController.IsGrounded)
            yield return null;

        inAction = false;
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Parkour action (vault / climb / etc.) — triggered by timed Jump press
    // ─────────────────────────────────────────────────────────────────────

    IEnumerator DoParkourAction(ParkourAction action)
    {
        inAction = true;
        playerController.SetControl(false);

        // Brief JumpStart beat before the parkour anim
        animator.SetBool("isJumping", true);
        animator.SetBool("isGrounded", false);
        animator.CrossFade("JumpStart", 0.1f);

        yield return null;
        yield return null;

        var jumpState = animator.GetNextAnimatorStateInfo(0);
        while (animator.IsInTransition(0)) yield return null;

        // Play JumpStart for ~35% of its length, then cut to parkour anim
        float jumpBeat = jumpState.length * 0.35f;
        float jt = 0f;
        while (jt < jumpBeat) { jt += Time.deltaTime; yield return null; }

        // ── Parkour animation ─────────────────────────────────────────────
        animator.SetBool("mirrorAction", action.Mirror);
        animator.CrossFade(action.AnimName, 0.15f);

        yield return null;
        yield return null;

        var animState = animator.GetNextAnimatorStateInfo(0);
        while (animator.IsInTransition(0)) yield return null;
        if (animState.length <= 0f)
            animState = animator.GetCurrentAnimatorStateInfo(0);

        float timer = 0f;
        while (timer <= animState.length)
        {
            timer += Time.deltaTime;

            if (action.RotateToObstacle)
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation,
                    action.TargetRotation,
                    playerController.RotationSpeed * Time.deltaTime);

            if (animator.IsInTransition(0) && timer > 0.5f)
                break;

            if (action.EnableTargetMatching)
                MatchTarget(action);

            yield return null;
        }

        yield return new WaitForSeconds(action.PostActionDelay);

        animator.SetBool("isJumping", false);
        animator.SetBool("isGrounded", true);

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
        promptActive = true;
        promptTimer = parkourTimingWindow;
        if (parkourPromptUI != null) parkourPromptUI.SetActive(true);
    }

    void HidePrompt()
    {
        promptActive = false;
        pendingAction = null;
        if (parkourPromptUI != null) parkourPromptUI.SetActive(false);
    }
}