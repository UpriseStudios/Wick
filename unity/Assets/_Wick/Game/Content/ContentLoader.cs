using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Object = UnityEngine.Object;

namespace Wick.Game
{
    /// <summary>
    /// Remote content (skins, themes, seasonal) through Addressables. The default skin and theme
    /// ship in the app; anything else is fetched ahead of time (Prefetch) and used once it has
    /// arrived. If a download fails or hasn't finished, the player simply gets the default: a raid
    /// never waits on, or breaks because of, the CDN. Catalogs are versioned: at start-up the game
    /// checks for a newer remote catalog and keeps the one it has if that check fails.
    /// </summary>
    public static class ContentLoader
    {
        private static readonly Dictionary<object, Object> Loaded = new Dictionary<object, Object>();
        private static readonly HashSet<object> Failed = new HashSet<object>();
        private static Task _init;

        /// <summary>Starts Addressables and applies any newer remote catalog. Safe to call repeatedly.</summary>
        public static Task InitializeAsync()
        {
            return _init ??= Initialize();
        }

        private static async Task Initialize()
        {
            try
            {
                await Addressables.InitializeAsync().Task;
                AsyncOperationHandle<List<string>> check = Addressables.CheckForCatalogUpdates(false);
                await check.Task;
                if (check.Status == AsyncOperationStatus.Succeeded && check.Result != null && check.Result.Count > 0)
                {
                    AsyncOperationHandle<List<UnityEngine.AddressableAssets.ResourceLocators.IResourceLocator>> update =
                        Addressables.UpdateCatalogs(check.Result, false);
                    await update.Task;
                    Addressables.Release(update);
                }

                Addressables.Release(check);
            }
            catch (Exception e)
            {
                Debug.LogWarning("Content catalog check failed; using the catalog we have: " + e.Message);
            }
        }

        /// <summary>The skin to show for id: fetched, bundled, or the default when it isn't here (yet).</summary>
        public static SkinDefinition Skin(SkinCatalog catalog, string id)
        {
            SkinListing listing = catalog.Listing(id);
            return Resolve(listing?.Bundled, listing?.Reference, catalog.Default);
        }

        public static ThemeDefinition Theme(ThemeCatalog catalog, string id)
        {
            ThemeListing listing = catalog.Listing(id);
            return Resolve(listing?.Bundled, listing?.Reference, catalog.Default);
        }

        /// <summary>Fetches the equipped skin and the chosen theme so the next raid can use them.</summary>
        public static async Task PrefetchAsync(SkinCatalog skins, ThemeCatalog themes)
        {
            await InitializeAsync();
            var work = new List<Task>();
            if (skins != null)
            {
                work.Add(Fetch(skins.Listing(skins.EquippedId)?.Reference));
            }

            if (themes != null)
            {
                work.Add(Fetch(themes.Listing(themes.CurrentId)?.Reference));
            }

            await Task.WhenAll(work);
        }

        /// <summary>
        /// The fallback rule on its own: a bundled asset wins, then a fetched one, then the default.
        /// In the Editor outside Play mode (tests, tools) the referenced asset is read directly.
        /// </summary>
        public static T Resolve<T>(T bundled, AssetReference reference, T fallback) where T : Object
        {
            if (bundled != null)
            {
                return bundled;
            }

            if (reference == null || !reference.RuntimeKeyIsValid())
            {
                return fallback;
            }

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                return reference.editorAsset as T ?? fallback;
            }
#endif
            return Loaded.TryGetValue(reference.RuntimeKey, out Object asset) && asset is T t ? t : fallback;
        }

        private static async Task Fetch(AssetReference reference)
        {
            if (reference == null || !reference.RuntimeKeyIsValid())
            {
                return;
            }

            object key = reference.RuntimeKey;
            if (Loaded.ContainsKey(key) || Failed.Contains(key))
            {
                return;
            }

            try
            {
                AsyncOperationHandle<Object> handle = Addressables.LoadAssetAsync<Object>(key);
                await handle.Task;
                if (handle.Status == AsyncOperationStatus.Succeeded && handle.Result != null)
                {
                    Loaded[key] = handle.Result; // kept for the session; skins and themes are small
                }
                else
                {
                    Failed.Add(key);
                    Debug.LogWarning("Couldn't download " + key + "; showing the default instead. " + handle.OperationException?.Message);
                    Addressables.Release(handle);
                }
            }
            catch (Exception e)
            {
                Failed.Add(key);
                Debug.LogWarning("Couldn't download " + key + "; showing the default instead. " + e.Message);
            }
        }
    }
}
