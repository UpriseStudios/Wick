// GET /api/vault/CODE -> the vault's public card, from Cloud Code's GetVaultCard, called as the
// UGS service account so web visitors never become players. Configure in Cloudflare Pages:
//   UGS_PROJECT_ID, UGS_ENVIRONMENT_ID   (variables)
//   UGS_KEY_ID, UGS_SECRET               (encrypted secrets; a service account allowed to run Cloud Code)

import { callModule, missingSettings } from "../../../lib/ugs.js";

const CODE = /^[23456789ABCDEFGHJKLMNPQRSTUVWXYZ]{8}$/;

export async function onRequestGet({ params, env }) {
  const code = String(params.code || "").toUpperCase();
  if (!CODE.test(code)) {
    return json({ ok: false, reason: "Not a vault code" }, 400, 3600);
  }

  const missing = missingSettings(env, ["UGS_PROJECT_ID", "UGS_ENVIRONMENT_ID", "UGS_KEY_ID", "UGS_SECRET"]);
  if (missing.length > 0) {
    // Names only, never values: says which settings this deployment's environment lacks.
    return json({ ok: false, reason: "Vault service unavailable", detail: "not configured: " + missing.join(", ") }, 500, 0);
  }

  try {
    const card = await callModule(env, "GetVaultCard", { code });
    return json(card, card && card.ok ? 200 : 404, 60);
  } catch (e) {
    return json({ ok: false, reason: "Vault service unavailable", detail: String(e.message || e) }, 502, 0);
  }
}

function json(body, status, maxAge) {
  return new Response(JSON.stringify(body), {
    status,
    headers: {
      "Content-Type": "application/json",
      "Cache-Control": maxAge > 0 ? `public, max-age=${maxAge}` : "no-store",
    },
  });
}
