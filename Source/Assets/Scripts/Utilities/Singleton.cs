using UnityEngine;

namespace SeaVillage.Utilities
{
    public class Singleton<T> : MonoBehaviour where T : MonoBehaviour
    {
        private static T instance;
        public static T Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindAnyObjectByType<T>();

                    if (instance == null)
                    {
                        GameObject singletonObject = new GameObject();
                        instance = singletonObject.AddComponent<T>();
                        singletonObject.name = typeof(T).Name;
                        DontDestroyOnLoad(singletonObject);
                    }
                }
                return instance;
            }
        }

        /// <summary>
        /// 인스턴스 존재 여부 확인. Instance getter와 달리 생성/탐색을 유발하지 않습니다.
        /// </summary>
        public static bool HasInstance => instance != null;

        protected virtual void Awake()
        {
            InitSingleton();
        }

        /// <summary>
        /// Singleton을 상속한 오브젝트를 DontDestroyOnLoad로 설정하고, 중복된 인스턴스가 있을 경우 파괴합니다. 해당 함수는 Singleton을 상속한 클래스(Manager 클래스)의 Awake()에서 호출되어야 합니다.
        /// </summary>
        protected void InitSingleton()
        {
            // 이미 인스턴스가 존재하고 현재 오브젝트가 그 인스턴스가 아닌 경우
            if (instance != null && instance != this)
            {
                Debug.LogWarning($"Duplicate {typeof(T).Name} detected. Destroying the duplicate.");
                Destroy(gameObject);
                return;
            }

            // 현재 오브젝트를 인스턴스로 설정
            instance = this as T;

            // root transform DontDestroyOnLoad 최적화
            Transform rootTransform = transform.root;
            if (rootTransform != transform)
                DontDestroyOnLoad(rootTransform.gameObject);
            else
                DontDestroyOnLoad(gameObject);
        }

        protected static void ResetInstance()
        {
            instance = null;
        }

        /// <summary>
        /// Singleton 오브젝트 파괴시 instance 참조를 null로 설정합니다.
        /// 추가적인 정리 작업이 필요할 경우 이 메서드를 override할 수 있습니다.
        /// </summary>
        protected virtual void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }
    }
}