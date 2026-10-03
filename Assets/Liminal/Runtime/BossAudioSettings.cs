using UnityEngine;

namespace Liminal
{
    [CreateAssetMenu(menuName="Liminal/Boss Audio Settings")]
    public sealed class BossAudioSettings : ScriptableObject
    {
        public AudioClip defaultRelease;
        public AudioClip serpentRelease;
        public AudioClip hermitRelease;
        public AudioClip whaleRelease;
        public AudioClip submarineRelease;
        [Range(0,1)] public float volume=1f;

        static BossAudioSettings current;
        public static BossAudioSettings Current
        {
            get {
                if(!current) current=Resources.Load<BossAudioSettings>("BossAudio");
                if(!current) current=CreateInstance<BossAudioSettings>();
                return current;
            }
        }

        public static AudioClip SelectReleaseClip(BossAudioSettings settings,int voice,AudioClip generatedFallback,
            out bool configured)
        {
            configured=false;
            if(!settings) return generatedFallback;
            AudioClip selected=voice switch {
                0=>settings.serpentRelease,
                1=>settings.hermitRelease,
                2=>settings.whaleRelease,
                3=>settings.submarineRelease,
                _=>null
            };
            if(selected) { configured=true;return selected; }
            if(settings.defaultRelease) { configured=true;return settings.defaultRelease; }
            return generatedFallback;
        }
    }
}
