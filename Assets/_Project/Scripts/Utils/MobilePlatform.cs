using UnityEngine;

namespace RulesShift
{
    /// <summary>
    /// The web build opened on a phone or a tablet: the run pauses (and the game saves) when the tab is hidden
    /// or the browser goes to the background — a call, another app, the screen turned off.
    /// The touch controls themselves live in TouchControls.
    /// </summary>
    public class MobilePlatform : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            if (!Application.isMobilePlatform) return;
            var go = new GameObject("MobilePlatform");
            DontDestroyOnLoad(go);
            go.AddComponent<MobilePlatform>();
        }

        void OnApplicationPause(bool paused)
        {
            if (!paused) return;
            var rm = RoomManager.I;
            if (rm != null && rm.State == GameState.Playing) rm.Pause();
            SaveSystem.Save();
        }

        void OnApplicationFocus(bool focus)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            // a phone browser reports focus changes on taps and page resizes: pausing on them locked the game in the
            // pause menu (every "Continue" paused it again). A hidden tab stops the game by itself, so just save.
            if (!focus) SaveSystem.Save();
#else
            if (!focus) OnApplicationPause(true);
#endif
        }
    }
}
