using System;
using SeaVillage.Core;
using UnityEngine;

namespace SeaVillage.Town
{
    /// <summary>
    /// 한 씬에 여러 하위 마을이 공존할 때(광산/동굴) 플레이어 위치로 현재 마을 town키를 갱신한다.
    /// </summary>
    public class SubTownResolver : MonoBehaviour
    {
        [Serializable]
        private struct SubTownRegion
        {
            [Tooltip("이 영역의 마을 식별자")]
            [SerializeField] private TownKey townKey;

            [Tooltip("영역 판정용 콜라이더")]
            [SerializeField] private Collider2D region;

            public TownKey TownKey => townKey;
            public Collider2D Region => region;
        }

        [SerializeField] private SubTownRegion[] _regions = Array.Empty<SubTownRegion>();

        private TownKey _currentKey = TownKey.Unknown;

        private void Update()
        {
            if (!GameManager.HasInstance)
                return;

            GameManager gm = GameManager.Instance;
            if (!gm.HasPlayer)
                return;

            TownKey resolved = ResolveTownKey(gm.Player.transform.position);

            // 어느 영역에도 없으면(연결 통로 등) 직전 마을 유지
            if (resolved == TownKey.Unknown || resolved == _currentKey)
                return;

            _currentKey = resolved;
            gm.SetCurrentTown(resolved);
        }

        // 플레이어 위치를 포함하는 첫 영역의 town키 반환, 없으면 Unknown
        private TownKey ResolveTownKey(Vector2 point)
        {
            for (int i = 0; i < _regions.Length; i++)
            {
                Collider2D region = _regions[i].Region;
                if (region != null && region.OverlapPoint(point))
                    return _regions[i].TownKey;
            }

            return TownKey.Unknown;
        }
    }
}
