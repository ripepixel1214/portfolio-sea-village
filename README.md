# 바다마을

여러 마을을 항해하며 지역별 시세에 맞춰 물건을 거래하고, 직접 가게를 운영해 자산을 늘리는 2D 무역 경영 시뮬레이션입니다.

| 구분 | 내용 |
| --- | --- |
| 개발 | 2025.09~진행 중, 출시 예정 |
| 팀 | 6명 |
| 환경 | Unity 6, C# |
| 담당 | 저장과 경제 시스템, 가게 운영, 거래 UI, 튜토리얼, 데이터 제작 도구 |

## 주요 기여

- **저장 비용 개선:** JsonUtility 기반 저장 경로를 MemoryPack과 재사용 버퍼로 변경하고, 저장 요청의 순차 처리와 임시 파일 검증을 구현했습니다.
- **가격 조회 캐시:** 효과 변경 시 조회용 배율을 갱신하고, 저장에는 효과의 원인 상태를 남겨 로드 시 캐시를 재구성하도록 했습니다.
- **가게 운영:** 마을별 가게 상태, 직원 배치, 진열 재고와 판매 정산을 구현하고, 방문 중인 마을의 고객 구매와 다른 마을의 날짜별 판매 처리를 연결했습니다.
- **콘텐츠 제작과 진행:** 기획자가 사용하는 데이터 변환 도구를 구현하고, 튜토리얼 진행 판단을 입력과 화면 표시에서 분리했습니다.

## 구현 상세와 코드

| 확인할 내용 | 설명 |
| --- | --- |
| [저장과 시세](Docs/StorageAndPrices.md) | 스냅샷, 버퍼 수명, 저장 실패 처리, 캐시 갱신, 측정 자료 |
| [가게 운영과 거래](Docs/ShopSystems.md) | 직원, 재고, 판매 정산, 구매와 판매 UI, 제작과 교환 |
| [튜토리얼과 게임 진행](Docs/Progression.md) | 행동 신호, 진행 복원, 구제 조건, 이벤트와 날짜 정산 |
| [UI와 씬 연동](Docs/UIAndIntegration.md) | 패널 수명, 인벤토리 표시, 입력 연결, 오디오와 외곽선 |
| [데이터 제작과 빌드 도구](Docs/Tools.md) | Sheets 다운로드, CSV 검증과 에셋 변환, 자동 빌드 |
| [전체 코드 목록](Docs/CodeIndex.md) | 기능별 파일 탐색 |

## 플레이 영상

### 항해

<table width="100%"><tr><td width="15%"></td><td width="70%">

https://github.com/user-attachments/assets/525bc75e-0066-4749-9abe-5113c124387e

</td><td width="15%"></td></tr></table>

### 물건 구매

<table width="100%"><tr><td width="15%"></td><td width="70%">

https://github.com/user-attachments/assets/323db34d-44cc-488f-b7ee-f24b2a54848d

</td><td width="15%"></td></tr></table>

### 플레이어 가게 운영

<table width="100%"><tr><td width="15%"></td><td width="70%">

https://github.com/user-attachments/assets/7d8ccc65-6963-42f6-9d65-13b7b26946dc

</td><td width="15%"></td></tr></table>

[공개 범위와 권리 안내](NOTICE.md)
