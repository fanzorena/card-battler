using UnityEngine;

namespace CardBattler.UI
{
    // One-shot UI and battle sounds. Silent in EditMode and when the bank is missing.
    public static class Sfx
    {
        static SoundBank bank;
        static AudioSource source;
        static float lastHover;

        static AudioSource Source
        {
            get
            {
                if (!Application.isPlaying) return null;
                if (source) return source;
                bank ??= Resources.Load<SoundBank>("UI/Sounds");
                if (!bank) return null;
                var go = new GameObject("Sfx");
                Object.DontDestroyOnLoad(go);
                source = go.AddComponent<AudioSource>();
                source.playOnAwake = false;
                return source;
            }
        }

        public static void Play(System.Func<SoundBank, AudioClip> pick, float volume = 0.7f, float pitch = 1)
        {
            var s = Source;
            var clip = s ? pick(bank) : null;
            if (!clip) return;
            s.pitch = pitch;
            s.PlayOneShot(clip, volume);
        }

        public static void Click() => Play(b => b.click, 0.6f);
        public static void Hover()
        {
            if (Time.unscaledTime - lastHover < 0.05f) return; // sweeping over a row of buttons
            lastHover = Time.unscaledTime;
            Play(b => b.hover, 0.25f, Random.Range(0.95f, 1.05f));
        }
    }
}
