using UnityEngine;
using UnityEngine.UI;

namespace SeaVillage.UI
{
    /// <summary>변동분을 증가/감소 Fill 이미지로 겹쳐 표시하는 게이지</summary>
    public class IncreasableGauge : Gauge
    {
        [Header("Delta Fill")]
        [SerializeField] private Image increasedFillImage;
        [SerializeField] private Image decreasedFillImage;

        private float deltaValue;

        /// <summary>max, current, 변동분으로 게이지 갱신</summary>
        public void UpdateGauge(float max, float current, float delta = 0f)
        {
            if (maxWidth <= 0)
                CacheMaxWidth();

            maxValue = max;
            currentValue = current;
            deltaValue = delta;

            UpdateFillImage();
        }

        /// <summary>변동분만 갱신</summary>
        public void UpdateIncreasedValue(float delta)
        {
            deltaValue = delta;
            UpdateFillImage();
        }

        /// <summary>변동분 초기화</summary>
        public void ResetIncreasedValue()
        {
            deltaValue = 0f;
            UpdateFillImage();
        }

        public override void RefreshGauge()
        {
            deltaValue = 0f;
            base.RefreshGauge();
        }

        protected override void UpdateFillImage()
        {
            float baseValue = Mathf.Max(0f, Mathf.Min(currentValue, currentValue + deltaValue));
            SetFillWidth(fillImage, baseValue);

            SetFillWidth(increasedFillImage, deltaValue > 0f ? currentValue + deltaValue : 0f);
            SetFillWidth(decreasedFillImage, deltaValue < 0f ? currentValue : 0f);
        }

        private void SetFillWidth(Image image, float value)
        {
            if (image == null || maxValue <= 0)
                return;

            float ratio = Mathf.Clamp01(value / maxValue);
            image.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, maxWidth * ratio);
        }
    }
}
