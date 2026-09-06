using System;
using SeaVillage.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SeaVillage.UI
{
    public class ExchangeOfferView : MonoBehaviour
    {
        [SerializeField] private MineExchangeReward _reward;
        [SerializeField] private Image _requiredItemImage;
        [SerializeField] private Image _rewardItemImage;
        [SerializeField] private Sprite _dayOption1Sprite;
        [SerializeField] private Sprite _dayOption2Sprite;
        [SerializeField] private Sprite _dayOption3Sprite;
        [SerializeField] private Sprite _dayOption4Sprite;
        [SerializeField] private TextMeshProUGUI _requiredCountText;
        [SerializeField] private TextMeshProUGUI _rewardCountText;
        [SerializeField] private Button _exchangeButton;

        public event Action<ExchangeOfferView> ExchangeRequested;

        public MineExchangeReward Reward => _reward;
        public Button ExchangeButton => _exchangeButton;

        private void OnEnable()
        {
            if (_exchangeButton != null)
                _exchangeButton.onClick.AddListener(RequestExchange);
        }

        private void OnDisable()
        {
            if (_exchangeButton != null)
                _exchangeButton.onClick.RemoveListener(RequestExchange);
        }

        public void Refresh(int optionIndex, int requiredCount, int rewardCount, bool hasEnoughItems)
        {
            Sprite requiredSprite = GetRequiredSprite(optionIndex);
            if (_requiredItemImage != null)
            {
                _requiredItemImage.sprite = requiredSprite;
                _requiredItemImage.enabled = requiredSprite != null;
            }

            if (_rewardItemImage != null)
                _rewardItemImage.enabled = _rewardItemImage.sprite != null;

            if (_requiredCountText != null)
            {
                _requiredCountText.text = requiredCount.ToString();
                _requiredCountText.color = hasEnoughItems
                    ? Color.black
                    : new Color32(220, 75, 75, 255);
            }

            if (_rewardCountText != null)
                _rewardCountText.text = rewardCount.ToString();
        }

        private Sprite GetRequiredSprite(int optionIndex)
        {
            return optionIndex switch
            {
                0 => _dayOption1Sprite,
                1 => _dayOption2Sprite,
                2 => _dayOption3Sprite,
                3 => _dayOption4Sprite,
                _ => null,
            };
        }

        private void RequestExchange()
        {
            ExchangeRequested?.Invoke(this);
        }
    }
}
