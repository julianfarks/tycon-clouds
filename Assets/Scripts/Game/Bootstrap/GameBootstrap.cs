using UnityEngine;

namespace CloudGarden.Bootstrap
{
    /// <summary>Creates the whole game from code when the (empty) scene loads.</summary>
    public static class GameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            if (Object.FindFirstObjectByType<GameController>() != null) return;
            GameObject go = new GameObject("CloudGarden");
            go.AddComponent<GameController>();
        }
    }
}
