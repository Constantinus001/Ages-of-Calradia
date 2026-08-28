using TaleWorlds.CampaignSystem;

namespace AgesOfCalradiaSuccession
{
    public sealed class SuccessionClaim
    {
        internal SuccessionClaim(Hero hero, Clan clan, int score, string explanation)
        {
            Hero = hero;
            Clan = clan;
            Score = score;
            Explanation = explanation ?? string.Empty;
        }

        public Hero Hero { get; private set; }
        public Clan Clan { get; private set; }
        public int Score { get; private set; }
        public string Explanation { get; private set; }
    }
}
