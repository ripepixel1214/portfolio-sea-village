using System;

namespace SeaVillage.Core
{
    public sealed class TutorialControlScope : IDisposable
    {
        private bool _disposed;

        public void BlockAllInteractions()
        {
            ThrowIfDisposed();
            TutorialInteractionPolicy.BlockAll();
        }

        public void RestrictTo(InteractionType interactionType)
        {
            ThrowIfDisposed();
            TutorialInteractionPolicy.RestrictTo(interactionType);
        }

        public void SetCommandInputBlocked(bool isBlocked)
        {
            ThrowIfDisposed();
            TutorialCommandInputPolicy.SetBlocked(isBlocked);
        }

        public void ClearRestrictions()
        {
            ThrowIfDisposed();
            TutorialInteractionPolicy.Clear();
            TutorialCommandInputPolicy.SetBlocked(false);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            TutorialInteractionPolicy.Clear();
            TutorialCommandInputPolicy.SetBlocked(false);
            _disposed = true;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(TutorialControlScope));
        }
    }

    /// <summary>
    /// 튜토리얼 중 허용할 월드 상호작용 범위를 관리
    /// </summary>
    public static class TutorialInteractionPolicy
    {
        private static bool _isRestricted;
        private static InteractionType? _allowedType;

        #region Properties

        public static bool IsRestricted => _isRestricted;

        #endregion

        #region Public API

        public static void BlockAll()
        {
            _isRestricted = true;
            _allowedType = null;
        }

        public static void RestrictTo(InteractionType interactionType)
        {
            _isRestricted = true;
            _allowedType = interactionType;
        }

        public static bool IsAllowed(IInteractable interactable)
        {
            if (!_isRestricted)
                return true;

            return interactable != null
                && _allowedType.HasValue
                && interactable.InteractionType == _allowedType.Value;
        }

        public static void Clear()
        {
            _isRestricted = false;
            _allowedType = null;
        }

        #endregion
    }

    /// <summary>
    /// 튜토리얼 중 플레이어 명령 입력 차단 상태를 관리
    /// </summary>
    public static class TutorialCommandInputPolicy
    {
        private static bool _isBlocked;

        #region Properties

        public static bool IsBlocked => _isBlocked;

        #endregion

        #region Public API

        public static void SetBlocked(bool isBlocked)
        {
            _isBlocked = isBlocked;
        }

        #endregion
    }
}
