using UnityEngine;

/// <summary>
/// Declares which world layer a scene belongs to when that scene is launched directly.
/// Portal travel may set the same value again when entering from another scene.
///
/// Unity setup:
///   1. Add this component to one root GameObject in a world scene.
///   2. Set World to the layer represented by that scene.
///   3. Do not add more than one WorldSceneIdentity to the same scene.
///
/// Runtime API: none; Awake updates WorldTravelState automatically.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-90)]
public sealed class WorldSceneIdentity : MonoBehaviour
{
    [SerializeField] private WorldLayer world = WorldLayer.WorldA;

    private void Awake()
    {
        if (WorldTravelState.Instance != null)
            WorldTravelState.Instance.SetCurrentWorld(world);
    }
}
