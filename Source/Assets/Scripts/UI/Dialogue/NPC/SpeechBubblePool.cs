using UnityEngine;
using UnityEngine.Pool;

namespace SeaVillage.UI.NPC
{
    public class SpeechBubblePool : MonoBehaviour
    {
        [SerializeField] private UISpeechBubbleController targetPrefab;
        [SerializeField] private Transform canvasTransform;

        private IObjectPool<UISpeechBubbleController> _pool;
        private bool _isInitialized;

        private void Awake()
        {
            if (!TryValidateConfiguration(out string failReason))
            {
                Debug.LogError($"[SpeechBubblePool] {failReason}", this);
                return;
            }

            Init();
            _isInitialized = true;
        }

        private void Init()
        {
            _pool = new ObjectPool<UISpeechBubbleController>(
                createFunc: CreateBubble,
                actionOnGet: OnActive,
                actionOnRelease: OnDeactivate,
                actionOnDestroy: OnDestroyBubble,
                collectionCheck: true,
                defaultCapacity: 5,
                maxSize: 30
            );
        }

        #region Pool 메서드

        private UISpeechBubbleController CreateBubble()
        {
            var bubble = Instantiate(targetPrefab, canvasTransform);
            bubble.SetPool(_pool);
            return bubble;
        }

        private void OnActive(UISpeechBubbleController bubble)
        {
            bubble.gameObject.SetActive(true);
        }

        private void OnDeactivate(UISpeechBubbleController bubble)
        {
            bubble.gameObject.SetActive(false);
        }

        private void OnDestroyBubble(UISpeechBubbleController bubble)
        {
            Destroy(bubble.gameObject);
        }

        #endregion

        #region Public API

        public bool TryValidateConfiguration(out string failReason)
        {
            if (targetPrefab == null)
            {
                failReason = "말풍선 프리팹 참조가 없습니다";
                return false;
            }

            if (canvasTransform == null || canvasTransform is not RectTransform)
            {
                failReason = "말풍선 Canvas RectTransform 참조가 없습니다";
                return false;
            }

            if (!targetPrefab.TryValidateConfiguration(out failReason))
            {
                failReason = $"말풍선 프리팹 설정이 올바르지 않습니다: {failReason}";
                return false;
            }

            failReason = string.Empty;
            return true;
        }

        public UISpeechBubbleController RequestBubble()
        {
            if (!_isInitialized || _pool == null)
            {
                Debug.LogError("[SpeechBubblePool] 초기화되지 않아 말풍선을 요청할 수 없습니다", this);
                return null;
            }

            return _pool.Get();
        }

        #endregion
    }
}
