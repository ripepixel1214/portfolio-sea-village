namespace SeaVillage.Core
{
    /// <summary>마을 식별자</summary>
    public enum TownKey
    {
        Unknown = 0,
        Start = 1,
        Forest = 2,
        Mine = 3,
        Sea = 4,
        // 5는 제거된 마을의 직렬화 값과 충돌하지 않도록 재사용하지 않는다
        Dessert = 6,
        Cave = 7,
    }
}
