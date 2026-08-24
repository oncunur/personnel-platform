"use client";

import { FormEvent, useEffect, useMemo, useState } from "react";
import { useActionDialog } from "../components/ActionDialog";
import { Icon } from "../components/Icon";
import { PageHeader } from "../components/PageHeader";

const apiBase = process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:8080";
type AuthResponse = { accessToken: string };
type Company = { id: string; code: string; name: string };
type SystemRow = { id: string; companyId: string; code: string; name: string; systemType: string; isActive: boolean };
type ImportJob = { id: string; companyId: string; integrationSystemId: string; targetType: string; originalFileName: string; status: string; headers: string[]; mapping: Record<string, string>; totalRows: number; successRows: number; errorRows: number; createdAt: string; completedAt: string | null; version: number };
type PreviewRow = { rowNumber: number; values: Record<string, string> };
type ImportUpload = { job: ImportJob; suggestedMapping: Record<string, string>; previewRows: PreviewRow[] };
type ImportRow = { rowNumber: number; status: string; errorCode: string | null; errorMessage: string | null; processedEntityType: string | null; processedEntityId: string | null; values: Record<string, string> };
type Validation = { job: ImportJob; validRows: number; invalidRows: number; errors: ImportRow[] };
type ProcessResult = { job: ImportJob; importedRows: number; errorRows: number };

const targetFields: Record<string, string[]> = {
  INTEGRATION_MAPPING: ["ENTITY_TYPE", "EXTERNAL_CODE", "INTERNAL_ENTITY_ID"],
  ERP_ACCOUNT_MAPPING: ["COST_CATEGORY", "ACCOUNT_CODE", "COUNTER_ACCOUNT_CODE"],
};

const targetLabels: Record<string, string> = {
  INTEGRATION_MAPPING: "Entegrasyon kod eşlemeleri",
  ERP_ACCOUNT_MAPPING: "ERP hesap eşlemeleri",
};

const fieldLabels: Record<string, string> = {
  ENTITY_TYPE: "Kayıt türü",
  EXTERNAL_CODE: "Dış sistem kodu",
  INTERNAL_ENTITY_ID: "Platform kayıt numarası",
  COST_CATEGORY: "Maliyet türü",
  ACCOUNT_CODE: "ERP hesap kodu",
  COUNTER_ACCOUNT_CODE: "Karşı hesap kodu",
};

const jobStatuses: Record<string, { label: string; tone: string }> = {
  UPLOADED: { label: "Eşleme bekliyor", tone: "warning" },
  VALIDATING: { label: "Doğrulanıyor", tone: "warning" },
  READY: { label: "İşleme hazır", tone: "success" },
  PROCESSING: { label: "İşleniyor", tone: "warning" },
  COMPLETED: { label: "Tamamlandı", tone: "success" },
  PARTIAL: { label: "Kısmen tamamlandı", tone: "warning" },
  FAILED: { label: "Başarısız", tone: "danger" },
};

function statusOf(value: string) {
  return jobStatuses[value] ?? { label: value, tone: "" };
}

export default function ImportsPage() {
  const [companies, setCompanies] = useState<Company[]>([]);
  const [systems, setSystems] = useState<SystemRow[]>([]);
  const [companyId, setCompanyId] = useState("");
  const [systemId, setSystemId] = useState("");
  const [targetType, setTargetType] = useState("INTEGRATION_MAPPING");
  const [jobs, setJobs] = useState<ImportJob[]>([]);
  const [job, setJob] = useState<ImportJob | null>(null);
  const [preview, setPreview] = useState<PreviewRow[]>([]);
  const [mapping, setMapping] = useState<Record<string, string>>({});
  const [validation, setValidation] = useState<Validation | null>(null);
  const [errors, setErrors] = useState<ImportRow[]>([]);
  const [message, setMessage] = useState("Veri içe aktarma merkezi yükleniyor…");
  const [busy, setBusy] = useState(false);
  const { ask, dialog } = useActionDialog();
  const companySystems = useMemo(() => systems.filter(x => x.companyId === companyId && x.isActive), [systems, companyId]);
  const activeJobs = useMemo(() => jobs.filter(x => !["COMPLETED", "PARTIAL", "FAILED"].includes(x.status)), [jobs]);
  const completedJobs = useMemo(() => jobs.filter(x => ["COMPLETED", "PARTIAL"].includes(x.status)), [jobs]);
  const totalErrorRows = useMemo(() => jobs.reduce((sum, item) => sum + item.errorRows, 0), [jobs]);

  useEffect(() => { void bootstrap(); }, []);
  useEffect(() => { if (companyId) { void loadJobs(); if (!companySystems.some(x => x.id === systemId)) setSystemId(companySystems[0]?.id ?? ""); } }, [companyId, systems]);

  async function bootstrap() {
    const [companyResponse, systemResponse] = await Promise.all([authFetch("/api/v1/organization/companies"), authFetch("/api/v1/integrations/systems")]);
    const cs = companyResponse?.ok ? await companyResponse.json() as Company[] : [];
    const ss = systemResponse?.ok ? await systemResponse.json() as SystemRow[] : [];
    setCompanies(cs); setSystems(ss);
    const firstCompany = cs[0]?.id ?? ss[0]?.companyId ?? "";
    if (firstCompany) setCompanyId(firstCompany);
    setMessage("Dosya yükleme, kolon eşleme, doğrulama ve kısmi içe aktarma akışı hazır.");
  }

  async function loadJobs() {
    if (!companyId) return;
    const response = await authFetch(`/api/v1/imports/?companyId=${companyId}&take=100`);
    setJobs(response?.ok ? await response.json() as ImportJob[] : []);
  }

  async function upload(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = event.currentTarget;
    const fileInput = form.elements.namedItem("file") as HTMLInputElement;
    const file = fileInput.files?.[0];
    if (!file || !companyId || !systemId) { setMessage("Şirket, sistem ve XLSX dosyası zorunludur."); return; }
    const body = new FormData(); body.set("companyId", companyId); body.set("integrationSystemId", systemId); body.set("targetType", targetType); body.set("file", file);
    setBusy(true);
    try {
      const response = await authFetch("/api/v1/imports/upload", { method: "POST", body });
      if (!response?.ok) { setMessage(await apiError(response, "Excel yüklenemedi.")); return; }
      const result = await response.json() as ImportUpload;
      setJob(result.job); setPreview(result.previewRows); setMapping(result.suggestedMapping); setValidation(null); setErrors([]);
      setMessage(`${result.job.totalRows} satır alındı. Kolon eşlemesini kontrol edip doğrulayın.`);
      fileInput.value = ""; await loadJobs();
    } finally { setBusy(false); }
  }

  async function validateMapping() {
    if (!job) return;
    setBusy(true);
    try {
      const response = await authFetch(`/api/v1/imports/${job.id}/mapping`, { method: "PUT", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ version: job.version, mapping }) });
      if (!response?.ok) { setMessage(await apiError(response, "Kolon eşlemeleri doğrulanamadı.")); return; }
      const result = await response.json() as Validation;
      setJob(result.job); setValidation(result); setErrors(result.errors);
      setMessage(`Doğrulama tamamlandı: ${result.validRows} geçerli, ${result.invalidRows} hatalı satır.`);
      await loadJobs();
    } finally { setBusy(false); }
  }

  async function processImport() {
    if (!job || job.status !== "READY") return;
    const confirmed = await ask({
      title: "Geçerli satırlar aktarılsın mı?",
      description: `${job.originalFileName} dosyasındaki ${job.totalRows} satır işlenecek; geçerli satırlar kaydedilirken hatalı satırlar inceleme için ayrılacak.`,
      confirmLabel: "Aktarımı başlat",
    });
    if (!confirmed) return;
    setBusy(true);
    try {
      const response = await authFetch(`/api/v1/imports/${job.id}/process`, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ version: job.version }) });
      if (!response?.ok) { setMessage(await apiError(response, "İçe aktarma işlenemedi.")); return; }
      const result = await response.json() as ProcessResult; setJob(result.job);
      setMessage(`İçe aktarma tamamlandı: ${result.importedRows} başarılı, ${result.errorRows} satır hatalı.`);
      await Promise.all([loadErrors(result.job.id), loadJobs()]);
    } finally { setBusy(false); }
  }

  async function openJob(row: ImportJob) {
    setJob(row); setPreview([]); setMapping(row.mapping ?? {}); setValidation(null);
    await loadErrors(row.id);
    setMessage(`${row.originalFileName} · ${statusOf(row.status).label}`);
  }

  async function loadErrors(id: string) {
    const response = await authFetch(`/api/v1/imports/${id}/rows?errorsOnly=true&take=1000`);
    setErrors(response?.ok ? await response.json() as ImportRow[] : []);
  }

  async function authFetch(path: string, init?: RequestInit): Promise<Response | null> {
    let token = sessionStorage.getItem("pp_access_token") ?? await refresh();
    if (!token) { window.location.replace("/login"); return null; }
    let response = await fetch(`${apiBase}${path}`, { ...init, headers: { ...(init?.headers ?? {}), Authorization: `Bearer ${token}` }, credentials: "include" });
    if (response.status !== 401) return response;
    token = await refresh(); if (!token) { window.location.replace("/login"); return response; }
    return fetch(`${apiBase}${path}`, { ...init, headers: { ...(init?.headers ?? {}), Authorization: `Bearer ${token}` }, credentials: "include" });
  }
  async function refresh(): Promise<string | null> { try { const r = await fetch(`${apiBase}/api/v1/auth/refresh`, { method: "POST", credentials: "include" }); if (!r.ok) return null; const b = await r.json() as AuthResponse; sessionStorage.setItem("pp_access_token", b.accessToken); return b.accessToken; } catch { return null; } }
  async function apiError(response: Response | null, fallback: string) { if (!response) return fallback; const body = await response.json().catch(() => null) as { error?: { code?: string; message?: string } } | null; return body?.error?.code ? `${body.error.code}: ${body.error.message ?? fallback}` : body?.error?.message ?? fallback; }
  const dateTime = (value: string | null) => value ? new Date(value).toLocaleString("tr-TR") : "—";
  const fields = targetFields[targetType] ?? [];

  return <main className="page-shell">
    <PageHeader eyebrow="Veri yönetimi" title="Veri içe aktarma" description="Excel dosyalarını yükleyin, kolonları sistem alanlarıyla eşleyin ve hatalı satırları işlem öncesinde ayırın." status={message} actions={<a className="secondary-button" href="/erp"><Icon name="plug" size={17}/>ERP aktarımı</a>}/>

    <section className="stat-grid" aria-label="İçe aktarma özeti">
      <article className="stat-card"><span className="stat-icon"><Icon name="box"/></span><span className="stat-copy"><strong>{jobs.length}</strong><span>Toplam dosya işi</span></span></article>
      <article className="stat-card"><span className="stat-icon"><Icon name="workflow"/></span><span className="stat-copy"><strong>{activeJobs.length}</strong><span>İşlem bekleyen</span></span></article>
      <article className="stat-card"><span className="stat-icon"><Icon name="chart"/></span><span className="stat-copy"><strong>{completedJobs.length}</strong><span>Tamamlanan iş</span></span></article>
      <article className="stat-card"><span className="stat-icon"><Icon name="bell"/></span><span className="stat-copy"><strong>{totalErrorRows}</strong><span>Toplam hatalı satır</span></span></article>
    </section>

    <section className="panel workspace-panel wide-workspace">
      <div className="workspace-copy"><span className="eyebrow dark">Aktarım kapsamı</span><h2>Dosyanın hedefini belirleyin</h2><p>Şirket, bağlı sistem ve aktarılacak veri türü tüm dosya için geçerlidir.</p></div>
      <div className="inline-form workspace-select">
        <label className="field-label">Şirket<select value={companyId} onChange={e => setCompanyId(e.target.value)} required><option value="">Şirket seçin</option>{companies.map(x => <option key={x.id} value={x.id}>{x.code} · {x.name}</option>)}</select></label>
        <label className="field-label">Entegrasyon sistemi<select value={systemId} onChange={e => setSystemId(e.target.value)} required><option value="">Sistem seçin</option>{companySystems.map(x => <option key={x.id} value={x.id}>{x.code} · {x.name}</option>)}</select></label>
        <label className="field-label">Aktarım türü<select value={targetType} onChange={e => { setTargetType(e.target.value); setMapping({}); }}><option value="INTEGRATION_MAPPING">Entegrasyon kod eşlemeleri</option><option value="ERP_ACCOUNT_MAPPING">ERP hesap eşlemeleri</option></select></label>
      </div>
    </section>

    <div className="process-guide" aria-label="İçe aktarma adımları">
      <div className="process-guide-item"><span className="process-guide-number">1</span><span><strong>Dosyayı yükleyin</strong><small>XLSX dosyasının ilk satırları önizlenir.</small></span></div>
      <div className="process-guide-item"><span className="process-guide-number">2</span><span><strong>Kolonları eşleyin</strong><small>Dosya başlıklarını sistem alanlarıyla bağlayın.</small></span></div>
      <div className="process-guide-item"><span className="process-guide-number">3</span><span><strong>Doğrulayın ve işleyin</strong><small>Geçerli satırlar yazılır, hatalar ayrı tutulur.</small></span></div>
    </div>

    <div className="content-stack">
      <section className="panel">
        <div className="panel-heading"><div><span className="eyebrow dark">1 · Dosya seçimi</span><h2>Excel dosyası yükle</h2><p>Yalnızca .xlsx biçimindeki dosyalar kabul edilir.</p></div></div>
        <form className="file-upload-surface" onSubmit={upload}><label className="field-label">Aktarılacak dosya<input name="file" type="file" accept=".xlsx,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" required /></label><div><strong>{targetLabels[targetType]}</strong><small>Seçili şirket ve sisteme aktarılacak</small></div><button className="primary-button" disabled={busy || !companyId || !systemId}><Icon name="plus" size={17}/>Dosyayı yükle</button></form>
      </section>

      {job ? <section className="panel attention-panel">
        <div className="panel-heading"><div><span className="eyebrow dark">Seçili iş</span><h2>{job.originalFileName}</h2><p>{targetLabels[job.targetType] ?? job.targetType}</p></div><span className={`status-badge ${statusOf(job.status).tone}`}>{statusOf(job.status).label}</span></div>
        <div className="detail-grid"><div className="detail-item"><span>Toplam satır</span><strong>{job.totalRows}</strong></div><div className="detail-item"><span>Başarılı</span><strong className="amount-positive">{job.successRows}</strong></div><div className="detail-item"><span>Hatalı</span><strong className={job.errorRows ? "amount-negative" : ""}>{job.errorRows}</strong></div></div>
      </section> : null}

      {job && !["COMPLETED", "PARTIAL", "FAILED"].includes(job.status) ? <section className="panel">
        <div className="panel-heading"><div><span className="eyebrow dark">2 · Kolon eşleme</span><h2>Sistem alanlarını eşleyin</h2><p>Her sistem alanı için dosyadaki karşılık gelen kolonu seçin.</p></div><strong>{job.totalRows}</strong></div>
        <div className="form-surface"><div className="inline-form">{fields.map(field => <label className="field-label" key={field}>{fieldLabels[field] ?? field}{field === "COUNTER_ACCOUNT_CODE" ? " (isteğe bağlı)" : ""}<select value={mapping[field] ?? ""} onChange={e => setMapping({ ...mapping, [field]: e.target.value })}><option value="">Kolon seçilmedi</option>{job.headers.map(x => <option key={x} value={x}>{x}</option>)}</select><small>{field}</small></label>)}</div></div>
        <div className="action-row"><button className="primary-button" type="button" disabled={busy} onClick={() => void validateMapping()}><Icon name="workflow" size={17}/>Eşlemeyi doğrula</button>{job.status === "READY" ? <button className="secondary-button button-success" type="button" disabled={busy} onClick={() => void processImport()}>Geçerli satırları aktar</button> : null}</div>
        {validation ? <div className={`selected-summary ${validation.invalidRows ? "" : "success"}`}><span className="selected-summary-copy"><strong>Doğrulama tamamlandı</strong><small>{validation.validRows} geçerli satır işlenebilir durumda.</small></span><span className={validation.invalidRows ? "review-count" : "amount-positive"}>{validation.invalidRows} hatalı satır</span></div> : null}
      </section> : null}

      {preview.length > 0 ? <section className="panel"><div className="panel-heading"><div><span className="eyebrow dark">Dosya önizlemesi</span><h2>İlk {preview.length} satır</h2><p>Aktarım öncesinde başlıkları ve örnek değerleri kontrol edin.</p></div></div><div className="table-wrap"><table className="data-table"><thead><tr><th>Satır</th>{job?.headers.map(x => <th key={x}>{x}</th>)}</tr></thead><tbody>{preview.map(row => <tr key={row.rowNumber}><td>{row.rowNumber}</td>{job?.headers.map(h => <td key={h}>{row.values[h] || "—"}</td>)}</tr>)}</tbody></table></div></section> : null}

      {errors.length > 0 ? <section className="panel attention-panel danger"><div className="panel-heading"><div><span className="eyebrow dark">İnceleme gerekli</span><h2>Hatalı satırlar</h2><p>Bu satırlar aktarılmadı. Dosyadaki değerleri düzelterek yeniden yükleyebilirsiniz.</p></div><strong>{errors.length}</strong></div><div className="table-wrap"><table className="data-table"><thead><tr><th>Satır</th><th>Hata kodu</th><th>Açıklama</th><th>Dosyadaki değerler</th></tr></thead><tbody>{errors.map(row => <tr key={row.rowNumber}><td>{row.rowNumber}</td><td><span className="status-badge danger">{row.errorCode ?? "Geçersiz"}</span></td><td>{row.errorMessage ?? "Açıklama bulunmuyor"}</td><td><small>{Object.entries(row.values).map(([k,v]) => `${k}: ${v}`).join(" · ")}</small></td></tr>)}</tbody></table></div></section> : null}

      <section className="panel"><div className="panel-heading"><div><span className="eyebrow dark">Geçmiş</span><h2>İçe aktarma işleri</h2><p>Daha önce yüklenen dosyaları ve sonuçlarını inceleyin.</p></div><strong>{jobs.length}</strong></div><div className="table-wrap"><table className="data-table"><thead><tr><th>Dosya</th><th>Aktarım türü</th><th>Durum</th><th>Satır</th><th>Başarılı / Hata</th><th>Zaman</th><th>İşlem</th></tr></thead><tbody>{jobs.length === 0 ? <tr><td className="empty-row" colSpan={7}>Henüz bir dosya yüklenmedi.</td></tr> : jobs.map(x => { const status = statusOf(x.status); return <tr key={x.id} className={job?.id === x.id ? "selected-row" : ""}><td><strong>{x.originalFileName}</strong><small>{x.id.slice(0, 8)}</small></td><td>{targetLabels[x.targetType] ?? x.targetType}</td><td><span className={`status-badge ${status.tone}`}>{status.label}</span></td><td>{x.totalRows}</td><td><span className="amount-positive">{x.successRows}</span> / <span className={x.errorRows ? "amount-negative" : ""}>{x.errorRows}</span></td><td>{dateTime(x.createdAt)}<small>{x.completedAt ? `Tamamlanma: ${dateTime(x.completedAt)}` : ""}</small></td><td><button className="secondary-button" type="button" onClick={() => void openJob(x)}>İncele</button></td></tr>; })}</tbody></table></div></section>
    </div>
    {dialog}
  </main>;
}
