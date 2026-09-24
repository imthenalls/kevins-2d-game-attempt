using System;

namespace Game.Core
{
    /// <summary>
    /// Engine-free weapon-swing hit selection for a planar (XZ) 3D swing. Given the attacker origin,
    /// a candidate's ground position, the current blade yaw, the swing reach and the half-angle of
    /// the frontal cone, it decides whether the candidate is hit. Plain C#, unit-testable.
    ///
    /// Unity setup: none. Called by EquippedWeaponVisual3D after its 3D overlap query.
    /// </summary>
    public static class WeaponSwingPolicy
    {
        public static bool IsInSwingCone(
            float originX, float originZ,
            float targetX, float targetZ,
            float bladeYawDegrees, float reach, float coneHalfAngleDegrees)
        {
            float dx = targetX - originX;
            float dz = targetZ - originZ;
            float sqr = (dx * dx) + (dz * dz);

            if (sqr > reach * reach)
                return false;

            if (sqr <= 0.000001f)
                return true; // standing on the attacker counts as in cone

            float targetYaw = (float)(Math.Atan2(-dz, dx) * (180.0 / Math.PI));
            return Math.Abs(DeltaAngle(bladeYawDegrees, targetYaw)) <= coneHalfAngleDegrees;
        }

        // Unity's Mathf.DeltaAngle equivalent: shortest signed difference from current to target.
        private static float DeltaAngle(float current, float target)
        {
            float delta = (target - current) % 360f;
            if (delta > 180f) delta -= 360f;
            else if (delta < -180f) delta += 360f;
            return delta;
        }
    }
}
