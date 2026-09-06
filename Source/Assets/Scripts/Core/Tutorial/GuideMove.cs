using System.Collections;
using Spine.Unity;
using UnityEngine;

namespace SeaVillage.Core
{
    /// <summary>
    /// 튜토리얼 안내자의 이동과 Spine 애니메이션만 담당
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class GuideMove : MonoBehaviour
    {
        [SerializeField] private Transform visual;
        [SerializeField] private float arrivalThreshold = 0.1f;
        [SerializeField] private string idleAnimationName = "Player_Idle";
        [SerializeField] private string walkAnimationName = "Player_Walk";

        private Rigidbody2D _body;
        private SkeletonAnimation _skeleton;
        private Coroutine _moveRoutine;

        #region MonoBehaviour

        private void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
            _skeleton = visual != null
                ? visual.GetComponentInChildren<SkeletonAnimation>(true)
                : GetComponentInChildren<SkeletonAnimation>(true);
            PlayAnimation(idleAnimationName);
        }

        private void OnDisable()
        {
            StopMovement();
        }

        #endregion

        #region Public API

        public void Move(Vector2 target, float speed)
        {
            StopMovement();
            if (speed <= 0f)
            {
                Debug.LogWarning($"[GuideMove] {name}: 이동 속도가 0 이하라 이동을 시작하지 않습니다: {speed}");
                return;
            }

            _moveRoutine = StartCoroutine(MoveRoutine(target, speed));
        }

        public void StopMovement()
        {
            if (_moveRoutine != null)
            {
                StopCoroutine(_moveRoutine);
                _moveRoutine = null;
            }

            if (_body != null)
                _body.linearVelocity = Vector2.zero;

            PlayAnimation(idleAnimationName);
        }

        public void LookAt(Vector2 target)
        {
            if (visual == null)
                return;

            Vector3 scale = visual.localScale;
            scale.x = target.x > transform.position.x
                ? -Mathf.Abs(scale.x)
                : Mathf.Abs(scale.x);
            visual.localScale = scale;
        }

        public bool TryValidateConfiguration(out string failReason)
        {
            failReason = string.Empty;
            SkeletonAnimation skeleton = visual != null
                ? visual.GetComponentInChildren<SkeletonAnimation>(true)
                : GetComponentInChildren<SkeletonAnimation>(true);
            if (skeleton == null || skeleton.SkeletonDataAsset == null)
            {
                failReason = "SkeletonAnimation 또는 SkeletonDataAsset이 없습니다";
                return false;
            }

            var skeletonData = skeleton.SkeletonDataAsset.GetSkeletonData(true);
            if (skeletonData == null)
            {
                failReason = "SkeletonData를 로드할 수 없습니다";
                return false;
            }

            if (skeletonData.FindAnimation(idleAnimationName) == null)
            {
                failReason = $"대기 애니메이션이 없습니다: {idleAnimationName}";
                return false;
            }

            if (skeletonData.FindAnimation(walkAnimationName) == null)
            {
                failReason = $"이동 애니메이션이 없습니다: {walkAnimationName}";
                return false;
            }

            return true;
        }

        #endregion

        #region Private Helpers

        private IEnumerator MoveRoutine(Vector2 target, float speed)
        {
            PlayAnimation(walkAnimationName);
            float targetX = target.x;
            while (Mathf.Abs(transform.position.x - targetX) > arrivalThreshold)
            {
                float currentX = transform.position.x;
                float nextX = Mathf.MoveTowards(currentX, targetX, speed * Time.deltaTime);
                _body.MovePosition(new Vector2(nextX, transform.position.y));
                LookAt(new Vector2(nextX, transform.position.y));
                yield return null;
            }

            _body.linearVelocity = Vector2.zero;
            _moveRoutine = null;
            PlayAnimation(idleAnimationName);
        }

        private void PlayAnimation(string animationName)
        {
            if (_skeleton == null || _skeleton.SkeletonDataAsset == null)
                return;

            var skeletonData = _skeleton.SkeletonDataAsset.GetSkeletonData(true);
            var animation = skeletonData?.FindAnimation(animationName);
            if (animation == null || _skeleton.AnimationName == animationName)
                return;

            _skeleton.AnimationState.SetAnimation(0, animation, true);
        }

        #endregion
    }
}
