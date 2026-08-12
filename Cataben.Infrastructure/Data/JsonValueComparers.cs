using System.Collections.Generic;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Cataben.Infrastructure.Data
{
    /// <summary>
    /// Value comparers for <see cref="Dictionary{TKey, TValue}"/> properties persisted as JSON
    /// through a value converter. Without an explicit comparer EF Core cannot detect in-place
    /// mutations of the converted value, so it warns at model build time and change tracking can
    /// miss edits (or re-persist unchanged rows). These comparers give correct value semantics by
    /// comparing the serialized JSON form, using the same <see cref="JsonSerializerDefaults.Web"/>
    /// options as the converters so comparison and persistence stay consistent.
    /// </summary>
    internal static class JsonValueComparers
    {
        private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

        public static ValueComparer<Dictionary<string, object>> StringObjectDictionary { get; } = new(
            equalsExpression: (a, b) => Serialize(a) == Serialize(b),
            hashCodeExpression: d => Serialize(d).GetHashCode(),
            snapshotExpression: d => JsonSerializer.Deserialize<Dictionary<string, object>>(Serialize(d), _jsonOptions) ?? new());

        public static ValueComparer<List<string>> StringList { get; } = new(
            equalsExpression: (a, b) => SerializeList(a) == SerializeList(b),
            hashCodeExpression: l => SerializeList(l).GetHashCode(),
            snapshotExpression: l => JsonSerializer.Deserialize<List<string>>(SerializeList(l), _jsonOptions) ?? new());

        private static string Serialize(Dictionary<string, object>? dictionary) =>
            JsonSerializer.Serialize(dictionary, _jsonOptions);

        private static string SerializeList(List<string>? list) =>
            JsonSerializer.Serialize(list, _jsonOptions);
    }
}
