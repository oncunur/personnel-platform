import { Icon } from "../components/Icon";
import { PageHeader } from "../components/PageHeader";

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
      payload: error instanceof Error ? error.message : "Bilinmeyen bağlantı hatası",
    };
  }
}

export default async function ApiHealthPage() {
  const health = await getHealth();

  return (
    <main className="shell narrow">
      <PageHeader eyebrow="Sistem durumu" title="Bağlantı kontrolü" description="Platform servislerinin yeni istekleri karşılamaya hazır olup olmadığını kontrol edin." actions={<a className="secondary-button" href="/">Ana sayfaya dön</a>}/>
      <section className={`panel attention-panel ${health.ok ? "success" : "danger"}`} role="status">
        <div className="panel-heading"><div><span className="eyebrow dark">Hazırlık denetimi</span><h2>{health.ok ? "Sistem kullanıma hazır" : "Sistem şu anda hazır değil"}</h2><p>{health.ok ? "Ana servis bağlantısı sağlıklı ve istek kabul ediyor." : "Ana servise ulaşılamadı veya hazırlık kontrolü başarısız oldu."}</p></div><span className={`status-badge ${health.ok ? "success" : "danger"}`}>{health.ok ? "Çevrimiçi" : "Kontrol gerekli"}</span></div>
        <div className="detail-grid"><div className="detail-item"><span>Bağlantı</span><strong>{health.ok ? "Başarılı" : "Başarısız"}</strong></div><div className="detail-item"><span>HTTP durumu</span><strong>{health.status}</strong></div><div className="detail-item"><span>Sonraki adım</span><strong>{health.ok ? "Platforma giriş yapın" : "Bir süre sonra tekrar deneyin"}</strong></div></div>
        <div className="detail-actions"><a className="primary-button" href={health.ok ? "/login" : "/api-health"}><Icon name={health.ok ? "arrow" : "workflow"} size={17}/>{health.ok ? "Platforma giriş yap" : "Tekrar kontrol et"}</a></div>
      </section>
      <details className="technical-details"><summary>Teknik servis yanıtını görüntüle</summary><pre>{JSON.stringify(health.payload, null, 2)}</pre></details>
    </main>
  );
}
