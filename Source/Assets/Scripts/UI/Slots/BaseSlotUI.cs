using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SeaVillage.Data;

namespace SeaVillage.UI
{
    /// <summary>
    /// 모든 슬롯 UI의 기본 클래스
    /// </summary>
    public abstract class BaseSlotUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] protected Image itemIcon;
        [SerializeField] protected TextMeshProUGUI infoText;

        protected Button slotButton;
        protected ItemData itemData;
        protected int quantity;
        
        // Properties
        public bool isEmpty { get; protected set;}

        public event Action OnSlotClicked;

        protected virtual void Awake()
        {
            slotButton = GetComponent<Button>();
            slotButton?.onClick.AddListener(() => OnSlotClicked?.Invoke());
            slotButton?.onClick.AddListener(Audio.AudioManager.TryPlayClickSfx);
        }

        /// <summary>
        /// 아이템과 수량으로 슬롯 초기화
        /// </summary>
        public abstract void Initialize(ItemData data, int itemQuantity);

        public abstract void UpdateInfoText();

        public virtual void InitializeEmpty()
        {
            isEmpty = true;
            itemData = null;
            quantity = 0;

            if (itemIcon != null)
                itemIcon.enabled = false;

            if (infoText != null)
                infoText.enabled = false;
        }

        protected void SetItemIcon(string imageName)
        {
            if (itemIcon == null)
            {
                Debug.LogWarning("Item Icon 참조가 설정되지 않았습니다.");
                return;
            }

            if (string.IsNullOrEmpty(imageName))
                return;

            // 1. 이미 연결된 Icon이 있는지 확인 (최적화 경로)
            if (itemData != null && itemData.Icon != null)
            {
                itemIcon.sprite = itemData.Icon;
                itemIcon.enabled = true;
                return;
            }

            // 2. 캐시/로드를 통해 가져오기
            Sprite iconSprite = UIManager.Instance.LoadItemIcon(imageName);
            if (iconSprite != null)
            {
                itemIcon.sprite = iconSprite;
                itemIcon.enabled = true;
                
                // 나중을 위해 데이터에 연결
                if (itemData != null) itemData.Icon = iconSprite;
            }
            else
                Debug.LogWarning($"아이템 아이콘을 로드할 수 없습니다: {imageName}");
        }

        public Button GetButton()
        {
            return slotButton;
        }

        public ItemData GetItemData()
        {
            return itemData;
        }

        public Image GetItemIcon()
        {
            return itemIcon;
        }

        public int GetQuantity()
        {
            return quantity;
        }

        public bool IsEmpty()
        {
            return isEmpty;
        }

        /// <summary>
        /// 슬롯의 스크린 좌표 반환
        /// </summary>
        /// <returns></returns>
        public Vector2 GetSlotScreenPosition()
        {
            RectTransform slotRect = GetComponent<RectTransform>();
            if (slotRect == null) return Vector2.zero;
            
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return Vector2.zero;
            
            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            
            // world position => screen position => canvas local position로 변환
            Vector2 localPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, slotRect.position),
                canvas.worldCamera,
                out localPoint
            );
            
            // 슬롯의 오른쪽 아래에 패널이 표시되도록 오프셋 추가
            Vector2 offset = new Vector2(slotRect.sizeDelta.x / 2, -slotRect.sizeDelta.y / 2);
            
            return localPoint + offset;
        }
    }
}
