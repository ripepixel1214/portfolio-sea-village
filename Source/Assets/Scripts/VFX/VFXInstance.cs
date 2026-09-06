using UnityEngine;
using System;

namespace SeaVillage.VFX
{
    /// <summary>
    /// VFX 인스턴스 클래스. VFX 게임 오브젝트, 상태, 타겟 추적 등을 관리
    /// Unity ObjectPool과 함께 사용하여 간단하고 효율적인 VFX 관리
    /// </summary>
    [Serializable]
    public class VFXInstance
    {
        [Header("기본 정보")]
        public int id;
        public VFXType type;
        public GameObject gameObject;
        public Transform transform;
        public bool isActive;
        
        [Header("시간 정보")]
        public float startTime;
        public float duration;
        
        [Header("추적 정보")]
        public Transform target;
        public Vector3 targetOffset;
        public bool followTarget;

        // 이벤트
        public event Action<VFXInstance> OnComplete;

        public VFXInstance()
        {
            id = 0;
            startTime = 0f;
            duration = 0f;
            isActive = false;
            followTarget = false;
        }

        /// <summary>
        /// VFX 인스턴스 초기화
        /// </summary>
        public void Initialize(int instanceId, VFXType vfxType, GameObject vfxObject, float vfxDuration = -1f)
        {
            id = instanceId;
            type = vfxType;
            gameObject = vfxObject;
            transform = vfxObject.transform;
            
            startTime = Time.time;
            duration = vfxDuration;
            isActive = true;
        }

        /// <summary>
        /// 타겟 설정
        /// </summary>
        public void SetTarget(Transform targetTransform, Vector3 offset = default)
        {
            target = targetTransform;
            targetOffset = offset;
            followTarget = target != null;
        }

        /// <summary>
        /// 매니저로부터 호출되는 VFX 업데이트 함수.
        /// 위치, 상태 등을 갱신한다
        /// </summary>
        public void UpdateInstance()
        {
            UpdatePosition();
        }

        /// <summary>
        /// VFX 위치 업데이트
        /// </summary>
        public void UpdatePosition()
        {
            if (followTarget && target != null && transform != null)
            {
                transform.position = target.position + targetOffset;
            }
        }

        /// <summary>
        /// VFX 완료 여부 확인
        /// </summary>
        public bool IsCompleted()
        {
            if (!isActive) return true;
            
            // 시간 기반 완료 체크
            if (duration > 0f && Time.time - startTime >= duration)
            {
                return true;
            }
            
            // 오브젝트가 비활성화되었으면 완료
            if (gameObject == null || !gameObject.activeInHierarchy)
            {
                return true;
            }
            
            return false;
        }

        /// <summary>
        /// VFX 정지
        /// </summary>
        public void Stop()
        {
            if (!isActive) return;
            
            if (gameObject != null)
            {
                gameObject.SetActive(false);
            }
            
            isActive = false;
            OnComplete?.Invoke(this);
        }

        /// <summary>
        /// 인스턴스 정리
        /// </summary>
        public void Cleanup()
        {
            Stop();
            OnComplete = null;
            target = null;
            transform = null;
        }
    }
}