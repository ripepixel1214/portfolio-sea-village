# UI와 씬 연동

[프로젝트](../README.md) / [전체 코드](CodeIndex.md)

## 패널 생성과 전환

[UIManager](../Source/Assets/Scripts/UI/UIManager.cs)에서 패널 타입과 인스턴스를 관리하고, [UIRegistry](../Source/Assets/Scripts/UI/Data/UIRegistry.cs)에 등록된 프리팹을 통해 필요한 패널을 생성하도록 했습니다. [UIPanel](../Source/Assets/Scripts/UI/Panels/UIPanel.cs)은 개별 화면의 공통 기반이며, [화면별 제어부](../Source/Assets/Scripts/UI/Controllers/)가 마을, 항해와 메인 메뉴 UI를 연결합니다.

패널을 새로 만드는 경우와 이미 열린 패널을 다시 사용하는 경우를 구분해 초기화와 표시 상태를 처리했습니다. 화면에 필요한 데이터가 달라지는 경우에는 [IContextualPanel](../Source/Assets/Scripts/UI/Panels/IContextualPanel.cs)로 문맥을 전달합니다.

## 인벤토리와 정보 표시

[PlayerInventoryPanel](../Source/Assets/Scripts/UI/Panels/PlayerInventoryPanel.cs)에서 목록과 입력을 처리하고, [PlayerInventoryViewPolicy](../Source/Assets/Scripts/UI/Inventory/PlayerInventoryViewPolicy.cs)에는 표시 대상, 시세 비율과 원산지 표현 규칙을 분리했습니다. 아이템 설명은 [ItemInformationViewPolicy](../Source/Assets/Scripts/UI/Inventory/ItemInformationViewPolicy.cs)와 [ItemInformationPanel](../Source/Assets/Scripts/UI/Panels/Ship/ItemInformationPanel.cs)로 연결했습니다.

성별 선택 화면과 캐릭터 표현은 [GenderSelectionPanel](../Source/Assets/Scripts/UI/Panels/Main/GenderSelectionPanel.cs), [PlayerAppearance](../Source/Assets/Scripts/Player/PlayerAppearance.cs)에 구현했습니다. 표시 정책과 프리팹 계약을 확인하는 [Editor 테스트](../Source/Assets/Editor/Player/Tests/)도 포함했습니다.

## 씬 전환과 입력 연결

[SceneChanger](../Source/Assets/Scripts/Utilities/SceneChanger.cs)에서 페이드와 씬 전환을 처리하고, 화면 전환 뒤 [ISceneRoot](../Source/Assets/Scripts/Core/ISceneRoot.cs)의 초기화를 호출하도록 연결했습니다. [GameBootstrapper](../Source/Assets/Scripts/Core/GameBootstrapper.cs)와 [GameManager](../Source/Assets/Scripts/Core/GameManager.cs)는 매니저 준비와 씬 루트를 연결하는 코드입니다.

육지의 [PlayerController](../Source/Assets/Scripts/Player/PlayerController.cs)와 항해의 [ShipController](../Source/Assets/Scripts/Ocean/Ship/ShipController.cs)에는 튜토리얼 행동 보고와 제어 제한을 연결했습니다. 항해, 이동, 고객 AI 등 공동 모듈 전체를 단독 구현한 것은 아닙니다. 저장 복사와 진행 연동을 수정한 파일도 원래 구조를 따라 확인할 수 있도록 함께 수록했습니다.

## 오디오와 시각 피드백

| 구현 | 코드와 범위 |
| --- | --- |
| 장면별 BGM과 음량 설정 | [AudioManager](../Source/Assets/Scripts/Audio/AudioManager.cs), [SceneBgm](../Source/Assets/Scripts/Audio/SceneBgm.cs), [GameSettingsPanel](../Source/Assets/Scripts/UI/Panels/GameSettingsPanel.cs) |
| 상호작용 대상의 외곽선 | [Outline.shader](../Source/Assets/Shaders/Outline.shader), [SpriteOutliner](../Source/Assets/Scripts/Utilities/SpriteOutliner.cs): 색상과 두께, 적용 상태 제어 |
| 말풍선 표시와 재사용 | [SpeechBubblePool](../Source/Assets/Scripts/UI/Dialogue/NPC/SpeechBubblePool.cs), [UISpeechBubbleController](../Source/Assets/Scripts/UI/Dialogue/NPC/UISpeechBubbleController.cs) |
| 마을 효과 연결 | [TownVFXController](../Source/Assets/Scripts/VFX/TownVFXController.cs), [VFXManager](../Source/Assets/Scripts/VFX/VFXManager.cs): 공동 효과 시스템의 화면 연동 |

렌더링과 효과 에셋 전체 제작이 아닌, 셰이더 구현과 게임 상태에 따른 표현 연결을 구분했습니다.
