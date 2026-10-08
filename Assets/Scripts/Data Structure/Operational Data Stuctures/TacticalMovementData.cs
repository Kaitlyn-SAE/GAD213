using UnityEngine;

namespace TheaterStrategy.Tactical
{
    public enum TacticalFormation
    {
        Line,    // Maximum frontal firepower abreast
        Column,  // Single file road march / narrow streets
        Wedge,   // V-formation breakthrough assault
        Box      // 2x2 balanced defensive perimeter
    }

    public enum OrderType
    {
        Move,
        ReverseMove,
        AttackMove
    }

    [System.Serializable]
    public struct TacticalOrder
    {
        public OrderType orderType;
        public Vector3 destination;
        public Vector3 finalFacingDirection; // Direction to face upon arrival
    }
}