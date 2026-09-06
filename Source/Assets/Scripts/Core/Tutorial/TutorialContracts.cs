using System;
using System.Collections.Generic;
using SeaVillage.Data;
using UnityEngine;

namespace SeaVillage.Core
{
    #region Enums

    public enum TutorialPlaybackState
    {
        Inactive,                   // 튜토리얼이 비활성 상태
        Presenting,                 // 튜토리얼 대사가 화면에 표시 중
        WaitingForInput,            // 사용자 입력 대기 중
        WaitingForAutoAdvance,      // 자동 진행 대기 중
        WaitingForTrigger,          // 대사 진행 이벤트 대기 중
        WaitingForActivation,       // 단계 활성화 이벤트 대기 중
        WaitingForEffect            // 단계 진입 효과 처리 중
    }

    public enum TutorialSignalResult
    {
        Ignored,                    // Signal이 무시됨
        ConditionProgressed,        // Signal이 조건 진행에 기여함
        DialogueAdvanced,           // Signal이 대사 진행에 기여함
        TutorialCompleted           // 튜토리얼 완료
    }

    public enum TutorialInputPolicy
    {
        Passthrough,                // 기존 UI와 월드 입력을 차단하지 않음
        BlockOutsidePrimaryHighlight // 첫 번째 Highlight 외부 입력을 차단함
    }

    public enum TutorialMovementConstraint
    {
        Unrestricted,
        Blocked,
        RightOnly,
        LeftOnly
    }

    #endregion

    #region Contexts

    public readonly struct TutorialDialogueContext
    {
        private static readonly IReadOnlyList<string> EmptyHighlightKeys = Array.Empty<string>();

        public TutorialDialogueContext(
            string tutorialId,
            TutorialDialogueType type,
            string script,
            int dialogueIndex,
            int dialogueCount,
            string placementKey,
            Vector2? boxPosition,
            IReadOnlyList<string> highlightKeys,
            TutorialInputPolicy inputPolicy,
            bool usesSequentialHighlights,
            int conditionProgress,
            int conditionCount)
        {
            TutorialId = tutorialId;
            Type = type;
            Script = script;
            DialogueIndex = dialogueIndex;
            DialogueCount = dialogueCount;
            PlacementKey = placementKey;
            BoxPosition = boxPosition;
            HighlightKeys = highlightKeys ?? EmptyHighlightKeys;
            InputPolicy = inputPolicy;
            UsesSequentialHighlights = usesSequentialHighlights;
            ConditionProgress = conditionProgress;
            ConditionCount = conditionCount;
        }

        public string TutorialId { get; }
        public TutorialDialogueType Type { get; }
        public string Script { get; }
        public int DialogueIndex { get; }
        public int DialogueCount { get; }
        public string PlacementKey { get; }
        public Vector2? BoxPosition { get; }
        public IReadOnlyList<string> HighlightKeys { get; }
        public TutorialInputPolicy InputPolicy { get; }
        public bool UsesSequentialHighlights { get; }
        public int ConditionProgress { get; }
        public int ConditionCount { get; }
    }

    public readonly struct TutorialConditionProgressContext
    {
        public TutorialConditionProgressContext(
            string tutorialId,
            int dialogueIndex,
            TutorialEvent tutorialEvent,
            int progress,
            int requiredCount)
        {
            TutorialId = tutorialId;
            DialogueIndex = dialogueIndex;
            Event = tutorialEvent;
            Progress = progress;
            RequiredCount = requiredCount;
        }

        public string TutorialId { get; }
        public int DialogueIndex { get; }
        public TutorialEvent Event { get; }
        public int Progress { get; }
        public int RequiredCount { get; }
    }

    #endregion

}
