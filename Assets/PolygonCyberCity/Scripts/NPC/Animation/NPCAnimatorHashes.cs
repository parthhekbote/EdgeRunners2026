using UnityEngine;

namespace PolygonCyberCity.NPC.Animation
{
    /// <summary>
    /// Static cache for all Animator parameter hash IDs used across the NPC animation and BT system.
    /// Eliminates string allocations and string-based parameter lookups during runtime.
    /// </summary>
    public static class NPCAnimatorHashes
    {
        // Locomotion Parameters
        public static readonly int MoveSpeed = Animator.StringToHash("MoveSpeed");
        public static readonly int Speed = Animator.StringToHash("Speed");
        public static readonly int CurrentGait = Animator.StringToHash("CurrentGait");
        public static readonly int StrafeX = Animator.StringToHash("StrafeDirectionX");
        public static readonly int StrafeZ = Animator.StringToHash("StrafeDirectionZ");
        public static readonly int TurnAmount = Animator.StringToHash("TurnAmount");
        public static readonly int IsWalking = Animator.StringToHash("IsWalking");
        public static readonly int IsRunning = Animator.StringToHash("IsRunning");
        public static readonly int IsStopped = Animator.StringToHash("IsStopped");
        public static readonly int IsCrouching = Animator.StringToHash("IsCrouching");
        public static readonly int IsGrounded = Animator.StringToHash("IsGrounded");
        public static readonly int IsJumping = Animator.StringToHash("IsJumping");
        public static readonly int IsStarting = Animator.StringToHash("IsStarting");
        public static readonly int MovementInputHeld = Animator.StringToHash("MovementInputHeld");

        // Combat Parameters
        public static readonly int SwordDrawn = Animator.StringToHash("SwordDrawn");
        public static readonly int SwordDrawnWeight = Animator.StringToHash("SwordDrawnWeight");
        public static readonly int AttackID = Animator.StringToHash("AttackID");
        public static readonly int Attack = Animator.StringToHash("Attack");
        public static readonly int CombatState = Animator.StringToHash("CombatState");
        public static readonly int IsStrafing = Animator.StringToHash("IsStrafing");
        public static readonly int BlockBegin = Animator.StringToHash("BlockBegin");
        public static readonly int BlockLoop = Animator.StringToHash("BlockLoop");
        public static readonly int BlockEnd = Animator.StringToHash("BlockEnd");

        // Reaction & Death Parameters
        public static readonly int HitDirection = Animator.StringToHash("HitDirection");
        public static readonly int HitType = Animator.StringToHash("HitType");
        public static readonly int Hit = Animator.StringToHash("Hit");
        public static readonly int Die = Animator.StringToHash("Die");
        public static readonly int DodgeDirection = Animator.StringToHash("DodgeDirection");
        public static readonly int Dodge = Animator.StringToHash("Dodge");
        public static readonly int Knockdown = Animator.StringToHash("Knockdown");

        // Perception & Fear Parameters
        public static readonly int PerceptionType = Animator.StringToHash("PerceptionType");
        public static readonly int PerceptionReact = Animator.StringToHash("PerceptionReact");
        public static readonly int IsPanicking = Animator.StringToHash("IsPanicking");
        public static readonly int PanicLookBack = Animator.StringToHash("PanicLookBack");

        // Activity & Gesture Parameters
        public static readonly int ActivityID = Animator.StringToHash("ActivityID");
        public static readonly int Activity = Animator.StringToHash("Activity");
        public static readonly int GestureID = Animator.StringToHash("GestureID");
        public static readonly int Gesture = Animator.StringToHash("Gesture");
        public static readonly int RandomIdle = Animator.StringToHash("RandomIdle");
    }
}
