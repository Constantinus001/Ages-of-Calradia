using System;

namespace AgesOfCalradiaReligions
{
    public enum CrownReligiousPolicy
    {
        UniversalProtection = 0,
        TraditionalTolerance = 1,
        OfficialSupremacy = 2,
        Suppression = 3
    }

    public enum HolySiteAccess
    {
        Open = 0,
        Restricted = 1,
        Closed = 2
    }

    public enum ReligiousIncidentType
    {
        None = 0,
        PilgrimMarket = 1,
        InterfaithFestival = 2,
        ClericalDispute = 3,
        SuppressionResistance = 4,
        SectarianViolence = 5
    }

    public enum ReligiousInstitutionTier
    {
        None = 0,
        Shrine = 1,
        Temple = 2,
        GreatSanctuary = 3
    }

    public enum ClergyGovernancePolicy
    {
        ClericalAutonomy = 0,
        CrownConcordat = 1,
        CrownSupervision = 2
    }

    public sealed class ReligionDefinition
    {
        internal ReligionDefinition(string id, string name, string family, string clergyTitle)
        {
            Id = id;
            Name = name;
            Family = family;
            ClergyTitle = clergyTitle;
        }

        public string Id { get; private set; }
        public string Name { get; private set; }
        public string Family { get; private set; }
        public string ClergyTitle { get; private set; }
    }

    public sealed class HeroReligionState
    {
        internal HeroReligionState(string heroId, string faithId, float zeal, int lastConversionDay)
        {
            HeroId = heroId ?? string.Empty;
            FaithId = faithId ?? string.Empty;
            Zeal = Math.Max(0f, Math.Min(100f, zeal));
            LastConversionDay = lastConversionDay;
            Piety = 0f;
            LastPilgrimageDay = -1;
            BirthFaithId = FaithId;
            ConversionCount = 0;
            ReligiousLegitimacy = 50f;
        }

        public string HeroId { get; internal set; }
        public string FaithId { get; internal set; }
        public float Zeal { get; internal set; }
        public int LastConversionDay { get; internal set; }
        public float Piety { get; internal set; }
        public int LastPilgrimageDay { get; internal set; }
        public string BirthFaithId { get; internal set; }
        public int ConversionCount { get; internal set; }
        public float ReligiousLegitimacy { get; internal set; }
    }

    public sealed class RealmReligionState
    {
        internal RealmReligionState(string kingdomId, string officialFaithId)
        {
            KingdomId = kingdomId ?? string.Empty;
            OfficialFaithId = officialFaithId ?? string.Empty;
            Policy = CrownReligiousPolicy.TraditionalTolerance;
            ClergyRelations = 50f;
            ReligiousUnity = 50f;
            ClergyGovernance = ClergyGovernancePolicy.CrownConcordat;
        }

        public string KingdomId { get; internal set; }
        public string OfficialFaithId { get; internal set; }
        public CrownReligiousPolicy Policy { get; internal set; }
        public float ClergyRelations { get; internal set; }
        public float ReligiousUnity { get; internal set; }
        public ClergyGovernancePolicy ClergyGovernance { get; internal set; }
    }

    public sealed class ClergyOfficeState
    {
        internal ClergyOfficeState(string settlementId, string faithId, string holderHeroId)
        {
            SettlementId = settlementId ?? string.Empty;
            FaithId = faithId ?? string.Empty;
            HolderHeroId = holderHeroId ?? string.Empty;
            Treasury = 0L;
            LastClergyTaxDay = -1;
        }

        public string SettlementId { get; internal set; }
        public string FaithId { get; internal set; }
        public string HolderHeroId { get; internal set; }
        public long Treasury { get; internal set; }
        public int LastClergyTaxDay { get; internal set; }
    }

    public sealed class HolySiteDefinition
    {
        internal HolySiteDefinition(string id, string settlementId, string name, params string[] faithIds)
        {
            Id = id;
            SettlementId = settlementId;
            Name = name;
            FaithIds = faithIds ?? new string[0];
        }

        public string Id { get; private set; }
        public string SettlementId { get; private set; }
        public string Name { get; private set; }
        public string[] FaithIds { get; private set; }
    }

    public sealed class HolySiteState
    {
        internal HolySiteState(string siteId)
        {
            SiteId = siteId ?? string.Empty;
            AccessByFaith = new HolySiteAccess[ReligionCatalog.FaithIds.Count];
        }

        public string SiteId { get; internal set; }
        internal HolySiteAccess[] AccessByFaith { get; set; }

        public HolySiteAccess GetAccess(string faithId)
        {
            int index = ReligionCatalog.IndexOf(faithId);
            return index < 0 || index >= AccessByFaith.Length ? HolySiteAccess.Closed : AccessByFaith[index];
        }
    }
}
