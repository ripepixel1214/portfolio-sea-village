/// <summary>
/// 상호작용 시스템이 외부 라우팅, 프롬프트, 하이라이트 기준으로 사용하는 대분류입니다.
/// 특수 상점의 건물 정체성은 SpecialShopType으로 구분합니다.
/// </summary>
public enum InteractionType
{
    None = 0,
    Ship = 1,           // 배 계열 상호작용
    BulletinBoard = 2,  // 게시판 계열 상호작용
    Shop = 3,           // 일반 상점 계열 상호작용
    SpecialShop = 4,    // 특수 상점 계열 상호작용
    PlayerShopLot = 5,  // 내 상점 부지 계열 상호작용
    PlayerShop = 6,     // 내 상점 계열 상호작용
    Pet = 7,            // 펫 계열 상호작용
    Quest = 8,          // 퀘스트 계열 상호작용
    Dialogue = 9,       // 대화 계열 상호작용
}
