using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace AgesOfCalradiaReligions
{
    public sealed class OpeningPeaceBehavior : CampaignBehaviorBase
    {
        private const string InitializedKey = "AgesOfCalradiaReligions.OpeningPeaceInitializedV1";
        private const string EndDayKey = "AgesOfCalradiaReligions.OpeningPeaceEndDayV1";
        private const int TreatyDays = 20;
        private bool _initialized;
        private int _endDay;
        private bool _isApplyingPeace;

        public override void RegisterEvents()
        {
            CampaignEvents.OnNewGameCreatedPartialFollowUpEndEvent.AddNonSerializedListener(this, OnNewGameReady);
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
            CampaignEvents.WarDeclared.AddNonSerializedListener(this, OnWarDeclared);
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData(InitializedKey, ref _initialized);
            dataStore.SyncData(EndDayKey, ref _endDay);
        }

        private void OnNewGameReady(CampaignGameStarter starter)
        {
            if (_initialized) return;
            _initialized = true;
            _endDay = CurrentDay + TreatyDays;
            ForceAllKingdomsPeace();
            InformationManager.DisplayMessage(new InformationMessage(
                "The Opening Peace binds every kingdom for 20 days.",
                Color.FromUint(0xE3C56FFFu)));
            ReligionDiagnostics.Info("Forced opening peace initialized through day " + _endDay + ".");
        }

        private void OnDailyTick()
        {
            if (IsTreatyActive) ForceAllKingdomsPeace();
        }

        private void OnWarDeclared(IFaction first, IFaction second, DeclareWarAction.DeclareWarDetail detail)
        {
            if (!IsTreatyActive || _isApplyingPeace) return;
            Kingdom firstKingdom = ResolveKingdom(first);
            Kingdom secondKingdom = ResolveKingdom(second);
            if (firstKingdom == null || secondKingdom == null || firstKingdom == secondKingdom) return;
            ApplyPeace(firstKingdom, secondKingdom);
            InformationManager.DisplayMessage(new InformationMessage(
                "The Opening Peace prevents war for " + Math.Max(0, _endDay - CurrentDay) + " more day(s).",
                Color.FromUint(0xE3C56FFFu)));
        }

        private bool IsTreatyActive
        {
            get { return _initialized && CurrentDay < _endDay; }
        }

        private static int CurrentDay
        {
            get { return (int)Math.Floor(CampaignTime.Now.ToDays); }
        }

        private void ForceAllKingdomsPeace()
        {
            for (int first = 0; first < Kingdom.All.Count; first++)
            {
                Kingdom firstKingdom = Kingdom.All[first];
                if (firstKingdom == null || firstKingdom.IsEliminated) continue;
                for (int second = first + 1; second < Kingdom.All.Count; second++)
                {
                    Kingdom secondKingdom = Kingdom.All[second];
                    if (secondKingdom == null || secondKingdom.IsEliminated) continue;
                    if (FactionManager.IsAtWarAgainstFaction(firstKingdom, secondKingdom)) ApplyPeace(firstKingdom, secondKingdom);
                }
            }
        }

        private void ApplyPeace(Kingdom first, Kingdom second)
        {
            try
            {
                _isApplyingPeace = true;
                MakePeaceAction.Apply(first, second);
            }
            catch (Exception exception)
            {
                ReligionDiagnostics.Error("The opening treaty could not end a kingdom war; it will retry on the next daily tick.", exception);
            }
            finally
            {
                _isApplyingPeace = false;
            }
        }

        private static Kingdom ResolveKingdom(IFaction faction)
        {
            Kingdom kingdom = faction as Kingdom;
            if (kingdom != null) return kingdom;
            Clan clan = faction as Clan;
            return clan == null ? null : clan.Kingdom;
        }
    }
}
