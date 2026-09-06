using UnityEngine;

namespace SeaVillage.Status
{
    public sealed class NPCStatus : Status
    {
        [Header("기본 스탯")]
        [SerializeField] private int _initialIntelligence = 5;
        [SerializeField] private int _initialCharm = 5;
        [SerializeField] private int _initialLoyalty = 5;

        [Header("행동 스탯")]
        [SerializeField] private int _initialSpeed = 5;
        [SerializeField] private int _initialBehaviourCount = 3;

        [Header("경제 스탯")]
        [SerializeField] private int _initialSatisfaction = 100;
        [SerializeField] private int _initialPurchaseRate = 30;

        [Header("스탯 효과 배율")]
        [SerializeField] private float _intelligenceMultiplier = 0.1f;
        [SerializeField] private float _charmMultiplier = 0.05f;
        [SerializeField] private float _loyaltyMultiplier = 10f;

        public float MoveSpeed => GetStatValue(StatusType.Speed);

        private void Awake()
        {
            InitializeStatus();
        }

        protected override void InitializeStatus()
        {
            AddDefaultStats();
        }

        protected override void AddDefaultStats()
        {
            AddStat(StatusType.Intelligence, _initialIntelligence, _intelligenceMultiplier);
            AddStat(StatusType.Charm, _initialCharm, _charmMultiplier);
            AddStat(StatusType.Loyalty, _initialLoyalty, _loyaltyMultiplier);
            AddStat(StatusType.Speed, _initialSpeed);
            AddStat(StatusType.BehaviourCount, _initialBehaviourCount);
            AddStat(StatusType.Satisfaction, _initialSatisfaction);
            AddStat(StatusType.PurchaseRate, _initialPurchaseRate);
        }
    }
}
