using System;
using System.Collections.Generic;
using MemoryPack;

namespace SeaVillage.Data
{
    /// <summary>
    /// 아이템 데이터
    /// </summary>
    [Serializable, MemoryPackable]
    public partial class ItemData
    {
        public int ID;
        public int Name;
        public int Description;
        public string Town;
        public List<string> Type;
        public float Weight;
        public int OriginPrice;
        public int PriceListID;
        public bool Unsellable;
        public int Recipe;
        public string Usage;
        public int Value;
        public string Image;

        // 런타임 아이콘 캐싱용. 저장/로드 대상에서 제외
        [MemoryPackIgnore]
        public UnityEngine.Sprite Icon;
    }

    /// <summary>
    /// 아이템 타입 메타 데이터
    /// </summary>
    [Serializable, MemoryPackable]
    public partial class ItemTypeData
    {
        public string ItemType;
        public string Name;
        public string Icon;
    }

    /// <summary>
    /// 아이템 시세 데이터
    /// 순환 버퍼를 사용하여 최근 N일간의 가격 히스토리를 저장
    /// </summary>
    [Serializable, MemoryPackable]
    public partial class ItemPriceData
    {
        public int ID;
        public string Town;
        public float Distance;
        public float Preference;

        // 히스토리 설정
        [MemoryPackIgnore]
        public const int MaxHistoryLength = 30;

        // 순환 버퍼 인덱스 (MemoryPack 직렬화 포함)
        public int HistoryHead = 0;
        public int HistoryTail = 0;
        public int HistoryCount = 0;

        // 가격 히스토리 배열
        public int[] PriceHistory = new int[MaxHistoryLength];

        /// <summary>
        /// 새로운 가격을 히스토리에 추가 (순환 버퍼 방식)
        /// </summary>
        public void AddPriceToHistory(int newPrice)
        {
            PriceHistory[HistoryHead] = newPrice;
            HistoryHead = (HistoryHead + 1) % MaxHistoryLength;

            if (HistoryCount < MaxHistoryLength)
                HistoryCount++;
            else
                // 버퍼가 꽉 찼으면 가장 오래된 데이터 위치 이동
                HistoryTail = (HistoryTail + 1) % MaxHistoryLength;
        }

        /// <summary>
        /// 모든 가격 히스토리 반환 (오래된 순서 => 최신 순서)
        /// </summary>
        public List<int> GetAllPriceHistory()
        {
            List<int> history = new List<int>(HistoryCount);

            for (int i = 0; i < HistoryCount; i++)
            {
                int index = (HistoryTail + i) % MaxHistoryLength;
                history.Add(PriceHistory[index]);
            }

            return history;
        }

        /// <summary>
        /// 최근 (days)일간의 가격 히스토리 반환
        /// </summary>
        public List<int> GetRecentPriceHistory(int days)
        {
            var allHistory = GetAllPriceHistory();
            int startIndex = Math.Max(0, allHistory.Count - days);
            return allHistory.GetRange(startIndex, allHistory.Count - startIndex);
        }

        /// <summary>
        /// 히스토리의 가장 최근 가격 조회
        /// </summary>
        public int GetLatestPrice()
        {
            if (HistoryCount == 0) return 0;
            int latestIndex = (HistoryHead - 1 + MaxHistoryLength) % MaxHistoryLength;
            return PriceHistory[latestIndex];
        }

        /// <summary>
        /// 가격 변동률 계산 (전일 대비, %)
        /// </summary>
        public float GetPriceChangeRate()
        {
            if (HistoryCount < 2) return 0f;

            int previousIndex = (HistoryHead - 2 + MaxHistoryLength) % MaxHistoryLength;
            int currentIndex = (HistoryHead - 1 + MaxHistoryLength) % MaxHistoryLength;
            int previousPrice = PriceHistory[previousIndex];
            int currentPrice = PriceHistory[currentIndex];

            if (previousPrice == 0) return 0f;
            return (float)(currentPrice - previousPrice) / previousPrice * 100f;
        }

        /// <summary>
        /// 가격 변동률 계산 (원본 가격 대비, %)
        /// </summary>
        public float GetPriceChangeRateFromOrigin(int originPrice)
        {
            if (originPrice == 0) return 0f;

            int latestPrice = GetLatestPrice();
            return (float)(latestPrice - originPrice) / originPrice * 100f;
        }

        /// <summary>
        /// 히스토리 초기화
        /// </summary>
        public void ClearHistory()
        {
            HistoryHead = 0;
            HistoryTail = 0;
            HistoryCount = 0;
            Array.Clear(PriceHistory, 0, PriceHistory.Length);
        }
    }

    /// <summary>
    /// 레시피 데이터
    /// </summary>
    [Serializable]
    public class RecipeData
    {
        public int ID;
        public int Stuff;
        public int Count;
    }

    /// <summary>
    /// 상점 데이터
    /// </summary>
    [Serializable, MemoryPackable]
    public partial class ShopData
    {
        public int ID;
        public int ItemID;
        public int Count;
        public int Condition;
    }

    /// <summary>
    /// 손님 데이터
    /// </summary>
    [Serializable]
    public class CustomerData
    {
        public int ID;
        public string Town;
        public int Money;
        public string ConsumptionType;
        public string Like;
        public int Favorite;
        public string Image;
    }

    /// <summary>
    /// 손님 스폰 데이터
    /// </summary>
    [Serializable]
    public class CustomerSpawnData
    {
        public int ID;
        public string Town;
        public int LoveLv;
        public string CustomerID;
        public float SpawnProbability;
    }

    /// <summary>
    /// 손님 대사 데이터
    /// </summary>
    [Serializable]
    public class CustomerDialogueData
    {
        public int ID;
        public string TownEffect;
        public string Town;
        public string Type;
        public string Condition0;
        public string Condition1;

        // TODO: ScriptID에 직접 대사 삽입으로 변경, 따라서 추후 변수명 변경 필요
        public string ScriptID;
    }

    /// <summary>
    /// 게시판 퀘스트 데이터 <br/>
    /// 코드 내에서는 quest라는 용어와 유사한 맥락으로 사용한다. 
    /// </summary>
    [Serializable]
    public class BoardData
    {
        public string Town;
        public bool StartQuest;
        public int ItemID;
        public string Description;
        public int Reward;
    }

    /// <summary>
    /// 특수효과 데이터
    /// </summary>
    [Serializable]
    public class SpecialEffectData
    {
        public int ID;
        public string Name; // 특수효과 이름
        public string Town; // 적용 마을
        public int ItemChange; // 가격이 변동되는 아이템 리스트, SpecialEffectItemChangeData 테이블을 참조
        public bool News; // "신문에 특수 소식으로 나올 수 있는 특수 효과인지 여부, False : 신문에 등장 X"
        public string Description; // 특수 효과 설명
        public string Icon; // 특수 효과 아이콘
    }

    /// <summary>
    /// 특수효과 데이터
    /// </summary>
    [Serializable]
    public class SpecialEffectItemChangeData
    {
        public int ID;
        public int ItemID;
        public int ItemChange;
    }

    ///<summary>
    /// 이벤트 데이터
    /// </summary>
    [Serializable]
    public class EventData
    {
        public string EventID;
        public string Name;
        public string SceneName;
        // DailyRandom: 항해씬 돌발 이벤트
        // OnEnterMap: 맵 진입 이벤트
        public string TriggerType;
        public bool Repeatable;
    }

    ///<summary>
    /// 이벤트 조건 데이터
    /// </summary>
    [Serializable]
    public class EventConditionData
    {
        public string EventID;

        // LoveLv: 특정 마을 호감도 조건
        // Favorite: 특정 아이템 선호도 조건(Customer ID 값, 이 값으로 Customer 테이블의 Like 필드 참조)
        // AfterDay: 특정 일수 이후 조건
        // Quest: 특정 퀘스트 완료 조건
        // Percent: 단순 확률 조건 (0 ~ 100%)
        public string ConditionType;
        public int Value;
        public string Variable;
    }

    /// <summary>
    /// 이벤트 시퀀스 데이터
    /// </summary>
    [Serializable]
    public class EventSequenceData
    {
        public string EventID;
        public int Step;
        public string Command;
        public string Target;
        public string Value;
        public int NextStep;
        public int FailStep;
    }

    ///<summary>
    /// 이벤트 대화 데이터
    /// </summary>
    [Serializable]
    public class EventDialogueData
    {
        public int ID;
        public int Speaker;
        public string Script;
        public string Emotion;
    }

    /// <summary>
    /// 스크립트 데이터
    /// </summary>
    [Serializable]
    public class ScriptData
    {
        public int ID;
        public string KR;
    }

    /// <summary>
    /// 튜토리얼 대사 출력 방식
    /// </summary>
    public enum TutorialDialogueType
    {
        Stop,
        Auto,
        Box
    }

    /// <summary>
    /// 튜토리얼 대사 데이터
    /// </summary>
    [Serializable]
    public class TutorialData
    {
        public string ID = string.Empty;
        public TutorialDialogueType Type;
        public string Script = string.Empty;
        public int Sequence;
    }

    /// <summary>
    /// 게임 내에서 빈번하게 사용될 변수 데이터
    /// </summary>
    [Serializable]
    public class VariableData
    {
        public string Variable;
        public string Type;
        public string Value;
        public string Description;
    }

    /// <summary>
    /// 열거형 데이터
    /// </summary>
    [Serializable]
    public class EnumData
    {
        public string EnumName;
        public string Description;
        public string Value;
    }

    #region Utilities
    [Serializable]
    public class CustomerConsumption
    {
        public enum ConsumptionType { Poor, Common, Rich }
        public ConsumptionType type;
    }
    #endregion
}
