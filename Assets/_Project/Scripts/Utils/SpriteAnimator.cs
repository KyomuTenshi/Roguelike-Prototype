using UnityEngine;

namespace RulesShift
{
    /// <summary>Plays an array of sprites. Used instead of Animator controllers: skins are just other arrays.</summary>
    public class SpriteAnimator : MonoBehaviour
    {
        public SpriteRenderer target;
        public Sprite[] frames;
        public float fps = 8f;
        public bool loop = true;
        public float speed = 1f;
        float t;

        public void Play(Sprite[] f, float framesPerSecond = -1f, bool restart = false, bool looping = true)
        {
            if (framesPerSecond > 0f) fps = framesPerSecond;
            loop = looping;
            if (f == frames && !restart) return;
            frames = f;
            t = 0f;
            Show();
        }

        public bool Finished => !loop && frames != null && t >= frames.Length;

        void Update()
        {
            if (frames == null || frames.Length == 0 || !target) return;
            t += Time.deltaTime * fps * speed;
            Show();
        }

        void Show()
        {
            if (frames == null || frames.Length == 0 || !target) return;
            int i = (int)t;
            i = loop ? i % frames.Length : Mathf.Min(i, frames.Length - 1);
            target.sprite = frames[i];
        }
    }
}
