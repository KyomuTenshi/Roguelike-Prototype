// Bridge from the game to the page (Assets/WebGLTemplates/RULECRAWL/index.html).
mergeInto(LibraryManager.library, {
  // Fullscreen right away: the page asks the browser itself, inside the activation of the click or key
  // that led here (Unity's own Screen.fullScreen waits for one more click). 1 = asked, 0 = no page helper.
  RulecrawlFullscreen: function () {
    if (typeof window.rulecrawlFullscreen !== "function") return 0;
    window.rulecrawlFullscreen();
    return 1;
  },

  // How late the speakers are behind the audio clock (seconds): Web Audio's base + output latency.
  // The Neon law moves its beat by this much. -1 when the browser does not say.
  RulecrawlAudioLatency: function () {
    try {
      var ctx = (typeof WEBAudio !== "undefined" && WEBAudio.audioContext) ? WEBAudio.audioContext : null;
      if (!ctx) return -1;
      var l = (ctx.baseLatency || 0) + (ctx.outputLatency || 0);
      return l > 0 ? l : -1;
    } catch (e) {
      return -1;
    }
  },
});
