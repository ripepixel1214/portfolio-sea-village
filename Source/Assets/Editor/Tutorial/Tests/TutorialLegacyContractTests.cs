using System.Collections.Generic;
using MemoryPack;
using NUnit.Framework;
using SeaVillage.Core;
using SeaVillage.Data;
using UnityEditor;
using UnityEngine;

namespace SeaVillage.Editor.Tests
{
    public static class TutorialLegacyContractTestRunner
    {
        [MenuItem("SeaVillage/Tutorial/Run Contract Tests")]
        public static void RunFromMenu() => RunAll();
        public static void RunFromCommandLine() => RunAll();

        private static void RunAll()
        {
            var tests = new TutorialLegacyContractTests();
            tests.Definitions_MatchCurrentDialogueDatabase();
            tests.Definitions_FollowSpreadsheetOrderWithUniqueStepIds();
            tests.Definitions_ReorderedSpreadsheetChangesRuntimeOrder();
            tests.Definitions_SettlementCostsUseCompositeHighlight();
            tests.Definitions_FirstWreckIsManualException();
            tests.FirstWreckRecovery_RequiresUnrecoverablePlayerState();
            tests.Runtime_IgnoresDuplicateAndOutOfOrderEvents();
            tests.Runtime_CompletesOnlyAfterOrderedEvents();
            tests.Runtime_AllThirtyStepsCompleteFromDefinitions();
            tests.ControlScope_DisposeAlwaysReleasesPolicies();
            tests.ProgressSaveData_RoundTripsWithoutDataLoss();
            Debug.Log("[TutorialContractTests] 11개 회귀 검증 통과");
        }
    }

    [TestFixture]
    public sealed class TutorialLegacyContractTests
    {
        private const string DatabasePath = "Assets/Resources/Data/ScriptableObjects/TutorialDatabase.asset";

        [Test]
        public void Definitions_MatchCurrentDialogueDatabase()
        {
            TutorialDatabase database = LoadDatabase();
            TutorialRepository repository = TutorialDefinitionCatalog.CreateRepository(database.Tutorials);
            Assert.That(repository.OrderedSteps.Count, Is.EqualTo(30));
            Assert.That(repository.AllSteps.Count, Is.EqualTo(31));
            int dialogueCount = 0;
            for (int i = 0; i < repository.OrderedSteps.Count; i++)
                dialogueCount += repository.OrderedSteps[i].Dialogues.Count;
            Assert.That(dialogueCount, Is.EqualTo(37));
            Assert.That(repository.TryValidate(out string failReason), Is.True, failReason);
        }

        [Test]
        public void Definitions_FollowSpreadsheetOrderWithUniqueStepIds()
        {
            TutorialDatabase database = LoadDatabase();
            TutorialRepository repository = TutorialDefinitionCatalog.CreateRepository(database.Tutorials);
            var ids = new HashSet<string>();
            var spreadsheetOrder = new List<string>();
            string previousId = string.Empty;
            for (int i = 0; i < database.Tutorials.Count; i++)
            {
                string dialogueId = database.Tutorials[i].ID;
                if (string.Equals(previousId, dialogueId, System.StringComparison.Ordinal))
                    continue;
                previousId = dialogueId;
                if (repository.TryGetByDialogueId(dialogueId, out TutorialStepDefinition step)
                    && step.IsOrdered)
                {
                    spreadsheetOrder.Add(dialogueId);
                }
            }

            Assert.That(repository.OrderedSteps.Count, Is.EqualTo(spreadsheetOrder.Count));
            for (int i = 0; i < repository.OrderedSteps.Count; i++)
            {
                TutorialStepDefinition step = repository.OrderedSteps[i];
                Assert.That(ids.Add(step.StepId), Is.True, $"중복 StepId: {step.StepId}");
                Assert.That(step.DialogueId, Is.EqualTo(spreadsheetOrder[i]));
            }
        }

        [Test]
        public void Definitions_ReorderedSpreadsheetChangesRuntimeOrder()
        {
            TutorialDatabase database = LoadDatabase();
            var reordered = new List<TutorialData>(database.Tutorials.Count);
            AppendDialogueGroup(database.Tutorials, "Tutorial_002", reordered);
            AppendDialogueGroup(database.Tutorials, "Tutorial_001", reordered);
            for (int i = 0; i < database.Tutorials.Count; i++)
            {
                TutorialData dialogue = database.Tutorials[i];
                if (dialogue.ID == "Tutorial_001" || dialogue.ID == "Tutorial_002")
                    continue;
                reordered.Add(dialogue);
            }

            TutorialRepository repository = TutorialDefinitionCatalog.CreateRepository(reordered);
            Assert.That(repository.TryValidate(out string failReason), Is.True, failReason);
            Assert.That(repository.OrderedSteps[0].DialogueId, Is.EqualTo("Tutorial_002"));
            Assert.That(repository.OrderedSteps[1].DialogueId, Is.EqualTo("Tutorial_001"));
        }

        [Test]
        public void Definitions_SettlementCostsUseCompositeHighlight()
        {
            TutorialRepository repository = CreateRepository();
            Assert.That(repository.TryGetByDialogueId("Tutorial_009", out TutorialStepDefinition step), Is.True);
            Assert.That(step.StepId, Is.EqualTo("settlement.costs"));
            Assert.That(step.Dialogues.Count, Is.EqualTo(1));

            TutorialDialogueDefinition dialogue = step.Dialogues[0];
            Assert.That(dialogue.DialogueKey, Is.EqualTo("settlement.costs.explanation"));
            Assert.That(dialogue.RequiredEvents.Count, Is.EqualTo(1));
            Assert.That(dialogue.RequiredEvents[0].Type, Is.EqualTo(TutorialEventType.UiElementActivated));
            Assert.That(dialogue.RequiredEvents[0].TargetId, Is.EqualTo(TutorialTargetIds.SettlementCosts));
            Assert.That(dialogue.HighlightKeys, Is.EqualTo(new[] { TutorialAnchorKeys.SettlementCosts }));
        }

        [Test]
        public void Definitions_FirstWreckIsManualException()
        {
            TutorialRepository repository = CreateRepository();
            Assert.That(
                repository.TryGetByDialogueId(
                    TutorialDefinitionCatalog.FirstWreckDialogueId,
                    out TutorialStepDefinition step),
                Is.True);
            Assert.That(step.StepId, Is.EqualTo(TutorialDefinitionCatalog.FirstWreckStepId));
            Assert.That(step.IsOrdered, Is.False);
            Assert.That(step.RequiredScene, Is.EqualTo(TutorialDefinitionCatalog.AnyTownScene));
            Assert.That(step.Dialogues.Count, Is.EqualTo(3));
            Assert.That(step.EntryActions[0], Is.EqualTo(TutorialActionType.ClosePanels));
            Assert.That(step.RecoveryActions[0], Is.EqualTo(TutorialActionType.ClosePanels));
            for (int i = 0; i < step.Dialogues.Count; i++)
                Assert.That(step.Dialogues[i].Type, Is.EqualTo(TutorialDialogueType.Stop));
            Assert.That(
                repository.OrderedSteps,
                Has.None.Matches<TutorialStepDefinition>(candidate => candidate.StepId == step.StepId));
        }

        [Test]
        public void FirstWreckRecovery_RequiresUnrecoverablePlayerState()
        {
            Assert.That(PlayerStateChecker.IsFirstWreckRecoveryRequired(0L, 0f, 0), Is.True);
            Assert.That(PlayerStateChecker.IsFirstWreckRecoveryRequired(0L, 0f, 19), Is.True);
            Assert.That(PlayerStateChecker.IsFirstWreckRecoveryRequired(0L, 0f, 20), Is.False);
            Assert.That(PlayerStateChecker.IsFirstWreckRecoveryRequired(1L, 0f, 0), Is.False);
            Assert.That(PlayerStateChecker.IsFirstWreckRecoveryRequired(0L, 1f, 0), Is.False);
        }

        [Test]
        public void Runtime_IgnoresDuplicateAndOutOfOrderEvents()
        {
            TutorialRuntime runtime = CreateWaitingRuntime("trade.return_to_shop");
            Report(runtime, TutorialEventType.InteractionCompleted, TutorialTargetIds.FoodShop, TutorialSignalResult.Ignored);
            Report(runtime, TutorialEventType.UiElementActivated, TutorialTargetIds.SettlementProceed, TutorialSignalResult.ConditionProgressed);
            Report(runtime, TutorialEventType.UiElementActivated, TutorialTargetIds.SettlementProceed, TutorialSignalResult.Ignored);
            Assert.That(runtime.ConditionProgress, Is.EqualTo(1));
        }

        [Test]
        public void Runtime_CompletesOnlyAfterOrderedEvents()
        {
            TutorialRuntime runtime = CreateWaitingRuntime("trade.return_to_shop");
            Report(runtime, TutorialEventType.UiElementActivated, TutorialTargetIds.SettlementProceed, TutorialSignalResult.ConditionProgressed);
            Report(runtime, TutorialEventType.InteractionCompleted, TutorialTargetIds.FoodShop, TutorialSignalResult.ConditionProgressed);
            Report(runtime, TutorialEventType.PanelOpened, TutorialTargetIds.PurchasePanel, TutorialSignalResult.TutorialCompleted);
            Assert.That(runtime.IsStepCompleted("trade.return_to_shop"), Is.True);
        }

        [Test]
        public void Runtime_AllThirtyStepsCompleteFromDefinitions()
        {
            TutorialRepository repository = CreateRepository();
            for (int stepIndex = 0; stepIndex < repository.OrderedSteps.Count; stepIndex++)
            {
                TutorialStepDefinition step = repository.OrderedSteps[stepIndex];
                TutorialRuntime runtime = CreateWaitingRuntime(step.StepId);
                while (runtime.HasActiveStep)
                {
                    TutorialDialogueDefinition dialogue = runtime.CurrentDialogueDefinition;
                    if (runtime.PlaybackState == TutorialPlaybackState.WaitingForInput)
                    {
                        Assert.That(runtime.TryAdvance(TutorialPlaybackState.WaitingForInput, out _, out string failure), Is.True, failure);
                    }
                    else if (runtime.PlaybackState == TutorialPlaybackState.WaitingForAutoAdvance)
                    {
                        Assert.That(runtime.TryAdvance(TutorialPlaybackState.WaitingForAutoAdvance, out _, out string failure), Is.True, failure);
                    }
                    else
                    {
                        for (int eventIndex = runtime.ConditionProgress; eventIndex < dialogue.RequiredEvents.Count; eventIndex++)
                        {
                            TutorialEventPattern pattern = dialogue.RequiredEvents[eventIndex];
                            var tutorialEvent = new TutorialEvent(pattern.Type, pattern.TargetId);
                            Assert.That(runtime.TryReportEvent(tutorialEvent, out _, out _, out string failure), Is.True, failure);
                        }
                    }

                    if (runtime.HasActiveStep && runtime.PlaybackState == TutorialPlaybackState.Presenting)
                        Assert.That(runtime.NotifyCurrentDialoguePresented(out string failure), Is.True, failure);
                }

                Assert.That(runtime.IsStepCompleted(step.StepId), Is.True, step.StepId);
            }
        }

        [Test]
        public void ProgressSaveData_RoundTripsWithoutDataLoss()
        {
            var original = new SaveData
            {
                completedTutorialIds = new List<string> { "Tutorial_001" },
                tutorialProgress = new TutorialProgressSaveData
                {
                    definitionVersion = TutorialDefinitionCatalog.DefinitionVersion,
                    activeStepId = "trade.return_to_shop",
                    activeDialogueKey = "trade.return_to_shop.sequence",
                    playbackState = (int)TutorialPlaybackState.WaitingForTrigger,
                    conditionProgress = 2,
                    completedStepIds = new List<string> { "town.intro" },
                    appliedEffectIds = new List<string> { "tutorial.price_change.food" }
                },
                firstWreckRecovery = new FirstWreckRecoverySaveData
                {
                    triggered = true,
                    pending = true,
                    rewardGranted = false
                }
            };
            SaveData restored = MemoryPackSerializer.Deserialize<SaveData>(MemoryPackSerializer.Serialize(original));
            Assert.That(restored.tutorialProgress.activeStepId, Is.EqualTo(original.tutorialProgress.activeStepId));
            Assert.That(restored.tutorialProgress.conditionProgress, Is.EqualTo(2));
            Assert.That(restored.tutorialProgress.completedStepIds, Is.EqualTo(original.tutorialProgress.completedStepIds));
            Assert.That(restored.tutorialProgress.appliedEffectIds, Is.EqualTo(original.tutorialProgress.appliedEffectIds));
            Assert.That(restored.firstWreckRecovery.triggered, Is.True);
            Assert.That(restored.firstWreckRecovery.pending, Is.True);
            Assert.That(restored.firstWreckRecovery.rewardGranted, Is.False);
        }

        [Test]
        public void ControlScope_DisposeAlwaysReleasesPolicies()
        {
            using (var scope = new TutorialControlScope())
            {
                scope.BlockAllInteractions();
                scope.SetCommandInputBlocked(true);
                Assert.That(TutorialInteractionPolicy.IsRestricted, Is.True);
                Assert.That(TutorialCommandInputPolicy.IsBlocked, Is.True);
            }

            Assert.That(TutorialInteractionPolicy.IsRestricted, Is.False);
            Assert.That(TutorialCommandInputPolicy.IsBlocked, Is.False);
        }

        private static TutorialRuntime CreateWaitingRuntime(string stepId)
        {
            TutorialRepository repository = CreateRepository();
            Assert.That(repository.TryGet(stepId, out TutorialStepDefinition step), Is.True);
            var dialogues = new List<TutorialData>();
            for (int i = 0; i < step.Dialogues.Count; i++)
                dialogues.Add(new TutorialData { ID = step.DialogueId, Type = step.Dialogues[i].Type, Sequence = i + 1 });
            var runtime = new TutorialRuntime(repository);
            Assert.That(runtime.TrySetPendingStep(stepId, out string pendingFailure), Is.True, pendingFailure);
            Assert.That(runtime.TryStartDialogue(dialogues, string.Empty, 0, out string startFailure), Is.True, startFailure);
            Assert.That(runtime.NotifyCurrentDialoguePresented(out string presentFailure), Is.True, presentFailure);
            return runtime;
        }

        private static void Report(TutorialRuntime runtime, TutorialEventType type, string targetId, TutorialSignalResult expected)
        {
            var tutorialEvent = new TutorialEvent(type, targetId);
            Assert.That(runtime.TryReportEvent(tutorialEvent, out TutorialSignalResult result, out _, out string failReason), Is.True, failReason);
            Assert.That(result, Is.EqualTo(expected));
        }

        private static TutorialDatabase LoadDatabase()
        {
            TutorialDatabase database = AssetDatabase.LoadAssetAtPath<TutorialDatabase>(DatabasePath);
            Assert.That(database, Is.Not.Null, $"TutorialDatabase 누락: {DatabasePath}");
            return database;
        }

        private static TutorialRepository CreateRepository()
        {
            TutorialDatabase database = LoadDatabase();
            return TutorialDefinitionCatalog.CreateRepository(database.Tutorials);
        }

        private static void AppendDialogueGroup(
            IReadOnlyList<TutorialData> source,
            string dialogueId,
            List<TutorialData> target)
        {
            for (int i = 0; i < source.Count; i++)
            {
                if (source[i].ID == dialogueId)
                    target.Add(source[i]);
            }
        }
    }
}
