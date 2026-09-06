/// <summary>
/// 특수 상점의 건물 정체성을 나타냅니다.
/// 상호작용 시 이 타입에 맞는 전용 패널을 엽니다.
/// </summary>
public enum SpecialShopType
{
    // 시작 마을
    Restaurant = 0,     // 식당
    TownOffice = 1,     // 시청

    // 숲속 마을
    AcornWorkshop = 2,  // 도토리 공방

    // 바위 마을
    RockExchange = 4,   // 바위 교환소
    Forge = 5,          // 대장간

    // 바다 마을
    Stage = 6,          // 무대
    PotionShop = 7,     // 포션 상점

    // 과자 마을
    TailorHouse = 8,    // 양장점

    // 동굴 마을
    Devil = 9,          // 악마 상점
}
