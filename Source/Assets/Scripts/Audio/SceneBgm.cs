using System;
using SeaVillage.Core;
using UnityEngine;

namespace SeaVillage.Audio
{
    [DisallowMultipleComponent]
    public sealed class SceneBgm : MonoBehaviour
    {
        [Serializable]
        private struct TownTrack
        {
            [SerializeField] private TownKey townKey;
            [SerializeField] private AudioClip clip;

            public TownKey TownKey => townKey;
            public AudioClip Clip => clip;
        }

        [SerializeField] private AudioClip defaultClip;
        [SerializeField] private TownTrack[] townTracks = Array.Empty<TownTrack>();

        public AudioClip ResolveClip(TownKey townKey)
        {
            for (int i = 0; i < townTracks.Length; i++)
            {
                if (townTracks[i].TownKey == townKey && townTracks[i].Clip != null)
                    return townTracks[i].Clip;
            }
            return defaultClip;
        }
    }
}
