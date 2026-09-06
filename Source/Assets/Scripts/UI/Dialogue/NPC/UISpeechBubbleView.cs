using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class UISpeechBubbleView : MonoBehaviour
{
    [SerializeField] private GameObject _textRoot;
    [SerializeField] private TMP_Text _contentText;
    [SerializeField] private float _maxTextWidth = 250f;
    [SerializeField] private Vector2 _textPadding = new Vector2(24f, 16f);
    [SerializeField] private GameObject _progressRoot;
    [SerializeField] private Image _progressFill;
    [SerializeField] private GameObject _costRoot;
    [SerializeField] private TMP_Text _costText;
    [SerializeField] private GameObject _emotionRoot;
    [SerializeField] private Image _emotionImage;

    private RectTransform _textRect;

    public bool TryValidateTextConfiguration(out string failReason)
    {
        if (_textRoot == null || _contentText == null)
        {
            failReason = "말풍선 텍스트 루트 또는 TMP 텍스트 참조가 없습니다";
            return false;
        }

        failReason = string.Empty;
        return true;
    }

    public void SetText(string text)
    {
        if (_contentText == null) return;
        _contentText.text = text;
        ResizeText(text);
    }

    public void SetProgress(float amount)
    {
        if (_progressFill != null) _progressFill.fillAmount = Mathf.Clamp01(amount);
    }

    public void SetCost(long amount)
    {
        if (_costText != null) _costText.text = $"- {amount} G";
    }

    public void SetEmotion(Sprite sprite)
    {
        if (_emotionImage != null) _emotionImage.sprite = sprite;
    }

    public void ShowText() => ActivateOnly(_textRoot);

    public void ShowProgress() => ActivateOnly(_progressRoot);

    public void ShowCost() => ActivateOnly(_costRoot);

    public void ShowEmotion() => ActivateOnly(_emotionRoot);

    public void Clear() => ActivateOnly(null);

    private void ResizeText(string text)
    {
        if (_contentText == null) return;
        if (_textRect == null && _textRoot != null) _textRect = _textRoot.GetComponent<RectTransform>();
        if (_textRect == null) return;

        float maxContent = Mathf.Max(1f, _maxTextWidth - _textPadding.x);
        float preferred = _contentText.GetPreferredValues(text).x;
        float contentWidth = Mathf.Min(preferred, maxContent);
        float contentHeight = _contentText.GetPreferredValues(text, contentWidth, 0f).y;

        _contentText.rectTransform.sizeDelta = new Vector2(contentWidth, contentHeight);
        _textRect.sizeDelta = new Vector2(contentWidth + _textPadding.x, contentHeight + _textPadding.y);
    }

    private void ActivateOnly(GameObject target)
    {
        SetPanel(_textRoot, target);
        SetPanel(_progressRoot, target);
        SetPanel(_costRoot, target);
        SetPanel(_emotionRoot, target);
    }

    private static void SetPanel(GameObject panel, GameObject target)
    {
        if (panel != null) panel.SetActive(panel == target);
    }
}
