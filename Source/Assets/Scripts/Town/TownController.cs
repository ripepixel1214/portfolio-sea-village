using System;
using System.Collections.Generic;
using SeaVillage.Core;
using SeaVillage.NPC;
using SeaVillage.Utilities;
using UnityEngine;
using UnityEngine.Serialization;

namespace SeaVillage.Town
{
    /// <summary>
    /// 마을 씬의 씬 루트(ISceneRoot). 마을 도메인(설정·NPC·상점·게시판·애니메이션)을 소유하고
    /// 화면 전환이 끝난 뒤 Initialize()에서 시동한다. NPC 에는 ITownContext 로만 노출된다.
    /// </summary>
    public class TownController : MonoBehaviour, ITownContext, ISceneRoot
    {
        [Header("마을 설정 값")]
        [SerializeField] private TownConfig _config;

        [Header("NPC 시스템")]
        [FormerlySerializedAs("_npcsManager")]
        [SerializeField] private NPCSystem _npcSystem;

        [Header("상점 시스템")]
        [FormerlySerializedAs("_shopSystem")]
        [SerializeField] private ShopManager _shopManager;

        [Header("마을 게시판")]
        [SerializeField] private NoticeBoard _noticeBoard;

        [Header("캐릭터 이동 영역 (Player·NPC, 1개 이상)")]
        [SerializeField] private BoxCollider2D[] _moveableBounds;

        [Header("마을 애니메이션 라이프사이클")]
        [FormerlySerializedAs("_mapAnimation")]
        [SerializeField] private TownAnimator _townAnimator;

        private bool _initialized;
        private MovableRegion _movableRegion;
        private TownProgressionManager _progressionManager;

        public string TownName => _config.TownName;

        /// <summary>
        /// 필수 도메인 참조 검증 후 씬 루트로 등록. 실제 시동은 Initialize()에서 한다.
        /// </summary>
        private void Awake()
        {
            if (_config == null)
            {
                throw new InvalidOperationException($"[TownController] {name}: TownConfig 미할당");
            }

            if (_npcSystem == null)
            {
                throw new InvalidOperationException($"[TownController] {name}: NPCSystem 미할당");
            }

            _movableRegion = new MovableRegion(_moveableBounds);
            if (_movableRegion.IsEmpty)
            {
                Debug.LogWarning($"[TownController] {name}: 캐릭터 이동 영역(_moveableBounds) 미설정 — 이동 제약 없이 동작");
            }

            GameManager.Instance.RegisterSceneRoot(this);
        }

        /// <summary>
        /// SceneChanger 전환을 거치지 않은 경우(에디터 직접 Play 등)의 폴백 시동.
        /// </summary>
        private void Start()
        {
            SceneChanger sceneChanger = SceneChanger.Instance;
            if (sceneChanger == null || !sceneChanger.IsTransitioning)
            {
                Initialize();
            }
        }

        private void OnEnable()
        {
            if (_initialized)
                SubscribeProgression();
        }

        private void OnDisable()
        {
            UnsubscribeProgression();
        }

        /// <summary>
        /// 화면 전환이 끝난 뒤 호출되는 시동 진입점. 소유 도메인을 의존순으로 시동한다(idempotent).
        /// </summary>
        public void Initialize()
        {
            if (_initialized)
            {
                return;
            }
            _initialized = true;

            int affinity = ResolveCurrentAffinity();
            _npcSystem.Initialize(this, _config, affinity);
            SubscribeProgression();
            _townAnimator?.Initialize();
        }

        private void HandleAffinityChanged(TownKey townKey, int newValue)
        {
            TownKey currentTownKey = TownProgressionManager.NormalizeTownKey(GameManager.Instance.CurrentTownKey);
            if (townKey == currentTownKey)
                ApplyAffinityToNpcSystem(newValue);
        }

        private void ApplyAffinityToNpcSystem(int affinity)
        {
            _npcSystem.UpdateLoveLevel(affinity);
            _npcSystem.UpdateMaxCount(_config.GetMaxNPCCount(affinity));
        }

        private int ResolveCurrentAffinity()
        {
            if (!TownProgressionManager.HasInstance || !GameManager.HasInstance)
                return 0;

            return TownProgressionManager.Instance.GetAffinity(GameManager.Instance.CurrentTownKey);
        }

        private void SubscribeProgression()
        {
            UnsubscribeProgression();
            if (!TownProgressionManager.HasInstance)
                return;

            _progressionManager = TownProgressionManager.Instance;
            _progressionManager.OnAffinityChanged += HandleAffinityChanged;
        }

        private void UnsubscribeProgression()
        {
            if (_progressionManager == null)
                return;

            _progressionManager.OnAffinityChanged -= HandleAffinityChanged;
            _progressionManager = null;
        }

        #region ITownContext

        public IReadOnlyList<IShop> GetAvailableShops()
            => _shopManager != null ? _shopManager.GetAvailable() : Array.Empty<IShop>();

        public INoticeBoard? NoticeBoard => _noticeBoard;

        public bool IsInMovableArea(Vector2 point)
        {
            if (_movableRegion == null || _movableRegion.IsEmpty)
            {
                return true;
            }

            return _movableRegion.Contains(point);
        }

        public Vector2 ClampToMovableArea(Vector2 point)
        {
            if (_movableRegion == null || _movableRegion.IsEmpty)
            {
                return point;
            }

            return _movableRegion.ClosestPointInside(point);
        }

        #endregion
    }
}
