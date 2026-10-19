using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Core;
using UnityEngine;
using UnityEngine.Purchasing;
using Wick.Economy;

namespace Wick.Services
{
    /// <summary>
    /// In-app purchases through Unity IAP. The game never grants anything itself: each pending
    /// order's receipt goes to ValidatePurchase, the server verifies it with the store and grants
    /// once per order, and only then is the order confirmed (so an interrupted purchase is retried
    /// on the next launch). The Editor uses Unity IAP's fake store, accepted only in development.
    /// Every purchase finishes with a reply, even when the store never answers, so the screens that
    /// wait on it can't get stuck.
    /// </summary>
    public sealed class Shop
    {
        private static StoreController _store;
        private static Task _connecting;
        private static TaskCompletionSource<bool> _fetched = new TaskCompletionSource<bool>();

        private static readonly TimeSpan ConnectWait = TimeSpan.FromSeconds(15);

        /// <summary>The store's own purchase sheet waits on the player, so this is only a backstop.</summary>
        private static readonly TimeSpan PurchaseWait = TimeSpan.FromMinutes(5);

        private static readonly Dictionary<string, TaskCompletionSource<PurchaseReply>> Waiting = new Dictionary<string, TaskCompletionSource<PurchaseReply>>();
        private static readonly EconomyClient Economy = new EconomyClient();

        public static IReadOnlyList<UnityEngine.Purchasing.Product> Products => _store?.GetProducts() ?? (IReadOnlyList<UnityEngine.Purchasing.Product>)Array.Empty<UnityEngine.Purchasing.Product>();

        public static Task ConnectAsync()
        {
            if (_connecting == null || _connecting.IsFaulted)
            {
                _connecting = Connect();
            }

            return _connecting;
        }

        private static async Task Connect()
        {
            await UgsBootstrap.EnsureSignedInAsync();
            _store = UnityIAPServices.StoreController();
            _store.OnPurchasePending += OnPending;
            _store.OnPurchaseFailed += OnFailed;
            _store.OnPurchaseDeferred += OnDeferred;
            _store.OnProductsFetched += _ => _fetched.TrySetResult(true);
            _store.OnProductsFetchFailed += f =>
            {
                Debug.LogWarning("Store products didn't load: " + f.FailureReason);
                _fetched.TrySetResult(false);
            };
            await _store.Connect();

            var definitions = new List<ProductDefinition>();
            foreach (Wick.Economy.Product p in EconomyClient.Config.Products)
            {
                definitions.Add(new ProductDefinition(p.Id, p.Id, Kind(p.Kind)));
            }

            _store.FetchProducts(definitions);
            _store.FetchPurchases(); // finishes any purchase interrupted last session
        }

        /// <summary>The store's localised price, or the indicative GBP price before it loads.</summary>
        public static string Price(string productId)
        {
            foreach (UnityEngine.Purchasing.Product p in Products)
            {
                if (p.definition.id == productId && !string.IsNullOrEmpty(p.metadata.localizedPriceString))
                {
                    return p.metadata.localizedPriceString;
                }
            }

            Wick.Economy.Product item = EconomyClient.Config.Product(productId);
            return item != null ? "£" + item.Price : "";
        }

        public static async Task<PurchaseReply> BuyAsync(string productId)
        {
            Task connecting = ConnectAsync();
            if (await Task.WhenAny(connecting, Task.Delay(ConnectWait)) != connecting || connecting.IsFaulted)
            {
                return Fail(productId, "The store isn't available right now.");
            }

            await Task.WhenAny(_fetched.Task, Task.Delay(ConnectWait));
            if (!Listed(productId))
            {
                // Unity IAP never answers a purchase of a product the store doesn't list.
                return Fail(productId, "This item isn't in the store yet.");
            }

            var done = new TaskCompletionSource<PurchaseReply>();
            Waiting[productId] = done;
            _store.PurchaseProduct(productId);
            if (await Task.WhenAny(done.Task, Task.Delay(PurchaseWait)) != done.Task)
            {
                Waiting.Remove(productId);
                return Fail(productId, "The store didn't answer. If you were charged, it will be added next time you play.");
            }

            return done.Task.Result;
        }

        private static bool Listed(string productId)
        {
            foreach (UnityEngine.Purchasing.Product p in Products)
            {
                if (p.definition.id == productId && p.availableToPurchase)
                {
                    return true;
                }
            }

            return false;
        }

        private static PurchaseReply Fail(string productId, string reason)
        {
            return new PurchaseReply { Ok = false, ProductId = productId, Reason = reason };
        }

        private static async void OnPending(PendingOrder order)
        {
            string productId = null;
            foreach (CartItem item in order.CartOrdered.Items())
            {
                productId = item.Product.definition.id;
            }

            PurchaseReply reply;
            try
            {
                reply = await Economy.ValidatePurchaseAsync(Backoff.NewRequestId(), productId, order.Info.Receipt);
                if (reply.Ok)
                {
                    _store.ConfirmPurchase(order); // only after the server has granted it
                    if (reply.NewlyGranted)
                    {
                        Wick.Economy.Product item = EconomyClient.Config.Product(productId);
                        Telemetry.Purchase(productId, item?.Price ?? "");
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("Purchase not verified yet, will retry next launch: " + e.Message);
                reply = new PurchaseReply { Ok = false, Reason = "Couldn't reach the server. Your purchase will be added next time you play." };
            }

            Complete(productId, reply);
        }

        private static void OnFailed(FailedOrder order)
        {
            foreach (CartItem item in order.CartOrdered.Items())
            {
                Complete(item.Product.definition.id, new PurchaseReply { Ok = false, Reason = order.FailureReason.ToString() });
            }
        }

        /// <summary>Paid later (e.g. cash at a shop): the pending order arrives when the payment does.</summary>
        private static void OnDeferred(DeferredOrder order)
        {
            foreach (CartItem item in order.CartOrdered.Items())
            {
                Complete(item.Product.definition.id, Fail(item.Product.definition.id, "Payment pending. It will be added when the payment goes through."));
            }
        }

        private static void Complete(string productId, PurchaseReply reply)
        {
            if (productId != null && Waiting.TryGetValue(productId, out TaskCompletionSource<PurchaseReply> waiting))
            {
                Waiting.Remove(productId);
                waiting.TrySetResult(reply);
            }
        }

        private static ProductType Kind(string kind)
        {
            switch (kind)
            {
                case "consumable": return ProductType.Consumable;
                case "subscription": return ProductType.Subscription;
                default: return ProductType.NonConsumable;
            }
        }
    }
}
