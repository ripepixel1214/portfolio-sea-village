using System;
using System.Collections;
using UnityEngine;
using SeaVillage.Utilities;

namespace SeaVillage.Core
{
    /// <summary>
    /// 게임 내 시간(날짜) 관리를 전담하는 매니저
    /// </summary>
    public class TimeManager : Singleton<TimeManager>
    {
        [Header("Time Settings")]
        [SerializeField] private float dayDuration = 30f;
        [SerializeField] private float townDayDuration = 30f;
        [SerializeField] private float oceanDayDuration = 3f;
        [SerializeField] private int currentDay = 1;
        [SerializeField] private bool autoProgress = true;

        [Header("Debug")]
        [SerializeField] private float elapsedTimeInCurrentDay = 0f;

        private const string OceanSceneName = "Ocean";

        // Events
        public static event Action<int> OnDayChanged; // 날짜 변경 시 (새로운 날짜 전달)
        public static event Action<int> OnBeforeDayChanged; // 날짜 변경 직전 (현재 날짜 전달)
        public static event Action<bool> OnGameplayPaused; // 게임플레이 일시정지/재개 (정지의 영향을 받을 클래스(NPC 등)에서 구독용)

        // Properties
        public int CurrentDay => currentDay;
        public float DayDuration => dayDuration;
        public float DayProgress => dayDuration > 0f ? elapsedTimeInCurrentDay / dayDuration : 0f; // 0~1 (당일 진행도)
        public bool AutoProgress => autoProgress;
        public bool IsInitialized { get; private set; } = false;
        public bool IsPaused => isPaused;

        private Coroutine dayProgressCoroutine;
        private bool isPaused = false;

        #region MonoBehaviour
        protected override void Awake()
        {
            base.Awake();
            Initialize();
        }

        /// <summary>
        /// GameManager에서 호출되는 초기화 메소드
        /// </summary>
        public void Initialize()
        {
            if (IsInitialized)
            {
                Debug.LogWarning("[TimeManager] Already initialized.");
                return;
            }

            // 자동 진행이 활성화되어 있으면 날짜 진행 코루틴 시작
            if (autoProgress)
                StartDayProgressCoroutine();

            IsInitialized = true;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            StopDayProgressCoroutine();
        }
        #endregion

        #region Day Management
        /// <summary>
        /// 날짜를 수동으로 진행시킨다 (일 단위)
        /// </summary>
        /// <param name="days">진행할 날짜 수</param>
        public void AdvanceDay(int days = 1)
        {
            if (days <= 0)
            {
                Debug.LogWarning($"[TimeManager] Invalid day count: {days}. Must be positive.");
                return;
            }

            int oldDay = currentDay;
            currentDay += days;
            elapsedTimeInCurrentDay = 0f;

            OnDayChanged?.Invoke(currentDay);
        }

        /// <summary>
        /// 특정 날짜로 직접 설정한다
        /// </summary>
        /// <param name="newDay">설정할 날짜</param>
        public void SetDay(int newDay)
        {
            if (newDay < 1)
            {
                Debug.LogWarning($"[TimeManager] Invalid day: {newDay}. Must be >= 1.");
                return;
            }

            if (newDay == currentDay)
                return;

            int oldDay = currentDay;
            currentDay = newDay;
            elapsedTimeInCurrentDay = 0f;

            // 이벤트 발생 (로드 시에는 이벤트를 발생시키지 않을 수도 있음)
            OnDayChanged?.Invoke(currentDay);
        }

        /// <summary>
        /// 날짜와 당일 진행도를 설정하되 이벤트를 발생시키지 않음 (로드 시 사용)
        /// </summary>
        /// <param name="dayProgress">0~1 비율. 하루 길이가 씬마다 다르므로 초가 아닌 비율로 받는다</param>
        public void SetDaySilent(int newDay, float dayProgress)
        {
            if (newDay < 1)
            {
                Debug.LogWarning($"[TimeManager] Invalid day: {newDay}. Must be >= 1.");
                return;
            }

            currentDay = newDay;
            elapsedTimeInCurrentDay = Mathf.Clamp01(dayProgress) * dayDuration;

            Debug.Log($"[TimeManager] Day set silently: {currentDay} ({DayProgress:P0})");
        }
        #endregion

        #region Scene Time
        /// <summary>
        /// 씬에 맞는 하루 길이로 전환하고 자동 진행 여부를 설정. 당일 진행도(비율)는 유지
        /// </summary>
        public void OnChangeSceneToTime(string destinationSceneName)
        {
            bool isOcean = destinationSceneName == OceanSceneName;

            float progress = DayProgress;
            dayDuration = isOcean ? oceanDayDuration : townDayDuration;
            elapsedTimeInCurrentDay = Mathf.Clamp01(progress) * dayDuration;

            // 항해 씬만 자동 진행, 마을은 수동(Day 버튼)
            SetAutoProgress(isOcean);
        }
        #endregion

        #region Auto Progress Control
        /// <summary>
        /// 자동 날짜 진행 시작
        /// </summary>
        public void StartDayProgressCoroutine()
        {
            if (dayProgressCoroutine != null)
            {
                Debug.LogWarning("[TimeManager] Day progress coroutine is already running.");
                return;
            }

            dayProgressCoroutine = StartCoroutine(DayProgressRoutine());
        }

        /// <summary>
        /// 자동 날짜 진행을 중단
        /// </summary>
        public void StopDayProgressCoroutine()
        {
            if (dayProgressCoroutine != null)
            {
                StopCoroutine(dayProgressCoroutine);
                dayProgressCoroutine = null;
            }
        }

        /// <summary>
        /// 시간 진행 일시정지 (게임플레이 요소만 정지, UI 영향 X)
        /// </summary>
        public void PauseTimeProgress()
        {
            if (isPaused) return;

            isPaused = true;
            OnGameplayPaused?.Invoke(true);
        }

        /// <summary>
        /// 시간 진행 재개
        /// </summary>
        public void ResumeTimeProgress()
        {
            if (!isPaused) return;

            isPaused = false;
            OnGameplayPaused?.Invoke(false);
        }

        /// <summary>
        /// 자동 진행 활성화/비활성화 토글
        /// </summary>
        public void SetAutoProgress(bool enabled)
        {
            autoProgress = enabled;

            if (enabled && dayProgressCoroutine == null)
                StartDayProgressCoroutine();
            else if (!enabled && dayProgressCoroutine != null)
                StopDayProgressCoroutine();
        }
        #endregion

        #region Coroutine
        /// <summary>
        /// 실시간 타이머를 통해 날짜를 자동으로 진행시키는 코루틴
        /// </summary>
        private IEnumerator DayProgressRoutine()
        {
            while (true)
            {
                // 일시정지 상태면 대기
                if (isPaused
                    || GameManager.Instance.CurrentGameState == GameState.MainMenu
                    || GameManager.Instance.CurrentGameState == GameState.Loading)
                {
                    yield return null;
                    continue;
                }

                elapsedTimeInCurrentDay += Time.deltaTime;

                // 1일이 경과했으면 날짜 진행
                if (elapsedTimeInCurrentDay >= dayDuration)
                {
                    // 초과 시간 보존 (정확성 유지용)
                    float overflow = elapsedTimeInCurrentDay - dayDuration;
                    elapsedTimeInCurrentDay = overflow;

                    // 날짜 변경 전 이벤트
                    OnBeforeDayChanged?.Invoke(currentDay);

                    // 날짜 진행
                    int oldDay = currentDay;
                    currentDay++;

                    // 날짜 변경 후 이벤트
                    OnDayChanged?.Invoke(currentDay);
                }

                yield return null;
            }
        }
        #endregion

        #region Debug Utilities
        /// <summary>
        /// 디버그: 1일을 즉시 진행
        /// </summary>
        [ContextMenu("Debug: Advance 1 Day")]
        public void DebugAdvanceOneDay()
        {
            AdvanceDay(1);
        }

        /// <summary>
        /// 디버그: 날짜 진행 속도 변경
        /// </summary>
        public void SetDayDuration(float newDuration)
        {
            if (newDuration <= 0)
            {
                Debug.LogWarning($"[TimeManager] Invalid day duration: {newDuration}. Must be > 0.");
                return;
            }

            dayDuration = newDuration;
        }
        #endregion
    }
}
