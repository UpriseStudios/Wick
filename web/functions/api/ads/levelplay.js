// LevelPlay's rewarded server-to-server callback. Set the callback URL in LevelPlay to:
//   https://wick.uprisestudios.co.uk/api/ads/levelplay?userId=[USER_ID]&rewards=[REWARDS]&eventId=[EVENT_ID]&dynamicUserId=[DYNAMIC_USER_ID]
// (LevelPlay appends timestamp and signature), and its Private Key as the LEVELPLAY_PRIVATE_KEY secret.
// The game sets the dynamic user id to "nonce~placement~context" before showing the ad.
// The reward itself is decided by Cloud Code (AdCallback): placement, caps and effects, once per eventId.

import { callModule, missingSettings } from "../../../lib/ugs.js";

const SETTINGS = ["UGS_PROJECT_ID", "UGS_ENVIRONMENT_ID", "UGS_KEY_ID", "UGS_SECRET", "LEVELPLAY_PRIVATE_KEY"];

export async function onRequestGet({ request, env }) {
  const q = new URL(request.url).searchParams;
  const userId = q.get("userId") || q.get("applicationUserId") || "";
  const eventId = q.get("eventId") || "";
  const rewards = q.get("rewards") || "";
  const timestamp = q.get("timestamp") || "";
  const signature = (q.get("signature") || "").toLowerCase();
  const dynamic = q.get("dynamicUserId") || q.get("dynamic_user_id") || "";

  const missing = missingSettings(env, SETTINGS);
  if (missing.length > 0) {
    return new Response("not configured: " + missing.join(", "), { status: 500 });
  }

  // md5(timestamp + eventId + userId + rewards + privateKey), userId URL-decoded (searchParams already is).
  const expected = await md5(timestamp + eventId + userId + rewards + env.LEVELPLAY_PRIVATE_KEY);
  if (!eventId || !userId || signature !== expected) {
    return new Response("bad signature", { status: 403 });
  }

  const [nonce, placement, context] = dynamic.split("~");
  try {
    await callModule(env, "AdCallback", { playerId: userId, placement: placement || "", adContext: context || "", nonce: nonce || eventId, eventId });
  } catch (e) {
    return new Response("try again", { status: 500 }); // LevelPlay retries until it sees eventId:OK
  }

  // Granted or refused (caps, wrong context), the event is settled: stop LevelPlay retrying.
  return new Response(eventId + ":OK", { headers: { "Content-Type": "text/plain" } });
}

async function md5(text) {
  const digest = await crypto.subtle.digest("MD5", new TextEncoder().encode(text)); // Workers support MD5
  return [...new Uint8Array(digest)].map(b => b.toString(16).padStart(2, "0")).join("");
}
