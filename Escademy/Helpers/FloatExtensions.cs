using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Escademy.Helpers
{
    public static class FloatExtensions
    {
        public static string RoundString(this float val)
        {
            return (Math.Round(val * 2, MidpointRounding.AwayFromZero) / 2)
                .ToString()
                .Replace(",", ".");
        }

        public static float RoundValue(this float val)
        {
            return (float)(Math.Round(val * 2, MidpointRounding.AwayFromZero) / 2);
        }
    }
}