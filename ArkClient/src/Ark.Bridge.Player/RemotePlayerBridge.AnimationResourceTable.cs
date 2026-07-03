using System.Collections.Generic;

namespace Ark.Bridge.Player;

public partial class RemotePlayerBridge
{
    private readonly record struct AnimationFragmentBinding(
        int FragmentId,
        string Name,
        string ResourcePath,
        string LayerMask,
        string StreamPolicy,
        int PreloadPriority);

    private static readonly IReadOnlyDictionary<int, AnimationFragmentBinding> AnimationResourceTable =
        new Dictionary<int, AnimationFragmentBinding>
        {
            [1] = new(1, "Seat_Idle_Base", "res://Animations/Rider/SeatIdle_Base.anim", "Base", "Resident", 90),
            [10] = new(10, "Stand_Idle_Base", "res://Animations/Rider/StandIdle_Base.anim", "Base", "Resident", 80),
            [20] = new(20, "Recoil_Upper", "res://Animations/Rider/Recoil_Upper.anim", "UpperBody", "Burst", 55),
            [21] = new(21, "Seat_Recoil_Upper", "res://Animations/Rider/SeatRecoil_Upper.anim", "UpperBody", "Burst", 58),
            [30] = new(30, "Reload_Upper", "res://Animations/Rider/Reload_Upper.anim", "UpperBody", "Burst", 65),
            [31] = new(31, "Seat_Service_Upper", "res://Animations/Rider/SeatService_Upper.anim", "UpperBody", "Burst", 72),
            [40] = new(40, "HitReact_Full", "res://Animations/Rider/HitReact_Full.anim", "FullBody", "Burst", 60),
            [50] = new(50, "Seat_Enter_Transition", "res://Animations/Rider/SeatEnter_Transition.anim", "FullBody", "StreamWarm", 75),
            [51] = new(51, "Seat_Enter_GunnerTransition", "res://Animations/Rider/SeatEnter_GunnerTransition.anim", "FullBody", "StreamWarm", 82),
            [60] = new(60, "Seat_Exit_Transition", "res://Animations/Rider/SeatExit_Transition.anim", "FullBody", "StreamWarm", 75),
            [70] = new(70, "Stand_Walk_Cycle", "res://Animations/Rider/StandWalk_Cycle.anim", "LowerBody", "StreamLoop", 70),
            [80] = new(80, "Stand_Run_Cycle", "res://Animations/Rider/StandRun_Cycle.anim", "LowerBody", "StreamLoop", 78),
        };

    private static AnimationFragmentBinding ResolveAnimationResourceBinding(int fragmentId)
    {
        if (AnimationResourceTable.TryGetValue(fragmentId, out var binding))
            return binding;

        return new AnimationFragmentBinding(fragmentId, $"Fragment_{fragmentId}", "res://Animations/Rider/Fallback.anim", "Base", "Burst", 40);
    }
}
