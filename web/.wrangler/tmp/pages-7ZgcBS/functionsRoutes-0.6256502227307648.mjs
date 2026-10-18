import { onRequestGet as __api_vault__code__js_onRequestGet } from "C:\\Users\\jamie\\Wick\\web\\functions\\api\\vault\\[code].js"

export const routes = [
    {
      routePath: "/api/vault/:code",
      mountPath: "/api/vault",
      method: "GET",
      middlewares: [],
      modules: [__api_vault__code__js_onRequestGet],
    },
  ]