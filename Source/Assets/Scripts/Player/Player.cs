using UnityEngine;
using SeaVillage.Core;
using SeaVillage.Data;
using Spine.Unity;

namespace SeaVillage.Player
{
    /// <summary>
    /// 각 Player 컴포넌트들을 관리하는 Player 메인 클래스
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class Player : MonoBehaviour
    {
        // References
        private PlayerController _playerController;
        private PlayerInteractor _playerInteractor;
        private SkeletonAnimation _skeletonAnimation;

        // Public Properties
        public bool IsComponentInitialized { get; private set; } = false;
        public PlayerInteractor Interactor => _playerInteractor;

        private void Awake()
        {
            InitializeComponents();
        }

        private void Start()
        {
            PlayerGender gender = SaveLoadManager.HasInstance
                && SaveLoadManager.Instance.CurrentGameData != null
                    ? SaveLoadManager.Instance.CurrentGameData.playerGender
                    : PlayerGender.Male;
            if (!PlayerAppearance.TryApply(_skeletonAnimation, gender))
                Debug.LogError("[Player] 저장된 플레이어 외형을 적용하지 못했습니다");
        }

        private void InitializeComponents()
        {
            if (_playerController == null) _playerController = GetComponent<PlayerController>();
            if (_playerInteractor == null) _playerInteractor = GetComponentInChildren<PlayerInteractor>();
            if (_skeletonAnimation == null) _skeletonAnimation = GetComponent<SkeletonAnimation>();

            IsComponentInitialized = true;
        }

        public bool HasInteractableTarget()
        {
            return _playerInteractor.HasInteractableTarget;
        }
    }
}
