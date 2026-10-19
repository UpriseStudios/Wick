// Calls WickModule as the UGS service account (UGS_KEY_ID / UGS_SECRET), so the site's endpoints
// act for the server, never as a player. Shared by the Pages Functions.

export async function serviceToken(env) {
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

export async function callModule(env, fn, params) {
  const token = await serviceToken(env);
  const response = await fetch(
    `https://cloud-code.services.api.unity.com/v1/projects/${env.UGS_PROJECT_ID}/modules/WickModule/${fn}`,
    {
      method: "POST",
      headers: { "Authorization": `Bearer ${token}`, "Content-Type": "application/json" },
      body: JSON.stringify({ params }),
    });
  if (!response.ok) {
    throw new Error("cloud code " + response.status);
  }

  return (await response.json()).output;
}

export function missingSettings(env, names) {
  return names.filter(k => !env[k]);
}
