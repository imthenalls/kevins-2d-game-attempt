using System;

namespace Game.Core
{
    /// <summary>
    /// Tuning for EquippedWeaponVisual (swing arc, after-swing trail, grip pivot). Plain C#, lives
    /// in Game.Data. Trail color is stored as RGBA floats and the grip as two floats; Unity
    /// references (Transform, SpriteRenderer) stay on the component.
    ///
    /// Unity setup: none. Held as a [SerializeField] WeaponVisualConfig field by EquippedWeaponVisual.
    /// Runtime API: plain public fields.
    /// </summary>
    [Serializable]
    public class WeaponVisualConfig
    {
        public float StartAngleOffset = -20f;
        public float EndAngleOffset = 200f;
        public float AttackRadiusMultiplier = 1.25f;

        public float TrailR = 1f;
        public float TrailG = 0.05f;
        public float TrailB = 0.03f;
        public float TrailA = 0.85f;
        public float TrailFadeTime = 0.28f;

        public float GripPivotX = 0.16f;
        public float GripPivotY = 0.18f;
    }
}
