using MemoryPack;
using System.Collections.Generic;

namespace SeaVillage.Data
{
    /// <summary>
    /// 저장될 게임 데이터 모델
    /// </summary>
    [MemoryPackable]
    public partial class SaveData
    {
        // MemoryPack은 null 문자열을 지원하지 않으므로 string 필드들을 빈 문자열로 초기화

        // Metadata
        public string saveDate = "";
        public string gameVersion = "";

        // Game Data
        public int gameDate = 1;
        // 하루 길이가 씬마다 다르므로 초가 아닌 0~1 비율로 저장한다
        public float dayProgress = 0f;

        public string gameState = "";

        // Player Data
        public long gold = 0L;
        [MemoryPackAllowSerialize]
        public Core.PlayerGender playerGender = Core.PlayerGender.Male;
        public PlayerStatSaveData playerStats = new PlayerStatSaveData();

        // Town Progression Data
        public TownProgressionSaveData townProgression = new TownProgressionSaveData();

        // Inventory Data
        public List<Core.InventoryItem> playerInventoryItems = new List<Core.InventoryItem>();
        public List<Core.InventoryItem> shipInventoryItems = new List<Core.InventoryItem>();
        public float shipFoodStorage = 0f;
        public int shipLevel = 0;
        public float shipBonusCapacity = 0f;

        // Item Data
        public List<ItemPriceData> itemPriceData = new List<ItemPriceData>();

        // NPC 상점 당일 판매량. 남은 재고는 저작 수량에서 빼 계산하므로 판매량만 저장한다
        public List<ShopStockSaveData> shopStock = new List<ShopStockSaveData>();

        // Player Shop Data
        public List<PlayerShopData> playerShops = new List<PlayerShopData>();

        // Event System Data
        public Dictionary<string, int> eventLastTriggerDays = new Dictionary<string, int>();
        public List<string> consumedEventIds = new List<string>();
        public List<string> completedQuestIds = new List<string>();
        public Dictionary<string, List<string>> boardAssignedSlots = new Dictionary<string, List<string>>();

        // 미배치 고용 직원
        public List<StaffData> hiredStaff = new List<StaffData>();

        // Ocean Data
        public OceanFogData oceanFog = new OceanFogData(); // 미니맵 안개(탐험한 셀 비트마스크)
        public string oceanLastVisitTown = "StartTown";

        // 저장 시점의 마을 씬. 항해 중 저장 시에는 마지막 기항 마을이 기록된다
        public string currentSceneName = "StartTown";

        // 활성 가격 효과. 파생 상태는 데이터베이스에서 재계산하므로 원본만 저장한다
        public List<ActiveSpecialEffectSaveData> activeSpecialEffects = new List<ActiveSpecialEffectSaveData>();
        public List<NormalEffectSaveData> activeNormalEffects = new List<NormalEffectSaveData>();

        // 저장 시점 플레이어 월드 위치(Town 한정). 같은 씬 내 하위 마을(광산/동굴) 복원에도 사용
        public float playerPosX = 0f;
        public float playerPosY = 0f;
        public bool hasPlayerPosition = false;

        // 완료된 튜토리얼 ID. 진행 중인 행은 저장하지 않고 완료 경계만 보존한다
        public List<string> completedTutorialIds = new List<string>();
        // 튜토리얼에서 예약한 식료품 가격 변동 대상 날짜. 0이면 예약 없음
        public int tutorialForcedFoodPriceTargetDay = 0;
        // 튜토리얼 보상 지급 여부. 완료 대사와 보상 지급 사이의 복구 경계를 구분한다
        public bool tutorialRewardGranted = false;
        // 안정된 StepId 기반 진행 상태와 멱등 부작용 기록
        public TutorialProgressSaveData tutorialProgress = new TutorialProgressSaveData();
        // 최초 난파 구제의 트리거·대기·보상 지급 경계
        public FirstWreckRecoverySaveData firstWreckRecovery = new FirstWreckRecoverySaveData();

        public SaveData() { }
    }
}
