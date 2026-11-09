# 튜토리얼과 게임 진행

[프로젝트](../README.md) / [전체 코드](CodeIndex.md)

## 튜토리얼에서 플레이어 행동 확인

이동이나 구매, 씬 전환은 서로 다른 코드에서 일어납니다. 이 행동을 [TutorialEvents](../Source/Assets/Scripts/Core/Tutorial/TutorialEvents.cs)로 넘기고 [TutorialRuntime](../Source/Assets/Scripts/Core/Tutorial/TutorialRuntime.cs)이 현재 단계에서 기다리던 행동인지 확인하게 했습니다.

대사와 순서는 [TutorialDatabase](../Source/Assets/Scripts/Data/Database/TutorialDatabase.cs)에서 읽고, [TutorialDefinitionCatalog](../Source/Assets/Scripts/Core/Tutorial/TutorialDefinitionCatalog.cs)에 단계별 행동 조건과 연출을 적었습니다. 대사를 바꿀 때 진행을 판정하는 코드까지 손대지 않게 나눴습니다.

| 역할 | 코드 |
| --- | --- |
| Unity에서 온 이벤트를 진행 코드로 넘김 | [TutorialController](../Source/Assets/Scripts/Core/Tutorial/TutorialController.cs) |
| 입력 제한, 보상 지급과 연출 | [TutorialEffectExecutor](../Source/Assets/Scripts/Core/Tutorial/TutorialEffectExecutor.cs) |
| 대사 표시와 강조 | [TutorialPresentationController](../Source/Assets/Scripts/UI/Tutorial/TutorialPresentationController.cs) |
| 허용한 UI 이외의 입력 차단 | [TutorialInputMask](../Source/Assets/Scripts/UI/Tutorial/TutorialInputMask.cs) |
| 안내 캐릭터 이동 | [GuideMove](../Source/Assets/Scripts/Core/Tutorial/GuideMove.cs) |

게임 상태를 읽어 안내를 띄우거나 입력을 제한하는 일은 `TutorialController`와 `TutorialEffectExecutor`에 맡겼습니다. `TutorialRuntime`에서는 다음 단계로 넘어갈지만 판단합니다.

## 저장한 진행과 화면 복원

플레이어가 멈춘 단계부터 이어갈 수 있도록 [TutorialProgressSaveData](../Source/Assets/Scripts/Data/TutorialProgressSaveData.cs)에 단계, 대사, 조건 진행도와 이미 적용한 연출 ID를 저장했습니다. 씬과 UI가 준비되면 [TutorialController](../Source/Assets/Scripts/Core/Tutorial/TutorialController.cs)가 [TutorialEffectExecutor](../Source/Assets/Scripts/Core/Tutorial/TutorialEffectExecutor.cs)로 입력 제한과 안내 연출을 복원합니다. [TutorialUIRecovery](../Source/Assets/Scripts/UI/Tutorial/TutorialUIRecovery.cs)는 저장한 진행도에 맞는 화면을 다시 엽니다.

화면을 다시 열면서 생긴 이벤트로 한 단계 더 진행되지 않도록 [TutorialManager](../Source/Assets/Scripts/Core/Tutorial/TutorialManager.cs)에서 복원 중 이벤트를 걸렀습니다. [TutorialRuntime](../Source/Assets/Scripts/Core/Tutorial/TutorialRuntime.cs)은 입력이나 조건을 기다리는 상태에서 대사를 다시 표시해도 진행도를 유지합니다. [대사와 단계 정의 검사](../Source/Assets/Editor/Tutorial/TutorialDefinitionValidator.cs), [진행과 복원 테스트 코드](../Source/Assets/Editor/Tutorial/Tests/TutorialContractTests.cs)

## 진행 불가 상황의 구제 조건

돈과 항해 식량이 없어도 가방 속 물건을 바꿔 필요한 식량을 마련할 수 있으면 항해를 이어갈 수 있습니다. [PlayerStateChecker](../Source/Assets/Scripts/Core/PlayerStateChecker.cs)에서 돈, 선박 식량과 가방 속 변환 가능한 아이템을 함께 확인하게 했습니다.

필요한 식량을 마련할 수 있는 시점에 아이템 검사를 멈춥니다. 난파 구제의 진행 상태도 [FirstWreckRecoverySaveData](../Source/Assets/Scripts/Data/FirstWreckRecoverySaveData.cs)에 저장해 튜토리얼을 이어갑니다.

## 이벤트와 게시판 퀘스트

이벤트가 대화나 선택지를 띄우면 [EventExecutionContext](../Source/Assets/Scripts/Event/Runtime/EventExecutionContext.cs)에 지금 단계와 다음 단계를 남깁니다. [EventCommand](../Source/Assets/Scripts/Event/EventCommand.cs)가 [서비스 인터페이스](../Source/Assets/Scripts/Event/Services/)를 통해 대화, 선택지, 상태 변경과 UI를 호출합니다. 실행 상태는 [EventRuntimeStore](../Source/Assets/Scripts/Event/EventRuntimeStore.cs)가 관리합니다.

마을마다 게시판에 올릴 퀘스트와 자리를 [BulletinBoardService](../Source/Assets/Scripts/Event/BoardQuest/BulletinBoardService.cs)에서 정하고 [BulletinBoardPanel](../Source/Assets/Scripts/UI/Panels/BulletinBoardPanel.cs)에 표시합니다. 게시판에서 고른 퀘스트를 실제 이벤트로 연결했습니다. [팀과 함께 수정한 파일](SourceMap.md)

여러 퀘스트를 동시에 진행할 수 있도록 [SaveData](../Source/Assets/Scripts/Data/SaveData.cs)에 활성 퀘스트 ID와 각각의 단계, 마을을 저장합니다. 활성 목록이 없는 저장 파일은 [SaveLoadManager](../Source/Assets/Scripts/Data/SaveLoadManager.cs)에서 예전 단일 ID를 읽습니다. [EventSequenceEntryResolver](../Source/Assets/Scripts/Event/Runtime/EventSequenceEntryResolver.cs)는 시퀀스와 NPC 정의를 보고 현재 마을에서 이어갈 단계를 찾습니다.

이벤트에서 필요한 NPC는 [EventNpcRuntime](../Source/Assets/Scripts/Event/Runtime/EventNpcRuntime.cs)이 찾거나 배치합니다. 이 NPC의 대화, 연출과 상호작용을 실행합니다. 종류와 배치 지점은 [EventNpcCatalog](../Source/Assets/Scripts/Event/EventNpcCatalog.cs)와 [EventNpcSpawnPoint](../Source/Assets/Scripts/Event/EventNpcSpawnPoint.cs)에서 정합니다.

## 날짜 진행과 항구 정산

[HarborSettlementProcessor](../Source/Assets/Scripts/Core/HarborSettlementProcessor.cs)에서 다음 날 필요한 돈과 식량을 미리 계산합니다. 실제로 정산할 때 보유량을 다시 확인해 비용과 식량을 차감하고, 체류 상태를 남긴 뒤 다음 날로 넘어갑니다.

[TimeManager](../Source/Assets/Scripts/Core/TimeManager.cs)에서 날짜가 바뀔 때 항구 정산, 가게와 튜토리얼이 진행되도록 연결했습니다. 마을 진행 상태는 [TownProgressionManager](../Source/Assets/Scripts/Core/TownProgressionManager.cs)에서 관리합니다.
