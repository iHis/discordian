using System;
using System.Runtime.InteropServices;

namespace DiscordIan.Helper
{
    public static class DateHelper
    {
        public static string ToWesternDate(string dateString)
        {
            if (DateTime.TryParse(dateString, out DateTime date))
            {
                return date.ToString("MMMM dd, yyyy");
            }

            return dateString;
        }

        public static string UnixTimeToDate(double unixTimeStamp)
        {
            DateTime dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc)
                .AddSeconds(unixTimeStamp);

            return UTCtoEST(dtDateTime);
        }

        public static string UTCtoEST(DateTime dateTime, string format = "MM/dd/yyyy hh:mm tt")
        {
            TimeZoneInfo timeZone = TimeZoneInfo.Utc;
            var dt = new DateTime(dateTime.Ticks, DateTimeKind.Utc);
            var timeZoneCode = "UTC";

            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    timeZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
                    timeZoneCode = "EST";
                }
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    timeZone = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
                    timeZoneCode = "EST";
                }

            }
            catch
            {

            }

            return TimeZoneInfo.ConvertTimeFromUtc(dt, timeZone)
                .ToString(format) + " " + timeZoneCode;
        }
    }
}
