// Ayudas para compilar el mismo código en .NET Framework 4.8 (la app) y .NET moderno
// (el banco de pruebas, que enlaza los archivos del motor).
using System;
using System.Diagnostics;

namespace Autoclicker
{
    internal static class Compat
    {
        public static int Clamp(int value, int min, int max) => value < min ? min : value > max ? max : value;
        public static long Clamp(long value, long min, long max) => value < min ? min : value > max ? max : value;
        public static double Clamp(double value, double min, double max) => value < min ? min : value > max ? max : value;

        public static bool IsDigit(char c) => c >= '0' && c <= '9';

        /// <summary>Tiempo transcurrido entre dos marcas de Stopwatch.GetTimestamp().</summary>
        public static TimeSpan Elapsed(long start, long end) =>
            TimeSpan.FromSeconds((end - start) / (double)Stopwatch.Frequency);

        public static TimeSpan Elapsed(long start) => Elapsed(start, Stopwatch.GetTimestamp());

        public static readonly int ProcessId = Process.GetCurrentProcess().Id;
    }
}

#if NETFRAMEWORK
namespace System.Runtime.CompilerServices
{
    // Necesario para "init" y "record" en .NET Framework.
    internal static class IsExternalInit { }
}
#endif
