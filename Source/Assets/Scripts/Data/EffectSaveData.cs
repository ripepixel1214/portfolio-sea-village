using MemoryPack;

namespace SeaVillage.Data
{
    /// <summary>
    /// 활성 특수 효과 저장 모델.
    /// 영향 아이템과 가격 배율은 SpecialEffectDatabase에서 결정적으로 재계산되므로 원본만 보관한다.
    /// </summary>
    [MemoryPackable]
    public partial class ActiveSpecialEffectSaveData
    {
        public int effectId = 0;
        public int remainingDays = 0;

        public ActiveSpecialEffectSaveData() { }
    }

    /// <summary>
    /// 카테고리별 누적 일반 효과 저장 모델.
    /// 아이템별 배율은 ItemDatabase에서 재계산되므로 카테고리 단위 원본만 보관한다.
    /// </summary>
    [MemoryPackable]
    public partial class NormalEffectSaveData
    {
        public string category = "";
        public float accumulatedOffset = 0f; // 기본가 대비 누적 퍼센트포인트 (0.4 = +40%)
        public float todayDelta = 0f;        // 당일 변동폭, 뉴스 표시용 (0 = 당일 미변동)

        public NormalEffectSaveData() { }
    }
}
