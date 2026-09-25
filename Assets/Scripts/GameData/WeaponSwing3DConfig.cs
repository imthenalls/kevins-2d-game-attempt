using System;

namespace Game.Core
{
    /// <summary>
    /// Tuning for the 3D melee swing visual and hit sweep. Plain C#, lives in Game.Data.
    ///
    /// Unity setup: none. Held as a [SerializeField] WeaponSwing3DConfig field by
    /// EquippedWeaponVisual3D.
    /// Runtime API: plain public fields.
    /// </summary>
    [Serializable]
    public class WeaponSwing3DConfig
    {
        /// <summary>Yaw the blade rests at when not swinging (degrees).</summary>
        public float RestYaw = 90f;

        /// <summary>Yaw the blade starts the sweep at, relative to rest (degrees).</summary>
        public float StartYaw = -85f;

        /// <summary>Yaw the blade ends the sweep at, relative to rest (degrees).</summary>
        public float EndYaw = 85f;

        /// <summary>Distance from the character the blade orbits at (world units).</summary>
        public float OrbitRadius = 0.5f;

        /// <summary>Blade length past the orbit point; the hit sample sits at OrbitRadius + BladeLength.</summary>
        public float BladeLength = 0.5f;

        /// <summary>Radius of the sphere sampled at the blade point for hits.</summary>
        public float HitRadius = 0.35f;

        /// <summary>Half-angle (degrees) around the current blade direction that counts as a hit.</summary>
        public float HitConeDegrees = 75f;

        /// <summary>Longest sprite dimension of the held weapon, normalized to this many world units.</summary>
        public float WeaponSpriteLength = 0.7f;

        /// <summary>
        /// In-plane roll (degrees) of the blade sprite in the billboard plane while resting. Tilts the
        /// blade so a down-angled held pose reads clearly; 0 keeps the sprite's authored orientation.
        /// </summary>
        public float SpriteRoll = 150f;
    }
}
