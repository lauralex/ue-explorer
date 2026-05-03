using System.Collections.Generic;

namespace UELib.Branch.UE3.RL
{
    /// <summary>
    /// Fallback name map for Rocket League native function indexes that aren't covered by any
    /// loaded UFunction (e.g. when the user only loads TAGame and the natives are declared in
    /// Engine/Core). Extracted by walking GNatives in
    /// <c>RocketLeague_Dumped_latest.exe</c> at 0x7FF6CF2AA580 and matching each function pointer
    /// against the (string_ptr, function_ptr) registration tables in .data.
    ///
    /// These names take effect via <c>NativeFunctionToken.IndexBuiltinNatives()</c>, which is
    /// called once on first use. Loaders that have a fresher source of truth (an actual loaded
    /// <c>UFunction.NativeToken</c>) override these entries — first registration wins, but
    /// IndexPackageNatives runs eagerly on package load while this fallback runs lazily.
    /// </summary>
    public static class RocketLeagueNativeNames
    {
        public static readonly IReadOnlyDictionary<ushort, string> Map = new Dictionary<ushort, string>
        {
            { 256, "Sleep" },
            { 258, "ClassIsChildOf" },
            { 261, "FinishAnim" },
            { 262, "SetCollision" },
            { 266, "Move" },
            { 267, "SetLocation" },
            { 270, "Add_QuatQuat" },
            { 271, "Subtract_QuatQuat" },
            { 272, "SetOwner" },
            { 275, "LessLess_VectorRotator" },
            { 276, "GreaterGreater_VectorRotator" },
            { 277, "Trace" },
            { 279, "Destroy" },
            { 280, "SetTimer" },
            { 281, "IsInState" },
            { 282, "SetStateTimer" },
            { 283, "SetCollisionSize" },
            { 284, "GetStateName" },
            { 287, "Multiply_RotatorFloat" },
            { 288, "Multiply_FloatRotator" },
            { 289, "Divide_RotatorFloat" },
            { 290, "MultiplyEqual_RotatorFloat" },
            { 291, "DivideEqual_RotatorFloat" },
            { 296, "Multiply_VectorVector" },
            { 297, "MultiplyEqual_VectorVector" },
            { 298, "SetBase" },
            { 299, "SetRotation" },
            { 300, "MirrorVectorByNormal" },
            { 304, "AllActors" },
            { 305, "ChildActors" },
            { 306, "BasedActors" },
            { 307, "TouchingActors" },
            { 309, "TraceActors" },
            { 311, "VisibleActors" },
            { 312, "VisibleCollidingActors" },
            { 313, "DynamicActors" },
            { 316, "Add_RotatorRotator" },
            { 317, "Subtract_RotatorRotator" },
            { 318, "AddEqual_RotatorRotator" },
            { 319, "SubtractEqual_RotatorRotator" },
            { 320, "RotRand" },
            { 321, "CollidingActors" },
            { 322, "ConcatEqual_StrStr" },
            { 323, "AtEqual_StrStr" },
            { 324, "SubtractEqual_StrStr" },
            { 325, "RLerp" },
            { 326, "LessEqual_StrStr" },
            { 327, "GreaterEqual_StrStr" },
            { 328, "Exp" },
            { 329, "Loge" },
            { 330, "Normalize" },
            { 332, "Enable" },
            { 333, "Disable" },
            { 500, "MoveTo" },
            { 502, "MoveToward" },
            { 508, "FinishRotation" },
            { 512, "MakeNoise" },
            { 514, "LineOfSightTo" },
            { 517, "FindPathToward" },
            { 518, "FindPathTo" },
            { 520, "ActorReachable" },
            { 521, "PointReachable" },
            { 524, "FindStairRotation" },
            { 525, "FindRandomDest" },
            { 526, "PickWallAdjust" },
            { 527, "WaitForLanding" },
            { 532, "PlayerCanSeeMe" },
            { 533, "CanSee" },
            { 536, "SaveConfig" },
            { 537, "CanSeeByPoints" },
            { 546, "UpdateURL" },
            { 547, "GetURLMap" },
            { 548, "FastTrace" },
            { 1500, "ProjectOnTo" },
            { 1501, "IsZero" },
            { 3969, "MoveSmooth" },
            { 3970, "SetPhysics" },
            { 3971, "AutonomousPhysics" },
        };
    }
}
