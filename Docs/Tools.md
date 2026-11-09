# 데이터 제작과 빌드 도구

[프로젝트](../README.md) / [전체 코드](CodeIndex.md)

## Sheets에서 게임 데이터까지

기획자가 시트를 고치면 Unity 에디터에서 CSV를 받아 게임 데이터로 바꿀 수 있도록 도구를 만들었습니다. 기획자와 팀원은 시트를 다시 내려받고 같은 도구에서 에셋으로 변환했습니다.

| 순서 | 코드 | 처리 |
| --- | --- | --- |
| 다운로드 | [GoogleSheetDownloader](../Source/Assets/Scripts/Data/GoogleSheetDownloader.cs) | 고른 시트를 CSV로 받고 HTTP 실패나 HTML 응답을 걸러낸 뒤 임시 파일로 교체 |
| 설정 | [GoogleSheetSettings](../Source/Assets/Scripts/Data/GoogleSheetSettings.cs), [SheetMapping](../Source/Assets/Scripts/Data/SheetMapping.cs) | 시트마다 저장할 CSV 파일 지정 |
| CSV 읽기 | [EditorCsvReader](../Source/Assets/Scripts/Data/EditorCsvReader.cs), [TutorialCsvParser](../Source/Assets/Scripts/Data/TutorialCsvParser.cs) | CSV 열과 튜토리얼 행 읽기 |
| 변환과 검증 | [DataConverter](../Source/Assets/Scripts/Data/DataConverter.cs), [DataValidator](../Source/Assets/Scripts/Data/DataValidator.cs) | 타입과 참조가 맞는지 확인하고 ScriptableObject 생성 |
| 게임에서 조회 | [DataManager](../Source/Assets/Scripts/Data/DataManager.cs), [Database](../Source/Assets/Scripts/Data/Database/) | 변환한 에셋을 로드하고 키로 데이터 조회 |

에디터에서 `SeaVillage > Google Sheet Downloader`와 `SeaVillage > Data Converter`를 엽니다. 다운로드할 때 바로 변환할 수도 있습니다. 게임은 변환된 에셋을 읽으므로 CSV를 고친 뒤 다시 변환해야 적용됩니다.

튜토리얼은 변환을 시작하기 전에 단계 데이터와 코드에서 요구하는 조건이 맞는지 검사합니다. 실패하면 기존 에셋을 유지합니다. 이 사전 검사는 튜토리얼에 적용하고 전체 테이블은 한 번의 트랜잭션으로 교체하지 않습니다.

## 자동 빌드와 산출물 배포

[빌드 워크플로](../Source/.github/workflows/build.yml)는 원본 저장소의 `master`에 푸시하거나 수동으로 실행하면 GameCI에서 Windows Development와 Android Release를 따로 빌드합니다. Android는 서명된 AAB를 먼저 만들고, 다음 작업에서 [APK 생성 스크립트](../Source/.github/scripts/build-universal-apk.sh)와 Bundletool로 범용 APK를 만듭니다. Windows ZIP, AAB, APK는 같은 GitHub Release에 첨부합니다.

[BuildScript](../Source/Assets/Editor/BuildScript.cs)는 선택한 플랫폼에 맞춰 빌드합니다. Android 배포 빌드 전에 설정과 서명을 확인하고, 씬은 EditorBuildSettings에서 활성화한 항목을 사용합니다.

## 개발 중 확인 도구

[DeveloperUGUIController](../Source/Assets/Scripts/Core/DeveloperUGUIController.cs)는 개발 중 게임 상태를 화면에서 바꿔 확인할 때 씁니다. [저장](../Source/Assets/Editor/SaveLoadBenchmarks/)과 [가격 조회](../Source/Assets/Editor/PriceLookupBenchmarks/) 벤치마크도 에디터에서 실행합니다. [측정 조건과 결과](StorageAndPrices.md)
