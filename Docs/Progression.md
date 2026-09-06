# 튜토리얼과 게임 진행

[프로젝트](../README.md) / [전체 코드](CodeIndex.md)

## 행동 신호와 진행 판단

이동, 구매와 장면 전환은 서로 다른 시스템에서 발생합니다. 각 시스템의 행동을 [TutorialEvents](../Source/Assets/Scripts/Core/Tutorial/TutorialEvents.cs)의 공통 이벤트로 전달하고, [TutorialRuntime](../Source/Assets/Scripts/Core/Tutorial/TutorialRuntime.cs)이 현재 단계에서 필요한 행동인지 판정하도록 했습니다.

대사와 순서는 변환된 [TutorialDatabase](../Source/Assets/Scripts/Data/Database/TutorialDatabase.cs)를 사용하고, [TutorialDefinitionCatalog](../Source/Assets/Scripts/Core/Tutorial/TutorialDefinitionCatalog.cs)에는 단계별 행동 조건과 연출 계약을 연결했습니다. 대사를 바꿀 때 진행 판단까지 다시 작성하지 않도록 데이터와 실행 책임을 나눴습니다.

| 역할 | 코드 |
| --- | --- |
| Unity 이벤트와 런타임 연결 | [TutorialController](../Source/Assets/Scripts/Core/Tutorial/TutorialController.cs) |
| 입력 제한, 보상과 연출 실행 | [TutorialEffectExecutor](../Source/Assets/Scripts/Core/Tutorial/TutorialEffectExecutor.cs) |
| 대사 표시와 강조 | [TutorialPresentationController](../Source/Assets/Scripts/UI/Tutorial/TutorialPresentationController.cs) |
| 허용 UI 외 입력 차단 | [TutorialInputMask](../Source/Assets/Scripts/UI/Tutorial/TutorialInputMask.cs) |
| 안내 캐릭터 이동 | [GuideMove](../Source/Assets/Scripts/Core/Tutorial/GuideMove.cs) |

진행 순서를 판단하는 코드와 화면 코드는 분리했지만, 단계별 연출은 실제 게임 시스템의 상태에 의존합니다. 이를 `TutorialEffectExecutor`와 제어부에 모아 런타임의 전이 판단에서 직접 처리하지 않도록 했습니다.

## 진행 복원과 실행 계약

[TutorialProgressSaveData](../Source/Assets/Scripts/Data/TutorialProgressSaveData.cs)에 현재 단계, 대사와 조건 진행도를 보관합니다. [TutorialDefinitionValidator](../Source/Assets/Editor/Tutorial/TutorialDefinitionValidator.cs)와 [TutorialLegacyContractTests](../Source/Assets/Editor/Tutorial/Tests/TutorialLegacyContractTests.cs)에서 데이터와 실행 계약을 확인하는 경로를 제공합니다.

## 진행 불가 상황의 구제 조건

돈과 항해 식량이 없어도 가방 속 아이템을 식량으로 바꿀 수 있으면 바로 진행 불가로 판단해서는 안 됩니다. [PlayerStateChecker](../Source/Assets/Scripts/Core/PlayerStateChecker.cs)에서 화폐, 선박 식량, 변환 가능한 가방 아이템을 함께 검사하도록 구현했습니다.

필요량을 확보할 수 있는 시점에는 아이템 순회를 종료합니다. 구제 진행 상태는 [FirstWreckRecoverySaveData](../Source/Assets/Scripts/Data/FirstWreckRecoverySaveData.cs)로 저장하고 튜토리얼 흐름과 연결했습니다.

## 이벤트와 게시판 퀘스트

[EventExecutionContext](../Source/Assets/Scripts/Event/Runtime/EventExecutionContext.cs)에 현재 단계와 다음 단계를 보관하고, [EventCommand](../Source/Assets/Scripts/Event/EventCommand.cs)와 [서비스 인터페이스](../Source/Assets/Scripts/Event/Services/)를 통해 대화, 선택지, 상태 변경과 UI를 연결했습니다. [EventRuntimeStore](../Source/Assets/Scripts/Event/EventRuntimeStore.cs)는 실행 상태를 관리합니다.

[BulletinBoardService](../Source/Assets/Scripts/Event/BoardQuest/BulletinBoardService.cs)는 마을별 퀘스트 목록과 화면 슬롯을 관리합니다. [BulletinBoardPanel](../Source/Assets/Scripts/UI/Panels/BulletinBoardPanel.cs)에서 표시하고 실제 이벤트 진행으로 연결했습니다. 공동 수정된 이벤트 코드의 파일별 이력은 [소스 출처](SourceMap.md)에 함께 기록했습니다.

## 날짜 진행과 항구 정산

[HarborSettlementProcessor](../Source/Assets/Scripts/Core/HarborSettlementProcessor.cs)에서 다음 날짜의 비용과 식량 변화를 미리 계산하고, 실행 시 보유량을 다시 확인하도록 했습니다. 비용 지불과 식량 소모 후 체류 상태를 기록하고 날짜를 진행합니다.

[TimeManager](../Source/Assets/Scripts/Core/TimeManager.cs)의 날짜 변경과 [TownProgressionManager](../Source/Assets/Scripts/Core/TownProgressionManager.cs)의 진행 상태에 연동했습니다. 시간 시스템 전체보다는 정산과 가게, 튜토리얼을 연결한 범위가 담당 내용입니다.
