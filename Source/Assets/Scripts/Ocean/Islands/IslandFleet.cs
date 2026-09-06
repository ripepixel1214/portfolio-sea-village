#nullable enable
using UnityEngine;
using SeaVillage.Core;
using SeaVillage.UI;
using SeaVillage.Utilities;

namespace SeaVillage.Ocean
{
    public sealed class IslandFleet
    {
        private readonly Ship ship;
        private readonly Island[] islands;
        private readonly OceanSceneConfig config;

        public IslandFleet(Ship ship, Island[] islands, OceanSceneConfig config)
        {
            this.ship = ship != null ? ship : throw new System.ArgumentNullException(nameof(ship));
            this.islands = islands ?? throw new System.ArgumentNullException(nameof(islands));
            this.config = config != null ? config : throw new System.ArgumentNullException(nameof(config));
        }

        public static int CurrentShipLevel()
            => InventoryManager.HasInstance ? InventoryManager.Instance.ShipLevel : 0;

        public static bool IsBlocked(Island island, int shipLevel)
            => island.RequiredShipLevel > shipLevel;

        public static string BlockMessage(Island island)
            => $"이 섬에 가려면 더 높은 등급의 배가 필요합니다. (필요 등급 Lv.{island.RequiredShipLevel})";

        public void PlaceShipAtStart()
        {
            string targetName = VoyageSession.ResolveOrDefault(config.DefaultIslandName);

            Island? target = FindByName(targetName);
            if (target == null)
            {
                Debug.LogWarning($"[IslandFleet] Island '{targetName}' 찾지 못함. Ship 스폰 생략");
                return;
            }

            ship.WarpTo(target.SpawnPosition);
        }

        public void Tick()
        {
            foreach (Island island in islands)
            {
                island.UpdateRegion(ship);
            }
        }

        public bool TryEnter()
        {
            Island? target = FindEnterable();
            if (target == null)
            {
                return false;
            }

            if (IsBlocked(target, CurrentShipLevel()))
            {
                if (UIManager.HasInstance)
                    UIManager.Instance.ShowAlertMessage(BlockMessage(target));
                return true;
            }

            GoToScene(target.IslandName);
            return true;
        }

        private Island? FindEnterable()
        {
            Vector2 shipPos = ship.WorldPosition;
            Island? best = null;
            float bestDistance = float.MaxValue;

            foreach (Island island in islands)
            {
                if (!island.Contains(shipPos))
                {
                    continue;
                }

                float distance = ship.DistanceTo(island);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = island;
                }
            }

            return best;
        }

        public void GoToScene(string sceneName)
        {
            SceneChanger sceneChanger = SceneChanger.Instance;
            if (sceneChanger == null || sceneChanger.IsTransitioning)
            {
                return;
            }

            if (!sceneChanger.IsSceneInBuildSettings(sceneName))
            {
                Debug.LogWarning($"[IslandFleet] 씬 '{sceneName}' 이 Build Settings 에 없음. 전환 생략");
                return;
            }

            VoyageSession.SetLastVisited(sceneName);
            sceneChanger.ChangeScene(sceneName);
        }

        private Island? FindByName(string islandName)
        {
            foreach (Island island in islands)
            {
                if (island.IslandName == islandName)
                {
                    return island;
                }
            }
            return null;
        }
    }
}
