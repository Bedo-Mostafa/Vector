using UnityEngine;

[CreateAssetMenu(menuName = "Parkour System/Custom Actions/New jump action")]
public class JumpAction : ParkourAction
{
    public override bool CheckIfPossible(ObstacleHitData hitData, Transform player)
    {
        // A standard jump is only possible if there is NO obstacle blocking us
        if (hitData.forwardHitFound)
            return false;

        return true;
    }
}