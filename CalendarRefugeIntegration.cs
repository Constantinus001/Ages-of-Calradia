using System;
using TaleWorlds.CampaignSystem;

namespace AgesOfCalradia
{
    /// <summary>
    /// Optional integration point for the separately loadable Refuges module.
    /// It keeps the calendar map-bar UI independent of the refuge runtime.
    /// </summary>
    public static class CalendarRefugeIntegration
    {
        private static readonly object SyncRoot = new object();
        private static Action _openCamp;

        public static bool IsAvailable
        {
            get { lock (SyncRoot) return _openCamp != null; }
        }

        public static void RegisterCampOpener(Action openCamp)
        {
            if (openCamp == null) throw new ArgumentNullException(nameof(openCamp));
            lock (SyncRoot)
            {
                _openCamp = openCamp;
            }
        }

        public static void UnregisterCampOpener(Action openCamp)
        {
            lock (SyncRoot)
            {
                if (_openCamp == openCamp)
                {
                    _openCamp = null;
                }
            }
        }

        public static bool TryOpenCamp()
        {
            Action openCamp;
            lock (SyncRoot)
            {
                openCamp = _openCamp;
            }

            if (openCamp == null) return false;
            openCamp();
            return true;
        }

        public static bool IsWinter()
        {
            return TwelveMonthCalendar.CalendarTimeMath.GetSeason(CampaignTime.Now)
                == (int)CampaignTime.Seasons.Winter;
        }
    }
}
