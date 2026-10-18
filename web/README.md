# wick.uprisestudios.co.uk

The share-link site, hosted on Cloudflare Pages. Plain HTML, CSS and a little JavaScript.

| Path | What |
| --- | --- |
| `/v/CODE` | Vault page (`public/vault.html` via `_redirects`): vault card, store buttons, copy code |
| `/api/vault/CODE` | Pages Function: the vault card from Cloud Code `GetVaultCard`, as a service account |
| `/.well-known/assetlinks.json` | Android App Links: package and signing-certificate fingerprints |
| `/.well-known/apple-app-site-association` | iOS Universal Links (replace `TEAMID` with the Apple Team ID) |

## Deploy

From `web/` (so Wrangler picks up `functions/` too):

```
cd web
npx wrangler pages deploy public --project-name wick-links
```

Then in the Pages project: add the custom domain `wick.uprisestudios.co.uk`, and set
`UGS_PROJECT_ID`, `UGS_ENVIRONMENT_ID` and the secrets `UGS_KEY_ID`, `UGS_SECRET` (a UGS service
account allowed to run Cloud Code).

## Signing fingerprints

`assetlinks.json` lists the debug key that signs development APKs on the build machine. Add the
Play App Signing key's SHA-256 (Play Console > Setup > App signing) before any store build, and
any other machine's debug key that builds test APKs.
