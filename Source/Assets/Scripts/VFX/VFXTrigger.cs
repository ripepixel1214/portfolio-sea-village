using UnityEngine;

namespace SeaVillage.VFX
{
    /// <summary>
    /// VFX 게임 오브젝트에 추가하여 Ontrigger 이벤트 기반 VFX 재생
    /// </summary>
    public class VFXTrigger : MonoBehaviour
    {
        [Header("VFX 설정")]
        [SerializeField] private VFXType vfxType = VFXType.Sparkle;
        [SerializeField] private bool playOnStart = false;
        [SerializeField] private bool playOnTriggerEnter = false;

        [Header("재생 설정")]
        [SerializeField] private Vector3 positionOffset = Vector3.zero;
        [SerializeField] private bool followThisTransform = false;
        [SerializeField] private string requiredTag = "";

        #region MonoBehaviour Events
        private void Start()
        {
            if (playOnStart)
            {
                PlayVFX();
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (playOnTriggerEnter && CheckTag(other.gameObject))
            {
                PlayVFX();
            }
        }
        #endregion

        /// <summary>
        /// 태그 확인. 선택적 필터링 적용. 기본적으로 빈 문자열이면 모든 태그 허용.
        /// </summary>
        private bool CheckTag(GameObject obj)
        {
            return string.IsNullOrEmpty(requiredTag) || obj.CompareTag(requiredTag);
        }

        /// <summary>
        /// VFX 재생
        /// </summary>
        public void PlayVFX()
        {
            Vector3 playPosition = transform.position + positionOffset;

            if (followThisTransform)
            {
                VFXManager.TryPlayVFX(vfxType, playPosition, transform, positionOffset);
            }
            else
            {
                VFXManager.TryPlayVFX(vfxType, playPosition);
            }
        }

        /// <summary>
        /// VFX 타입 변경
        /// </summary>
        public void SetVFXType(VFXType newType)
        {
            vfxType = newType;
        }
    }
}