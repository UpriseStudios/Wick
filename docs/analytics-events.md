# Analytics events

Custom events the game sends to UGS Analytics (`Services/Telemetry.cs`). Each needs its schema in
the UGS dashboard (Analytics > Event Manager) before it shows up; events that don't match a schema
are rejected. Nothing is sent without consent: adults only, and with UMP configured only when the
player agreed (development builds count as agreeing until UMP is set up).

| Event | Parameter | Type | Notes |
| --- | --- | --- | --- |
| `raid_end` | `vaultId` | string | |
| | `outcome` | string | Escaped, Snuffed or TimedOut |
| | `stars` | integer | |
| | `durationTicks` | integer | 30 ticks per second |
| | `flickersLeft` | integer | |
| | `snuffCause` | string | Object type that took the last flicker; empty unless snuffed |
| `raid_quit` | `tick` | integer | Left the app mid-raid |
| | `flickersLeft` | integer | |
| `vault_published` | `traps` | integer | Traps and guardians |
| | `buildPoints` | integer | |
| | `clearCheckAttempts` | integer | Test runs since the last publish |
| `vault_shared` | `channel` | string | link or clip |
| | `vaultCode` | string | |
| `link_opened` | `vaultCode` | string | |
| | `installed` | boolean | True when the app was already installed |
| | `source` | string | link, referrer or paste |
| `ad_reward` | `placement` | string | raid_double_loot, upgrade_speedup, daily_vault_retry, shop_chest |
| `purchase` | `product` | string | Product id |
| | `price` | string | Indicative GBP price |
| `pass_tier` | `tier` | integer | Tier claimed |
| | `daysIntoSeason` | integer | |
