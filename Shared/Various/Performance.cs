using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.Various
{
    public class Performance
    {
        public static T MeasureExecutionTime<T>(Func<T> function)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            T result = function();
            stopwatch.Stop();
            Console.WriteLine($"Execution Time: {stopwatch.ElapsedMilliseconds} ms");
            return result;
        }

    }
}
