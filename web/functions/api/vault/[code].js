// GET /api/vault/CODE -> the vault's public card, from Cloud Code's GetVaultCard, called as the
// UGS service account so web visitors never become players. Configure in Cloudflare Pages:
//   UGS_PROJECT_ID, UGS_ENVIRONMENT_ID   (variables)
//   UGS_KEY_ID, UGS_SECRET               (encrypted secrets; a service account allowed to run Cloud Code)

const CODE = /^[23456789ABCDEFGHJKLMNPQRSTUVWXYZ]{8}$/;

export async function onRequestGet({ params, env }) {
  const code = String(params.code || "").toUpperCase();
  if (!CODE.test(code)) {
    return json({ ok: false, reason: "Not a vault code" }, 400, 3600);
  }

  const missing = ["UGS_PROJECT_ID", "UGS_ENVIRONMENT_ID", "UGS_KEY_ID", "UGS_SECRET"].filter(k => !env[k]);
  if (missing.length > 0) {
    // Names only, never values: says which settings this deployment's environment lacks.
    return json({ ok: false, reason: "Vault service unavailable", detail: "not configured: " + missing.join(", ") }, 500, 0);
  }

  try {
    const token = await serviceToken(env);
    const response = await fetch(
      `https://cloud-code.services.api.unity.com/v1/projects/${env.UGS_PROJECT_ID}/modules/WickModule/GetVaultCard`,
      {
        method: "POST",
        headers: { "Authorization": `Bearer ${token}`, "Content-Type": "application/json" },
        body: JSON.stringify({ params: { code } }),
      });
    if (!response.ok) {
      return json({ ok: false, reason: "Vault service unavailable", detail: "cloud code " + response.status }, 502, 0);
    }

    const card = (await response.json()).output;
    return json(card, card && card.ok ? 200 : 404, 60);
  } catch (e) {
    return json({ ok: false, reason: "Vault service unavailable", detail: String(e.message || e) }, 502, 0);
  }
}

async function serviceToken(env) {
  const url = `https://services.api.unity.com/auth/v1/token-exchange?projectId=${env.UGS_PROJECT_ID}&environmentId=${env.UGS_ENVIRONMENT_ID}`;
  const response = await fetch(url, {
    method: "POST",
    headers: { "Authorization": "Basic " + btoa(`${env.UGS_KEY_ID}:${env.UGS_SECRET}`) },
  });
  if (!response.ok) {
    throw new Error("token exchange " + response.status);
  }

  return (await response.json()).accessToken;
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
