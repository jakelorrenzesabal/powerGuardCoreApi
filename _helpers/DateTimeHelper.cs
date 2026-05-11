using System;

namespace PowerGuardCoreApi._Helpers
{
    public static class DateTimeHelper
    {
        private static readonly TimeZoneInfo PhilippineTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time");

        /// <summary>
        /// Gets the current time in UTC (always store UTC in database)
        /// </summary>
        public static DateTime GetUtcNow()
        {
            return DateTime.UtcNow;
        }

        /// <summary>
        /// Converts UTC time to Philippine Time for display
        /// </summary>
        public static DateTime ConvertToPhilippineTime(DateTime utcTime)
        {
            if (utcTime.Kind != DateTimeKind.Utc)
                utcTime = DateTime.SpecifyKind(utcTime, DateTimeKind.Utc);
            return TimeZoneInfo.ConvertTime(utcTime, PhilippineTimeZone);
        }

        /// <summary>
        /// Converts Philippine Time to UTC for storage
        /// </summary>
        public static DateTime ConvertToUtc(DateTime philippineTime)
        {
            return TimeZoneInfo.ConvertTimeToUtc(philippineTime, PhilippineTimeZone);
        }
    }
}
