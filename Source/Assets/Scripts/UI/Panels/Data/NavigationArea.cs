namespace SeaVillage.UI
{
    /// <summary>
    /// 슬롯에서 네비게이션 가능한 영역의 타입을 정의
    /// </summary>
    public enum NavigationArea
    {
        // Common Panels
        CloseButton,
        CancelButton,

        // Main Save Slot Panel
        SaveSlot,
        DeleteButton,

        // PurchasePanel
        PurchaseButton,
        ShopGrid,
        CartGrid,
        
        // SellPanel
        SellButton,
        SellListGrid,
        
        // PlayerInventoryPanel, ShipInventoryPanel, SellPanel
        PlayerInventory,

        // PlayerInventoryPanel
        PlayerInventoryFilter,

        // ShipInventoryPanel
        ShipInventory,

        // SubmissionPanel
        SubmissionInventory,

        // SubmissionPanel
        SubmitButton,

        // BulletinBoardPanel
        QuestAcceptButton,

        // PlayerShopBuildPanel
        PlayerShopBuildInventory,
        PlayerShopConfirmButton,

        // PlayerShopStandPanel
        PlayerShopStandGrid,
    }
}
