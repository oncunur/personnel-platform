"use client";

import { FormEvent, useEffect, useMemo, useState } from "react";

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
  const [message, setMessage] = useState("Excel Import Center yükleniyor…");
  const [busy, setBusy] = useState(false);
  const companySystems = useMemo(() => systems.filter(x => x.companyId === companyId && x.isActive), [systems, companyId]);

  useEffect(() => { void bootstrap(); }, []);
  useEffect(() => { if (companyId) { void loadJobs(); if (!companySystems.some(x => x.id === systemId)) setSystemId(companySystems[0]?.id ?? ""); } }, [companyId, systems]);

  async function bootstrap() {
    const [companyResponse, systemResponse] = await Promise.all([authFetch("/api/v1/organization/companies"), authFetch("/api/v1/integrations/systems")]);
    const cs = companyResponse?.ok ? await companyResponse.json() as Company[] : [];
    const ss = systemResponse?.ok ? await systemResponse.json() as SystemRow[] : [];
    setCompanies(cs); setSystems(ss);
    const firstCompany = cs[0]?.id ?? ss[0]?.companyId ?? "";
    if (firstCompany) setCompanyId(firstCompany);
    setMessage("XLSX → kolon mapping → validation → partial import akışı hazır.");
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
      setMessage(`${result.job.totalRows} satır alındı. Kolon mapping'i kontrol edip doğrulayın.`);
      fileInput.value = ""; await loadJobs();
    } finally { setBusy(false); }
  }

  async function validateMapping() {
    if (!job) return;
    setBusy(true);
    try {
      const response = await authFetch(`/api/v1/imports/${job.id}/mapping`, { method: "PUT", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ version: job.version, mapping }) });
      if (!response?.ok) { setMessage(await apiError(response, "Mapping doğrulanamadı.")); return; }
      const result = await response.json() as Validation;
      setJob(result.job); setValidation(result); setErrors(result.errors);
      setMessage(`Validation tamamlandı: ${result.validRows} geçerli, ${result.invalidRows} hatalı satır.`);
      await loadJobs();
    } finally { setBusy(false); }
  }

  async function processImport() {
    if (!job || job.status !== "READY") return;
    if (!confirm(`${job.totalRows} satırlık import işlensin mi? Geçerli satırlar yazılacak, hatalı satırlar ayrı tutulacak.`)) return;
    setBusy(true);
    try {
      const response = await authFetch(`/api/v1/imports/${job.id}/process`, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ version: job.version }) });
      if (!response?.ok) { setMessage(await apiError(response, "Import işlenemedi.")); return; }
      const result = await response.json() as ProcessResult; setJob(result.job);
      setMessage(`Import tamamlandı: ${result.importedRows} başarılı, ${result.errorRows} satır hatalı.`);
      await Promise.all([loadErrors(result.job.id), loadJobs()]);
    } finally { setBusy(false); }
  }

  async function openJob(row: ImportJob) {
    setJob(row); setPreview([]); setMapping(row.mapping ?? {}); setValidation(null);
    await loadErrors(row.id);
    setMessage(`${row.originalFileName} · ${row.status}`);
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

  return <main className="shell">
    <a className="back" href="/dashboard">← Dashboard</a>
    <section className="hero compact"><span className="eyebrow">SPRINT 14 · INT-005</span><h1>Excel Import Center</h1><p>{message}</p></section>

    <section className="panel audit-panel"><div className="panel-heading"><div><span className="eyebrow dark">UPLOAD</span><h2>XLSX Kaynak</h2></div></div><form className="inline-form" onSubmit={upload}><label className="field-label">Şirket<select value={companyId} onChange={e => setCompanyId(e.target.value)} required><option value="">Seçin</option>{companies.map(x => <option key={x.id} value={x.id}>{x.code} · {x.name}</option>)}</select></label><label className="field-label">Entegrasyon sistemi<select value={systemId} onChange={e => setSystemId(e.target.value)} required><option value="">Seçin</option>{companySystems.map(x => <option key={x.id} value={x.id}>{x.code} · {x.systemType}</option>)}</select></label><label className="field-label">Hedef<select value={targetType} onChange={e => { setTargetType(e.target.value); setMapping({}); }}><option value="INTEGRATION_MAPPING">Integration Mapping</option><option value="ERP_ACCOUNT_MAPPING">ERP Account Mapping</option></select></label><label className="field-label">Excel<input name="file" type="file" accept=".xlsx,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" required /></label><button className="primary-button" disabled={busy}>Yükle & Preview</button></form></section>

    {job && !["COMPLETED", "PARTIAL", "FAILED"].includes(job.status) ? <section className="panel audit-panel"><div className="panel-heading"><div><span className="eyebrow dark">COLUMN MAPPING</span><h2>{job.originalFileName}</h2></div><strong>{job.totalRows} satır</strong></div><div className="inline-form">{fields.map(field => <label className="field-label" key={field}>{field}{field === "COUNTER_ACCOUNT_CODE" ? " (opsiyonel)" : ""}<select value={mapping[field] ?? ""} onChange={e => setMapping({ ...mapping, [field]: e.target.value })}><option value="">Eşleme yok</option>{job.headers.map(x => <option key={x} value={x}>{x}</option>)}</select></label>)}<button className="primary-button" type="button" disabled={busy} onClick={() => void validateMapping()}>Mapping doğrula</button>{job.status === "READY" ? <button className="secondary-button" type="button" disabled={busy} onClick={() => void processImport()}>Partial import işle</button> : null}</div>{validation ? <p><strong>{validation.validRows}</strong> geçerli · <strong>{validation.invalidRows}</strong> hatalı</p> : null}</section> : null}

    {preview.length > 0 ? <section className="panel audit-panel"><div className="panel-heading"><div><span className="eyebrow dark">PREVIEW</span><h2>İlk {preview.length} satır</h2></div></div><div className="table-wrap"><table className="data-table"><thead><tr><th>Row</th>{job?.headers.map(x => <th key={x}>{x}</th>)}</tr></thead><tbody>{preview.map(row => <tr key={row.rowNumber}><td>{row.rowNumber}</td>{job?.headers.map(h => <td key={h}>{row.values[h] || "—"}</td>)}</tr>)}</tbody></table></div></section> : null}

    {errors.length > 0 ? <section className="panel audit-panel"><div className="panel-heading"><div><span className="eyebrow dark">ROW ERRORS</span><h2>Hatalı Satırlar</h2></div><strong>{errors.length}</strong></div><div className="table-wrap"><table className="data-table"><thead><tr><th>Row</th><th>Kod</th><th>Mesaj</th><th>Kaynak</th></tr></thead><tbody>{errors.map(row => <tr key={row.rowNumber}><td>{row.rowNumber}</td><td><strong>{row.errorCode ?? "INVALID"}</strong></td><td>{row.errorMessage ?? "—"}</td><td><small>{Object.entries(row.values).map(([k,v]) => `${k}=${v}`).join(" · ")}</small></td></tr>)}</tbody></table></div></section> : null}

    <section className="panel audit-panel"><div className="panel-heading"><div><span className="eyebrow dark">HISTORY</span><h2>Import İşleri</h2></div><strong>{jobs.length}</strong></div><div className="table-wrap"><table className="data-table"><thead><tr><th>Dosya</th><th>Hedef</th><th>Durum</th><th>Satır</th><th>Başarılı / Hata</th><th>Zaman</th><th></th></tr></thead><tbody>{jobs.length === 0 ? <tr><td colSpan={7}>Import işi yok.</td></tr> : jobs.map(x => <tr key={x.id}><td><strong>{x.originalFileName}</strong></td><td>{x.targetType}</td><td>{x.status}</td><td>{x.totalRows}</td><td>{x.successRows} / {x.errorRows}</td><td>{dateTime(x.createdAt)}<small>{x.completedAt ? `Bitti: ${dateTime(x.completedAt)}` : ""}</small></td><td><button className="secondary-button" type="button" onClick={() => void openJob(x)}>Aç</button></td></tr>)}</tbody></table></div></section>
  </main>;
}
