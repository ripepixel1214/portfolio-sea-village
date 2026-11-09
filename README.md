# 바다마을

여러 마을을 항해하며 지역별 시세에 맞춰 물건을 거래하고, 직접 가게를 운영해 자산을 늘리는 2D 무역 경영 시뮬레이션입니다.

<table align="center" width="100%">
  <tr>
    <th width="360">구분</th>
    <th width="840">내용</th>
  </tr>
  <tr>
    <td>개발</td>
    <td>2025.09~2026.09</td>
  </tr>
  <tr>
    <td>팀</td>
    <td>6명</td>
  </tr>
  <tr>
    <td>환경</td>
    <td>Unity 6, C# / Android</td>
  </tr>
  <tr>
    <td>담당</td>
    <td>저장과 경제 시스템, 가게 운영, 거래 UI, 튜토리얼, 데이터 제작 도구</td>
  </tr>
</table>

## 주요 기여

- **저장 직렬화:** JsonUtility로 JSON을 만들던 코드를 MemoryPack으로 바꾸고 출력 버퍼를 재사용했습니다. Unity Editor에서 약 10.1 MiB의 저장 데이터를 직렬화하는 데 걸린 평균 시간이 926ms에서 91.6ms로 줄었습니다. [측정 조건과 저장 코드](Docs/StorageAndPrices.md)
- **시세 이력과 가격 조회:** 마을과 품목별 최근 30개 가격을 순환 버퍼에 남겼습니다. 로드할 때는 저장해 둔 시세 효과로 가격 조회에 쓸 배율을 다시 계산합니다. [이력과 캐시 구조](Docs/StorageAndPrices.md)
- **가게 운영:** 마을별로 직원, 진열 재고와 정산금을 관리했습니다. 플레이어가 있는 마을에서는 고객이 사 간 물건을 재고와 매출에 반영하고, 다른 마을의 판매량은 날짜가 지날 때 계산합니다.
- **튜토리얼:** 이동이나 구매 같은 행동을 확인하는 코드를 화면 표시에서 분리했습니다. 로드하면 저장한 대사와 진행도에 맞춰 필요한 화면과 안내 연출을 다시 띄웁니다. [튜토리얼과 게임 진행](Docs/Progression.md)
- **데이터와 빌드:** 기획용 스프레드시트를 게임 데이터로 변환하는 에디터 도구를 만들었습니다. CI에서 Windows 개발 빌드와 Android 배포용 AAB, APK를 만듭니다. [데이터 변환과 빌드](Docs/Tools.md)

## 구현 상세와 코드

<table align="center" width="100%">
  <tr>
    <th width="360">기능</th>
    <th width="840">설명</th>
  </tr>
  <tr>
    <td><a href="Docs/StorageAndPrices.md">저장과 시세</a></td>
    <td>저장 속도 측정, 파일 교체, 시세 이력과 가격 조회</td>
  </tr>
  <tr>
    <td><a href="Docs/ShopSystems.md">가게 운영과 거래</a></td>
    <td>직원, 재고, 판매 정산, 구매와 판매 UI, 제작과 교환</td>
  </tr>
  <tr>
    <td><a href="Docs/Progression.md">튜토리얼과 게임 진행</a></td>
    <td>튜토리얼 진행과 복원, 난파 구제, 퀘스트와 항구 정산</td>
  </tr>
  <tr>
    <td><a href="Docs/UIAndIntegration.md">UI와 씬 연동</a></td>
    <td>화면 열기와 재사용, 인벤토리, 입력과 효과</td>
  </tr>
  <tr>
    <td><a href="Docs/Tools.md">데이터 제작과 빌드 도구</a></td>
    <td>Sheets 다운로드, CSV 검증과 에셋 변환, Windows와 Android 자동 빌드</td>
  </tr>
  <tr>
    <td><a href="Docs/CodeIndex.md">전체 코드 목록</a></td>
    <td>기능별 코드 파일</td>
  </tr>
  <tr>
    <td><a href="Docs/SourceMap.md">소스 출처</a></td>
    <td>원본 버전과 파일별 Git 기록</td>
  </tr>
</table>

## 플레이 영상

### 항해

<table align="center" width="100%"><tr><td width="1200" align="center">

https://github.com/user-attachments/assets/525bc75e-0066-4749-9abe-5113c124387e

</td></tr></table>

### 물건 구매

<table align="center" width="100%"><tr><td width="1200" align="center">

https://github.com/user-attachments/assets/323db34d-44cc-488f-b7ee-f24b2a54848d

</td></tr></table>

### 플레이어 가게 운영

<table align="center" width="100%"><tr><td width="1200" align="center">

https://github.com/user-attachments/assets/7d8ccc65-6963-42f6-9d65-13b7b26946dc

</td></tr></table>
