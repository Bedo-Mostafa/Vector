using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Parkour System/New parkour action")]
public class ParkourAction : ScriptableObject
{
    [SerializeField] string animName;
    [SerializeField] string obstacleTag;

    [SerializeField] float minHeight;
    [SerializeField] float maxHeight;

    [SerializeField] bool rotateToObstacle;
    [SerializeField] float postActionDelay;

    [Header("Target Matching")]
    [SerializeField] bool enableTargetMatching = true;
    [SerializeField] protected AvatarTarget matchBodyPart;
    [SerializeField] float matchStartTime;
    [SerializeField] float matchTargetTime;
    [SerializeField] Vector3 matchPosWeight = new Vector3(0, 1, 0);

    [Header("Action Type")]
    [Tooltip("If true, triggers PlayerController physics instead of target-matching animation")]
    [SerializeField] bool isPhysicsJump;

    [Header("Action Type Settings")]
    [SerializeField] bool isFallback;

    [Header("Action Timing")]
    [Tooltip("The exact distance from the obstacle where the animation will start playing.")]
    [SerializeField] float actionTriggerDistance = 1f;

    public Quaternion TargetRotation { get; set; }
    public Vector3 MatchPos { get; set; }
    public bool Mirror { get; set; }

    public virtual bool CheckIfPossible(ObstacleHitData hitData, Transform player)
    {
        // ADD THIS SAFETY CHECK:
        // If the raycast didn't hit anything, we obviously can't do a parkour move!
        if (!hitData.forwardHitFound || !hitData.heightHitFound)
            return false;

        // Check Tag
        if (!string.IsNullOrEmpty(obstacleTag) && hitData.forwardHit.transform.tag != obstacleTag)
            return false;

        // Height Tag
        float height = hitData.heightHit.point.y - player.position.y;
        if (height < minHeight || height > maxHeight)
            return false;

        if (rotateToObstacle)
            TargetRotation = Quaternion.LookRotation(-hitData.forwardHit.normal);

        if (enableTargetMatching)
            MatchPos = hitData.heightHit.point;

        return true;
    }

    public string AnimName => animName;
    public bool RotateToObstacle => rotateToObstacle;
    public float PostActionDelay => postActionDelay;

    public bool EnableTargetMatching => enableTargetMatching;
    public AvatarTarget MatchBodyPart => matchBodyPart;
    public float MatchStartTime => matchStartTime;
    public float MatchTargetTime => matchTargetTime;
    public Vector3 MatchPosWeight => matchPosWeight;

    public bool IsPhysicsJump => isPhysicsJump;
    public bool IsFallback => isFallback;
    public float ActionTriggerDistance => actionTriggerDistance;
}
