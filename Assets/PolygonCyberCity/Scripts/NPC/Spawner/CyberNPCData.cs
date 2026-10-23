using System;
using UnityEngine;

namespace PolygonCyberCity.NPC.Spawner
{
    public enum NPCType
    {
        Civilian,
        StreetThug,
        Mercenary,
        RichCitizen,
        Cyborg,
        HelperBot
    }

    public enum NPCFaction
    {
        Civilians,
        ArasakaSecurity,
        TygerClaws,
        Maelstrom,
        TraumaTeam,
        Moxes,
        ServiceRobotics
    }

    public enum ThreatRating
    {
        Low,
        Moderate,
        High,
        Lethal
    }

    [System.Serializable]
    public struct ScannerProfile
    {
        public string CharacterName;
        public string Cyberware;
        public NPCFaction Faction;
        public ThreatRating Threat;
        public float Bounty;
    }
}
