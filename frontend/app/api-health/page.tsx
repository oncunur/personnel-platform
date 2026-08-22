type HealthResult = {
  ok: boolean;
  status: string;
  payload?: unknown;
};

async function getHealth(): Promise<HealthResult> {
  const apiUrl = process.env.API_INTERNAL_URL ?? "http://localhost:8080";

  try {
    const response = await fetch(`${apiUrl}/health/ready`, { cache: "no-store" });
    const payload = await response.json();
    return { ok: response.ok, status: response.status.toString(), payload };
  } catch (error) {
    return {
      ok: false,
      status: "unreachable",
      payload: error instanceof Error ? error.message : "Unknown error",
    };
  }
}

export default async function ApiHealthPage() {
  const health = await getHealth();

  return (
    <main className="shell narrow">
      <a className="back" href="/">← Ana sayfa</a>
      <section className="hero compact">
        <span className="eyebrow">API READINESS</span>
        <h1>{health.ok ? "Sistem hazır" : "Sistem henüz hazır değil"}</h1>
        <p>HTTP durumu: {health.status}</p>
        <pre>{JSON.stringify(health.payload, null, 2)}</pre>
      </section>
    </main>
  );
}
