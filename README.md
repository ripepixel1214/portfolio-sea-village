# 바다마을

여러 마을을 항해하며 지역별 시세에 맞춰 물건을 거래하고, 직접 가게를 운영해 자산을 늘리는 2D 무역 경영 시뮬레이션입니다.

| 구분 | 내용 |
| --- | --- |
| 개발 | 2025.09 시작, 개발 및 개선 중 |
| 팀 | 6명 |
| 환경 | Unity 6, C# |
| 담당 | 저장과 데이터 관리, 경제 시스템, 튜토리얼, UI 연동, 에디터 도구, 자동 빌드, 렌더링 피드백 |

## 플레이 영상

### 항해

<table width="100%"><tr><td width="15%"></td><td width="70%">

https://github.com/user-attachments/assets/525bc75e-0066-4749-9abe-5113c124387e

</td><td width="15%"></td></tr></table>

### 물건 구매

<table width="100%"><tr><td width="15%"></td><td width="70%">

https://github.com/user-attachments/assets/323db34d-44cc-488f-b7ee-f24b2a54848d

</td><td width="15%"></td></tr></table>

## 핵심 구현

### 1. MemoryPack과 재사용 버퍼로 저장 직렬화 비용 감소

저장할 상태가 늘어날수록 전체 JSON 문자열을 만드는 비용과 임시 메모리 할당이 함께 증가했습니다. 파일 쓰기만 비동기로 바꾸는 것으로는 직렬화 비용이 남기 때문에 바이너리 직렬화로 전환하고, 직렬화 버퍼를 재사용하도록 개선했습니다.

동시에 저장 요청을 직렬 처리하고, 임시 파일을 검증한 뒤 기존 파일을 교체하도록 구성했습니다. 성능 개선과 저장 도중 실패했을 때의 파일 처리 경계를 함께 다룬 사례입니다.

- [SaveLoadManager.cs](Source/Assets/Scripts/Data/SaveLoadManager.cs)
- [저장 데이터](Source/Assets/Scripts/Data/SaveData.cs), [리스트 스냅샷 복사](Source/Assets/Scripts/Data/SaveSnapshotList.cs)
- [저장 성능 비교 코드](Source/Assets/Editor/SaveLoadBenchmarks/SaveLoadBenchmarkRunner.cs)

### 2. 가격 변동의 원인 상태와 조회용 캐시 분리

가격에 적용되는 일반 효과를 조회할 때마다 다시 계산하지 않도록, 효과가 변경되는 시점에 키별 배율을 재구성했습니다. 저장에는 효과의 원인 상태를 남기고, 불러올 때 조회용 캐시를 다시 만들어 중복 상태를 관리하지 않도록 했습니다.

- [최종 가격 계산](Source/Assets/Scripts/Data/RuntimeItemPriceManager.cs)
- [일반 효과와 캐시](Source/Assets/Scripts/Data/NormalEffectManager.cs), [특수 효과](Source/Assets/Scripts/Data/SpecialEffectManager.cs)
- [캐시 성능 비교 코드](Source/Assets/Editor/PriceLookupBenchmarks/PriceLookupBenchmarkRunner.cs), [측정 결과](Evidence/PriceLookupBenchmark-20260816-191207.csv)
- [가격 이력 표시](Source/Assets/Scripts/UI/Graph/PriceHistoryGraphDisplay.cs), [그래프 렌더링](Source/Assets/Scripts/UI/Graph/PriceHistoryGraphRenderer.cs)

### 3. 서로 다른 게임 행동을 공통 신호로 변환한 튜토리얼 진행

이동, 구매, 장면 전환처럼 서로 다른 시스템의 행동을 `TutorialEvent`로 변환하고, 현재 단계가 요구하는 신호를 순서대로 만족할 때만 진행하도록 구현했습니다. 진행 판단을 담당하는 런타임과 게임 시스템을 연결하는 제어부, 화면 표시와 입력 제한을 분리하여 튜토리얼 연출이 게임 로직에 직접 의존하지 않도록 구성했습니다.

완료 단계와 현재 대사, 조건 진행도를 저장하고 복원하며, 데이터 참조 누락과 잘못된 단계 연결은 에디터 검증에서 확인하도록 했습니다.

- [진행 상태와 신호 판정](Source/Assets/Scripts/Core/Tutorial/TutorialRuntime.cs)
- [게임 시스템 이벤트 연결](Source/Assets/Scripts/Core/Tutorial/TutorialController.cs), [공통 이벤트 정의](Source/Assets/Scripts/Core/Tutorial/TutorialEvents.cs)
- [화면 표시](Source/Assets/Scripts/UI/Tutorial/TutorialPresentationController.cs), [입력 제한](Source/Assets/Scripts/UI/Tutorial/TutorialInputMask.cs)
- [데이터 검증](Source/Assets/Editor/Tutorial/TutorialDefinitionValidator.cs), [호환성 테스트](Source/Assets/Editor/Tutorial/Tests/TutorialLegacyContractTests.cs)

## 제작과 배포 도구

기획 데이터를 게임 데이터 에셋으로 변환하는 에디터 도구와 검증 로직을 구현했습니다. 기획자와 팀원이 사용했으며, 데이터 수정 결과를 게임에 반영하는 과정에서 참조와 값의 오류를 확인하도록 했습니다.

- [데이터 변환 도구](Source/Assets/Scripts/Data/DataConverter.cs)
- [데이터 검증](Source/Assets/Scripts/Data/DataValidator.cs)
- [자동 빌드 워크플로](Source/.github/workflows/build.yml), [Unity 빌드 진입점](Source/Assets/Editor/BuildScript.cs)

## 추가 구현

상호작용 가능한 오브젝트의 가시성을 높이기 위해 외곽선 색상과 두께를 조절할 수 있는 URP 셰이더와 적용 컴포넌트를 구현했습니다.

- [외곽선 셰이더](Source/Assets/Shaders/Outline.shader)
- [외곽선 적용 컴포넌트](Source/Assets/Scripts/Utilities/SpriteOutliner.cs)
