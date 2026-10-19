using System;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Wick.Economy;

namespace Wick.CloudCode
{
    /// <summary>A store's answer about one receipt.</summary>
    public sealed class PurchaseVerdict
    {
        public bool Ok { get; set; }
        public string Reason { get; set; }
        public string OrderId { get; set; }
        public string ProductId { get; set; }

        /// <summary>Subscriptions: when the paid period ends.</summary>
        public long ExpiresMs { get; set; }

        /// <summary>A test or fake-store purchase (development only).</summary>
        public bool Test { get; set; }

        public static PurchaseVerdict Fail(string reason)
        {
            return new PurchaseVerdict { Ok = false, Reason = reason };
        }
    }

    public interface IReceiptVerifier
    {
        /// <param name="receipt">Unity IAP's unified receipt JSON (Store, TransactionID, Payload).</param>
        Task<PurchaseVerdict> VerifyAsync(string productId, string kind, string receipt);
    }

    /// <summary>Unity IAP's receipt wrapper: {"Store": ..., "TransactionID": ..., "Payload": ...}.</summary>
    public static class UnityReceipt
    {
        public static (string Store, string TransactionId, string Payload) Parse(string receipt)
        {
            JObject o = JObject.Parse(receipt);
            return ((string)o["Store"], (string)o["TransactionID"], (string)o["Payload"]);
        }
    }

    /// <summary>
    /// Google Play: checks the purchase token with the Play Developer API as the service account
    /// in Secret Manager. Products must be purchased (purchaseState 0); subscriptions active.
    /// </summary>
    public sealed class GooglePlayVerifier : IReceiptVerifier
    {
        public const string Scope = "https://www.googleapis.com/auth/androidpublisher";

        private readonly GoogleAuth _auth;
        private readonly IHttp _http;
        private readonly string _package;

        public GooglePlayVerifier(GoogleAuth auth, IHttp http, string packageName)
        {
            _auth = auth;
            _http = http;
            _package = packageName;
        }

        public async Task<PurchaseVerdict> VerifyAsync(string productId, string kind, string receipt)
        {
            string token;
            string orderId;
            try
            {
                (string store, _, string payload) = UnityReceipt.Parse(receipt);
                if (store != "GooglePlay")
                {
                    return PurchaseVerdict.Fail("Not a Google Play receipt");
                }

                JObject purchase = JObject.Parse((string)JObject.Parse(payload)["json"]);
                token = (string)purchase["purchaseToken"];
                orderId = (string)purchase["orderId"];
                if ((string)purchase["productId"] != productId || (string)purchase["packageName"] != _package)
                {
                    return PurchaseVerdict.Fail("The receipt is for another product");
                }
            }
            catch (Exception)
            {
                return PurchaseVerdict.Fail("The receipt couldn't be read");
            }

            string bearer = await _auth.AccessTokenAsync(Scope);
            string baseUrl = "https://androidpublisher.googleapis.com/androidpublisher/v3/applications/" + _package + "/purchases/";
            if (kind == "subscription")
            {
                (int status, string body) = await _http.GetAsync(baseUrl + "subscriptionsv2/tokens/" + Uri.EscapeDataString(token), bearer);
                if (status != 200)
                {
                    return PurchaseVerdict.Fail("Google Play didn't recognise the subscription (" + status + ")");
                }

                JObject sub = JObject.Parse(body);
                string state = (string)sub["subscriptionState"];
                bool active = state == "SUBSCRIPTION_STATE_ACTIVE" || state == "SUBSCRIPTION_STATE_IN_GRACE_PERIOD";
                DateTime expiry = (DateTime?)sub["lineItems"]?[0]?["expiryTime"] ?? DateTime.MinValue;
                return active
                    ? new PurchaseVerdict { Ok = true, OrderId = (string)sub["latestOrderId"] ?? orderId, ProductId = productId,
                        ExpiresMs = new DateTimeOffset(DateTime.SpecifyKind(expiry, DateTimeKind.Utc)).ToUnixTimeMilliseconds(),
                        Test = sub["testPurchase"] != null }
                    : PurchaseVerdict.Fail("The subscription isn't active");
            }

            (int code, string json) = await _http.GetAsync(baseUrl + "products/" + Uri.EscapeDataString(productId) + "/tokens/" +
                Uri.EscapeDataString(token), bearer);
            if (code != 200)
            {
                return PurchaseVerdict.Fail("Google Play didn't recognise the purchase (" + code + ")");
            }

            JObject p = JObject.Parse(json);
            if ((int?)p["purchaseState"] != 0)
            {
                return PurchaseVerdict.Fail("The purchase isn't complete");
            }

            return new PurchaseVerdict { Ok = true, OrderId = (string)p["orderId"] ?? orderId, ProductId = productId, Test = (int?)p["purchaseType"] == 0 };
        }
    }

    /// <summary>Unity IAP's fake store (Editor and dev builds without a store): development only.</summary>
    public sealed class FakeStoreVerifier : IReceiptVerifier
    {
        public Task<PurchaseVerdict> VerifyAsync(string productId, string kind, string receipt)
        {
            try
            {
                (string store, string transaction, _) = UnityReceipt.Parse(receipt);
                bool fake = store == "fake" || store == "FakeStore";
                if (!fake || string.IsNullOrEmpty(transaction))
                {
                    return Task.FromResult(PurchaseVerdict.Fail("Not a fake-store receipt"));
                }

                return Task.FromResult(new PurchaseVerdict
                {
                    Ok = true, OrderId = "fake." + transaction, ProductId = productId, Test = true,
                    ExpiresMs = kind == "subscription" ? DateTimeOffset.UtcNow.AddDays(30).ToUnixTimeMilliseconds() : 0,
                });
            }
            catch (Exception)
            {
                return Task.FromResult(PurchaseVerdict.Fail("The receipt couldn't be read"));
            }
        }
    }

    /// <summary>Tries each verifier in turn (Google Play in production; plus the fake store in development).</summary>
    public sealed class ReceiptVerifiers : IReceiptVerifier
    {
        private readonly IReceiptVerifier[] _verifiers;

        public ReceiptVerifiers(params IReceiptVerifier[] verifiers)
        {
            _verifiers = verifiers;
        }

        public async Task<PurchaseVerdict> VerifyAsync(string productId, string kind, string receipt)
        {
            PurchaseVerdict last = PurchaseVerdict.Fail("Purchases can't be verified yet");
            foreach (IReceiptVerifier v in _verifiers)
            {
                last = await v.VerifyAsync(productId, kind, receipt);
                if (last.Ok)
                {
                    return last;
                }
            }

            return last;
        }
    }

    public sealed class PurchaseResult
    {
        public bool Ok { get; set; }
        public string Reason { get; set; }
        public string ProductId { get; set; }

        /// <summary>False when this order was already granted (a retry or a restore).</summary>
        public bool NewlyGranted { get; set; }

        public Grant Grant { get; set; }
    }

    /// <summary>
    /// ValidatePurchase: verifies the receipt with the store, then grants the product exactly once
    /// per store order (a request id makes retries free, and the order id makes a re-sent or
    /// restored receipt grant nothing new).
    /// </summary>
    public sealed class PurchaseService
    {
        private readonly IGameStore _store;
        private readonly IEconomySource _economy;
        private readonly IReceiptVerifier _verifier;

        public PurchaseService(IGameStore store, IEconomySource economy, IReceiptVerifier verifier)
        {
            _store = store;
            _economy = economy;
            _verifier = verifier;
        }

        public async Task<PurchaseResult> ValidateAsync(string playerId, string requestId, string productId, string receipt)
        {
            if (!Idempotency.IsValidRequestId(requestId))
            {
                return new PurchaseResult { Ok = false, Reason = "Bad request id" };
            }

            EconomyConfig cfg = await _economy.GetAsync();
            Product product = cfg.Product(productId);
            if (product == null || string.IsNullOrEmpty(receipt))
            {
                return new PurchaseResult { Ok = false, Reason = "Unknown product", ProductId = productId };
            }

            PurchaseVerdict verdict = await _verifier.VerifyAsync(productId, product.Kind, receipt);
            if (!verdict.Ok)
            {
                return new PurchaseResult { Ok = false, Reason = verdict.Reason, ProductId = productId };
            }

            return await Locked.UpdateAsync(_store, playerId, new[] { Keys.Wallet, EconomyKeys.Inventory, EconomyKeys.Pass, Keys.Idempotency }, s =>
            {
                if (Idempotency.TryGet(s, requestId, out PurchaseResult prior))
                {
                    return new Plan<PurchaseResult>(prior);
                }

                Wallet wallet = Json.Read<Wallet>(s.Json(Keys.Wallet));
                Inventory inventory = Json.Read<Inventory>(s.Json(EconomyKeys.Inventory));
                PassState pass = Json.Read<PassState>(s.Json(EconomyKeys.Pass));
                var result = new PurchaseResult { Ok = true, ProductId = productId, Reason = "Already granted" };
                bool owned = product.Kind == "nonConsumable" && inventory.Products.Contains(productId);
                if (!inventory.Orders.Contains(verdict.OrderId) && !owned)
                {
                    Grants.Apply(product.Grant, cfg, wallet, inventory, pass);
                    if (product.Grant.Club)
                    {
                        inventory.ClubUntilMs = Math.Max(inventory.ClubUntilMs, verdict.ExpiresMs);
                    }

                    result.NewlyGranted = true;
                    result.Reason = "Granted";
                    result.Grant = product.Grant;
                }
                else if (product.Grant.Club)
                {
                    inventory.ClubUntilMs = Math.Max(inventory.ClubUntilMs, verdict.ExpiresMs); // a renewal
                }

                if (!inventory.Orders.Contains(verdict.OrderId))
                {
                    inventory.Orders.Add(verdict.OrderId);
                }

                if (!inventory.Products.Contains(productId))
                {
                    inventory.Products.Add(productId);
                }

                return new Plan<PurchaseResult>(result)
                    .Write(Keys.Wallet, Json.Write(wallet))
                    .Write(EconomyKeys.Inventory, Json.Write(inventory))
                    .Write(EconomyKeys.Pass, Json.Write(pass))
                    .Write(Keys.Idempotency, Idempotency.Add(s, requestId, result));
            });
        }
    }
}
