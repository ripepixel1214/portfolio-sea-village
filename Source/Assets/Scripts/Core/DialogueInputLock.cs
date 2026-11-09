using System;

namespace SeaVillage.Core
{
    /// <summary>대사 진행 중 지정된 대상 외의 게임 플레이 입력을 차단</summary>
    public static class DialogueInputLock
    {
        private static IInteractable _allowedInteraction;
        private static Scope _activeScope;

        public static bool IsActive => _activeScope != null;

        public static bool TryAcquire(IInteractable allowedInteraction, out IDisposable scope)
        {
            scope = null;
            if (allowedInteraction == null)
                return false;
            if (IsActive)
                return false;

            _allowedInteraction = allowedInteraction;
            _activeScope = new Scope();
            scope = _activeScope;
            return true;
        }

        public static bool IsInteractionAllowed(IInteractable interactable)
        {
            return !IsActive || ReferenceEquals(_allowedInteraction, interactable);
        }

        private static void Release(Scope scope)
        {
            if (!ReferenceEquals(_activeScope, scope))
                return;

            _activeScope = null;
            _allowedInteraction = null;
        }

        private sealed class Scope : IDisposable
        {
            private bool _disposed;

            public void Dispose()
            {
                if (_disposed)
                    return;

                Release(this);
                _disposed = true;
            }
        }
    }
}
