using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace AgesOfCalradiaLogistics
{
    /// <summary>
    /// Refills depleted ammunition only while a friendly agent is in the train's
    /// marked range. Every three rounds transferred consumes one campaign
    /// reserve point; no reserve means no refill.
    /// </summary>
    public sealed class BaggageResupplyMissionBehavior : MissionLogic
    {
        private const float ResupplyIntervalSeconds = 3f;
        private const int RoundsPerReservePoint = 3;
        private float _elapsed;
        private float _summaryElapsed;
        private int _transferredRounds;
        private int _consumedReserve;

        public override void OnMissionTick(float dt)
        {
            _elapsed += dt;
            if (_elapsed < ResupplyIntervalSeconds)
            {
                return;
            }

            _elapsed = 0f;
            ResupplySide(BattleSideEnum.Attacker);
            ResupplySide(BattleSideEnum.Defender);
            _summaryElapsed += ResupplyIntervalSeconds;
            if (_summaryElapsed >= 30f && _consumedReserve > 0)
            {
                LogisticsDiagnostics.Info(string.Format("Battle resupply summary: {0} round(s) transferred using {1} reserve point(s) in the last {2:F0}s.", _transferredRounds, _consumedReserve, _summaryElapsed));
                _summaryElapsed = 0f;
                _transferredRounds = 0;
                _consumedReserve = 0;
            }
        }

        private void ResupplySide(BattleSideEnum side)
        {
            BaggageTrainLocation location;
            if (!BaggageTrainRegistry.TryGet(side, out location))
            {
                return;
            }

            MobileParty reserveParty = GetReserveParty(side);
            LogisticsReserveBehavior reserves = LogisticsReserveBehavior.Active;
            if (reserveParty == null || reserves == null || reserves.GetReserve(reserveParty) <= 0)
            {
                return;
            }

            Team team = Mission.Teams.FirstOrDefault(candidate => candidate.Side == side);
            if (team == null)
            {
                return;
            }

            float radiusSquared = location.Radius * location.Radius;
            foreach (Agent agent in team.ActiveAgents)
            {
                if (agent.Health <= 0f || agent.Position.DistanceSquared(location.Position) > radiusSquared)
                {
                    continue;
                }

                if (!TryResupplyAgent(agent, reserveParty, reserves))
                {
                    return;
                }
            }
        }

        private MobileParty GetReserveParty(BattleSideEnum side)
        {
            MapEvent battle = PlayerEncounter.Battle;
            if (battle == null)
            {
                return null;
            }

            MapEventParty mainParty = battle.PartiesOnSide(side)
                .FirstOrDefault(mapParty => mapParty.Party == PartyBase.MainParty);
            if (mainParty != null)
            {
                return MobileParty.MainParty;
            }

            MapEventParty eligibleParty = battle.PartiesOnSide(side)
                .FirstOrDefault(mapParty => mapParty.Party != null &&
                    LogisticsReserveBehavior.IsEligible(mapParty.Party.MobileParty));
            return eligibleParty == null ? null : eligibleParty.Party.MobileParty;
        }

        private bool TryResupplyAgent(
            Agent agent,
            MobileParty reserveParty,
            LogisticsReserveBehavior reserves)
        {
            for (EquipmentIndex slot = EquipmentIndex.WeaponItemBeginSlot;
                slot < EquipmentIndex.NumAllWeaponSlots;
                slot++)
            {
                MissionWeapon weapon = agent.Equipment[slot];
                if (weapon.IsEmpty || !weapon.IsAnyAmmo())
                {
                    continue;
                }

                short desiredAmount = weapon.ModifiedMaxAmount;
                if (desiredAmount > 1)
                {
                    desiredAmount--;
                }

                if (weapon.Amount >= desiredAmount)
                {
                    continue;
                }

                int transferAmount = System.Math.Min(RoundsPerReservePoint, desiredAmount - weapon.Amount);
                if (!reserves.TryConsumeReserve(reserveParty, 1))
                {
                    return false;
                }

                agent.SetWeaponAmountInSlot(
                    slot,
                    (short)(weapon.Amount + transferAmount),
                    enforcePrimaryItem: false);
                _transferredRounds += transferAmount;
                _consumedReserve++;
            }

            return true;
        }
    }
}
