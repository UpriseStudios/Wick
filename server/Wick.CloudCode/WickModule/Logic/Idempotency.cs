using System.Collections.Generic;

namespace Wick.CloudCode
{
    /// <summary>
    /// The idempotency layer: per player, the results of their most recent requests by request id.
    /// A retried request returns its stored result instead of running again. The record is written
    /// in the same atomic batch as the effect it describes, so a request is either fully applied
    /// and recorded, or neither. (Write locks stop concurrent requests overwriting each other;
    /// this stops one request applying twice.)
    /// </summary>
    public static class Idempotency
    {
        public const int Capacity = 32;

        public static bool IsValidRequestId(string requestId)
        {
            if (string.IsNullOrEmpty(requestId) || requestId.Length < 8 || requestId.Length > 64)
            {
                return false;
            }

            foreach (char c in requestId)
            {
                bool ok = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '-' || c == '_';
                if (!ok)
                {
                    return false;
                }
            }

            return true;
        }

        public static bool TryGet<T>(Snapshot snapshot, string requestId, out T result) where T : new()
        {
            foreach (Entry e in Read(snapshot))
            {
                if (e.RequestId == requestId)
                {
                    result = Json.Read<T>(e.Result);
                    return true;
                }
            }

            result = default;
            return false;
        }

        /// <summary>The idempotency item's new JSON with this result added (oldest dropped past Capacity).</summary>
        public static string Add(Snapshot snapshot, string requestId, object result)
        {
            List<Entry> entries = Read(snapshot);
            entries.Add(new Entry { RequestId = requestId, Result = Json.Write(result) });
            while (entries.Count > Capacity)
            {
                entries.RemoveAt(0);
            }

            return Json.Write(new Book { Entries = entries });
        }

        private static List<Entry> Read(Snapshot snapshot)
        {
            return Json.Read<Book>(snapshot.Json(Keys.Idempotency)).Entries ?? new List<Entry>();
        }

        private sealed class Book
        {
            public List<Entry> Entries { get; set; } = new List<Entry>();
        }

        private sealed class Entry
        {
            public string RequestId { get; set; }

            /// <summary>The stored result, as JSON.</summary>
            public string Result { get; set; }
        }
    }
}
