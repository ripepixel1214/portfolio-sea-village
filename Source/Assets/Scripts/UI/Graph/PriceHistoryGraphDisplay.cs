using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;
using SeaVillage.Data;
using SeaVillage.Core;
using SeaVillage.UI.Tutorial;
using UnityEngine.EventSystems;

namespace SeaVillage.UI
{
    /// <summary>
    /// Canvas RawImage에 UI Toolkit 그래프를 표시하는 컴포넌트(직접 부착)
    /// </summary>
    [RequireComponent(typeof(RawImage))]
    public class PriceHistoryGraphDisplay : MonoBehaviour, IPointerClickHandler
    {
        [Header("Graph Settings")]
        [SerializeField] private int graphWidth = 400;
        [SerializeField] private int graphHeight = 200;
        
        [Header("Colors")]
        [SerializeField] private Color pointColor = new Color(0.8f, 0.2f, 0f, 1f);
        [SerializeField] private Color lineColor = new Color(1f, 0.4f, 0.2f, 1f);
        
        [Header("UI Toolkit Theme (Required)")]
        [SerializeField] private ThemeStyleSheet themeStyleSheet;

        private RawImage rawImage;
        private PriceHistoryGraphRenderer graphRenderer;
        private bool isInitialized = false;

        private int currentItemPriceId;
        private string currentTown;

        private void Awake()
        {
            rawImage = GetComponent<RawImage>();
            TutorialUIBinding.Bind(this, TutorialAnchorKeys.PriceGraph);
        }

        private void OnEnable()
        {
            Initialize();
            graphRenderer?.SetActive(true);
        }

        private void OnDisable()
        {
            graphRenderer?.SetActive(false);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            TutorialEventReporter.Report(TutorialEventType.UiElementActivated, TutorialTargetIds.PriceGraph, source: TutorialEventSource.UserInterface);
        }

        private void OnDestroy()
        {
            Cleanup();
        }

        /// <summary>
        /// 그래프 초기화
        /// </summary>
        public void Initialize()
        {
            if (isInitialized) return;

            // 테마가 없으면 기본 런타임 테마 로드 시도
            if (themeStyleSheet == null)
            {
                themeStyleSheet = Resources.Load<ThemeStyleSheet>("UnityThemes/UnityDefaultRuntimeTheme");
                if (themeStyleSheet == null)
                {
                    Debug.LogError("PriceHistoryGraphDisplay: ThemeStyleSheet이 설정되지 않았습니다. Inspector에서 설정하거나 Resources 폴더에 테마를 추가하세요.");
                    return;
                }
            }

            graphRenderer = new PriceHistoryGraphRenderer();
            if (!graphRenderer.Initialize(graphWidth, graphHeight, themeStyleSheet))
            {
                Debug.LogError("PriceHistoryGraphDisplay: Graph renderer initialization failed. Check ThemeStyleSheet assignment.");
                graphRenderer.Dispose();
                graphRenderer = null;
                return;
            }
            graphRenderer.SetColors(lineColor, pointColor);

            rawImage.texture = graphRenderer.OutputTexture;

            isInitialized = true;
        }

        /// <summary>
        /// 아이템 ID, 마을로 그래프 표시
        /// </summary>
        public void ShowPriceHistory(int itemPriceId, string town)
        {
            if (!isInitialized) Initialize();
            if (!isInitialized) return; // Initialize 실패 시

            currentItemPriceId = itemPriceId;
            currentTown = town;

            var priceManager = RuntimeItemPriceManager.Instance;
            List<int> priceHistory = priceManager.GetPriceHistory(itemPriceId, town);
            graphRenderer.UpdateGraph(priceHistory ?? new List<int>());
        }

        /// <summary>
        /// 가격 리스트로 직접 그래프 표시
        /// </summary>
        public void ShowPriceHistory(List<int> priceHistory)
        {
            if (!isInitialized) Initialize();
            if (!isInitialized) return;

            graphRenderer.UpdateGraph(priceHistory ?? new List<int>());
        }

        /// <summary>
        /// 현재 필드 기준으로 그래프 새로고침
        /// </summary>
        public void Refresh()
        {
            if (currentItemPriceId > 0 && !string.IsNullOrEmpty(currentTown))
                ShowPriceHistory(currentItemPriceId, currentTown);
        }

        public void SetColors(Color line, Color point)
        {
            lineColor = line;
            pointColor = point;
            graphRenderer?.SetColors(line, point);
        }

        private void Cleanup()
        {
            graphRenderer?.Dispose();
            graphRenderer = null;
            
            if (rawImage != null)
                rawImage.texture = null;
        }
    }
}
