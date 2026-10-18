using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudCode.Shared;
using Unity.Services.CloudSave.Model;

namespace Wick.CloudCode
{
    /// <summary>IGameStore over Cloud Save, with the module's service token (server authority).</summary>
    public sealed class CloudSaveStore : IGameStore
    {
        private readonly IGameApiClient _api;
        private readonly IExecutionContext _ctx;

        public CloudSaveStore(IGameApiClient api, IExecutionContext ctx)
        {
            _api = api;
            _ctx = ctx;
        }

        public async Task<Dictionary<string, StoredItem>> GetProtectedAsync(string playerId, IReadOnlyList<string> keys)
        {
            ApiResponse<GetItemsResponse> response = await _api.CloudSaveData.GetProtectedItemsAsync(
                _ctx, _ctx.ServiceToken, _ctx.ProjectId, playerId, keys.ToList());

            var items = new Dictionary<string, StoredItem>();
            foreach (Item item in response.Data.Results)
            {
                items[item.Key] = new StoredItem(item.Key, JsonConvert.SerializeObject(item.Value), item.WriteLock);
            }

            return items;
        }

        public async Task SetProtectedBatchAsync(string playerId, IReadOnlyList<ItemWrite> writes)
        {
            var body = new SetItemBatchBody(writes
                .Select(w => new SetItemBody(w.Key, JToken.Parse(w.Json), w.WriteLock))
                .ToList());
            try
            {
                await _api.CloudSaveData.SetProtectedItemBatchAsync(_ctx, _ctx.ServiceToken, _ctx.ProjectId, playerId, body);
            }
            catch (ApiException e) when (e.Response != null && e.Response.StatusCode == HttpStatusCode.Conflict)
            {
                throw new WriteConflictException("Cloud Save write lock conflict for " + playerId);
            }
        }

        public async Task SetPrivateCustomAsync(string customId, string key, string json)
        {
            await _api.CloudSaveData.SetPrivateCustomItemAsync(
                _ctx, _ctx.ServiceToken, _ctx.ProjectId, customId, new SetItemBody(key, JToken.Parse(json)));
        }

        public async Task SetPrivateCustomBatchAsync(string customId, IReadOnlyList<ItemWrite> writes)
        {
            var body = new SetItemBatchBody(writes.Select(w => new SetItemBody(w.Key, JToken.Parse(w.Json), w.WriteLock)).ToList());
            await _api.CloudSaveData.SetPrivateCustomItemBatchAsync(_ctx, _ctx.ServiceToken, _ctx.ProjectId, customId, body);
        }

        public async Task<Dictionary<string, StoredItem>> GetPrivateCustomAsync(string customId, IReadOnlyList<string> keys)
        {
            ApiResponse<GetItemsResponse> response = await _api.CloudSaveData.GetPrivateCustomItemsAsync(
                _ctx, _ctx.ServiceToken, _ctx.ProjectId, customId, keys.ToList());
            var items = new Dictionary<string, StoredItem>();
            foreach (Item item in response.Data.Results)
            {
                items[item.Key] = new StoredItem(item.Key, JsonConvert.SerializeObject(item.Value), item.WriteLock);
            }

            return items;
        }

        public async Task<List<QueryHit>> QueryPrivateCustomAsync(IReadOnlyList<QueryFilter> filters, IReadOnlyList<string> returnKeys,
            int sampleSize)
        {
            var fields = filters.Select(f => new FieldFilter(f.Key, f.Value, Op(f.Op), true)).ToList();
            var body = new QueryIndexBody(fields, returnKeys.ToList(), 0, 0, sampleSize);
            ApiResponse<QueryIndexResponse> response = await _api.CloudSaveData.QueryPrivateCustomDataAsync(
                _ctx, _ctx.ServiceToken, _ctx.ProjectId, body);

            var hits = new List<QueryHit>();
            foreach (QueryIndexResponseResultsInner r in response.Data.Results)
            {
                var values = new Dictionary<string, string>();
                foreach (Item item in r.Data)
                {
                    values[item.Key] = JsonConvert.SerializeObject(item.Value);
                }

                hits.Add(new QueryHit(r.Id.GetString(), values));
            }

            return hits;
        }

        private static FieldFilter.OpEnum Op(QueryOp op)
        {
            switch (op)
            {
                case QueryOp.Ge: return FieldFilter.OpEnum.GE;
                case QueryOp.Le: return FieldFilter.OpEnum.LE;
                default: return FieldFilter.OpEnum.EQ;
            }
        }
    }
}
