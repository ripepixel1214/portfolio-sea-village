# 가게 운영과 거래

[프로젝트](../README.md) / [전체 코드](CodeIndex.md)

## 마을별 가게 상태

마을을 떠나도 직원, 진열 재고와 정산 내역이 유지돼야 하므로, 화면 오브젝트가 아닌 [PlayerShopManager](../Source/Assets/Scripts/Core/PlayerShop/PlayerShopManager.cs)에서 마을별 상태를 관리하도록 했습니다. 외부에는 읽기 전용 상태를 제공하고 변경은 건설, 업그레이드, 직원 배치, 진열 명령으로 요청하도록 구성했습니다.

- [PlayerShopStateByTown](../Source/Assets/Scripts/Core/PlayerShop/PlayerShopStateByTown.cs): 마을별 가게 상태
- [PlayerShopStateReadOnly](../Source/Assets/Scripts/Core/PlayerShop/PlayerShopStateReadOnly.cs): 외부 조회 경계
- [PlayerShopTownProcessor](../Source/Assets/Scripts/Core/PlayerShop/PlayerShopTownProcessor.cs): 가게 구매와 업그레이드 조건
- [가게 UI](../Source/Assets/Scripts/UI/Panels/PlayerShop/): 건설, 진열, 재고와 직원 화면

## 직원 고용과 배치

[StaffProfile](../Source/Assets/Scripts/Core/PlayerShop/StaffProfile.cs)의 고용 상태와 마을별 역할 배치를 분리했습니다. `TryHireStaff`, `TryAssignStaff`에서 비용과 배치 조건을 확인하고, [StaffRules](../Source/Assets/Scripts/Core/PlayerShop/StaffRules.cs)에서 역할별 효과를 계산하도록 했습니다. 직원 화면은 Manager API를 통해 상태를 변경하도록 연결했습니다.

## 판매 확정과 정산

`TryRecordSaleBatch`에서 한 고객의 구매 항목을 아이템별로 합산한 뒤 재고, 구매 수량과 누적 금액 범위를 검사합니다. 검사가 끝나면 재고와 판매 내역을 반영하고 화면에 변경을 알리도록 했습니다. 반복 항목은 Dictionary로 합치지만 거래 전체에는 항목 순회와 검증이 필요합니다.

플레이어가 있는 마을에서는 고객 구매 결과를 가게에 반영하고, 다른 마을은 `HandleDayChanged`와 `SimulateOneDay`에서 미처리 날짜의 판매를 계산합니다. 마지막 처리 날짜를 기준으로 진행하며, 정산금 수령은 `TryCollectSettlement`로 분리했습니다.

- [판매 요청](../Source/Assets/Scripts/Core/PlayerShop/PlayerShopSaleRequest.cs), [판매 결과](../Source/Assets/Scripts/Core/PlayerShop/PlayerShopSaleResult.cs)
- [정산 내역](../Source/Assets/Scripts/Core/PlayerShop/PlayerShopSalesRecord.cs), [정산 화면](../Source/Assets/PlayerShopSettlementPanel.cs)
- [고객 구매 연동](../Source/Assets/Scripts/NPC/Customer/Purchase/CustomerPurchaseLogic.cs): 팀 공동 모듈의 가게 판매 연결 코드

고객 AI와 이동 전체가 아닌 가게 상태와 구매 결과의 연결을 담당했습니다.

## 물건 구매와 판매 UI

[ShopModel](../Source/Assets/Scripts/UI/Panels/Shop/MVP/ShopModel.cs)에 원본 재고와 선택 수량을 분리했습니다. 수량을 바꿀 때 허용 재고와 구매 무게를 검사하고 `OnDataChanged`로 표시를 갱신합니다. [PurchasePresenter](../Source/Assets/Scripts/UI/Panels/Shop/MVP/PurchasePresenter.cs)와 [SellPresenter](../Source/Assets/Scripts/UI/Panels/Shop/MVP/SellPresenter.cs)가 각각 구매와 판매 화면을 연결합니다.

공통 모델을 사용하되 구매의 무게 제한과 판매의 출처 인벤토리처럼 서로 다른 조건은 유지했습니다.

## 제작, 교환과 강화

화면 입력과 실행 규칙을 분리해 패널에서 처리 결과와 실패 사유를 받아 표시하도록 했습니다.

| 기능 | 실행 코드 |
| --- | --- |
| 재료 확인, 소모와 결과물 지급 | [RecipeCraftProcessor](../Source/Assets/Scripts/Core/Services/SpecialShop/RecipeCraftProcessor.cs) |
| 아이템 교환 | [ExchangeProcessor](../Source/Assets/Scripts/Core/Services/SpecialShop/ExchangeProcessor.cs) |
| 강화 조건과 결과 적용 | [EnhancementProcessor](../Source/Assets/Scripts/Core/Services/SpecialShop/EnhancementProcessor.cs) |
| 선박 업그레이드 | [ShipUpgradeProcessor](../Source/Assets/Scripts/Core/Services/SpecialShop/ShipUpgradeProcessor.cs) |
| 공통 아이템 소모와 복구 | [InventoryTransaction](../Source/Assets/Scripts/Core/Services/SpecialShop/InventoryTransaction.cs) |
| 보상 해금과 지급 | [DollUnlockPolicy](../Source/Assets/Scripts/Core/Services/SpecialShop/DollUnlockPolicy.cs), [DollRewardProcessor](../Source/Assets/Scripts/Core/Services/SpecialShop/DollRewardProcessor.cs) |

제작 코드에는 재료 소모 도중 또는 결과물 지급 실패 시 이미 소모한 재료를 되돌리는 경로를 구현했습니다. 이는 게임 내 실패 처리이며 데이터베이스 수준의 트랜잭션 보장을 뜻하지 않습니다.
