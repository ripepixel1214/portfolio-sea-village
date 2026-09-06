using UnityEngine;
using TMPro;

public class SpecialNewsItem : MonoBehaviour
{
    [SerializeField]
    private TextMeshProUGUI effectTitleText;
    [SerializeField]
    private TextMeshProUGUI descriptionText;

    public void SetInformation(string town, string effectName, string description)
    {
        if (effectTitleText == null || descriptionText == null)
        {
            Debug.LogWarning("UI Text components are not assigned.");
            return;
        }

        effectTitleText.text = $"{town} [{effectName}]!!!";
        descriptionText.text = description;
    }
}
