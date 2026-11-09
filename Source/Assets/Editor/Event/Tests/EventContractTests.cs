using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SeaVillage.Core;
using SeaVillage.Data;
using SeaVillage.Event;
using SeaVillage.Event.Services;
using SeaVillage.UI;
using UnityEditor;
using UnityEngine;

namespace SeaVillage.Editor.Tests
{
    [TestFixture]
    public sealed class EventContractTests
    {
        private const string EventDatabasePath = "Assets/Resources/Data/ScriptableObjects/EventDatabase.asset";
        private const string EventNpcCatalogPath = "Assets/Resources/Data/ScriptableObjects/EventNpcCatalog.asset";
        private const string EventPanelPrefabPath = "Assets/Resources/Prefabs/UI/Panels/Event Panel.prefab";

        private static readonly string[] EventsAwaitingContent =
        {
            "EVT_StoryOnTheWater",
        };

        private static readonly string[] NpcsAwaitingAssets =
        {
            "NPC_Desset_RagDoll",
        };

        private static readonly string[] QuestFlowEventIds =
        {
            "EVT_BestSword_1",
            "EVT_BestSword_2",
            "EVT_Book",
            "EVT_Clothes",
            "EVT_FarmerTool",
            "EVT_ForMaster",
            "EVT_ScretHoney",
            "EVT_Sweet",
            "EVT_SweetAndSalty",
        };

        [Test]
        public void Sequences_UseSupportedCommandsAndValidStepReferences()
        {
            EventDatabase database = LoadEventDatabase();
            var factory = new EventCommandFactory();

            foreach (IGrouping<string, EventSequenceData> eventGroup in database.Sequences.GroupBy(sequence => sequence.EventID))
            {
                HashSet<int> steps = eventGroup.Select(sequence => sequence.Step).ToHashSet();
                foreach (EventSequenceData sequence in eventGroup)
                {
                    Assert.That(factory.Get(sequence.Command), Is.Not.Null,
                        $"지원하지 않는 명령: {sequence.EventID} step {sequence.Step} {sequence.Command}");
                    AssertStepExists(sequence.EventID, sequence.Step, "NextStep", sequence.NextStep, steps);
                    AssertStepExists(sequence.EventID, sequence.Step, "FailStep", sequence.FailStep, steps);
                }
            }
        }

        [Test]
        public void Definitions_MissingSequencesAreExplicitlyTracked()
        {
            EventDatabase database = LoadEventDatabase();
            HashSet<string> sequenceEventIds = database.Sequences.Select(sequence => sequence.EventID).ToHashSet();
            string[] missing = database.Events
                .Select(eventData => eventData.EventID)
                .Where(eventId => !sequenceEventIds.Contains(eventId))
                .OrderBy(eventId => eventId)
                .ToArray();

            Assert.That(missing, Is.EqualTo(EventsAwaitingContent));
        }

        [Test]
        public void ClearConditions_ReferenceExistingEventIds()
        {
            EventDatabase database = LoadEventDatabase();
            HashSet<string> eventIds = database.Events.Select(eventData => eventData.EventID).ToHashSet();

            foreach (EventConditionData condition in database.Conditions)
            {
                if (!string.Equals(condition.ConditionType, EventManager.EVENT_CONDITION_CLEAR, StringComparison.OrdinalIgnoreCase))
                    continue;

                Assert.That(condition.Variable, Is.Not.Empty, $"Clear 선행 이벤트 누락: {condition.EventID}");
                Assert.That(eventIds.Contains(condition.Variable), Is.True,
                    $"Clear 선행 이벤트가 존재하지 않음: {condition.EventID} -> {condition.Variable}");
            }
        }

        [Test]
        public void QuestFlows_HaveDetectableAcceptanceAndContinuation()
        {
            EventDatabase database = LoadEventDatabase();
            string[] detected = database.Sequences
                .GroupBy(sequence => sequence.EventID)
                .Where(group => EventSequenceEntryResolver.IsQuestFlow(group.ToList()))
                .Select(group => group.Key)
                .OrderBy(eventId => eventId)
                .ToArray();

            Assert.That(detected, Is.EqualTo(QuestFlowEventIds));
        }

        [Test]
        public void ReferencedEventNpcs_MissingAssetsAreExplicitlyTracked()
        {
            EventDatabase database = LoadEventDatabase();
            EventNpcCatalog catalog = AssetDatabase.LoadAssetAtPath<EventNpcCatalog>(EventNpcCatalogPath);
            Assert.That(catalog, Is.Not.Null, $"EventNpcCatalog 누락: {EventNpcCatalogPath}");

            string[] missing = database.Sequences
                .SelectMany(GetReferencedNpcIds)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(npcId => !catalog.TryGet(npcId, out _))
                .OrderBy(npcId => npcId)
                .ToArray();

            Assert.That(missing, Is.EqualTo(NpcsAwaitingAssets));
        }

        [Test]
        public void ChoiceService_WaitsForPanelCloseBeforeCompletingSelection()
        {
            GameObject managerObject = null;
            GameObject panelObject = null;
            GameObject timeManagerObject = null;

            try
            {
                bool hadTimeManager = TimeManager.HasInstance;
                if (!hadTimeManager)
                {
                    timeManagerObject = new GameObject("Event Choice Test TimeManager");
                    timeManagerObject.AddComponent<TimeManager>();
                    _ = TimeManager.Instance;
                }

                UIManager manager;
                if (UIManager.HasInstance)
                {
                    manager = UIManager.Instance;
                }
                else
                {
                    managerObject = new GameObject("Event Choice Test UIManager");
                    managerObject.AddComponent<UIManager>();
                    manager = UIManager.Instance;
                }

                GameObject panelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EventPanelPrefabPath);
                Assert.That(panelPrefab, Is.Not.Null, $"EventPanel 프리팹 누락: {EventPanelPrefabPath}");

                panelObject = UnityEngine.Object.Instantiate(panelPrefab);
                panelObject.name = "Event Choice Test Panel";
                panelObject.SetActive(false);

                EventPanel panel = panelObject.GetComponent<EventPanel>();
                Assert.That(panel, Is.Not.Null, "EventPanel 컴포넌트 누락");
                manager.RegisterPanel(panel);

                var options = new List<EventSequenceData>
                {
                    new EventSequenceData { EventID = "EVT_Test", Step = 11, Command = "Choice", Value = "첫 번째", NextStep = 20 },
                    new EventSequenceData { EventID = "EVT_Test", Step = 11, Command = "Choice", Value = "두 번째", NextStep = 30 },
                };

                EventSequenceData chosen = null;
                IEnumerator routine = new DefaultChoiceService().Choose(options, result => chosen = result);

                Assert.That(routine.MoveNext(), Is.True, "선택 입력 대기 전에 코루틴이 종료됨");
                Assert.That(panel.State, Is.EqualTo(PanelState.Open));

                panel.ClickSelectedButton();

                Assert.That(routine.MoveNext(), Is.True, "패널 닫힘 완료 전에 선택 콜백이 실행됨");
                Assert.That(panel.State, Is.EqualTo(PanelState.Closing));
                Assert.That(chosen, Is.Null);

                panel.OnClose();

                Assert.That(routine.MoveNext(), Is.False);
                Assert.That(chosen, Is.SameAs(options[0]));

            }
            finally
            {
                if (panelObject != null)
                    UnityEngine.Object.DestroyImmediate(panelObject);
                if (managerObject != null)
                    UnityEngine.Object.DestroyImmediate(managerObject);
                if (timeManagerObject != null)
                    UnityEngine.Object.DestroyImmediate(timeManagerObject);
            }
        }

        [Test]
        public void PanelCommand_WaitsForResultPanelCloseBeforeAdvancing()
        {
            GameObject managerObject = null;
            GameObject panelObject = null;
            GameObject timeManagerObject = null;

            try
            {
                if (!TimeManager.HasInstance)
                {
                    timeManagerObject = new GameObject("Event Result Test TimeManager");
                    timeManagerObject.AddComponent<TimeManager>();
                    _ = TimeManager.Instance;
                }

                UIManager manager;
                if (UIManager.HasInstance)
                {
                    manager = UIManager.Instance;
                }
                else
                {
                    managerObject = new GameObject("Event Result Test UIManager");
                    managerObject.AddComponent<UIManager>();
                    manager = UIManager.Instance;
                }

                GameObject panelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EventPanelPrefabPath);
                Assert.That(panelPrefab, Is.Not.Null, $"EventPanel 프리팹 누락: {EventPanelPrefabPath}");

                panelObject = UnityEngine.Object.Instantiate(panelPrefab);
                panelObject.name = "Event Result Test Panel";
                panelObject.SetActive(false);

                EventPanel panel = panelObject.GetComponent<EventPanel>();
                Assert.That(panel, Is.Not.Null, "EventPanel 컴포넌트 누락");
                manager.RegisterPanel(panel);

                var resultStep = new EventSequenceData
                {
                    EventID = "EVT_Pirate",
                    Step = 31,
                    Command = EventCommandFactory.EVENT_COMMAND_PANEL,
                    Value = "결과",
                    NextStep = 32,
                };
                var nextStep = new EventSequenceData
                {
                    EventID = "EVT_Pirate",
                    Step = 32,
                    Command = EventCommandFactory.EVENT_COMMAND_CALCULATE,
                    Target = EventManager.CONTEXT_KEY_GOLD,
                    Value = "200",
                    NextStep = -1,
                };
                var context = new EventExecutionContext(
                    new EventData { EventID = "EVT_Pirate", SceneName = "Ocean", TriggerType = EventManager.EVENT_TRIGGER_DAILYRANDOM },
                    new List<EventSequenceData> { resultStep, nextStep });

                IEnumerator commandRoutine = new PanelCommand().Execute(
                    context,
                    new List<EventSequenceData> { resultStep },
                    null);

                Assert.That(commandRoutine.MoveNext(), Is.True);
                Assert.That(commandRoutine.Current, Is.InstanceOf<IEnumerator>());

                var resultRoutine = (IEnumerator)commandRoutine.Current;
                Assert.That(resultRoutine.MoveNext(), Is.True, "결과 확인 입력 대기 전에 코루틴이 종료됨");
                Assert.That(panel.State, Is.EqualTo(PanelState.Open));

                panel.ClickSelectedButton();

                Assert.That(resultRoutine.MoveNext(), Is.True, "결과 패널 닫힘 완료 전에 다음 스텝으로 진행됨");
                Assert.That(panel.State, Is.EqualTo(PanelState.Closing));
                Assert.That(context.NextStep, Is.EqualTo(-1));

                panel.OnClose();

                Assert.That(resultRoutine.MoveNext(), Is.False);
                Assert.That(commandRoutine.MoveNext(), Is.False);
                Assert.That(context.NextStep, Is.EqualTo(32));
            }
            finally
            {
                if (panelObject != null)
                    UnityEngine.Object.DestroyImmediate(panelObject);
                if (managerObject != null)
                    UnityEngine.Object.DestroyImmediate(managerObject);
                if (timeManagerObject != null)
                    UnityEngine.Object.DestroyImmediate(timeManagerObject);
            }
        }

        private static IEnumerable<string> GetReferencedNpcIds(EventSequenceData sequence)
        {
            if ((string.Equals(sequence.Command, EventCommandFactory.EVENT_COMMAND_DIALOGUE, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(sequence.Command, EventCommandFactory.EVENT_COMMAND_SETACTIVE, StringComparison.OrdinalIgnoreCase))
                && !string.IsNullOrWhiteSpace(sequence.Target))
            {
                yield return sequence.Target.Trim();
            }

            if (string.Equals(sequence.Command, EventCommandFactory.EVENT_COMMAND_INTERACTION, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(sequence.Value))
            {
                yield return sequence.Value.Trim();
            }
        }

        private static void AssertStepExists(
            string eventId,
            int sourceStep,
            string fieldName,
            int targetStep,
            HashSet<int> steps)
        {
            if (targetStep < 0)
                return;

            Assert.That(steps.Contains(targetStep), Is.True,
                $"존재하지 않는 {fieldName}: {eventId} step {sourceStep} -> {targetStep}");
        }

        private static EventDatabase LoadEventDatabase()
        {
            EventDatabase database = AssetDatabase.LoadAssetAtPath<EventDatabase>(EventDatabasePath);
            Assert.That(database, Is.Not.Null, $"EventDatabase 누락: {EventDatabasePath}");
            return database;
        }
    }
}
