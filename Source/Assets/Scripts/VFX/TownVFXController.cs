using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace SeaVillage.VFX
{
    /// <summary>
    /// Town 씬 VFX 컨트롤러
    /// 현재 Town 씬만 존재하므로 별도 컨트롤러로 생성하였으나, 추후 씬이 추가됨에 따라 공통 VFXController로 구현 후 상속하도록 변경 가능
    /// </summary>
    public class TownVFXController : MonoBehaviour
    {
        [Header("환경 설정")]
        [SerializeField] private Transform[] smokePoints; // 연기 위치들
        [SerializeField] private Transform[] lightPoints; // 등불 위치들
        
        [Header("효과 설정")]
        [SerializeField] private bool enableAmbientEffects = true;
        [SerializeField] private float ambientInterval = 10f;
        
        // Town 씬에서 지속적으로 재생될 효과들의 ID list
        private List<int> persistentEffects = new List<int>();
        private Coroutine ambientCoroutine;

        #region MonoBehaviour
        private void Start()
        {
            InitializeEffects();
            StartAmbientEffects();
        }

        private void OnDestroy()
        {
            StopAllEffects();
        }
        #endregion

        #region 초기화
        /// <summary>
        /// 초기 효과 설정
        /// </summary>
        private void InitializeEffects()
        {
            // 굴뚝 연기
            StartSmokeEffects();

            // 등불
            StartPointLightEffects();
        }

        /// <summary>
        /// 굴뚝 연기 시작
        /// </summary>
        private void StartSmokeEffects()
        {
            if (smokePoints == null) return;

            foreach (var point in smokePoints)
            {
                if (point != null)
                {
                    int id = VFXManager.TryPlayVFX(VFXType.Smoke, point.position, point, Vector3.up * 0.5f);
                    if (id != -1)
                    {
                        persistentEffects.Add(id);
                    }
                }
            }
        }

        /// <summary>
        /// 등불 효과 시작
        /// </summary>
        private void StartPointLightEffects()
        {
            if (lightPoints == null) return;

            foreach (var point in lightPoints)
            {
                if (point != null)
                {
                    int id = VFXManager.TryPlayVFX(VFXType.PointLight, point.position, point);
                    if (id != -1)
                    {
                        persistentEffects.Add(id);
                    }
                }
            }
        }

        /// <summary>
        /// 환경 효과 시작
        /// </summary>
        private void StartAmbientEffects()
        {
            if (enableAmbientEffects)
            {
                ambientCoroutine = StartCoroutine(AmbientEffectsCoroutine());
            }
        }

        /// <summary>
        /// 환경 효과 코루틴. 환경 효과는 특정 오브젝트가 아닌, 씬 전반적으로 영향을 미치는 효과들을 의미함(e.g. 바람, 먼지, 나뭇잎 등등)
        /// </summary>
        private IEnumerator AmbientEffectsCoroutine()
        {
            while (enableAmbientEffects)
            {
                yield return new WaitForSeconds(ambientInterval);
                
                Vector3 randomPos = transform.position + new Vector3(
                    Random.Range(-10f, 10f),
                    Random.Range(1f, 3f),
                    Random.Range(-10f, 10f)
                );
                
                // 랜덤 위치에 먼지 효과를 재생하도록 작성했으나 추후 바람, 나뭇잎 효과 등등 추가 가능
                // VFXManager.TryPlayVFX(VFXType.{}, randomPos);
            }
        }
        #endregion

        #region NPC 효과
        /// <summary>
        /// 손님 등장 효과
        /// </summary>
        public void PlayCustomerSpawn(Vector3 position, bool isSpecial = false)
        {
            VFXManager.TryPlayVFX(VFXType.CharacterSpawn, position);
            
            if (isSpecial)
            {
                // 특별한 손님은 반짝임 추가
                StartCoroutine(DelayedSparkle(position, 0.5f));
            }
        }

        /// <summary>
        /// 지연된 반짝임
        /// </summary>
        private IEnumerator DelayedSparkle(Vector3 position, float delay)
        {
            yield return new WaitForSeconds(delay);
            VFXManager.TryPlayVFX(VFXType.Sparkle, position + Vector3.up);
        }

        /// <summary>
        /// 상호작용 힌트 표시
        /// </summary>
        public void ShowInteractionHint(Transform target, bool show = true)
        {
            if (show)
            {
                VFXManager.TryPlayVFX(VFXType.InteractionHint, target.position, target, Vector3.up * 2f);
            }
            else
            {
                // 간단한 구현에서는 자동으로 사라지도록 함
            }
        }
        #endregion

        #region UI VFXs
        /// <summary>
        /// 코인 획득 효과
        /// </summary>
        public void PlayCoinGain(Vector3 sourcePosition, int amount = 1)
        {
            VFXManager.TryPlayVFX(VFXType.CoinGain, sourcePosition);
            
            // 양이 많으면 반짝임 추가
            if (amount > 10)
            {
                StartCoroutine(DelayedSparkle(sourcePosition, 0.3f));
            }
            
            // 코인 획득 사운드 재생
        }

        /// <summary>
        /// 레벨업 효과
        /// </summary>
        public void PlayLevelUp(Vector3 position)
        {
            VFXManager.TryPlayVFX(VFXType.LevelUp, position);
            
            // 폭발 효과 추가
            StartCoroutine(DelayedExplosion(position, 0.5f));
        }

        /// <summary>
        /// 지연된 폭발 효과
        /// </summary>
        private IEnumerator DelayedExplosion(Vector3 position, float delay)
        {
            yield return new WaitForSeconds(delay);
            VFXManager.TryPlayVFX(VFXType.Explosion, position);
        }
        #endregion

        #region Utilities
        /// <summary>
        /// 환경 효과 활성화/비활성화
        /// </summary>
        public void SetAmbientEffects(bool enable)
        {
            enableAmbientEffects = enable;
            
            if (enable && ambientCoroutine == null)
            {
                ambientCoroutine = StartCoroutine(AmbientEffectsCoroutine());
            }
            else if (!enable && ambientCoroutine != null)
            {
                StopCoroutine(ambientCoroutine);
                ambientCoroutine = null;
            }
        }

        /// <summary>
        /// 모든 효과 정지
        /// </summary>
        public void StopAllEffects()
        {
            // 지속 효과 정리 (OnDestroy에서도 호출되므로 HasInstance로 확인)
            if (VFXManager.HasInstance)
            {
                VFXManager vfxManager = VFXManager.Instance;
                foreach (int id in persistentEffects)
                    vfxManager.StopVFX(id);
            }
            persistentEffects.Clear();

            // 코루틴 정지
            if (ambientCoroutine != null)
            {
                StopCoroutine(ambientCoroutine);
                ambientCoroutine = null;
            }
        }
        #endregion
    }
}