using Serilog;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace alxnbl.OneNoteMdExporter.Infrastructure
{
    internal sealed class ExportStateStore
    {
        private const string StateFileName = ".export-state.json";

        private readonly string _filePath;
        private readonly Dictionary<string, DateTime> _entries;
        private bool _dirty;

        private ExportStateStore(string filePath, Dictionary<string, DateTime> entries)
        {
            _filePath = filePath;
            _entries = entries;
        }

        public static ExportStateStore Load(string baseRoot)
        {
            if (string.IsNullOrWhiteSpace(baseRoot))
            {
                throw new ArgumentException("Base root cannot be null or empty.", nameof(baseRoot));
            }

            var path = Path.Combine(baseRoot, StateFileName);

            try
            {
                if (!File.Exists(path))
                {
                    return new ExportStateStore(path, new Dictionary<string, DateTime>());
                }

                var json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return new ExportStateStore(path, new Dictionary<string, DateTime>());
                }

                var rawEntries = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
                var parsedEntries = rawEntries
                    .Select(kvp => (kvp.Key, Value: ParseDate(kvp.Value)))
                    .Where(tuple => tuple.Value.HasValue)
                    .ToDictionary(tuple => tuple.Key, tuple => tuple.Value!.Value);

                return new ExportStateStore(path, parsedEntries);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Unable to load export state from {Path}. Continuing without persisted state.", path);
                return new ExportStateStore(path, new Dictionary<string, DateTime>());
            }
        }

        private static DateTime? ParseDate(string rawValue)
        {
            if (string.IsNullOrWhiteSpace(rawValue))
                return null;

            if (DateTime.TryParse(rawValue, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
            {
                return parsed.Kind == DateTimeKind.Utc ? parsed.ToLocalTime() : parsed;
            }

            return null;
        }

        public DateTime? GetLastExport(string notebookId)
        {
            if (string.IsNullOrWhiteSpace(notebookId))
                return null;

            return _entries.TryGetValue(notebookId, out var value) ? value : null;
        }

        public void UpdateLastExport(string notebookId, DateTime timestamp)
        {
            if (string.IsNullOrWhiteSpace(notebookId))
                return;

            var normalized = timestamp.Kind == DateTimeKind.Utc ? timestamp : timestamp.ToUniversalTime();
            _entries[notebookId] = normalized.ToLocalTime();
            _dirty = true;
        }

        public void Save()
        {
            if (!_dirty)
                return;

            try
            {
                var directory = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                var payload = _entries.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToUniversalTime().ToString("o"));
                var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_filePath, json);
                _dirty = false;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Unable to persist export state to {Path}.", _filePath);
            }
        }
    }
}
