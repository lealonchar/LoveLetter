# Love Letter — Multiplayer Web Game

Vue 3 frontend + ASP.NET Core SignalR backend.

## Deployment settings

Railway backend environment variables:

```text
AllowedOrigins=https://your-vercel-app.vercel.app
```

Use the exact Vercel site origin with no trailing slash. If you use Vercel preview deployments too, add them comma-separated.

Vercel frontend environment variables:

```text
VITE_SERVER_URL=https://your-railway-backend.up.railway.app
```

Use the Railway public backend URL with no `/gamehub` at the end. Redeploy the frontend after changing `VITE_SERVER_URL`, because Vite reads it during the build.


