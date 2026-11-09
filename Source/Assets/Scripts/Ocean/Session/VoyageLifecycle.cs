using System;

namespace SeaVillage.Ocean
{
    public enum VoyagePhase
    {
        Preparing,
        Sailing,
        Ending
    }

    /// <summary>씬 시동부터 귀항까지 배와 항해 이벤트가 공유하는 수명주기.</summary>
    public sealed class VoyageLifecycle
    {
        public VoyagePhase Phase { get; private set; } = VoyagePhase.Preparing;
        public bool IsFoodDepleted { get; private set; }
        public bool CanControl => Phase == VoyagePhase.Sailing && !IsFoodDepleted;
        public event Action Changed;

        public void StartSailing()
        {
            if (Phase != VoyagePhase.Preparing)
                return;

            Phase = VoyagePhase.Sailing;
            Changed?.Invoke();
        }

        public void End()
        {
            if (Phase == VoyagePhase.Ending)
                return;

            Phase = VoyagePhase.Ending;
            Changed?.Invoke();
        }

        public void ObserveFood(float food)
        {
            if (Phase == VoyagePhase.Ending || IsFoodDepleted == (food <= 0f))
                return;

            IsFoodDepleted = food <= 0f;
            Changed?.Invoke();
        }

        /// <summary>진행 중인 이벤트와 후속 UI 처리가 끝난 뒤 귀항을 한 번만 시작한다.</summary>
        public bool TryBeginFoodReturn(bool isBusy)
        {
            if (Phase != VoyagePhase.Sailing || !IsFoodDepleted || isBusy)
                return false;

            End();
            return true;
        }
    }
}
