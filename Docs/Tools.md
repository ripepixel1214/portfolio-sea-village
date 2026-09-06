# 데이터 제작과 빌드 도구

[프로젝트](../README.md) / [전체 코드](CodeIndex.md)

## Sheets에서 게임 데이터까지

기획자가 관리하는 스프레드시트를 CSV로 받아 게임 데이터 에셋으로 변환하는 도구를 구현했습니다. 기획자와 팀원이 사용했으며, 데이터 변경 후 다운로드와 변환을 같은 작업 흐름에서 수행하도록 연결했습니다.

| 순서 | 코드 | 처리 |
| --- | --- | --- |
| 다운로드 | [GoogleSheetDownloader](../Source/Assets/Scripts/Data/GoogleSheetDownloader.cs) | 선택한 시트를 CSV로 수신, HTTP 실패와 HTML 응답 확인, 임시 파일 기록 후 교체 |
| 설정 | [GoogleSheetSettings](../Source/Assets/Scripts/Data/GoogleSheetSettings.cs), [SheetMapping](../Source/Assets/Scripts/Data/SheetMapping.cs) | 시트 이름과 CSV 파일 연결 |
| 파싱 | [EditorCsvReader](../Source/Assets/Scripts/Data/EditorCsvReader.cs), [TutorialCsvParser](../Source/Assets/Scripts/Data/TutorialCsvParser.cs) | CSV와 튜토리얼 행 해석 |
| 변환과 검증 | [DataConverter](../Source/Assets/Scripts/Data/DataConverter.cs), [DataValidator](../Source/Assets/Scripts/Data/DataValidator.cs) | 타입과 참조 검사, ScriptableObject 생성 |
| 게임에서 조회 | [DataManager](../Source/Assets/Scripts/Data/DataManager.cs), [Database](../Source/Assets/Scripts/Data/Database/) | 변환된 데이터 로드와 키 기반 접근 |

에디터 메뉴는 `SeaVillage > Google Sheet Downloader`와 `SeaVillage > Data Converter`입니다. 다운로드 후 자동 변환을 선택할 수 있습니다. 런타임은 변환된 에셋을 사용하므로 CSV 수정만으로 게임 데이터가 바뀌지는 않습니다.

튜토리얼은 전체 변환을 시작하기 전에 데이터와 실행 계약을 검사하고, 실패하면 기존 데이터가 교체되지 않도록 했습니다. 이는 모든 테이블을 하나의 원자적 작업으로 교체한다는 의미는 아닙니다.

## 자동 빌드와 산출물 배포

[빌드 워크플로](../Source/.github/workflows/build.yml)는 원본 저장소의 master push를 기준으로 GameCI를 실행하고, Windows 빌드 폴더 전체를 압축해 GitHub Release에 게시하도록 구성했습니다.

[BuildScript](../Source/Assets/Editor/BuildScript.cs)는 개발용과 배포용 빌드 옵션을 구분합니다. 씬 목록은 EditorBuildSettings의 활성 항목을 사용해 별도의 목록을 중복 관리하지 않도록 했습니다. 게임 서비스 업데이트가 아닌 빌드 산출물 배포 자동화입니다.

## 개발 중 확인 도구

[DeveloperUGUIController](../Source/Assets/Scripts/Core/DeveloperUGUIController.cs)는 개발 중 상태를 조작하는 화면 코드입니다. [저장 벤치마크](../Source/Assets/Editor/SaveLoadBenchmarks/)와 [가격 조회 벤치마크](../Source/Assets/Editor/PriceLookupBenchmarks/)는 측정 설정과 결과를 에디터에서 확인하는 도구이며, 조건과 결과 파일은 [저장과 시세](StorageAndPrices.md)에 연결했습니다.
