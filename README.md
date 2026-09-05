# 바다마을

마을을 오가며 물건을 거래하고, 변화하는 시세에 맞춰 상점을 운영하는 Unity 기반 무역 경영 시뮬레이션입니다.

저는 저장 시스템, 시세 계산과 데이터 관리, 기획 데이터 변환 도구를 중심으로 개발했습니다. 이 저장소에는 해당 구현을 검토할 수 있는 코드와 기술 설명을 정리했습니다.

| 구분 | 내용 |
| --- | --- |
| 개발 | 2025.09 시작, 개발 및 개선 중 |
| 팀 | 6명 |
| 환경 | Unity 6, C# |
| 담당 | 저장과 데이터 관리, 경제 시스템, 튜토리얼, UI 연동, 에디터 도구, 자동 빌드 |
| 공개 범위 | 핵심 소스와 벤치마크 코드 발췌, 전체 실행 프로젝트 제외 |

## 핵심 구현

### 1. MemoryPack과 재사용 버퍼로 저장 직렬화 비용 감소

저장할 상태가 늘어날수록 전체 JSON 문자열을 만드는 비용과 임시 메모리 할당이 함께 증가했습니다. 파일 쓰기만 비동기로 바꾸는 것으로는 직렬화 비용이 남기 때문에 바이너리 직렬화로 전환하고, 직렬화 버퍼를 재사용하도록 개선했습니다.

동시에 저장 요청을 직렬 처리하고, 임시 파일을 검증한 뒤 기존 파일을 교체하도록 구성했습니다. 성능 개선과 저장 도중 실패했을 때의 파일 처리 경계를 함께 다룬 사례입니다.

- [기술 설명과 측정 범위](Docs/SaveSystem.md)
- [SaveLoadManager.cs](Source/Assets/Scripts/Data/SaveLoadManager.cs)
- [저장 데이터](Source/Assets/Scripts/Data/SaveData.cs), [리스트 스냅샷 복사](Source/Assets/Scripts/Data/SaveSnapshotList.cs)

### 2. 가격 변동의 원인 상태와 조회용 캐시 분리

가격에 적용되는 일반 효과를 조회할 때마다 다시 계산하지 않도록, 효과가 변경되는 시점에 키별 배율을 재구성했습니다. 저장에는 효과의 원인 상태를 남기고, 불러올 때 조회용 캐시를 다시 만들어 중복 상태를 관리하지 않도록 했습니다.

캐싱한 범위는 일반 효과의 배율 계산입니다. 최종 가격 계산 전체를 상수 시간으로 바꿨다고 주장하지 않습니다.

- [계산 흐름과 캐시 갱신](Docs/PriceSimulation.md)
- [최종 가격 계산](Source/Assets/Scripts/Data/RuntimeItemPriceManager.cs)
- [일반 효과와 캐시](Source/Assets/Scripts/Data/NormalEffectManager.cs), [특수 효과](Source/Assets/Scripts/Data/SpecialEffectManager.cs)

## 함께 구현한 제작 도구

기획 데이터를 게임 데이터 에셋으로 변환하는 에디터 도구와 검증 로직을 구현했습니다. 기획자와 팀원이 사용했으며, 데이터 수정 결과를 게임에 반영하는 과정에서 참조와 값의 오류를 확인하도록 했습니다.

- [데이터 변환 도구](Source/Assets/Scripts/Data/DataConverter.cs)
- [데이터 검증](Source/Assets/Scripts/Data/DataValidator.cs)
- [빌드 진입점](Source/Assets/Editor/BuildScript.cs)

## 코드 확인 안내

원본 게임에 의존하는 발췌본이므로 이 저장소만으로 게임을 빌드하거나 벤치마크를 실행할 수는 없습니다. 저장과 캐시 벤치마크는 각각의 실험 브랜치에서 가져왔으며, 런타임 코드와 기준 커밋이 다릅니다. 측정 결과와 한계는 각 기술 문서에 구분했습니다.

[파일별 원본 경로와 기준 커밋](Docs/SourceMap.md), [공개 범위와 권리 안내](NOTICE.md)

<!-- DEMO_VIDEO: 촬영 완료 후 실제 영상 링크 삽입 -->
