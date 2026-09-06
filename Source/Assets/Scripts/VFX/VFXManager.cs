using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using SeaVillage.Utilities;
using Sirenix.OdinInspector;

namespace SeaVillage.VFX
{
    /// <summary>
    /// VFX 관리 매니저
    /// </summary>
    public class VFXManager : Singleton<VFXManager>
    {
        [Title("VFX 설정")]
        [SerializeField] private VFXData[] vfxDataArray;
        [SerializeField] private bool enableVFX = true;
        [SerializeField] private int maxActiveVFX = 100;
        [SerializeField] private int defaultPoolSize = 10;
        
        // VFX 데이터 딕셔너리
        private Dictionary<VFXType, VFXData> vfxDataDict = new Dictionary<VFXType, VFXData>();
        
        // 활성 VFX 인스턴스들
        private Dictionary<int, VFXInstance> activeInstances = new Dictionary<int, VFXInstance>();
        
        // VFXType별 ObjectPool 딕셔너리
        private Dictionary<VFXType, ObjectPool<GameObject>> vfxPools = new Dictionary<VFXType, ObjectPool<GameObject>>();
        
        // ID 생성기. 1부터 시작하는 자동 증가값
        private int nextInstanceId = 1;
        
        // 컨테이너
        private Transform vfxContainer;

        // 프로퍼티
        public bool IsVFXEnabled => enableVFX;
        public int ActiveVFXCount => activeInstances.Count;

        #region MonoBehaviour
        protected override void Awake()
        {
            base.Awake();
            Initialize();
        }

        private void Start()
        {
            LoadVFXData();
            SetupPools();
        }

        private void Update()
        {
            if (enableVFX)
            {
                UpdateVFX();
            }
        }

        protected override void OnDestroy()
        {
            Cleanup();
            base.OnDestroy();
        }
        #endregion

        #region 초기화
        /// <summary>
        /// 초기화
        /// </summary>
        private void Initialize()
        {
            vfxContainer = new GameObject("VFX_Container").transform;
            vfxContainer.SetParent(transform);
        }

        /// <summary>
        /// VFX 데이터 로드
        /// </summary>
        private void LoadVFXData()
        {
            vfxDataDict.Clear();
            
            if (vfxDataArray != null)
            {
                foreach (var data in vfxDataArray)
                {
                    if (data != null && data.prefab != null)
                    {
                        vfxDataDict[data.type] = data;
                    }
                }
            }
        }

        /// <summary>
        /// Unity ObjectPool 설정
        /// </summary>
        private void SetupPools()
        {
            foreach (var kvp in vfxDataDict)
            {
                var vfxType = kvp.Key;
                var data = kvp.Value;
                
                if (data.usePooling && data.prefab != null)
                {
                    // 풀 사이즈 계산
                    int poolSize = Mathf.Max(1, data.poolSize > 0 ? data.poolSize : defaultPoolSize);
                    int maxPoolSize = Mathf.Max(poolSize, poolSize * 2);
                    
                    var pool = new ObjectPool<GameObject>(
                        createFunc: () => CreatePooledObject(data.prefab),
                        actionOnGet: (obj) => OnGetFromPool(obj),
                        actionOnRelease: (obj) => OnReturnToPool(obj),
                        actionOnDestroy: (obj) => OnDestroyPooledObject(obj),
                        collectionCheck: false,
                        defaultCapacity: poolSize,
                        maxSize: maxPoolSize
                    );
                    
                    vfxPools[vfxType] = pool;
                }
            }
        }

        /// <summary>
        /// 풀용 오브젝트 생성
        /// </summary>
        private GameObject CreatePooledObject(GameObject prefab)
        {
            GameObject obj = Instantiate(prefab, vfxContainer);
            obj.SetActive(false);
            return obj;
        }

        /// <summary>
        /// 풀에서 가져올 때 호출
        /// </summary>
        private void OnGetFromPool(GameObject obj)
        {
            obj.SetActive(true);
        }

        /// <summary>
        /// 풀로 반환할 때 호출
        /// </summary>
        private void OnReturnToPool(GameObject obj)
        {
            obj.SetActive(false);
            obj.transform.SetParent(vfxContainer);
        }

        /// <summary>
        /// 풀 오브젝트 삭제시 호출
        /// </summary>
        private void OnDestroyPooledObject(GameObject obj)
        {
            if (obj != null) 
                Destroy(obj);
        }
        #endregion

        #region VFX 재생
        /// <summary>
        /// VFX 재생
        /// </summary>
        /// <param name="target">이 vfx가 따라가야 할 target transform, 없다면 null</param>
        /// <returns>생성된 VFX 인스턴스 ID, 실패시 -1</returns>
        /// <summary>
        /// 인스턴스를 생성하지 않고 VFX 재생 (준비 전이면 -1)
        /// </summary>
        public static int TryPlayVFX(VFXType type, Vector3 position, Transform target = null, Vector3 offset = default)
            => HasInstance ? Instance.PlayVFX(type, position, target, offset) : -1;

        public int PlayVFX(VFXType type, Vector3 position, Transform target = null, Vector3 offset = default)
        {
            if (!enableVFX) return -1;

            // 최대 개수 체크
            if (activeInstances.Count >= maxActiveVFX)
            {
                CleanupOldest();
            }

            // VFX 데이터 확인
            if (!vfxDataDict.TryGetValue(type, out VFXData data))
            {
                Debug.LogWarning($"VFX Manager: VFX not found: {type}");
                return -1;
            }

            return CreateVFX(type, position, target, offset, data);
        }

        /// <summary>
        /// VFX 생성
        /// </summary>
        private int CreateVFX(VFXType type, Vector3 position, Transform target, Vector3 offset, VFXData data)
        {
            // 오브젝트 가져오기
            GameObject vfxObject = GetVFXObject(data);
            if (vfxObject == null) return -1;

            // 위치 설정
            vfxObject.transform.position = position;
            vfxObject.transform.SetParent(vfxContainer);

            // 인스턴스 생성
            var instance = new VFXInstance();

            if (data.isPersistent)
                instance.Initialize(nextInstanceId++, type, vfxObject);
            else
            {
                instance.Initialize(nextInstanceId++, type, vfxObject, data.duration);
                instance.OnComplete += OnVFXComplete;
            }

            // 타겟 설정
            if (target != null)
            {
                instance.SetTarget(target, offset);
            }

            // 등록
            activeInstances[instance.id] = instance;

            // // 오디오 재생
            // if (data.playSound && !string.IsNullOrEmpty(data.soundName))
            // {
            //     // 추후 SFX 에셋 추가 시 활성화
            //     AudioManager.Instance?.PlaySfx(data.soundName, data.soundVolume);
            // }

            return instance.id;
        }

        /// <summary>
        /// VFX 오브젝트 가져오기
        /// </summary>
        private GameObject GetVFXObject(VFXData data)
        {
            if (data.usePooling && vfxPools.TryGetValue(data.type, out ObjectPool<GameObject> pool))
            {
                return pool.Get();
            }
            else
            {
                return Instantiate(data.prefab, vfxContainer);
            }
        }
        #endregion

        #region VFX 제어
        /// <summary>
        /// VFX 정지
        /// </summary>
        public void StopVFX(int instanceId)
        {
            if (activeInstances.TryGetValue(instanceId, out VFXInstance instance))
            {
                instance.Stop();
            }
        }

        // 인스턴스 리스트 (GC 최소화용 재사용 리스트)
        private readonly List<VFXInstance> instanceBuffer = new List<VFXInstance>();

        /// <summary>
        /// 특정 타입의 모든 VFX 정지
        /// </summary>
        public void StopAllVFX(VFXType type)
        {
            instanceBuffer.Clear();
            
            foreach (var instance in activeInstances.Values)
            {
                if (instance.type == type)
                {
                    instanceBuffer.Add(instance);
                }
            }

            for (int i = 0; i < instanceBuffer.Count; i++)
            {
                instanceBuffer[i].Stop();
            }
        }

        /// <summary>
        /// 모든 VFX 정지
        /// </summary>
        public void StopAllVFX()
        {
            instanceBuffer.Clear();
            instanceBuffer.AddRange(activeInstances.Values);
            
            for (int i = 0; i < instanceBuffer.Count; i++)
            {
                instanceBuffer[i].Stop();
            }
        }
        #endregion

        #region 업데이트 및 관리

        // 완료된 인스턴스 ID 리스트 (GC 최소화용 재사용 리스트)
        private readonly List<int> completedIds = new List<int>();

        /// <summary>
        /// VFX 업데이트
        /// </summary>
        private void UpdateVFX()
        {
            completedIds.Clear();

            foreach (var kvp in activeInstances)
            {
                var instance = kvp.Value;
                
                // VFX 업데이트 호출
                instance.UpdateInstance();
                
                // 완료 체크
                if (instance.IsCompleted())
                {
                    completedIds.Add(kvp.Key);
                }
            }

            // 완료된 것들 정리
            for (int i = 0; i < completedIds.Count; i++)
            {
                var id = completedIds[i];
                if (activeInstances.TryGetValue(id, out VFXInstance instance))
                {
                    OnVFXComplete(instance);
                }
            }
        }

        /// <summary>
        /// VFX 완료 처리
        /// </summary>
        private void OnVFXComplete(VFXInstance instance)
        {
            if (instance == null || !activeInstances.ContainsKey(instance.id))
                return;

            // 제거
            activeInstances.Remove(instance.id);

            // 오브젝트 반납
            ReturnVFXObject(instance);

            // 정리
            instance.Cleanup();
        }

        /// <summary>
        /// VFX 오브젝트 반납
        /// </summary>
        private void ReturnVFXObject(VFXInstance instance)
        {
            if (instance.gameObject == null) return;

            var data = vfxDataDict[instance.type];
            
            if (data.usePooling && vfxPools.TryGetValue(instance.type, out ObjectPool<GameObject> pool))
            {
                pool.Release(instance.gameObject);
            }
            else
            {
                Destroy(instance.gameObject);
            }
        }

        /// <summary>
        /// 가장 오래된 VFX 정리
        /// </summary>
        private void CleanupOldest()
        {
            VFXInstance oldest = null;
            float oldestTime = float.MaxValue;

            foreach (var instance in activeInstances.Values)
            {
                if (instance.startTime < oldestTime)
                {
                    oldestTime = instance.startTime;
                    oldest = instance;
                }
            }

            oldest?.Stop();
        }
        #endregion

        #region Utilities
        /// <summary>
        /// VFX 기능 활성화/비활성화
        /// </summary>
        public void SetVFXEnabled(bool enabled)
        {
            enableVFX = enabled;
            if (!enabled)
            {
                StopAllVFX();
            }
        }

        /// <summary>
        /// VFX 데이터 가져오기
        /// </summary>
        public VFXData GetVFXData(VFXType type)
        {
            return vfxDataDict.TryGetValue(type, out VFXData data) ? data : null;
        }

        /// <summary>
        /// VFX 존재 여부 확인
        /// </summary>
        public bool HasVFX(VFXType type)
        {
            return vfxDataDict.ContainsKey(type);
        }

        /// <summary>
        /// 모든 VFX 풀과 활성화된 VFX 정리
        /// </summary>
        private void Cleanup()
        {
            StopAllVFX();
            
            foreach (var pool in vfxPools.Values)
            {
                pool.Clear();
            }
            
            vfxPools.Clear();
            activeInstances.Clear();
        }
        #endregion
    }
}