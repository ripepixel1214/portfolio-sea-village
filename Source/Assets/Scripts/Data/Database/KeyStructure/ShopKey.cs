using System;

namespace SeaVillage.Data
{
    /// <summary>
    /// 상점 조회용 키 구조체 (상점 ID + 아이템 ID)
    /// </summary>
    [Serializable]
    public struct ShopKey : IEquatable<ShopKey>
    {
        public int id;
        public int itemId;

        public ShopKey(int id, int itemId)
        {
            this.id = id;
            this.itemId = itemId;
        }

        public bool Equals(ShopKey other)
        {
            return id == other.id && itemId == other.itemId;
        }

        public override bool Equals(object obj)
        {
            return obj is ShopKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (id * 397) ^ itemId;
            }
        }

        public static bool operator ==(ShopKey left, ShopKey right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ShopKey left, ShopKey right)
        {
            return !left.Equals(right);
        }

        public override string ToString()
        {
            return $"({id}, {itemId})";
        }
    }
}
