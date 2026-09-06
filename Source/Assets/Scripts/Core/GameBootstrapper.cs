using System.Collections;
using UnityEngine;

namespace SeaVillage.Core
{
    /// <summary>
    /// 게임 초기화와 전체 시스템 설정을 담당하는 부트스트래퍼 클래스
    /// </summary>
    public class GameBootstrapper : MonoBehaviour
    {
        [Header("Initialization Settings")]
        // 게임 시작 시 자동 초기화 여부
        [SerializeField] private bool initializeOnStart = true;

        public bool IsGameManagerEnsured { get; private set; } = false;

        private void Awake()
        {
            if (initializeOnStart)
            {
                InitializeGame();
            }
        }

        /// <summary>
        /// 필요한 매니저 클래스들의 유무를 확인하고, 게임을 초기화하는 함수
        /// </summary>
        private void InitializeGame()
        {
            Debug.Log("=== Starting 바다 마을 Bootstrapping ===");

            // 1. 게임 매니저 확인
            EnsureGameManager();

            // 2. 게임 데이터 초기화
            InitializeGameData();

            // 3. 테스트 데이터 생성 로직(필요시)
            // 4. 게임 세이브 데이터 로드 로직

            // 5. 초기화 완료 처리
            StartCoroutine(FinalizeInitializationCoroutine());
        }

        private void EnsureGameManager()
        {
            GameManager gm = GameManager.Instance;
            Debug.Log($"GameBootstrapper: {gm} instance accessed.");

            // 매니저들의 초기화는 GameManager의 InitializeManagers 코루틴에서 처리
            
            IsGameManagerEnsured = gm != null;
            if (IsGameManagerEnsured)
                Debug.Log("Game manager ensured successfully.");
            else
                Debug.LogError("GameManager instance not found! Ensure a GameManager is present in the scene.");
        }

        private void InitializeGameData()
        {
            // 게임 데이터 초기화 로직이 들어갈 예정
            Debug.Log("Game data initialized.");
        }

        private IEnumerator FinalizeInitializationCoroutine()
        {
            GameManager gameManager = GameManager.HasInstance ? GameManager.Instance : null;
            if (gameManager == null)
            {
                Debug.LogError("GameBootstrapper: GameManager가 없어 초기화를 완료할 수 없습니다");
                yield break;
            }

            yield return new WaitUntil(() =>
                gameManager == null
                || gameManager.IsAllManagerInitialized
                || gameManager.HasInitializationFailed);

            if (gameManager == null)
            {
                Debug.LogError("GameBootstrapper: 초기화 중 GameManager가 파괴되었습니다");
                yield break;
            }

            if (gameManager.HasInitializationFailed)
            {
                Debug.LogError($"GameBootstrapper: 게임 초기화 실패: {gameManager.InitializationFailureReason}");
                yield break;
            }

            Debug.Log("Game initialization finalized");
        }
    }
}
