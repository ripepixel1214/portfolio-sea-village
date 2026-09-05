using System;

namespace SeaVillage.Data
{
    /// <summary>
    /// 아이템 가격 조회용 기본키 구조체
    /// </summary>
    [Serializable]
    public struct ItemPriceKey : IEquatable<ItemPriceKey>
    {
        public int id;
        public string town;

        public ItemPriceKey(int id, string town)
        {
            this.id = id;
            this.town = town ?? string.Empty;
        }

        public bool Equals(ItemPriceKey other)
        {
            return id == other.id && town == other.town;
        }

        public override bool Equals(object obj)
        {
            return obj is ItemPriceKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (id * 397) ^ (town != null ? town.GetHashCode() : 0);
            }
        }

        public static bool operator ==(ItemPriceKey left, ItemPriceKey right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ItemPriceKey left, ItemPriceKey right)
        {
            return !left.Equals(right);
        }

        public override string ToString()
        {
            return $"({id}, {town})";
        }
    }
}