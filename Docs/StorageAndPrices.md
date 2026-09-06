# 저장과 시세

[프로젝트](../README.md) / [전체 코드](CodeIndex.md)

## 저장할 상태와 직렬화 버퍼

인벤토리, 가격 효과, 가게와 튜토리얼 진행을 하나의 저장 데이터로 수집합니다. 저장 시 전체 JSON 문자열을 생성하던 경로를 바이너리 직렬화로 변경하고, 스냅샷 리스트와 출력 버퍼를 재사용하도록 개선했습니다.

코드는 다음 순서로 확인할 수 있습니다.

1. [SaveData](../Source/Assets/Scripts/Data/SaveData.cs): 저장 필드와 시스템별 모델
2. [SaveSnapshotList](../Source/Assets/Scripts/Data/SaveSnapshotList.cs): 기존 리스트 용량을 활용한 스냅샷 복사
3. [SaveLoadManager](../Source/Assets/Scripts/Data/SaveLoadManager.cs): 상태 수집, 직렬화, 파일 기록과 복원

버퍼 재사용은 반복 할당을 줄이는 대신 메모리를 유지합니다. 실제 저장 코드는 큰 버퍼를 계속 보유하지 않도록 4MiB 기준의 재설정 경로를 두었습니다.

## 저장 순서와 파일 교체

동시에 들어오는 저장과 로드 요청을 `SemaphoreSlim`으로 직렬 처리하도록 했습니다. 임시 파일에 기록하고 역직렬화로 검증한 뒤 기존 파일을 교체하며, 복구 파일을 읽는 경로를 함께 구성했습니다.

로드한 데이터는 대상 매니저가 준비되기 전에 적용하지 않도록 보류합니다. 가게와 인벤토리의 지연 적용 코루틴에서 준비 상태와 타임아웃을 확인할 수 있습니다. 요청 순서 제어, 파일 검증, 런타임 데이터 적용을 구분해 처리했습니다.

## 가격 계산과 캐시 수명

[RuntimeItemPriceManager](../Source/Assets/Scripts/Data/RuntimeItemPriceManager.cs)의 `GetCurrentPrice`에서 기본 가격, 특수 효과, 일반 효과를 순서대로 적용합니다. [ItemPriceKey](../Source/Assets/Scripts/Data/Database/KeyStructure/ItemPriceKey.cs)는 가격 항목과 마을을 묶어 조회 대상을 구분합니다.

[NormalEffectManager](../Source/Assets/Scripts/Data/NormalEffectManager.cs)는 효과가 바뀔 때 키별 배율을 갱신하고, 가격 조회에서는 계산된 배율을 사용하도록 구현했습니다. 효과 목록 순회를 조회마다 반복하지 않는 대신 캐시 메모리와 갱신 비용이 생깁니다. 효과 추가, 만료, 로드가 캐시 갱신으로 이어지는 경로를 함께 확인할 수 있습니다.

[EffectSaveData](../Source/Assets/Scripts/Data/EffectSaveData.cs)에는 효과 상태를 저장합니다. 로드 시 이를 복원해 조회용 캐시를 다시 구성하므로 최종 가격과 캐시를 중복 저장하지 않습니다. [SpecialEffectManager](../Source/Assets/Scripts/Data/SpecialEffectManager.cs)의 별도 효과 계산은 일반 효과 캐시와 구분했습니다.

가격 이력은 [PriceHistoryGraphDisplay](../Source/Assets/Scripts/UI/Graph/PriceHistoryGraphDisplay.cs)와 [PriceHistoryGraphRenderer](../Source/Assets/Scripts/UI/Graph/PriceHistoryGraphRenderer.cs)로 표시합니다. 그래프는 시세 변화의 표현이고, 캐시 성능은 아래 측정 자료로 확인합니다.

## 성능 측정 자료

| 자료 | 측정 범위 |
| --- | --- |
| [저장 비교 코드](../Source/Assets/Editor/SaveLoadBenchmarks/SaveLoadBenchmarkRunner.cs), [CSV](../Evidence/snapshot-pipeline-20260815-221944.csv) | 동일 저장 스냅샷을 86개 복제한 약 10MiB 바이너리 데이터, Editor, 워밍업 10회와 측정 100회 |
| [가격 조회 비교 코드](../Source/Assets/Editor/PriceLookupBenchmarks/PriceLookupBenchmarkRunner.cs), [CSV](../Evidence/PriceLookupBenchmark-20260816-191207.csv) | 합성 데이터에 대한 반복 조회, 직접 계산 경로와 캐시 경로 비교 |

저장 비교의 직렬화 구간에는 JSON용 데이터 변환과 문자열 생성 등이 포함됩니다. 라이브러리 단독 호출 비교나 전체 Save/Load 시간으로 해석하지 않습니다. `GC.Alloc`은 할당 이벤트 수이며 할당 바이트나 GC 수집 횟수가 아닙니다. 벤치마크의 워밍업 후 버퍼 유지 조건은 실제 저장 코드의 큰 버퍼 재설정 조건과 다릅니다.
