using System.Collections.Generic;
using System.Linq;
using SeaVillage.Data;

namespace SeaVillage.Event
{
    /// <summary>
    /// 런타임에서 이벤트 실행 컨텍스트를 관리하기 위한 클래스
    /// </summary>
    public sealed class EventExecutionContext
    {
        private readonly Dictionary<int, List<EventSequenceData>> _steps;

        public string RuntimeId { get; set; }

        public int CurrentStep { get; set; }
        public int NextStep { get; private set; }
        
        public EventData EventData { get; }
        public IReadOnlyList<EventSequenceData> Sequences { get; }
        public EventState State { get; set; }

        public EventExecutionContext(EventData data, List<EventSequenceData> sequences)
        {
            EventData = data;
            Sequences = sequences;
            _steps = sequences
                .GroupBy(x => x.Step)
                .ToDictionary(g => g.Key, g => g.ToList());
            State = EventState.Ready;
            NextStep = -1;
        }

        public List<EventSequenceData> GetSequencesByStep(int step)
        {
            return _steps.TryGetValue(step, out var list) ? list : null;
        }

        public void SetNextStep(int step)
        {
            NextStep = step;
        }
    }
}