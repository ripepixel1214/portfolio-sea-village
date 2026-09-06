using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SeaVillage.UI
{
    public class SpecialShopActionEntryView : MonoBehaviour
    {
        [SerializeField] private Image _primaryIcon;
        [SerializeField] private Image _secondaryIcon;
        [SerializeField] private TextMeshProUGUI _titleText;
        [SerializeField] private TextMeshProUGUI _detailText;
        [SerializeField] private TextMeshProUGUI _progressText;
        [SerializeField] private Button _actionButton;
        [SerializeField] private TextMeshProUGUI _actionButtonText;

        public Image PrimaryIcon => _primaryIcon;
        public Image SecondaryIcon => _secondaryIcon;
        public TextMeshProUGUI TitleText => _titleText;
        public TextMeshProUGUI DetailText => _detailText;
        public TextMeshProUGUI ProgressText => _progressText;
        public Button ActionButton => _actionButton;
        public TextMeshProUGUI ActionButtonText => _actionButtonText;

        private void Awake()
        {
            if (_actionButton != null)
                _actionButton.onClick.AddListener(Audio.AudioManager.TryPlayClickSfx);
        }

        private void OnDestroy()
        {
            if (_actionButton != null)
                _actionButton.onClick.RemoveListener(Audio.AudioManager.TryPlayClickSfx);
        }
    }
}
