using System;
using UnityEngine;

namespace Rill.Core
{
    public enum SecretKind
    {
        Fossil = 0,
        Ruin = 1,
        Spring = 2,
        CaveMouth = 3,
        Geode = 4
    }

    /// <summary>
    /// Something buried in the rock. You cannot dig for it. You can only route water over it,
    /// run after run, and watch the ground lower toward the reveal.
    /// </summary>
    [Serializable]
    public class SecretSite
    {
        public int Cell;                 // grid index
        public float RevealElevation;    // revealed once Height[Cell] <= this
        public SecretKind Kind;
        public bool Revealed;
        public int RevealedOnRun = -1;

        /// <summary>Springs and cave mouths re-plumb the mountain when they open.</summary>
        public bool ChangesPlumbing => Kind == SecretKind.Spring || Kind == SecretKind.CaveMouth;

        public string DisplayName
        {
            get
            {
                switch (Kind)
                {
                    case SecretKind.Fossil: return "Fossil";
                    case SecretKind.Ruin: return "Ruin";
                    case SecretKind.Spring: return "Hidden spring";
                    case SecretKind.CaveMouth: return "Cave mouth";
                    default: return "Geode";
                }
            }
        }

        public Color Tint
        {
            get
            {
                switch (Kind)
                {
                    case SecretKind.Fossil: return new Color(0.94f, 0.90f, 0.72f);
                    case SecretKind.Ruin: return new Color(0.78f, 0.74f, 0.66f);
                    case SecretKind.Spring: return new Color(0.55f, 0.92f, 0.95f);
                    case SecretKind.CaveMouth: return new Color(0.16f, 0.14f, 0.18f);
                    default: return new Color(0.72f, 0.55f, 0.95f);
                }
            }
        }
    }
}
