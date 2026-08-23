"use client";

import { FormEvent, useEffect, useMemo, useState } from "react";

const apiBase = process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:8080";
type AuthResponse = { accessToken: string };
type Company = { id: string; code: string; name: string };
type SystemRow = { id: string; companyId: string; code: string; name: string; systemType: string; isActive: boolean };
type AccountMapping = { id: string; companyId: string; integrationSystemId: string; costCategory: string; accountCode: string; counterAccountCode: string | null; isActive: boolean; version: number };
type CurrencyTotal = { currency: string; sentAmount: number; acceptedAmount: number; varianceAmount: number };
type Batch = { id: string; companyId: string; integrationSystemId: string; fromDate: string; toDate: string; status: string; lineCount: number; acceptedLines: number; rejectedLines: number; totals: CurrencyTotal[]; createdAt: string; sentAt: string | null; reconciledAt: string | null; closedAt: string | null; version: number };
type Line = { id: string; batchId: string; costEntryId: string; externalLineKey: string; sourceType: string; sourceId: string; employeeId: string | null; projectId: string | null; costCenterId: string | null; costDate: string; costCategory: string; accountCode: string; counterAccountCode: string | null; sentAmount: number; currency: string; status: string; acceptedAmount: number | null; varianceAmount: number | null; externalReference: string | null; errorCode: string | null; errorMessage: string | null; reconciledAt: string | null };
type ReconDraft = { status: string; acceptedAmount: string; externalReference: string; errorCode: string; errorMessage: string };

export default function ErpPage() {
  const today = new Date().toISOString().slice(0, 10);
  const [companies, setCompanies] = useState<Company[]>([]);
  const [systems, setSystems] = useState<SystemRow[]>([]);
  const [companyId, setCompanyId] = useState("");
  const [systemId, setSystemId] = useState("");
  const [mappings, setMappings] = useState<AccountMapping[]>([]);
  const [batches, setBatches] = useState<Batch[]>([]);
  const [selectedBatch, setSelectedBatch] = useState<Batch | null>(null);
  const [lines, setLines] = useState<Line[]>([]);
  const [recon, setRecon] = useState<Record<string, ReconDraft>>({});
  const [from, setFrom] = useState(`${today.slice(0, 7)}-01`);
  const [to, setTo] = useState(today);
  const [mappingForm, setMappingForm] = useState({ costCategory: "PAYROLL", accountCode: "", counterAccountCode: "" });
  const [message, setMessage] = useState("ERP Center yükleniyor…");
  const [busy, setBusy] = useState(false);
  const erpSystems = useMemo(() => systems.filter(x => x.companyId === companyId && x.systemType === "ERP" && x.isActive), [systems, companyId]);

  useEffect(() => { void bootstrap(); }, []);
  useEffect(() => { if (companyId && !erpSystems.some(x => x.id === systemId)) setSystemId(erpSystems[0]?.id ?? ""); }, [companyId, systems]);
  useEffect(() => { if (systemId) void loadErpData(); else { setMappings([]); setBatches([]); setSelectedBatch(null); setLines([]); } }, [systemId]);

  async function bootstrap() {
    const [companyResponse, systemResponse] = await Promise.all([authFetch("/api/v1/organization/companies"), authFetch("/api/v1/integrations/systems")]);
    const cs = companyResponse?.ok ? await companyResponse.json() as Company[] : [];
    const ss = systemResponse?.ok ? await systemResponse.json() as SystemRow[] : [];
    setCompanies(cs); setSystems(ss);
    const firstErp = ss.find(x => x.systemType === "ERP" && x.isActive);
    const firstCompany = cs.find(x => x.id === firstErp?.companyId)?.id ?? firstErp?.companyId ?? cs[0]?.id ?? "";
    if (firstCompany) setCompanyId(firstCompany);
    if (firstErp) setSystemId(firstErp.id);
    setMessage("Cost category mapping, export batch ve reconciliation akışı hazır.");
  }

  async function loadErpData() {
    if (!systemId) return;
    const [mappingResponse, batchResponse] = await Promise.all([
      authFetch(`/api/v1/erp/account-mappings?systemId=${systemId}`),
      authFetch(`/api/v1/erp/batches?companyId=${companyId}&systemId=${systemId}&take=100`),
    ]);
    setMappings(mappingResponse?.ok ? await mappingResponse.json() as AccountMapping[] : []);
    const rows = batchResponse?.ok ? await batchResponse.json() as Batch[] : [];
    setBatches(rows);
    if (selectedBatch) {
      const current = rows.find(x => x.id === selectedBatch.id) ?? null;
      setSelectedBatch(current);
      if (current) await loadLines(current);
    }
  }

  async function createMapping(event: FormEvent) {
    event.preventDefault(); if (!companyId || !systemId) return;
    setBusy(true);
    try {
      const response = await authFetch("/api/v1/erp/account-mappings", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ companyId, integrationSystemId: systemId, costCategory: mappingForm.costCategory, accountCode: mappingForm.accountCode, counterAccountCode: mappingForm.counterAccountCode || null }) });
      if (!response?.ok) { setMessage(await apiError(response, "Account mapping oluşturulamadı.")); return; }
      setMappingForm(x => ({ ...x, accountCode: "", counterAccountCode: "" })); setMessage("ERP account mapping oluşturuldu."); await loadErpData();
    } finally { setBusy(false); }
  }

  async function toggleMapping(row: AccountMapping) {
    setBusy(true);
    try {
      const response = await authFetch(`/api/v1/erp/account-mappings/${row.id}`, { method: "PUT", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ accountCode: row.accountCode, counterAccountCode: row.counterAccountCode, isActive: !row.isActive, version: row.version }) });
      if (!response?.ok) { setMessage(await apiError(response, "Mapping güncellenemedi.")); return; }
      setMessage(`Mapping ${row.isActive ? "pasife" : "aktife"} alındı.`); await loadErpData();
    } finally { setBusy(false); }
  }

  async function createBatch(event: FormEvent) {
    event.preventDefault(); if (!companyId || !systemId) return;
    setBusy(true);
    try {
      const response = await authFetch("/api/v1/erp/batches", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ companyId, integrationSystemId: systemId, fromDate: from, toDate: to }) });
      if (!response?.ok) { setMessage(await apiError(response, "ERP batch oluşturulamadı.")); return; }
      const batch = await response.json() as Batch; setSelectedBatch(batch); setMessage(`${batch.lineCount} maliyet satırı ile ERP batch oluşturuldu.`); await loadErpData(); await loadLines(batch);
    } finally { setBusy(false); }
  }

  async function loadLines(batch: Batch) {
    const response = await authFetch(`/api/v1/erp/batches/${batch.id}/lines`);
    const rows = response?.ok ? await response.json() as Line[] : [];
    setLines(rows); setSelectedBatch(batch);
    const draft: Record<string, ReconDraft> = {};
    for (const line of rows) draft[line.externalLineKey] = { status: line.status === "REJECTED" ? "REJECTED" : "ACCEPTED", acceptedAmount: String(line.acceptedAmount ?? line.sentAmount), externalReference: line.externalReference ?? "", errorCode: line.errorCode ?? "", errorMessage: line.errorMessage ?? "" };
    setRecon(draft);
  }

  async function downloadBatch(batch: Batch) {
    setBusy(true);
    try {
      const response = await authFetch(`/api/v1/erp/batches/${batch.id}/file`);
      if (!response?.ok) { setMessage(await apiError(response, "ERP dosyası indirilemedi.")); return; }
      const blob = await response.blob(); const url = URL.createObjectURL(blob); const a = document.createElement("a"); a.href = url; a.download = `erp-batch-${batch.id}.csv`; a.click(); window.setTimeout(() => URL.revokeObjectURL(url), 60000);
    } finally { setBusy(false); }
  }

  async function sendBatch(batch: Batch) {
    if (!confirm("Batch ERP'ye gönderilmiş olarak işaretlensin mi? Bu işlemden sonra cost satırları yeni batch'e alınmaz.")) return;
    setBusy(true);
    try {
      const response = await authFetch(`/api/v1/erp/batches/${batch.id}/send`, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ version: batch.version }) });
      if (!response?.ok) { setMessage(await apiError(response, "Batch SENT durumuna alınamadı.")); return; }
      const updated = await response.json() as Batch; setSelectedBatch(updated); setMessage("ERP batch SENT durumuna alındı; reconciliation bekleniyor."); await loadErpData(); await loadLines(updated);
    } finally { setBusy(false); }
  }

  async function reconcileBatch() {
    if (!selectedBatch) return;
    const payload = lines.map(line => { const x = recon[line.externalLineKey]; return { externalLineKey: line.externalLineKey, status: x?.status ?? "ACCEPTED", acceptedAmount: Number(x?.acceptedAmount ?? line.sentAmount), externalReference: x?.externalReference || null, errorCode: x?.errorCode || null, errorMessage: x?.errorMessage || null }; });
    setBusy(true);
    try {
      const response = await authFetch(`/api/v1/erp/batches/${selectedBatch.id}/reconcile`, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ version: selectedBatch.version, lines: payload }) });
      if (!response?.ok) { setMessage(await apiError(response, "Reconciliation kaydedilemedi.")); return; }
      const updated = await response.json() as Batch; setSelectedBatch(updated); setMessage(`Reconciliation tamamlandı: ${updated.status}. Variance sıfır ve tüm satırlar ACCEPTED ise batch kapatılabilir.`); await loadErpData(); await loadLines(updated);
    } finally { setBusy(false); }
  }

  async function closeBatch(batch: Batch) {
    setBusy(true);
    try {
      const response = await authFetch(`/api/v1/erp/batches/${batch.id}/close`, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ version: batch.version }) });
      if (!response?.ok) { setMessage(await apiError(response, "Batch kapatılamadı.")); return; }
      const updated = await response.json() as Batch; setSelectedBatch(updated); setMessage("ERP batch CLOSED. Reconciliation tamamlandı."); await loadErpData();
    } finally { setBusy(false); }
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
  const money = (value: number, currency: string) => `${value.toLocaleString("tr-TR", { minimumFractionDigits: 2, maximumFractionDigits: 2 })} ${currency}`;
  const totals = (batch: Batch) => batch.totals.map(x => `${x.currency}: sent ${x.sentAmount.toFixed(2)} / accepted ${x.acceptedAmount.toFixed(2)} / Δ ${x.varianceAmount.toFixed(2)}`).join(" · ") || "—";

  return <main className="shell">
    <a className="back" href="/dashboard">← Dashboard</a>
    <section className="hero compact"><span className="eyebrow">SPRINT 14 · INT-006 / INT-007</span><h1>ERP Export & Reconciliation</h1><p>{message}</p></section>
    <section className="panel audit-panel"><div className="inline-form"><label className="field-label">Şirket<select value={companyId} onChange={e => setCompanyId(e.target.value)}><option value="">Seçin</option>{companies.map(x => <option key={x.id} value={x.id}>{x.code} · {x.name}</option>)}</select></label><label className="field-label">ERP Sistemi<select value={systemId} onChange={e => setSystemId(e.target.value)}><option value="">Seçin</option>{erpSystems.map(x => <option key={x.id} value={x.id}>{x.code} · {x.name}</option>)}</select></label><a className="secondary-button" href="/imports">Excel Import Center</a></div></section>

    <section className="panel audit-panel"><div className="panel-heading"><div><span className="eyebrow dark">ACCOUNT MAPPING</span><h2>Cost Category → ERP Account</h2></div><strong>{mappings.length}</strong></div><form className="inline-form" onSubmit={createMapping}><input placeholder="Cost category" value={mappingForm.costCategory} onChange={e => setMappingForm({ ...mappingForm, costCategory: e.target.value })} required /><input placeholder="Account code" value={mappingForm.accountCode} onChange={e => setMappingForm({ ...mappingForm, accountCode: e.target.value })} required /><input placeholder="Counter account (opsiyonel)" value={mappingForm.counterAccountCode} onChange={e => setMappingForm({ ...mappingForm, counterAccountCode: e.target.value })} /><button className="primary-button" disabled={busy || !systemId}>Mapping ekle</button></form><div className="table-wrap"><table className="data-table"><thead><tr><th>Category</th><th>Account</th><th>Counter</th><th>Durum</th><th></th></tr></thead><tbody>{mappings.length === 0 ? <tr><td colSpan={5}>Account mapping yok.</td></tr> : mappings.map(x => <tr key={x.id}><td><strong>{x.costCategory}</strong></td><td>{x.accountCode}</td><td>{x.counterAccountCode ?? "—"}</td><td>{x.isActive ? "ACTIVE" : "PASSIVE"}</td><td><button className="secondary-button" type="button" disabled={busy} onClick={() => void toggleMapping(x)}>{x.isActive ? "Pasife al" : "Aktife al"}</button></td></tr>)}</tbody></table></div></section>

    <section className="panel audit-panel"><div className="panel-heading"><div><span className="eyebrow dark">EXPORT BATCH</span><h2>Cost Ledger → ERP</h2></div></div><form className="inline-form" onSubmit={createBatch}><label className="field-label">Başlangıç<input type="date" value={from} onChange={e => setFrom(e.target.value)} required /></label><label className="field-label">Bitiş<input type="date" value={to} onChange={e => setTo(e.target.value)} required /></label><button className="primary-button" disabled={busy || !systemId}>Batch oluştur</button></form><div className="table-wrap"><table className="data-table"><thead><tr><th>Dönem</th><th>Status</th><th>Line</th><th>Accepted / Rejected</th><th>Tutar / Variance</th><th></th></tr></thead><tbody>{batches.length === 0 ? <tr><td colSpan={6}>ERP batch yok.</td></tr> : batches.map(x => <tr key={x.id}><td>{x.fromDate} → {x.toDate}<small>{new Date(x.createdAt).toLocaleString("tr-TR")}</small></td><td><strong>{x.status}</strong></td><td>{x.lineCount}</td><td>{x.acceptedLines} / {x.rejectedLines}</td><td><small>{totals(x)}</small></td><td><button className="secondary-button" type="button" onClick={() => void loadLines(x)}>Aç</button><button className="secondary-button" type="button" onClick={() => void downloadBatch(x)}>CSV</button>{x.status === "DRAFT" ? <button className="secondary-button" type="button" onClick={() => void sendBatch(x)}>SENT</button> : null}{x.status === "ACCEPTED" ? <button className="secondary-button" type="button" onClick={() => void closeBatch(x)}>Close</button> : null}</td></tr>)}</tbody></table></div></section>

    {selectedBatch ? <section className="panel audit-panel"><div className="panel-heading"><div><span className="eyebrow dark">RECONCILIATION</span><h2>{selectedBatch.status} · {selectedBatch.id.slice(0, 8)}</h2></div><strong>{lines.length} line</strong></div><p>{totals(selectedBatch)}</p><div className="table-wrap"><table className="data-table"><thead><tr><th>Key / Tarih</th><th>Kaynak</th><th>Category / Account</th><th>Sent</th><th>ERP Status</th><th>Accepted</th><th>Variance</th><th>Reference / Error</th></tr></thead><tbody>{lines.map(line => { const draft = recon[line.externalLineKey]; return <tr key={line.id}><td><code>{line.externalLineKey}</code><small>{line.costDate}</small></td><td>{line.sourceType}<small>{line.sourceId}</small></td><td>{line.costCategory}<small>{line.accountCode}</small></td><td><strong>{money(line.sentAmount, line.currency)}</strong></td><td>{["SENT", "PARTIALLY_ACCEPTED", "REJECTED"].includes(selectedBatch.status) ? <select value={draft?.status ?? "ACCEPTED"} onChange={e => setRecon({ ...recon, [line.externalLineKey]: { ...(draft ?? { acceptedAmount: String(line.sentAmount), externalReference: "", errorCode: "", errorMessage: "" }), status: e.target.value } })}><option>ACCEPTED</option><option>REJECTED</option></select> : line.status}</td><td>{["SENT", "PARTIALLY_ACCEPTED", "REJECTED"].includes(selectedBatch.status) ? <input type="number" step="0.01" value={draft?.acceptedAmount ?? String(line.sentAmount)} onChange={e => setRecon({ ...recon, [line.externalLineKey]: { ...(draft ?? { status: "ACCEPTED", externalReference: "", errorCode: "", errorMessage: "" }), acceptedAmount: e.target.value } })} /> : line.acceptedAmount === null ? "—" : money(line.acceptedAmount, line.currency)}</td><td>{line.varianceAmount === null ? "—" : money(line.varianceAmount, line.currency)}</td><td>{["SENT", "PARTIALLY_ACCEPTED", "REJECTED"].includes(selectedBatch.status) ? <><input placeholder="ERP ref" value={draft?.externalReference ?? ""} onChange={e => setRecon({ ...recon, [line.externalLineKey]: { ...(draft ?? { status: "ACCEPTED", acceptedAmount: String(line.sentAmount), errorCode: "", errorMessage: "" }), externalReference: e.target.value } })} /><input placeholder="Error message" value={draft?.errorMessage ?? ""} onChange={e => setRecon({ ...recon, [line.externalLineKey]: { ...(draft ?? { status: "ACCEPTED", acceptedAmount: String(line.sentAmount), externalReference: "", errorCode: "" }), errorMessage: e.target.value } })} /></> : <>{line.externalReference ?? "—"}<small>{line.errorMessage ?? ""}</small></>}</td></tr>; })}</tbody></table></div>{["SENT", "PARTIALLY_ACCEPTED", "REJECTED"].includes(selectedBatch.status) ? <button className="primary-button" disabled={busy || lines.length === 0} onClick={() => void reconcileBatch()}>Reconciliation kaydet</button> : null}</section> : null}
  </main>;
}
