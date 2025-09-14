using System;
using TradingPlatform.BusinessLayer;

namespace DivergentStrV0_1.Utils
{
    public static class AppLog
    {
        private static void Write(string component, string message, LoggingLevel level)
        {
            var prefix = string.IsNullOrWhiteSpace(component) ? "General" : component.Trim();
            Core.Instance.Loggers.Log($"[{prefix}] {message}", level);
        }

        public static void Info(string component, string message) => Write(component, message, LoggingLevel.System);
        public static void System(string component, string message) => Write(component, message, LoggingLevel.System);
        public static void Trading(string component, string message) => Write(component, message, LoggingLevel.Trading);
        public static void Error(string component, string message) => Write(component, message, LoggingLevel.Error);
        public static void Error(string component, string message, Exception ex) => Write(component, $"{message} | Exception: {ex.Message}", LoggingLevel.Error);
    }
}

