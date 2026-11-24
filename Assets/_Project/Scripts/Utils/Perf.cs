using UnityEngine;

namespace RulesShift
{
    /// <summary>
    /// Lighter graphics for phones and tablets (the web build opened in a mobile browser):
    /// the picture stays the same, the expensive parts get cheaper.
    /// </summary>
    public static class Perf
    {
        /// <summary>A phone or a tablet (also in a mobile browser). Never in the editor.</summary>
        public static bool LowEnd => Application.isMobilePlatform && !Application.isEditor;
    }
}
