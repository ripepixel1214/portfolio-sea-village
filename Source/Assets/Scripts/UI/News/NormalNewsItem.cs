using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class NormalNewsItem : MonoBehaviour
{
    [SerializeField]
    private TextMeshProUGUI categoryText;
    [SerializeField]
    private TextMeshProUGUI fluctuationText;
    [SerializeField]
    private Image fluctuationIcon;

    public void SetInformation(string category, string fluctuationRate)
    {
        if (categoryText == null || fluctuationText == null)
        {
            Debug.LogWarning("UI Text components are not assigned.");
            return;
        }

        categoryText.text = category;
        fluctuationText.text = fluctuationRate;

        if (fluctuationRate.StartsWith("+"))
        {
            fluctuationIcon.color = Color.red;
            fluctuationIcon.transform.localRotation = Quaternion.Euler(0, 0, 0);
        }
        else if (fluctuationRate.StartsWith("-"))
        {
            fluctuationIcon.color = Color.blue;
            fluctuationIcon.transform.localRotation = Quaternion.Euler(180, 0, 0);
        }
        else
            fluctuationIcon.color = Color.gray;
    }
}
