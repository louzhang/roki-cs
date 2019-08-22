using System;

namespace Roki.Core.Extentions
{
    public static class NumberExtensions
    {
        public static int KiB(this int value)
        {
            return value * 1024;
        }

        public static int KB(this int value)
        {
            return value * 1000;
        }

        public static int MiB(this int value)
        {
            return value.KiB() * 1024;
        }

        public static int MB(this int value)
        {
            return value.KB() * 1000;
        }

        public static int GiB(this int value)
        {
            return value.MiB() * 1024;
        }

        public static int GB(this int value)
        {
            return value.MB() * 1000;
        }

        public static ulong KiB(this ulong value)
        {
            return value * 1024;
        }

        public static ulong KB(this ulong value)
        {
            return value * 1000;
        }

        public static ulong MiB(this ulong value)
        {
            return value.KiB() * 1024;
        }

        public static ulong MB(this ulong value)
        {
            return value.KB() * 1000;
        }

        public static ulong GiB(this ulong value)
        {
            return value.MiB() * 1024;
        }

        public static ulong GB(this ulong value)
        {
            return value.MB() * 1000;
        }

        public static bool IsInteger(this decimal number)
        {
            return number == Math.Truncate(number);
        }

        public static DateTime ToUnixTimestamp(this double number)
        {
            return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(number);
        }

        public static string DegreesToCardinal(this double degrees)
        {
            string[] cardinals = {"N", "NE", "E", "SE", "S", "SW", "W", "NW", "N"};
            return cardinals[(int) Math.Round(degrees % 360 / 45)];
        }
    }
}