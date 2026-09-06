using UnityEngine;
using Unity.Cinemachine;
using SeaVillage.Core;
using SeaVillage.Ocean.Maps;
using SeaVillage.UI;
using SeaVillage.Utilities;

namespace SeaVillage.Ocean
{
    // Ocean 씬 총괄 매니저 = 컴포지션 루트. 조립·중앙 틱 분배·교차 중재만 담당하고
    // 판정·계산은 각 도메인(IslandFleet·OceanEventManager·ShipController)이 소유한다
    public class OceanManager : MonoBehaviour, ISceneRoot
    {
        [SerializeField] private Ship ship;
        [SerializeField] private Island[] islands;
        [SerializeField] private OceanSceneConfig config;
        [SerializeField] private GridGenerator grid;
        [SerializeField] private CinemachineCamera oceanCamera;
        [SerializeField] private CinemachineConfiner2D cameraConfiner;
        [SerializeField] private BoxCollider2D cameraBounds;

        [Header("항해 이벤트")]
        [SerializeField] private OceanEventManager oceanEventManager;

        [Header("미니맵")]
        [SerializeField] private UIMinimapView minimap;

        private IslandFleet fleet;

        private bool initialized;
        private bool isRunning;

        /// <summary>
        /// 씬 루트 등록. 실제 시동은 Initialize()에서 한다.
        /// </summary>
        private void Awake()
        {
            GameManager.Instance.RegisterSceneRoot(this);
        }

        /// <summary>
        /// 씬 활성화 직후(화면 암전 상태) 배를 출항지에 배치해 페이드 인 전에 위치를 확정한다.
        /// 전환을 거치지 않은 경우(에디터 직접 Play 등)는 여기서 폴백 시동까지 수행한다.
        /// </summary>
        private void Start()
        {
            EnsureFleet();
            fleet.PlaceShipAtStart();

            SceneChanger sceneChanger = SceneChanger.Instance;
            if (sceneChanger == null || !sceneChanger.IsTransitioning)
            {
                Initialize();
            }
        }

        /// <summary>
        /// 화면 전환이 끝난 뒤 호출되는 시동 진입점. 소유 도메인을 의존순으로 시동한다(idempotent).
        /// </summary>
        public void Initialize()
        {
            if (initialized)
            {
                return;
            }
            initialized = true;

            EnsureFleet();
            SeaArea sea = grid.ResolveSeaArea();

            UpdateCameraBound(sea);

            TimeManager.Instance.OnChangeSceneToTime("Ocean");

            minimap.Initialize(ship.transform, islands, sea, config.DefaultIslandName);

            oceanEventManager.Initialize(sea.Center, sea.Size, config.DefaultIslandName, fleet.GoToScene);
            oceanEventManager.VoyageEnded += HandleVoyageEnded;

            ship.ConfigureBounds(sea.Center, sea.Size, config.WrapInsetMargin);
            ship.Warped += HandleShipWarped;
            ship.Interacted += HandleInteract;

            isRunning = true;
        }

        private void EnsureFleet()
        {
            if (fleet == null)
            {
                fleet = new IslandFleet(ship, islands, config);
            }
        }

        private void UpdateCameraBound(SeaArea sea)
        {
            if (cameraConfiner == null || cameraBounds == null)
            {
                return;
            }

            cameraBounds.transform.position = sea.Center;
            cameraBounds.size = sea.Size;
            cameraConfiner.BoundingShape2D = cameraBounds;
            cameraConfiner.InvalidateCache();
        }

        // 중앙 틱: 루트는 호출만, 판정은 각 도메인 소유 (배 경계는 ShipController 자체 수행)
        private void LateUpdate()
        {
            // ship 은 씬 언로드 중 OceanManager 보다 먼저 파괴될 수 있어 NRE 가드
            if (!isRunning || ship == null)
            {
                return;
            }

            oceanEventManager.Tick(ship.WorldPosition);
            fleet.Tick();
        }

        // 상호작용 단일 진입점: 인접 보물 상호작용
        private void HandleInteract()
        {
            if (UIManager.HasInstance && UIManager.Instance.IsAnyPanelOpened)
                return;

            if (fleet.TryEnter())
                return;

            oceanEventManager.TryInteractTreasure();
        }

        // 항해 종료(식량 고갈 귀항 등) 통지 시 씬 구동 중지
        private void HandleVoyageEnded()
        {
            isRunning = false;
        }

        // X 래핑 시 추적 카메라에 같은 델타 통지해 함께 워프
        private void HandleShipWarped(Vector2 delta)
        {
            if (oceanCamera != null)
            {
                oceanCamera.OnTargetObjectWarped(ship.transform, new Vector3(delta.x, delta.y, 0f));
            }
        }

        // 구독·도메인 정리 (영속 싱글턴 누수 방지)
        private void OnDestroy()
        {
            if (ship != null)
            {
                ship.Interacted -= HandleInteract;
                ship.Warped -= HandleShipWarped;
            }

            if (oceanEventManager != null) oceanEventManager.VoyageEnded -= HandleVoyageEnded;
        }
    }
}
