using Microsoft.Win32;
using System;

namespace RefaccionariaPOS.Security
{
    public static class DemoLicenseService
    {
        private static readonly TimeSpan DemoDuration = TimeSpan.FromDays(1);
        private const string RegistryPath = @"Software\ServicioAutomotrizLopezSuite\Demo1Dia";
        private const string StartDateValue = "StartDateUtc";

        public static bool Validate(out string message)
        {
            TimeSpan remaining = GetRemainingTime();

            if (remaining <= TimeSpan.Zero)
            {
                message = "El demo de 1 dia ha vencido. Contacta al proveedor para activar la aplicacion.";
                return false;
            }

            message = $"Demo activo. Tiempo restante: {FormatRemainingTime(remaining)}.";
            return true;
        }

        public static TimeSpan GetRemainingTime()
        {
            DateTime now = DateTime.UtcNow;
            DateTime startDate = GetOrCreateStartDate(now);
            DateTime expiresAt = startDate.Add(DemoDuration);
            return expiresAt - now;
        }

        public static string FormatRemainingTime(TimeSpan remaining)
        {
            if (remaining <= TimeSpan.Zero)
            {
                return "00h 00m";
            }

            int totalHours = (int)Math.Ceiling(remaining.TotalMinutes) / 60;
            int minutes = (int)Math.Ceiling(remaining.TotalMinutes) % 60;
            return $"{totalHours:00}h {minutes:00}m";
        }

        private static DateTime GetOrCreateStartDate(DateTime now)
        {
            using RegistryKey key = Registry.CurrentUser.CreateSubKey(RegistryPath);
            string? storedValue = key.GetValue(StartDateValue)?.ToString();

            if (DateTime.TryParse(storedValue, out DateTime startDate))
            {
                return startDate.ToUniversalTime();
            }

            key.SetValue(StartDateValue, now.ToString("O"), RegistryValueKind.String);
            return now;
        }
    }
}
