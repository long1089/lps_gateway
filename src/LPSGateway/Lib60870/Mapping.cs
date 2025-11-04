using System.Collections.Generic;

namespace LPSGateway.Lib60870
{
    /// <summary>
    /// Mapping between IEC-102 TYPE IDs and table names
    /// </summary>
    public static class Mapping
    {
        private static readonly Dictionary<byte, string> _typeIdToTable = new()
        {
            { 0x90, "basic_info" },
            { 0x91, "power_quality" },
            { 0x92, "voltage_data" },
            { 0x93, "current_data" },
            { 0x94, "power_data" },
            { 0x95, "energy_data" },
            { 0x96, "demand_data" },
            { 0x97, "event_log" },
            { 0x98, "alarm_data" },
            { 0x99, "load_profile" },
            { 0x9A, "harmonic_data" },
            { 0x9B, "phase_data" },
            { 0x9C, "transformer_data" },
            { 0x9D, "meter_status" },
            { 0x9E, "communication_log" },
            { 0x9F, "billing_data" },
            { 0xA0, "tariff_data" },
            { 0xA1, "time_sync" },
            { 0xA2, "configuration" },
            { 0xA3, "calibration" },
            { 0xA4, "maintenance" },
            { 0xA5, "diagnostic" },
            { 0xA6, "security_log" },
            { 0xA7, "network_stats" },
            { 0xA8, "custom_data" }
        };

        private static readonly Dictionary<string, byte> _tableToTypeId;

        static Mapping()
        {
            _tableToTypeId = new Dictionary<string, byte>();
            foreach (var kvp in _typeIdToTable)
            {
                _tableToTypeId[kvp.Value] = kvp.Key;
            }
        }

        /// <summary>
        /// Get table name from TYPE ID
        /// </summary>
        public static string? GetTableName(byte typeId)
        {
            return _typeIdToTable.TryGetValue(typeId, out var tableName) ? tableName : null;
        }

        /// <summary>
        /// Get TYPE ID from table name
        /// </summary>
        public static byte? GetTypeId(string tableName)
        {
            return _tableToTypeId.TryGetValue(tableName, out var typeId) ? typeId : null;
        }

        /// <summary>
        /// Get all supported TYPE IDs
        /// </summary>
        public static IEnumerable<byte> GetAllTypeIds()
        {
            return _typeIdToTable.Keys;
        }

        /// <summary>
        /// Get all supported table names
        /// </summary>
        public static IEnumerable<string> GetAllTableNames()
        {
            return _typeIdToTable.Values;
        }
    }
}
