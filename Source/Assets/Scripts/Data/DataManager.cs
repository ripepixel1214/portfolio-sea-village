using System.IO;
using System.Collections.Generic;
using UnityEngine;
using SeaVillage.Utilities;

namespace SeaVillage.Data
{
    public class DataManager : Singleton<DataManager>
    {
        public const string CSV_FOLDER_PATH = "Assets/Resources/Data/CSV/";
        public const string SO_FOLDER_PATH = "Assets/Resources/Data/ScriptableObjects/";
        public const string ItemCSVFileName = "Item.csv";
        public const string ItemTypeCSVFileName = "ItemType.csv";
        public const string ItemPriceCSVFileName = "ItemPrice.csv";
        public const string RecipeCSVFileName = "Recipe.csv";
        public const string ShopCSVFileName = "Shop.csv";
        public const string CustomerCSVFileName = "Customer.csv";
        public const string CustomerSpawnCSVFileName = "CustomerSpawn.csv";
        public const string CustomerDialogueCSVFileName = "CustomerDialogue.csv";
        public const string EventCSVFileName = "Event.csv";
        public const string EventConditionCSVFileName = "EventCondition.csv";
        public const string EventSequenceCSVFileName = "EventSequence.csv";
        public const string EventDialogueCSVFileName = "EventDialogue.csv";
        public const string ScriptCSVFileName = "Script.csv";
        public const string VariableCSVFileName = "Variable.csv";
        public const string BoardCSVFileName = "Board.csv";
        public const string SpecialEffectCSVFileName = "SpecialEffect.csv";
        public const string SpecialEffectItemChangeCSVFileName = "SpecialEffectItemChange.csv";
        public const string EnumCSVFileName = "Enum.csv";
        public const string TutorialCSVFileName = "Tutorial.csv";

        private const string RESOURCES_SO_PATH = "Data/ScriptableObjects/";

        [Header("Database References")]
        [SerializeField] private ItemDatabase itemDatabase;
        [SerializeField] private ItemTypeDatabase itemTypeDatabase;
        [SerializeField] private ItemPriceDatabase itemPriceDatabase;
        [SerializeField] private RecipeDatabase recipeDatabase;
        [SerializeField] private ShopDatabase shopDatabase;
        [SerializeField] private CustomerDatabase customerDatabase;
        [SerializeField] private BoardDatabase boardDatabase;
        [SerializeField] private EventDatabase eventDatabase;
        [SerializeField] private ScriptDatabase scriptDatabase;
        [SerializeField] private TutorialDatabase tutorialDatabase;
        [SerializeField] private GameDatabase gameDatabase;
        [SerializeField] private SpecialEffectDatabase specialEffectDatabase;
        [SerializeField] private StaffCatalog staffCatalog;
        [SerializeField] private PlayerShopUpgradeCatalog playerShopUpgradeCatalog;
        [SerializeField] private SpecialShopContentCatalog specialShopContentCatalog;

        // Public Properties
        public ItemDatabase ItemDatabase => itemDatabase;
        public ItemTypeDatabase ItemTypeDatabase => itemTypeDatabase;
        public ItemPriceDatabase ItemPriceDatabase => itemPriceDatabase;
        public RecipeDatabase RecipeDatabase => recipeDatabase;
        public ShopDatabase ShopDatabase => shopDatabase;
        public CustomerDatabase CustomerDatabase => customerDatabase;
        public BoardDatabase BoardDatabase => boardDatabase;
        public EventDatabase EventDatabase => eventDatabase;
        public ScriptDatabase ScriptDatabase => scriptDatabase;
        public TutorialDatabase TutorialDatabase => tutorialDatabase;
        public GameDatabase GameDatabase => gameDatabase;
        public SpecialEffectDatabase SpecialEffectDatabase => specialEffectDatabase;
        public StaffCatalog StaffCatalog => staffCatalog;
        public PlayerShopUpgradeCatalog PlayerShopUpgradeCatalog => playerShopUpgradeCatalog;
        public SpecialShopContentCatalog SpecialShopContentCatalog => specialShopContentCatalog;
        
        protected override void Awake()
        {
            base.Awake();
            LoadAllDatabases();
        }
        
        private void LoadAllDatabases()
        {
            if (itemDatabase == null)
                itemDatabase = Resources.Load<ItemDatabase>(Path.Combine(RESOURCES_SO_PATH, nameof(ItemDatabase)));

            if (itemTypeDatabase == null)
                itemTypeDatabase = Resources.Load<ItemTypeDatabase>(Path.Combine(RESOURCES_SO_PATH, nameof(ItemTypeDatabase)));

            if (itemPriceDatabase == null)
                itemPriceDatabase = Resources.Load<ItemPriceDatabase>(Path.Combine(RESOURCES_SO_PATH, nameof(ItemPriceDatabase)));
            
            if (recipeDatabase == null)
                recipeDatabase = Resources.Load<RecipeDatabase>(Path.Combine(RESOURCES_SO_PATH, nameof(RecipeDatabase)));
            
            if (shopDatabase == null)
                shopDatabase = Resources.Load<ShopDatabase>(Path.Combine(RESOURCES_SO_PATH, nameof(ShopDatabase)));
            
            if (customerDatabase == null)
                customerDatabase = Resources.Load<CustomerDatabase>(Path.Combine(RESOURCES_SO_PATH, nameof(CustomerDatabase)));

            if (boardDatabase == null)
                boardDatabase = Resources.Load<BoardDatabase>(Path.Combine(RESOURCES_SO_PATH, nameof(BoardDatabase)));

            if (eventDatabase == null)
                eventDatabase = Resources.Load<EventDatabase>(Path.Combine(RESOURCES_SO_PATH, nameof(EventDatabase)));
            
            if (scriptDatabase == null)
                scriptDatabase = Resources.Load<ScriptDatabase>(Path.Combine(RESOURCES_SO_PATH, nameof(ScriptDatabase)));

            if (tutorialDatabase == null)
                tutorialDatabase = Resources.Load<TutorialDatabase>(Path.Combine(RESOURCES_SO_PATH, nameof(TutorialDatabase)));
            
            if (gameDatabase == null)
                gameDatabase = Resources.Load<GameDatabase>(Path.Combine(RESOURCES_SO_PATH, nameof(GameDatabase)));
            
            if (specialEffectDatabase == null)
                specialEffectDatabase = Resources.Load<SpecialEffectDatabase>(Path.Combine(RESOURCES_SO_PATH, nameof(SpecialEffectDatabase)));

            if (staffCatalog == null)
                staffCatalog = Resources.Load<StaffCatalog>(Path.Combine(RESOURCES_SO_PATH, nameof(StaffCatalog)));

            if (playerShopUpgradeCatalog == null)
                playerShopUpgradeCatalog = Resources.Load<PlayerShopUpgradeCatalog>(Path.Combine(RESOURCES_SO_PATH, nameof(PlayerShopUpgradeCatalog)));

            if (specialShopContentCatalog == null)
                specialShopContentCatalog = Resources.Load<SpecialShopContentCatalog>(Path.Combine(RESOURCES_SO_PATH, nameof(SpecialShopContentCatalog)));

            ValidateDatabases();
            
            // UI Manager가 존재한다면 아이콘 미리 연결
            if (UI.UIManager.HasInstance)
                UI.UIManager.Instance.LinkIconsToDatabase();
        }
        
        private void ValidateDatabases()
        {
            if (itemDatabase == null)
                Debug.LogWarning($"{nameof(itemDatabase)}를 찾을 수 없습니다. {SO_FOLDER_PATH} 폴더에 {nameof(itemDatabase)}.asset이 있는지 확인하세요.");

            if (itemTypeDatabase == null)
                Debug.LogWarning($"{nameof(itemTypeDatabase)}를 찾을 수 없습니다. {SO_FOLDER_PATH} 폴더에 {nameof(itemTypeDatabase)}.asset이 있는지 확인하세요.");

            if (itemPriceDatabase == null)
                Debug.LogWarning($"{nameof(ItemPriceDatabase)}를 찾을 수 없습니다. {SO_FOLDER_PATH} 폴더에 {nameof(ItemPriceDatabase)}.asset이 있는지 확인하세요.");
            
            if (recipeDatabase == null)
                Debug.LogWarning($"{nameof(RecipeDatabase)}를 찾을 수 없습니다. {SO_FOLDER_PATH} 폴더에 {nameof(RecipeDatabase)}.asset이 있는지 확인하세요.");
            
            if (shopDatabase == null)
                Debug.LogWarning($"{nameof(ShopDatabase)}를 찾을 수 없습니다. {SO_FOLDER_PATH} 폴더에 {nameof(ShopDatabase)}.asset이 있는지 확인하세요.");
            
            if (customerDatabase == null)
                Debug.LogWarning($"{nameof(CustomerDatabase)}를 찾을 수 없습니다. {SO_FOLDER_PATH} 폴더에 {nameof(CustomerDatabase)}.asset이 있는지 확인하세요.");

            if (boardDatabase == null)
                Debug.LogWarning($"{nameof(BoardDatabase)}를 찾을 수 없습니다. {SO_FOLDER_PATH} 폴더에 {nameof(BoardDatabase)}.asset이 있는지 확인하세요.");

            if (eventDatabase == null)
                Debug.LogWarning($"{nameof(EventDatabase)}를 찾을 수 없습니다. {SO_FOLDER_PATH} 폴더에 {nameof(EventDatabase)}.asset이 있는지 확인하세요.");
            
            if (scriptDatabase == null)
                Debug.LogWarning($"{nameof(ScriptDatabase)}를 찾을 수 없습니다. {SO_FOLDER_PATH} 폴더에 {nameof(ScriptDatabase)}.asset이 있는지 확인하세요.");

            if (tutorialDatabase == null)
                Debug.LogWarning($"{nameof(TutorialDatabase)}를 찾을 수 없습니다. {SO_FOLDER_PATH} 폴더에 {nameof(TutorialDatabase)}.asset이 있는지 확인하세요.");
            
            if (gameDatabase == null)
                Debug.LogWarning($"{nameof(GameDatabase)}를 찾을 수 없습니다. {SO_FOLDER_PATH} 폴더에 {nameof(GameDatabase)}.asset이 있는지 확인하세요.");
            
            if (specialEffectDatabase == null)
                Debug.LogWarning($"{nameof(SpecialEffectDatabase)}를 찾을 수 없습니다. {SO_FOLDER_PATH} 폴더에 {nameof(SpecialEffectDatabase)}.asset이 있는지 확인하세요.");

            if (staffCatalog == null)
                Debug.LogWarning($"{nameof(StaffCatalog)}를 찾을 수 없습니다. {SO_FOLDER_PATH} 폴더에 {nameof(StaffCatalog)}.asset이 있는지 확인하세요.");

            if (playerShopUpgradeCatalog == null)
                Debug.LogWarning($"{nameof(PlayerShopUpgradeCatalog)}를 찾을 수 없습니다. {SO_FOLDER_PATH} 폴더에 {nameof(PlayerShopUpgradeCatalog)}.asset이 있는지 확인하세요.");

            if (specialShopContentCatalog == null)
                Debug.LogWarning($"{nameof(SpecialShopContentCatalog)}를 찾을 수 없습니다. {SO_FOLDER_PATH} 폴더에 {nameof(SpecialShopContentCatalog)}.asset이 있는지 확인하세요.");

        }
        
        #region Getters From Database
        public ItemData GetItem(int itemId)
        {
            return itemDatabase?.GetItem(itemId);
        }

        public ItemData GetItemByName(int itemName)
        {
            return itemDatabase?.GetItemByName(itemName);
        }

        public ItemData GetItemByItemPriceID(int itemPriceID)
        {
            return itemDatabase?.GetItemByItemPriceID(itemPriceID);
        }

        public ItemTypeData GetItemType(string itemType)
        {
            return itemTypeDatabase?.GetItemType(itemType);
        }

        public string GetItemTypeDisplayName(string itemType)
        {
            if (string.IsNullOrWhiteSpace(itemType))
                return string.Empty;

            return itemTypeDatabase?.GetItemTypeDisplayName(itemType) ?? itemType.Trim();
        }

        public string GetItemTypeIconName(string itemType)
        {
            if (string.IsNullOrWhiteSpace(itemType))
                return string.Empty;

            return itemTypeDatabase?.GetItemTypeIconName(itemType) ?? string.Empty;
        }
        
        /// <summary>
        /// 런타임 아이템 가격 조회 (RuntimeItemPriceManager 우선 사용)
        /// </summary>
        public int GetItemPrice(int itemPriceId, string town)
        {
            int price = RuntimeItemPriceManager.Instance.GetCurrentPrice(itemPriceId, town);

            if (price >= 0)
                return price;
            else
                return GetDefaultItemPrice(itemPriceId);
        }

        /// <summary>
        /// 아이템 기본 가격 조회
        /// </summary>
        public int GetDefaultItemPrice(int itemPriceId)
        {
            return itemDatabase?.GetItemOriginalPrice(itemPriceId) ?? 0;
        }

        /// <summary>
        /// 아이템 가격 히스토리 조회 (그래프 표시용)
        /// </summary>
        public List<int> GetItemPriceHistory(int itemId, string town)
        {
            return RuntimeItemPriceManager.Instance.GetPriceHistory(itemId, town) ?? new List<int>();
        }

        /// <summary>
        /// 아이템 가격 히스토리 기준 최솟값/최댓값 조회, 
        /// 유효 히스토리가 없으면 fallbackPrice를 반환
        /// </summary>
        public void GetItemPriceRange(int itemPriceId, string town, int fallbackPrice, out int minPrice, out int maxPrice)
        {
            minPrice = fallbackPrice;
            maxPrice = fallbackPrice;

            var priceHistory = GetItemPriceHistory(itemPriceId, town);
            if (priceHistory == null)
                return;

            for (int i = 0; i < priceHistory.Count; i++)
            {
                int historyPrice = priceHistory[i];
                if (historyPrice <= 0)
                    continue;

                if (historyPrice < minPrice)
                    minPrice = historyPrice;
                if (historyPrice > maxPrice)
                    maxPrice = historyPrice;
            }
        }
        
        public string GetScriptTextKR(int scriptId)
        {
            return scriptDatabase?.GetScriptKR(scriptId) ?? "";
        }

        public bool TryGetTutorialDialogues(string tutorialId, out IReadOnlyList<TutorialData> dialogues)
        {
            dialogues = null;
            return tutorialDatabase != null
                && tutorialDatabase.TryGetDialogues(tutorialId, out dialogues);
        }
        
        public CustomerData GetCustomer(int customerId)
        {
            return customerDatabase?.GetCustomer(customerId);
        }

        public List<BoardData> GetBoardsByTown(string town)
        {
            return boardDatabase?.GetBoardsByTown(town) ?? new List<BoardData>();
        }

        public List<BoardData> GetStartBoardsByTown(string town)
        {
            return boardDatabase?.GetStartBoardsByTown(town) ?? new List<BoardData>();
        }

        public T GetGameVariable<T>(string variableName)
        {
            return gameDatabase != null ? gameDatabase.GetVariableValue<T>(variableName) : default(T);
        }
        #endregion
        
        public bool IsDataLoaded()
        {
            return itemDatabase != null && 
                   itemTypeDatabase != null &&
                   itemPriceDatabase != null &&
                   recipeDatabase != null && 
                   shopDatabase != null && 
                   customerDatabase != null && 
                   boardDatabase != null &&
                   eventDatabase != null && 
                   scriptDatabase != null && 
                   tutorialDatabase != null &&
                   gameDatabase != null &&
                   specialEffectDatabase != null &&
                   staffCatalog != null &&
                   playerShopUpgradeCatalog != null &&
                   specialShopContentCatalog != null;
        }
        
        #region Debug
        [ContextMenu("Reload All Databases")]
        public void ReloadAllDatabases()
        {
            LoadAllDatabases();
            Debug.Log("모든 데이터베이스를 다시 로드했습니다.");
        }

        [ContextMenu("Print Database Info")]
        public void PrintDatabaseInfo()
        {
            Debug.Log($"=== 데이터베이스 정보 ===");
            Debug.Log($"Items: {itemDatabase?.Items.Count ?? 0}개");
            Debug.Log($"Item Types: {itemTypeDatabase?.ItemTypes.Count ?? 0}개");
            Debug.Log($"Item Prices: {itemPriceDatabase?.DefaultItemPriceDict.Count ?? 0}개");
            Debug.Log($"Recipes: {recipeDatabase?.Recipes.Count ?? 0}개");
            Debug.Log($"Shops: {shopDatabase?.ShopDict.Count ?? 0}개");
            Debug.Log($"Customers: {customerDatabase?.Customers.Count ?? 0}개");
            Debug.Log($"Boards: {boardDatabase?.Boards.Count ?? 0}개");
            Debug.Log($"Events: {eventDatabase?.Events.Count ?? 0}개");
            Debug.Log($"Scripts: {scriptDatabase?.Scripts.Count ?? 0}개");
            Debug.Log($"Tutorials: {tutorialDatabase?.Tutorials.Count ?? 0}개");
            Debug.Log($"Customer Spawns: {gameDatabase?.CustomerSpawns.Count ?? 0}개");
            Debug.Log($"Customer Dialogues: {gameDatabase?.CustomerDialogues.Count ?? 0}개");
            Debug.Log($"Variables: {gameDatabase?.Variables.Count ?? 0}개");
        }
        #endregion
    }
}
