using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace eDocGenLib.Utils
{
    public static class ExtensionHelper
    {
        public static int? TryGetInt(this string item)
        {
            int i;
            bool success = int.TryParse(item, out i);
            return success ? (int?)i : (int?)null;
        }
        public static double? TryGetDouble(this string item)
        {
            double i;
            bool success = double.TryParse(item, out i);
            return success ? (double?)i : (double?)null;
        }
        public static decimal? TryGetDecimal(this string item)
        {
            decimal i;
            bool success = decimal.TryParse(item, out i);
            return success ? (decimal?)i : (decimal?)null;
        }
        public static double StdDev<T>(this IEnumerable<T> list, Func<T, double> values)
        {
            // ref: https://stackoverflow.com/questions/2253874/linq-equivalent-for-standard-deviation
            // ref: http://warrenseen.com/blog/2006/03/13/how-to-calculate-standard-deviation/ 
            var mean = 0.0;
            var sum = 0.0;
            var stdDev = 0.0;
            var n = 0;
            foreach (var value in list.Select(values))
            {
                n++;
                var delta = value - mean;
                mean += delta / n;
                sum += delta * (value - mean);
            }
            if (1 < n)
                stdDev = Math.Sqrt(sum / (n - 1));

            return stdDev;

        }
        public static Dictionary<T, U> AddRange<T, U>(this Dictionary<T, U> destination, Dictionary<T, U> source)
        {
            if (destination == null) destination = new Dictionary<T, U>();
            foreach (var e in source)
                destination.Add(e.Key, e.Value);
            return destination;
        }
    }
}
