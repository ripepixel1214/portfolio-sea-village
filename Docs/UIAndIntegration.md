# UI와 씬 연동

[프로젝트](../README.md) / [전체 코드](CodeIndex.md)

## 패널 생성과 전환

[UIManager](../Source/Assets/Scripts/UI/UIManager.cs)는 화면을 처음 열 때 [UIRegistry](../Source/Assets/Scripts/UI/Data/UIRegistry.cs)에서 해당 프리팹을 찾아 만듭니다. 패널은 [UIPanel](../Source/Assets/Scripts/UI/Panels/UIPanel.cs)을 공통으로 쓰고, [화면별 제어부](../Source/Assets/Scripts/UI/Controllers/)가 마을, 항해와 메인 메뉴를 연결합니다.

이미 열었던 패널은 다시 사용합니다. 화면을 띄울 때 초기화가 필요한지, 표시만 바꿀지 구분하고 이번에 보여줄 데이터는 [IContextualPanel](../Source/Assets/Scripts/UI/Panels/IContextualPanel.cs)로 넘깁니다.

## 인벤토리와 정보 표시

[PlayerInventoryPanel](../Source/Assets/Scripts/UI/Panels/PlayerInventoryPanel.cs)은 목록과 입력을 처리합니다. 무엇을 목록에 넣고 시세 비율과 원산지를 어떻게 보여줄지는 [PlayerInventoryViewPolicy](../Source/Assets/Scripts/UI/Inventory/PlayerInventoryViewPolicy.cs)에서 정합니다. 아이템 설명은 [ItemInformationViewPolicy](../Source/Assets/Scripts/UI/Inventory/ItemInformationViewPolicy.cs)가 정한 값을 [ItemInformationPanel](../Source/Assets/Scripts/UI/Panels/Ship/ItemInformationPanel.cs)에 표시합니다.

[GenderSelectionPanel](../Source/Assets/Scripts/UI/Panels/Main/GenderSelectionPanel.cs)에서 고른 성별에 맞춰 [PlayerAppearance](../Source/Assets/Scripts/Player/PlayerAppearance.cs)가 캐릭터 외형을 적용합니다. [에디터 테스트 코드](../Source/Assets/Editor/Player/Tests/)에서 표시 규칙과 프리팹 구성을 확인할 수 있습니다.

[MainSaveSlotPanel](../Source/Assets/Scripts/UI/Panels/Main/MainSaveSlotPanel.cs)은 저장 정보를 읽기 전까지 로딩을 표시하고 슬롯 입력을 잠급니다. 읽은 뒤에는 [SaveSlotUI](../Source/Assets/Scripts/UI/Slots/SaveSlotUI.cs)에서 빈 슬롯과 이어 할 수 있는 슬롯을 구분합니다.

## 씬 전환과 입력 연결

[SceneChanger](../Source/Assets/Scripts/Utilities/SceneChanger.cs)에서 페이드와 씬 전환을 처리합니다. 전환이 끝나면 [ISceneRoot](../Source/Assets/Scripts/Core/ISceneRoot.cs)의 초기화를 호출합니다. [GameBootstrapper](../Source/Assets/Scripts/Core/GameBootstrapper.cs)와 [GameManager](../Source/Assets/Scripts/Core/GameManager.cs)에서 씬을 열 때 필요한 매니저를 준비합니다.

육지의 [PlayerController](../Source/Assets/Scripts/Player/PlayerController.cs)와 항해의 [ShipController](../Source/Assets/Scripts/Ocean/Ship/ShipController.cs)에서 플레이어 행동을 튜토리얼에 알리고, 진행 단계에 맞게 입력을 제한했습니다. 항해, 이동과 고객 AI는 팀 코드입니다. 그중 저장할 상태를 복사하고 게임 진행에 연결하는 부분을 수정했습니다.

## 오디오와 시각 피드백

| 구현 | 코드와 범위 |
| 기능 | 코드 |
| 장면별 BGM과 음량 설정 | [AudioManager](../Source/Assets/Scripts/Audio/AudioManager.cs), [SceneBgm](../Source/Assets/Scripts/Audio/SceneBgm.cs), [GameSettingsPanel](../Source/Assets/Scripts/UI/Panels/GameSettingsPanel.cs) |
| 상호작용할 수 있는 대상에 외곽선 표시 | [Outline.shader](../Source/Assets/Shaders/Outline.shader), [SpriteOutliner](../Source/Assets/Scripts/Utilities/SpriteOutliner.cs): 외곽선 색, 두께와 표시 여부 |
| 말풍선 표시와 재사용 | [SpeechBubblePool](../Source/Assets/Scripts/UI/Dialogue/NPC/SpeechBubblePool.cs), [UISpeechBubbleController](../Source/Assets/Scripts/UI/Dialogue/NPC/UISpeechBubbleController.cs) |
| 마을의 효과 표시 | [TownVFXController](../Source/Assets/Scripts/VFX/TownVFXController.cs), [VFXManager](../Source/Assets/Scripts/VFX/VFXManager.cs): 팀이 만든 효과를 게임 상태에 맞춰 표시 |

상호작용할 수 있는 대상에 외곽선을 띄우는 셰이더를 만들고, 게임 상태가 바뀔 때 표시할 효과를 연결했습니다.
