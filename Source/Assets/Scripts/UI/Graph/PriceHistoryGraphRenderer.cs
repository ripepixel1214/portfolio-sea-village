using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace SeaVillage.UI
{
    /// <summary>
    /// UI Toolkit을 사용하여 가격 히스토리 그래프를 RenderTexture로 출력하는 렌더러
    /// </summary>
    public class PriceHistoryGraphRenderer : IDisposable
    {
        private const int PointRadius = PriceGraphScaleUtility.PointRadius;
        private const int LineThickness = 2;

        private List<VisualElement> dataPoints = new List<VisualElement>();
        private List<VisualElement> lineSegments = new List<VisualElement>();
        
        private Color pointColor = new Color(0.9f, 0.3f, 0.1f, 1f);
        private Color lineColor = new Color(1f, 0.4f, 0.2f, 1f);
        private Color backgroundColor = new Color(0f, 0f, 0f, 0.1f);
        
        private int textureWidth;
        private int textureHeight;

        private UIDocument uiDocument;
        private RenderTexture renderTexture;
        private VisualElement graphArea;

        // Properties
        public RenderTexture OutputTexture => renderTexture;

        /// <summary>
        /// 그래프 렌더러 초기화
        /// </summary>
        public bool Initialize(int width, int height, ThemeStyleSheet themeStyleSheet)
        {
            if (themeStyleSheet == null)
            {
                Debug.LogError("PriceHistoryGraphRenderer: ThemeStyleSheet is null. Graph renderer initialization aborted.");
                return false;
            }

            textureWidth = width;
            textureHeight = height;
            
            renderTexture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32);
            renderTexture.Create();  // GPU에 실제 메모리 할당

            // MonoBehaviour인 UIDocument를 위한 런타임 전용 임시 GameObject 생성
            GameObject graphObject = new GameObject("PriceHistoryGraph_UIDocument");
            graphObject.hideFlags = HideFlags.HideAndDontSave;
            graphObject.SetActive(false);

            // UIDocument 컴포넌트 추가 및 설정
            uiDocument = graphObject.AddComponent<UIDocument>();
            uiDocument.panelSettings = CreatePanelSettings(width, height, themeStyleSheet);
            graphObject.SetActive(true);

            BuildVisualTree();

            return true;
        }

        /// <summary>
        /// PanelSettings 생성 => UI Toolkit 렌더링 설정
        /// </summary>
        private PanelSettings CreatePanelSettings(int width, int height, ThemeStyleSheet themeStyleSheet)
        {
            PanelSettings settings = ScriptableObject.CreateInstance<PanelSettings>();
            
            settings.targetTexture = renderTexture;
            settings.themeStyleSheet = themeStyleSheet;
            settings.scaleMode = PanelScaleMode.ConstantPixelSize;
            settings.referenceResolution = new Vector2Int(width, height);
            settings.clearColor = true;
            settings.colorClearValue = backgroundColor;
            
            return settings;
        }

        /// <summary>
        /// UI 요소 구조 생성(그래프 영역)
        /// </summary>
        private void BuildVisualTree()
        {
            var root = uiDocument.rootVisualElement;
            root.pickingMode = PickingMode.Ignore;

            // flexGrow = 1 => Flexbox에서 남는 공간을 모두 차지
            root.style.flexGrow = 1;
            root.style.backgroundColor = backgroundColor;
            
            // 내부 여백 (자식 요소와의 간격)
            root.style.paddingTop = 10;
            root.style.paddingBottom = 10;
            root.style.paddingLeft = 10;
            root.style.paddingRight = 10;

            graphArea = new VisualElement();
            graphArea.pickingMode = PickingMode.Ignore;

            // name: 디버깅 및 UXML에서 요소 식별용
            graphArea.name = "graph-area";

            graphArea.style.flexGrow = 1;
            graphArea.style.position = Position.Relative;
            graphArea.style.backgroundColor = backgroundColor;

            // 각 모서리 개별 설정
            graphArea.style.borderTopLeftRadius = 6;
            graphArea.style.borderTopRightRadius = 6;
            graphArea.style.borderBottomLeftRadius = 6;
            graphArea.style.borderBottomRightRadius = 6;
            
            // padding 고려
            graphArea.style.width = textureWidth - 20;
            graphArea.style.height = textureHeight - 20;
            
            // root에 자식으로 추가
            root.Add(graphArea);
        }

        /// <summary>
        /// 가격 데이터 리스트로 그래프 업데이트
        /// </summary>
        public void UpdateGraph(List<int> priceHistory)
        {
            if (priceHistory == null || priceHistory.Count == 0)
            {
                ClearGraphData();
                return;
            }

            ClearGraphData();

            PriceGraphScaleUtility.ComputeRange(priceHistory, priceHistory[priceHistory.Count - 1], out int minPrice, out int maxPrice);

            float areaWidth = textureWidth - 20;
            float areaHeight = textureHeight - 20;
            
            int count = priceHistory.Count;

            if (count == 1)
            {
                float yPercent = PriceGraphScaleUtility.ValueToNormalizedY(priceHistory[0], minPrice, maxPrice);
                float y = PriceGraphScaleUtility.NormalizedYToPixelY(yPercent, areaHeight);
                var line = CreateLineSegment(new Vector2(PointRadius, y), new Vector2(areaWidth - PointRadius, y));
                graphArea.Add(line);
                lineSegments.Add(line);
                graphArea.MarkDirtyRepaint();
                return;
            }

            List<Vector2> positions = new List<Vector2>();

            for (int i = 0; i < count; i++)
            {
                float xPercent = (float)i / (count - 1);
                
                float yPercent = PriceGraphScaleUtility.ValueToNormalizedY(priceHistory[i], minPrice, maxPrice);

                // 실제 픽셀 좌표로 변환 (여백 고려)
                float x = xPercent * (areaWidth - PointRadius * 2) + PointRadius;
                float y = PriceGraphScaleUtility.NormalizedYToPixelY(yPercent, areaHeight);
                
                positions.Add(new Vector2(x, y));

                var point = CreateDataPoint(x, y);
                graphArea.Add(point);
                dataPoints.Add(point);
            }

            // 좌표 변환 완료, 라인 세그먼트 그리기
            for (int i = 0; i < positions.Count - 1; i++)
            {
                var line = CreateLineSegment(positions[i], positions[i + 1]);
                graphArea.Insert(0, line);
                lineSegments.Add(line);
            }

            graphArea.MarkDirtyRepaint();
        }

        /// <summary>
        /// 그래프를 소유한 UGUI가 비활성화되면 UI Toolkit 패널도 함께 비활성화
        /// </summary>
        public void SetActive(bool isActive)
        {
            if (uiDocument == null || uiDocument.gameObject.activeSelf == isActive)
                return;

            uiDocument.gameObject.SetActive(isActive);
        }

        /// <summary>
        /// 원형 데이터 포인트 생성
        /// </summary>
        private VisualElement CreateDataPoint(float x, float y)
        {
            var point = new VisualElement();
            point.pickingMode = PickingMode.Ignore;

            point.style.position = Position.Absolute;
            
            // 부모 왼쪽 상단 모서리 기준 오프셋
            point.style.left = x - PointRadius;
            point.style.top = y - PointRadius;
            
            point.style.width = PointRadius * 2;
            point.style.height = PointRadius * 2;

            // borderRadius를 너비(지름)의 50%로 설정하여 원형 생성
            point.style.borderTopLeftRadius = PointRadius;
            point.style.borderTopRightRadius = PointRadius;
            point.style.borderBottomLeftRadius = PointRadius;
            point.style.borderBottomRightRadius = PointRadius;
            
            point.style.backgroundColor = pointColor;
            
            return point;
        }

        /// <summary>
        /// 두 점을 연결하는 라인 세그먼트 생성
        /// </summary>
        private VisualElement CreateLineSegment(Vector2 start, Vector2 end)
        {
            var line = new VisualElement();
            line.pickingMode = PickingMode.Ignore;

            float dx = end.x - start.x;
            float dy = end.y - start.y;
            
            float length = Mathf.Sqrt(dx * dx + dy * dy);
            
            float angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
            
            line.style.position = Position.Absolute;
            line.style.left = start.x;
            line.style.top = start.y - LineThickness * 0.5f; // 라인 두께 절반만큼 위로 오프셋
            
            line.style.width = length;
            line.style.height = LineThickness;
            line.style.backgroundColor = lineColor;
            
            // transformOrigin (회전 기준점) => UGUI의 pivot과 유사
            // 왼쪽 중앙 기준 회전
            line.style.transformOrigin = new TransformOrigin(0, Length.Percent(50));
            
            // z축으로 angle만큼 회전
            line.transform.rotation = Quaternion.Euler(0, 0, angle);
            
            return line;
        }

        private void ClearGraphData()
        {
            // RemoveFromHierarchy(): 메모리에서 완전히 제거되지는 않음 (GC 대상이 됨)
            foreach (var point in dataPoints)
                point.RemoveFromHierarchy();
            foreach (var line in lineSegments)
                line.RemoveFromHierarchy();
                
            dataPoints.Clear();
            lineSegments.Clear();
        }

        public void SetColors(Color line, Color point)
        {
            lineColor = line;
            pointColor = point;
        }

        #region IDisposable
        /// <summary>
        /// 메모리 누수 방지
        /// </summary>
        public void Dispose()
        {
            // panelSettings, UI document 모두 명시적으로 Destroy 호출 필요
            if (uiDocument != null)
            {
                if (uiDocument.panelSettings != null)
                    UnityEngine.Object.Destroy(uiDocument.panelSettings);
                UnityEngine.Object.Destroy(uiDocument.gameObject);
            }

            if (renderTexture != null)
            {
                // Destroy만 호출하면 다음 프레임까지 GPU 메모리 점유하므로, Release()로 GPU 메모리 즉시 해제
                renderTexture.Release();
                UnityEngine.Object.Destroy(renderTexture);
            }
        }
        #endregion
    }
}
