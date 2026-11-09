# 가게 운영과 거래

[프로젝트](../README.md) / [전체 코드](CodeIndex.md)

## 마을별 가게 상태

마을을 떠나도 직원, 진열 재고와 정산 내역이 남아 있어야 했습니다. 화면을 닫아도 상태를 유지하도록 [PlayerShopManager](../Source/Assets/Scripts/Core/PlayerShop/PlayerShopManager.cs)에 마을별 데이터를 뒀습니다. 화면은 상태를 읽고 건설, 업그레이드, 직원 배치나 진열을 매니저에 요청합니다.

- [PlayerShopStateByTown](../Source/Assets/Scripts/Core/PlayerShop/PlayerShopStateByTown.cs): 마을별 가게 상태
- [PlayerShopStateReadOnly](../Source/Assets/Scripts/Core/PlayerShop/PlayerShopStateReadOnly.cs): 화면에서 가게 상태를 읽을 때 사용
- [PlayerShopTownProcessor](../Source/Assets/Scripts/Core/PlayerShop/PlayerShopTownProcessor.cs): 가게 구매와 업그레이드 조건
- [가게 UI](../Source/Assets/Scripts/UI/Panels/PlayerShop/): 건설, 진열, 재고와 직원 화면

## 직원 고용과 배치

직원 고용은 [StaffProfile](../Source/Assets/Scripts/Core/PlayerShop/StaffProfile.cs)에 기록하고, 어느 마을에서 어떤 역할을 맡는지는 따로 관리합니다. 직원 화면에서 `TryHireStaff`나 `TryAssignStaff`로 요청하면 매니저가 비용과 배치 조건을 확인합니다. 배치 효과는 [StaffRules](../Source/Assets/Scripts/Core/PlayerShop/StaffRules.cs)에서 계산합니다.

## 판매 확정과 정산

한 고객이 같은 품목을 여러 번 사면 `TryRecordSaleBatch`에서 Dictionary로 수량을 합칩니다. 전체 구매 항목의 재고, 수량과 누적 금액이 유효한지 확인한 뒤 재고와 판매 내역을 바꾸고 화면에 알립니다.

플레이어가 있는 마을은 고객이 구매할 때 판매를 기록합니다. 다른 마을은 날짜가 바뀔 때 `HandleDayChanged`와 `SimulateOneDay`에서 마지막으로 처리한 날 이후의 판매를 계산합니다. 정산금은 `TryCollectSettlement`로 받습니다.

- [판매 요청](../Source/Assets/Scripts/Core/PlayerShop/PlayerShopSaleRequest.cs), [판매 결과](../Source/Assets/Scripts/Core/PlayerShop/PlayerShopSaleResult.cs)
- [정산 내역](../Source/Assets/Scripts/Core/PlayerShop/PlayerShopSalesRecord.cs), [정산 화면](../Source/Assets/PlayerShopSettlementPanel.cs)
- [고객 구매 연동](../Source/Assets/Scripts/NPC/Customer/Purchase/CustomerPurchaseLogic.cs): 고객이 구매한 물건을 가게 재고와 판매 내역에 반영

팀의 고객 코드에서 가게 상태와 구매 결과를 연결하는 부분을 맡았습니다.

이벤트에서 상점 NPC가 필요하면 [EventNpcRuntime](../Source/Assets/Scripts/Event/Runtime/EventNpcRuntime.cs)이 [상점 배치 지점](../Source/Assets/Scripts/Event/Runtime/EventNpcShopSpawnPoint.cs)에 맞는 NPC를 준비합니다. 외형과 말풍선 위치는 [EventNpcShopVisual](../Source/Assets/Scripts/Event/Runtime/EventNpcShopVisual.cs)에서 정합니다.

## 물건 구매와 판매 UI

[ShopModel](../Source/Assets/Scripts/UI/Panels/Shop/MVP/ShopModel.cs)에서 현재 재고와 플레이어가 고른 수량을 따로 관리했습니다. 수량이 바뀌면 재고 한도와 구매 무게를 확인하고 `OnDataChanged`로 화면을 갱신합니다.

[PurchasePresenter](../Source/Assets/Scripts/UI/Panels/Shop/MVP/PurchasePresenter.cs)와 [SellPresenter](../Source/Assets/Scripts/UI/Panels/Shop/MVP/SellPresenter.cs)는 같은 모델을 씁니다. 구매는 무게 한도를 확인하고, 판매는 물건을 가져올 인벤토리를 지정합니다.

## 제작, 교환과 강화

화면에서 받은 입력은 아래 실행 코드에 넘깁니다. 처리가 끝나면 화면에서 결과나 실패 이유를 보여줍니다.

| 기능 | 실행 코드 |
| --- | --- |
| 재료 확인, 소모와 결과물 지급 | [RecipeCraftProcessor](../Source/Assets/Scripts/Core/Services/SpecialShop/RecipeCraftProcessor.cs) |
| 아이템 교환 | [ExchangeProcessor](../Source/Assets/Scripts/Core/Services/SpecialShop/ExchangeProcessor.cs) |
| 강화 조건과 결과 적용 | [EnhancementProcessor](../Source/Assets/Scripts/Core/Services/SpecialShop/EnhancementProcessor.cs) |
| 선박 업그레이드 | [ShipUpgradeProcessor](../Source/Assets/Scripts/Core/Services/SpecialShop/ShipUpgradeProcessor.cs) |
| 공통 아이템 소모와 복구 | [InventoryTransaction](../Source/Assets/Scripts/Core/Services/SpecialShop/InventoryTransaction.cs) |
| 보상 해금과 지급 | [DollUnlockPolicy](../Source/Assets/Scripts/Core/Services/SpecialShop/DollUnlockPolicy.cs), [DollRewardProcessor](../Source/Assets/Scripts/Core/Services/SpecialShop/DollRewardProcessor.cs) |

재료를 빼는 도중 문제가 생기거나 결과물을 줄 수 없으면, 제작에 이미 쓴 재료를 돌려주게 했습니다.
