using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Pool;

namespace SeaVillage.UI.NPC
{
    public class UISpeechBubbleController : MonoBehaviour
    {
        [SerializeField] private float _typingSpeed = 0.05f;
        [SerializeField] private float _displayDuration = 2f;

        private UISpeechBubbleView _view;
        private UISpeechBubbleFollower _follower;
        private IObjectPool<UISpeechBubbleController> _pool;
        private Transform _boundTarget;

        private Coroutine _typingCoroutine;
        private Coroutine _autoHideCoroutine;
        private Coroutine _progressCoroutine;

        private WaitForSeconds _typingWait;
        private WaitForSecondsRealtime _typingRealtimeWait;
        private WaitForSeconds _displayWait;
        private string _activeText = string.Empty;
        private Action _typingCompleted;
        private bool _autoHideAfterTyping;
        private bool _useUnscaledTyping;

        #region Properties

        public bool IsTyping => _typingCoroutine != null;

        #endregion

        #region MonoBehaviour

        private void Awake()
        {
            Init();
        }

        private void Init()
        {
            _view = this.TryGetAndChecked<UISpeechBubbleView>();
            _follower = this.TryGetAndChecked<UISpeechBubbleFollower>();

            _typingWait = new WaitForSeconds(_typingSpeed);
            _typingRealtimeWait = new WaitForSecondsRealtime(_typingSpeed);
            _displayWait = new WaitForSeconds(_displayDuration);
        }

        #endregion

        #region Public API

        public void SetPool(IObjectPool<UISpeechBubbleController> pool)
        {
            _pool = pool;
        }

        public bool TryValidateConfiguration(out string failReason)
        {
            UISpeechBubbleView view = GetComponent<UISpeechBubbleView>();
            if (view == null)
            {
                failReason = "UISpeechBubbleView가 없습니다";
                return false;
            }

            if (!view.TryValidateTextConfiguration(out failReason))
                return false;

            if (GetComponent<UISpeechBubbleFollower>() == null)
            {
                failReason = "UISpeechBubbleFollower가 없습니다";
                return false;
            }

            if (GetComponent<RectTransform>() == null)
            {
                failReason = "RectTransform이 없습니다";
                return false;
            }

            failReason = string.Empty;
            return true;
        }

        public bool IsBoundTo(Transform target)
        {
            return target != null && _boundTarget == target;
        }

        public void PlayText(Transform target, string text, Vector2? positionOffset = null)
        {
            BeginText(target, text, true, false, null, positionOffset);
        }

        public void PlayPersistentText(
            Transform target,
            string text,
            Action onTypingCompleted,
            Vector2? positionOffset = null)
        {
            BeginText(target, text, false, true, onTypingCompleted, positionOffset);
        }

        public bool CompleteTyping()
        {
            if (_typingCoroutine == null)
                return false;

            StopCoroutine(_typingCoroutine);
            _typingCoroutine = null;
            CompleteTextTyping();
            return true;
        }

        public void PlayThinking(Transform target, Vector2? positionOffset = null)
        {
            Begin(target, positionOffset);
            _view.SetText("...");
            _view.ShowText();
        }

        public void PlayProgress(
            Transform target,
            float duration,
            Vector2? positionOffset = null)
        {
            Begin(target, positionOffset);
            _view.SetProgress(0f);
            _view.ShowProgress();
            _progressCoroutine = StartCoroutine(ProgressRoutine(duration));
        }

        public void PlayCost(
            Transform target,
            long amount,
            Vector2? positionOffset = null)
        {
            Begin(target, positionOffset);
            _view.SetCost(amount);
            _view.ShowCost();
            _autoHideCoroutine = StartCoroutine(AutoHideRoutine());
        }

        public void PlayEmotion(
            Transform target,
            Sprite sprite,
            float duration,
            Vector2? positionOffset = null)
        {
            if (sprite == null)
            {
                Stop();
                return;
            }

            Begin(target, positionOffset);
            _view.SetEmotion(sprite);
            _view.ShowEmotion();
            _autoHideCoroutine = StartCoroutine(AutoHideRoutine(duration));
        }

        public void Stop()
        {
            StopCurrentCoroutines();
            Release();
        }

        #endregion

        #region Private Helpers

        private void BeginText(
            Transform target,
            string text,
            bool autoHideAfterTyping,
            bool useUnscaledTyping,
            Action onTypingCompleted,
            Vector2? positionOffset)
        {
            Begin(target, positionOffset);
            _activeText = text ?? string.Empty;
            _autoHideAfterTyping = autoHideAfterTyping;
            _useUnscaledTyping = useUnscaledTyping;
            _typingCompleted = onTypingCompleted;
            _view.ShowText();
            _typingCoroutine = StartCoroutine(TypingRoutine());
        }

        private void Begin(Transform target, Vector2? positionOffset)
        {
            _boundTarget = target;
            if (positionOffset.HasValue)
            {
                _follower.Bind(target, positionOffset.Value);
            }
            else
            {
                _follower.Bind(target);
            }
            StopCurrentCoroutines();
            _view.Clear();
        }

        private IEnumerator TypingRoutine()
        {
            var buffer = new StringBuilder();

            foreach (char letter in _activeText)
            {
                buffer.Append(letter);
                _view.SetText(buffer.ToString());
                yield return _useUnscaledTyping ? _typingRealtimeWait : _typingWait;
            }

            _typingCoroutine = null;
            CompleteTextTyping();
        }

        private void CompleteTextTyping()
        {
            _view.SetText(_activeText);

            Action completed = _typingCompleted;
            _typingCompleted = null;
            completed?.Invoke();

            if (_autoHideAfterTyping)
                _autoHideCoroutine = StartCoroutine(AutoHideRoutine());
        }

        private IEnumerator ProgressRoutine(float duration)
        {
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                _view.SetProgress(elapsed / duration);
                yield return null;
            }

            _view.SetProgress(1f);
            _progressCoroutine = null;
        }

        private IEnumerator AutoHideRoutine()
        {
            yield return _displayWait;

            _autoHideCoroutine = null;
            Release();
        }

        private IEnumerator AutoHideRoutine(float duration)
        {
            yield return new WaitForSeconds(duration);

            _autoHideCoroutine = null;
            Release();
        }

        private void StopCurrentCoroutines()
        {
            if (_typingCoroutine != null)
            {
                StopCoroutine(_typingCoroutine);
                _typingCoroutine = null;
            }

            if (_autoHideCoroutine != null)
            {
                StopCoroutine(_autoHideCoroutine);
                _autoHideCoroutine = null;
            }

            if (_progressCoroutine != null)
            {
                StopCoroutine(_progressCoroutine);
                _progressCoroutine = null;
            }

            _activeText = string.Empty;
            _typingCompleted = null;
            _autoHideAfterTyping = false;
            _useUnscaledTyping = false;
        }

        private void Release()
        {
            _boundTarget = null;
            _follower.Unbind();
            _view.Clear();

            _pool.Release(this);
        }

        #endregion
    }
}
