using UnityEngine;
using System;
using Sirenix.OdinInspector;

namespace SeaVillage.VFX
{
    /// <summary>
    /// VFX 설정 데이터
    /// </summary>
    [Serializable]
    public class VFXData
    {
        [Title("기본 정보")]
        public VFXType type;
        public string name;
        public GameObject prefab;

        [Title("재생 설정")]
        public bool isPersistent = false;
        [HideIf("isPersistent", true)]
        public float duration = 1.0f;
        
        [Title("오디오 연동")]
        public bool playSound = false;
        [ShowIf("playSound", true)]
        public string soundName;
        [ShowIf("playSound", true)]
        public float soundVolume = 1.0f;
        
        [Title("풀링 설정")]
        public bool usePooling = true;
        [ShowIf("usePooling", true)]
        public int poolSize = 5;

        public VFXData()
        {
            type = VFXType.Sparkle;
            name = "";
        }

        public VFXData(VFXType vfxType, GameObject vfxPrefab)
        {
            type = vfxType;
            name = vfxType.ToString();
            prefab = vfxPrefab;
        }
    }

    /// <summary>
    /// VFX 타입 enum. 하나의 VFXType은 하나의 VFX 프리팹에 매핑
    /// </summary>
    public enum VFXType
    {
        // 항해씬 VFX
        ShipWake, // 배 물보라
        Wind, // 바람
        Treasure, // 보물 발견

        // 마을씬 VFX
        Smoke, // 연기
        PointLight, // 등불
        CharacterSpawn, // 손님 등장
        InteractionHint, // 상호작용 표시

        // UI VFX
        CoinGain, // 코인 획득
        Sparkle, // 반짝임
        LevelUp, // 레벨업

        // 공용 VFX
        Explosion,
        Dust
    }
}