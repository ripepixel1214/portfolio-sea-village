# 저장과 시세

[프로젝트](../README.md) / [전체 코드](CodeIndex.md)

## 저장할 상태와 직렬화 버퍼

인벤토리, 가격 효과, 가게 상태와 튜토리얼 진행도를 한 번에 저장합니다. 저장할 때마다 JSON 문자열을 만들던 코드를 MemoryPack으로 바꿨습니다. 상태를 복사하는 스냅샷 리스트와 직렬화 결과를 쓰는 버퍼는 다음 저장에도 씁니다.

1. [SaveData](../Source/Assets/Scripts/Data/SaveData.cs): 게임 상태를 저장하는 데이터
2. [SaveSnapshotList](../Source/Assets/Scripts/Data/SaveSnapshotList.cs): 기존 리스트에 저장할 상태 복사
3. [SaveLoadManager](../Source/Assets/Scripts/Data/SaveLoadManager.cs): 상태 수집부터 파일 기록, 복원까지

버퍼를 다시 쓰면 매번 할당할 필요가 줄지만, 한 번 커진 용량이 계속 남습니다. 파일 기록을 마치고 버퍼를 정리할 때 용량이 4MiB를 넘으면 새 버퍼로 바꿨습니다.

## 저장 순서와 파일 교체

직렬화 결과인 `ReadOnlyMemory<byte>`는 재사용 버퍼를 가리킵니다. 파일에 쓰는 동안 다음 요청이 버퍼를 덮어쓰지 않도록 [SaveLoadManager](../Source/Assets/Scripts/Data/SaveLoadManager.cs)에서 `SemaphoreSlim`으로 저장과 로드 요청을 하나씩 실행하게 했습니다. 파일 기록과 검증, 버퍼 정리가 끝나면 잠금을 풉니다.

임시 파일의 길이와 역직렬화 결과를 확인하고 기존 파일을 교체합니다. 교체 중 예외가 났을 때 원래 경로에 파일이 없고 `.bak`가 남아 있으면 백업을 원래 위치로 되돌립니다. 로드는 원래 저장 경로를 읽습니다.

가게와 인벤토리 데이터는 각 매니저가 준비된 뒤 적용합니다. 코루틴에서 준비 여부를 확인하고 기다리는 시간에도 한도를 뒀습니다.

## 가격 계산과 배율 캐시

[RuntimeItemPriceManager](../Source/Assets/Scripts/Data/RuntimeItemPriceManager.cs)의 `GetCurrentPrice`에서 기본 가격에 특수 효과, 일반 효과를 차례로 적용합니다. [ItemPriceKey](../Source/Assets/Scripts/Data/Database/KeyStructure/ItemPriceKey.cs)로 품목과 마을을 묶어 조회합니다.

가격을 읽을 때마다 일반 효과를 전부 순회하지 않도록 [NormalEffectManager](../Source/Assets/Scripts/Data/NormalEffectManager.cs)에 키별 배율을 캐시했습니다. 효과가 바뀔 때 캐시를 갱신하는 비용과 배율을 보관할 메모리가 듭니다. 특수 효과의 배율은 [SpecialEffectManager](../Source/Assets/Scripts/Data/SpecialEffectManager.cs)가 따로 계산합니다.

[EffectSaveData](../Source/Assets/Scripts/Data/EffectSaveData.cs)에 효과 상태만 남기고, 조회용 배율은 로드할 때 다시 계산합니다. 현재 가격과 그 계산에 쓰는 캐시를 중복해서 저장할 필요가 없었습니다.

## 최근 시세 이력

[ItemPriceData](../Source/Assets/Scripts/Data/Database/DataStructures.cs#L48)에 마을과 품목별 최근 30개 가격을 순환 버퍼로 남겼습니다. 버퍼가 차면 가장 오래된 가격부터 덮어씁니다. 배열과 `HistoryHead`, `HistoryTail`, `HistoryCount`를 함께 저장해 로드한 뒤에도 기록 순서가 이어집니다.

[시세 그래프](../Source/Assets/Scripts/UI/Graph/PriceHistoryGraphDisplay.cs)는 버퍼에 남은 가격을 읽고, 현재 가격 조회에는 효과 배율을 씁니다.

## 성능 측정 자료

| 자료 | 측정 범위와 결과 |
| --- | --- |
| [저장 비교 코드](../Source/Assets/Editor/SaveLoadBenchmarks/SaveLoadBenchmarkRunner.cs), [CSV](../Evidence/snapshot-pipeline-20260815-221944.csv) | Unity Editor, 동일 스냅샷 86개를 복제한 약 10.1 MiB 데이터, 워밍업 10회 후 100회 측정. 직렬화 구간 평균은 JsonUtility 기반 925.99ms, MemoryPack과 재사용 버퍼 91.63ms |
| [가격 조회 비교 코드](../Source/Assets/Editor/PriceLookupBenchmarks/PriceLookupBenchmarkRunner.cs), [CSV](../Evidence/PriceLookupBenchmark-20260816-191207.csv) | 합성 데이터에 대한 반복 조회, 직접 계산 경로와 캐시 경로 비교 |

이 수치는 파일을 쓰기 전의 직렬화 시간입니다. JSON용 데이터 변환과 문자열 생성은 포함하고 디스크 기록과 전체 Save/Load 시간은 뺐습니다. `GC.Alloc`은 할당 이벤트 수입니다. 벤치마크는 실제 저장과 달리 워밍업 때 확보한 버퍼 용량을 유지한 채 측정했습니다.
