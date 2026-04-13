type HttpClient = {
  fetch(url: string, init?: RequestInit): Promise<Response>;
};

type ProjectionSnapshot = {
  revision: number;
  refreshedAtUtc: string;
  pendingOrders: number;
  failedOrders: number;
  stale: boolean;
};

type DiagnosticsRecord = {
  route: string;
  elapsedMilliseconds: number;
  statusCode: number;
  correlationId: string;
};

async function ensureOk(response: Response, route: string): Promise<Response> {
  if (!response.ok) {
    throw new Error(`request to ${route} failed with status ${response.status}`);
  }

  return response;
}

export async function loadClient(httpClient: HttpClient): Promise<void> {
  await ensureOk(await httpClient.fetch("/api/orders"), "/api/orders");
  await ensureOk(await httpClient.fetch("/api/orders/projection"), "/api/orders/projection");
  await ensureOk(await httpClient.fetch("/api/orders/health"), "/api/orders/health");
}

export async function sendAsync(httpClient: HttpClient, payload: unknown): Promise<Response> {
  return ensureOk(
    await httpClient.fetch("/api/send", {
      method: "POST",
      headers: {
        "content-type": "application/json",
        "x-client-feature": "projection-refresh",
      },
      body: JSON.stringify(payload),
    }),
    "/api/send",
  );
}

export async function retrySendAsync(httpClient: HttpClient, payload: unknown): Promise<Response> {
  return ensureOk(
    await httpClient.fetch("/api/send/retry", {
      method: "POST",
      headers: {
        "content-type": "application/json",
        "x-retry-reason": "projection-lag",
      },
      body: JSON.stringify(payload),
    }),
    "/api/send/retry",
  );
}

export async function loadProjection(httpClient: HttpClient): Promise<ProjectionSnapshot> {
  const response = await ensureOk(
    await httpClient.fetch("/api/orders/projection/details"),
    "/api/orders/projection/details",
  );

  return response.json() as Promise<ProjectionSnapshot>;
}

export async function loadProjectionHistory(httpClient: HttpClient): Promise<ProjectionSnapshot[]> {
  const response = await ensureOk(
    await httpClient.fetch("/api/orders/projection/history?window=24h"),
    "/api/orders/projection/history?window=24h",
  );

  return response.json() as Promise<ProjectionSnapshot[]>;
}

export async function loadDiagnostics(httpClient: HttpClient): Promise<DiagnosticsRecord[]> {
  const response = await ensureOk(
    await httpClient.fetch("/api/orders/diagnostics/httpclient"),
    "/api/orders/diagnostics/httpclient",
  );

  return response.json() as Promise<DiagnosticsRecord[]>;
}

export async function warmProjection(httpClient: HttpClient): Promise<void> {
  await ensureOk(
    await httpClient.fetch("/api/orders/projection/warmup", {
      method: "POST",
      headers: {
        "x-warmup-mode": "generated-bindings",
      },
    }),
    "/api/orders/projection/warmup",
  );
}
