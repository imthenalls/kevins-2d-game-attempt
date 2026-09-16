using System;

namespace Game.Core
{
    /// <summary>
    /// Authoritative movement, facing, and dash tuning for a top-down player controller. Plain C#
    /// with no UnityEngine dependency, so it lives in Game.Data, can be tested without a scene,
    /// and is the single source the presentation reads (instead of serialized MonoBehaviour
    /// fields).
    ///
    /// Unity setup: none. Held as a [SerializeField] PlayerMovementConfig field by
    /// PlayerController2D, which reads it in Awake. Unity-native values are stored in pure form:
    /// the legacy dash key is a KeyCode value as int, and the trail color is RGBA floats.
    ///
    /// Runtime API: plain public fields. PlayerController2D.MoveSpeed and ApplyAvatarProfile write
    /// back into this object.
    /// </summary>
    [Serializable]
    public class PlayerMovementConfig
    {
        // Top-down movement
        public float MoveSpeed = 6f;
        public bool LockRotation = true;
        public bool ForceNoGravity = true;

        // Movement facing
        public bool FaceMovementDirection = true;
        /// <summary>Direction the sprite tip points at zero rotation. Use 90 for up, 0 for right.</summary>
        public float SpriteForwardAngle = 90f;
        /// <summary>Degrees per second. Set to 0 for immediate facing.</summary>
        public float FacingTurnSpeed = 0f;
        public float FacingInputDeadZone = 0.01f;

        // Dash
        public bool DashEnabled = true;
        public float DashDistanceInPlayerLengths = 5f;
        public float DashSpeedMultiplier = 6f;
        public float DashCooldown = 0.4f;
        public int MaxDashCharges = 3;
        public float DashRechargeSeconds = 15f;
        /// <summary>UnityEngine.KeyCode value used by the legacy input fallback. 304 = LeftShift.</summary>
        public int LegacyDashKeyCode = 304;

        // Dash trail
        public float DashTrailR = 0.2f;
        public float DashTrailG = 0.75f;
        public float DashTrailB = 1f;
        public float DashTrailA = 0.75f;
        public float DashTrailFadeTime = 0.3f;
        public float DashTrailWidthInPlayerLengths = 0.8f;

        // Dash trail shape
        public float DashTrailMinVertexDistance = 0.04f;
        public int DashTrailCornerVertices = 3;
        public int DashTrailCapVertices = 2;
        public float DashTrailEndColorMultiplier = 0.55f;
        public float DashTrailWidthStartTime = 0f;
        public float DashTrailWidthStart = 1f;
        public float DashTrailWidthMidTime = 0.65f;
        public float DashTrailWidthMid = 0.45f;
        public float DashTrailWidthEndTime = 1f;
        public float DashTrailWidthEnd = 0.02f;

        // Clamp limits
        /// <summary>Fallback body length when the controller has no collider.</summary>
        public float DefaultPlayerLength = 1f;
        public float MinPlayerLength = 0.1f;
        public float MinMoveSpeed = 0f;
        public float MinDashDistanceInPlayerLengths = 0.1f;
        public float MinDashSpeedMultiplier = 1f;
        public float MinDashCooldown = 0f;
        public int MinDashCharges = 1;
        public float MinDashRechargeSeconds = 0.1f;
        public float MinDashSpeed = 0.01f;
        public float MinDashRechargeInterval = 0.1f;
    }
}
