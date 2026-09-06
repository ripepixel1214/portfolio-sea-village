using SeaVillage.Core;
using Spine.Unity;
using UnityEngine;
using UnityEngine.UI;

namespace SeaVillage.UI
{
    public sealed class GenderSelectionPanel : UIPanel
    {
        private const string IdleAnimation = "Player_Idle";
        private const string WalkAnimation = "Player_Walk";

        [Header("Preview")]
        [SerializeField] private SkeletonDataAsset playerSkeletonData;
        [SerializeField] private Material skeletonGraphicMaterial;
        [SerializeField] private RectTransform malePreviewRoot;
        [SerializeField] private RectTransform femalePreviewRoot;

        [Header("Selection")]
        [SerializeField] private Button maleButton;
        [SerializeField] private Button femaleButton;
        [SerializeField] private Button startButton;
        [SerializeField] private GameObject maleSelectedIndicator;
        [SerializeField] private GameObject femaleSelectedIndicator;

        private MainMenuUIController _mainMenuController;
        private SkeletonGraphic _malePreview;
        private SkeletonGraphic _femalePreview;
        private PlayerGender _selectedGender = PlayerGender.Male;
        private int _slotIndex = -1;
        private bool _isBusy;

        public void Initialize(int slotIndex, MainMenuUIController mainMenuController)
        {
            _slotIndex = slotIndex;
            _mainMenuController = mainMenuController;
        }

        public override void OnOpen()
        {
            base.OnOpen();
            _isBusy = false;
            EnsurePreviews();
            SelectGender(PlayerGender.Male);
        }

        protected override void AddListeners()
        {
            if (maleButton != null)
                maleButton.onClick.AddListener(HandleMaleSelected);
            if (femaleButton != null)
                femaleButton.onClick.AddListener(HandleFemaleSelected);
            if (startButton != null)
                startButton.onClick.AddListener(HandleStartRequested);
        }

        protected override void RemoveListeners()
        {
            if (maleButton != null)
                maleButton.onClick.RemoveListener(HandleMaleSelected);
            if (femaleButton != null)
                femaleButton.onClick.RemoveListener(HandleFemaleSelected);
            if (startButton != null)
                startButton.onClick.RemoveListener(HandleStartRequested);
        }

        private void HandleMaleSelected()
        {
            SelectGender(PlayerGender.Male);
        }

        private void HandleFemaleSelected()
        {
            SelectGender(PlayerGender.Female);
        }

        private void HandleStartRequested()
        {
            if (_isBusy)
                return;

            _ = StartSelectedGameAsync();
        }

        private async System.Threading.Tasks.Task StartSelectedGameAsync()
        {
            if (_mainMenuController == null || _slotIndex < 0)
            {
                Debug.LogError("[GenderSelectionPanel] 신규 게임 시작 정보가 없습니다");
                return;
            }

            SetBusy(true);
            bool started = await _mainMenuController.StartNewGameAsync(_slotIndex, _selectedGender);
            if (!started && isActiveAndEnabled)
                SetBusy(false);
        }

        private void SelectGender(PlayerGender gender)
        {
            _selectedGender = PlayerGenderPolicy.Normalize(gender);
            bool isMale = _selectedGender == PlayerGender.Male;
            if (maleSelectedIndicator != null)
                maleSelectedIndicator.SetActive(isMale);
            if (femaleSelectedIndicator != null)
                femaleSelectedIndicator.SetActive(!isMale);

            SetPreviewAnimation(_malePreview, isMale ? WalkAnimation : IdleAnimation);
            SetPreviewAnimation(_femalePreview, isMale ? IdleAnimation : WalkAnimation);
        }

        private void EnsurePreviews()
        {
            if (_malePreview == null)
                _malePreview = CreatePreview(malePreviewRoot, PlayerGender.Male, "Male Preview");
            if (_femalePreview == null)
                _femalePreview = CreatePreview(femalePreviewRoot, PlayerGender.Female, "Female Preview");
        }

        private SkeletonGraphic CreatePreview(
            RectTransform parent,
            PlayerGender gender,
            string objectName)
        {
            if (parent == null || playerSkeletonData == null || skeletonGraphicMaterial == null)
            {
                Debug.LogError("[GenderSelectionPanel] 캐릭터 미리보기 설정이 누락되었습니다");
                return null;
            }

            SkeletonGraphic preview = SkeletonGraphic.NewSkeletonGraphicGameObject(
                playerSkeletonData,
                parent,
                skeletonGraphicMaterial);
            preview.name = objectName;
            preview.raycastTarget = false;
            preview.initialSkinName = PlayerGenderPolicy.GetSkinName(gender);
            preview.Initialize(true);
            preview.Skeleton.SetSkin(preview.initialSkinName);
            preview.Skeleton.SetSlotsToSetupPose();

            RectTransform rectTransform = preview.rectTransform;
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            rectTransform.localScale = Vector3.one * 0.25f;
            return preview;
        }

        private static void SetPreviewAnimation(SkeletonGraphic preview, string animationName)
        {
            if (preview == null || preview.AnimationState == null)
                return;
            if (preview.AnimationState.GetCurrent(0)?.Animation?.Name == animationName)
                return;

            preview.AnimationState.SetAnimation(0, animationName, true);
        }

        private void SetBusy(bool isBusy)
        {
            _isBusy = isBusy;
            if (maleButton != null)
                maleButton.interactable = !isBusy;
            if (femaleButton != null)
                femaleButton.interactable = !isBusy;
            if (startButton != null)
                startButton.interactable = !isBusy;
        }
    }
}
