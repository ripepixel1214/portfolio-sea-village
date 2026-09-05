# 바다마을

여러 마을을 항해하며 지역별 시세에 맞춰 물건을 거래하고, 직접 가게를 운영해 자산을 늘리는 2D 무역 경영 시뮬레이션입니다.

| 구분 | 내용 |
| --- | --- |
| 개발 | 2025.09 시작, 개발 및 개선 중 |
| 팀 | 6명 |
| 환경 | Unity 6, C# |
| 담당 | 저장과 데이터 관리, 경제 시스템, 튜토리얼, UI 연동, 에디터 도구, 자동 빌드 |

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

## 함께 구현한 제작 도구

기획 데이터를 게임 데이터 에셋으로 변환하는 에디터 도구와 검증 로직을 구현했습니다. 기획자와 팀원이 사용했으며, 데이터 수정 결과를 게임에 반영하는 과정에서 참조와 값의 오류를 확인하도록 했습니다.

- [데이터 변환 도구](Source/Assets/Scripts/Data/DataConverter.cs)
- [데이터 검증](Source/Assets/Scripts/Data/DataValidator.cs)
- [빌드 진입점](Source/Assets/Editor/BuildScript.cs)
