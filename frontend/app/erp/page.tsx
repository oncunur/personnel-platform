"use client";

import { FormEvent, useEffect, useMemo, useState } from "react";
import { Icon } from "../components/Icon";
import { PageHeader } from "../components/PageHeader";

const apiBase = process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:8080";
type AuthResponse = { accessToken: string };
type Company = { id: string; code: string; name: string };
type SystemRow = { id: string; companyId: string; code: string; name: string; systemType: string; isActive: boolean };
type AccountMapping = { id: string; companyId: string; integrationSystemId: string; costCategory: string; accountCode: string; counterAccountCode: string | null; isActive: boolean; version: number };
type CurrencyTotal = { currency: string; sentAmount: number; acceptedAmount: number; varianceAmount: number };
type Batch = { id: string; companyId: string; integrationSystemId: string; fromDate: string; toDate: string; status: string; lineCount: number; acceptedLines: number; rejectedLines: number; totals: CurrencyTotal[]; createdAt: string; sentAt: string | null; reconciledAt: string | null; closedAt: string | null; version: number };
type Line = { id: string; batchId: string; costEntryId: string; externalLineKey: string; sourceType: string; sourceId: string; employeeId: string | null; projectId: string | null; costCenterId: string | null; costDate: string; costCategory: string; accountCode: string; counterAccountCode: string | null; sentAmount: number; currency: string; status: string; acceptedAmount: number | null; varianceAmount: number | null; externalReference: string | null; errorCode: string | null; errorMessage: string | null; reconciledAt: string | null };
type ReconDraft = { status: string; acceptedAmount: string; externalReference: string; errorCode: string; errorMessage: string };

const batchStatuses: Record<string, { label: string; tone: string }> = {
  DRAFT: { label: "Hazırlanıyor", tone: "warning" },
  SENT: { label: "ERP yanıtı bekleniyor", tone: "warning" },
  PARTIALLY_ACCEPTED: { label: "Kısmen kabul edildi", tone: "warning" },
  ACCEPTED: { label: "Kabul edildi", tone: "success" },
  REJECTED: { label: "Reddedildi", tone: "danger" },
  CLOSED: { label: "Kapatıldı", tone: "success" },
};

const lineStatuses: Record<string, { label: string; tone: string }> = {
  ACCEPTED: { label: "Kabul", tone: "success" },
  REJECTED: { label: "Red", tone: "danger" },
  SENT: { label: "Gönderildi", tone: "warning" },
};

const categoryLabels: Record<string, string> = {
  PAYROLL: "Bordro",
  MEAL: "Yemek",
  ACCOMMODATION: "Konaklama",
  VEHICLE: "Araç",
  ASSET: "Demirbaş",
};

function statusOf(value: string) {
  return batchStatuses[value] ?? { label: value, tone: "" };
}

function lineStatusOf(value: string) {
  return lineStatuses[value] ?? { label: value, tone: "" };
}

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
  const [message, setMessage] = useState("ERP aktarım merkezi yükleniyor…");
  const [busy, setBusy] = useState(false);
  const erpSystems = useMemo(() => systems.filter(x => x.companyId === companyId && x.systemType === "ERP" && x.isActive), [systems, companyId]);
  const activeMappings = useMemo(() => mappings.filter(x => x.isActive), [mappings]);
  const openBatches = useMemo(() => batches.filter(x => x.status !== "CLOSED"), [batches]);
  const rejectedLineCount = useMemo(() => batches.reduce((sum, batch) => sum + batch.rejectedLines, 0), [batches]);

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
    setMessage("Hesap eşlemeleri, dışa aktarım paketleri ve mutabakat akışı hazır.");
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
      if (!response?.ok) { setMessage(await apiError(response, "Hesap eşlemesi oluşturulamadı.")); return; }
      setMappingForm(x => ({ ...x, accountCode: "", counterAccountCode: "" })); setMessage("ERP hesap eşlemesi oluşturuldu."); await loadErpData();
    } finally { setBusy(false); }
  }

  async function toggleMapping(row: AccountMapping) {
    setBusy(true);
    try {
      const response = await authFetch(`/api/v1/erp/account-mappings/${row.id}`, { method: "PUT", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ accountCode: row.accountCode, counterAccountCode: row.counterAccountCode, isActive: !row.isActive, version: row.version }) });
      if (!response?.ok) { setMessage(await apiError(response, "Hesap eşlemesi güncellenemedi.")); return; }
      setMessage(`Hesap eşlemesi ${row.isActive ? "pasife" : "aktife"} alındı.`); await loadErpData();
    } finally { setBusy(false); }
  }

  async function createBatch(event: FormEvent) {
    event.preventDefault(); if (!companyId || !systemId) return;
    setBusy(true);
    try {
      const response = await authFetch("/api/v1/erp/batches", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ companyId, integrationSystemId: systemId, fromDate: from, toDate: to }) });
      if (!response?.ok) { setMessage(await apiError(response, "ERP aktarım paketi oluşturulamadı.")); return; }
      const batch = await response.json() as Batch; setSelectedBatch(batch); setMessage(`${batch.lineCount} maliyet satırıyla ERP aktarım paketi oluşturuldu.`); await loadErpData(); await loadLines(batch);
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
    if (!confirm("Paket ERP'ye gönderilmiş olarak işaretlensin mi? Bu işlemden sonra maliyet satırları yeni bir pakete alınmaz.")) return;
    setBusy(true);
    try {
      const response = await authFetch(`/api/v1/erp/batches/${batch.id}/send`, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ version: batch.version }) });
      if (!response?.ok) { setMessage(await apiError(response, "Paket gönderildi durumuna alınamadı.")); return; }
      const updated = await response.json() as Batch; setSelectedBatch(updated); setMessage("ERP aktarım paketi gönderildi; mutabakat yanıtı bekleniyor."); await loadErpData(); await loadLines(updated);
    } finally { setBusy(false); }
  }

  async function reconcileBatch() {
    if (!selectedBatch) return;
    const payload = lines.map(line => { const x = recon[line.externalLineKey]; return { externalLineKey: line.externalLineKey, status: x?.status ?? "ACCEPTED", acceptedAmount: Number(x?.acceptedAmount ?? line.sentAmount), externalReference: x?.externalReference || null, errorCode: x?.errorCode || null, errorMessage: x?.errorMessage || null }; });
    setBusy(true);
    try {
      const response = await authFetch(`/api/v1/erp/batches/${selectedBatch.id}/reconcile`, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ version: selectedBatch.version, lines: payload }) });
      if (!response?.ok) { setMessage(await apiError(response, "Mutabakat kaydedilemedi.")); return; }
      const updated = await response.json() as Batch; setSelectedBatch(updated); setMessage(`Mutabakat tamamlandı: ${statusOf(updated.status).label}. Fark sıfır ve tüm satırlar kabul edildiyse paket kapatılabilir.`); await loadErpData(); await loadLines(updated);
    } finally { setBusy(false); }
  }

  async function closeBatch(batch: Batch) {
    setBusy(true);
    try {
      const response = await authFetch(`/api/v1/erp/batches/${batch.id}/close`, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ version: batch.version }) });
      if (!response?.ok) { setMessage(await apiError(response, "Paket kapatılamadı.")); return; }
      const updated = await response.json() as Batch; setSelectedBatch(updated); setMessage("ERP aktarım paketi kapatıldı. Mutabakat tamamlandı."); await loadErpData();
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
  const totals = (batch: Batch) => batch.totals.map(x => `${x.currency}: gönderilen ${x.sentAmount.toFixed(2)} · kabul ${x.acceptedAmount.toFixed(2)} · fark ${x.varianceAmount.toFixed(2)}`).join(" · ") || "Tutar bilgisi yok";
  const canReconcile = selectedBatch ? ["SENT", "PARTIALLY_ACCEPTED", "REJECTED"].includes(selectedBatch.status) : false;

  return <main className="page-shell">
    <PageHeader eyebrow="Finans entegrasyonu" title="ERP aktarım ve mutabakat" description="Maliyetleri ERP hesaplarına eşleyin, dönem paketlerini oluşturun ve ERP yanıtlarını tek akışta sonuçlandırın." status={message} actions={<a className="secondary-button" href="/imports"><Icon name="workflow" size={17}/>Veri içe aktarma</a>}/>

    <section className="stat-grid" aria-label="ERP aktarım özeti">
      <article className="stat-card"><span className="stat-icon"><Icon name="plug"/></span><span className="stat-copy"><strong>{activeMappings.length}</strong><span>Aktif hesap eşlemesi</span></span></article>
      <article className="stat-card"><span className="stat-icon"><Icon name="box"/></span><span className="stat-copy"><strong>{batches.length}</strong><span>Toplam aktarım paketi</span></span></article>
      <article className="stat-card"><span className="stat-icon"><Icon name="workflow"/></span><span className="stat-copy"><strong>{openBatches.length}</strong><span>Sonuçlanmayı bekleyen</span></span></article>
      <article className="stat-card"><span className="stat-icon"><Icon name="bell"/></span><span className="stat-copy"><strong>{rejectedLineCount}</strong><span>Reddedilen satır</span></span></article>
    </section>

    <section className="panel workspace-panel">
      <div className="workspace-copy"><span className="eyebrow dark">Çalışma alanı</span><h2>Aktarım kapsamı</h2><p>İşlem yapacağınız şirketi ve bağlı ERP sistemini seçin.</p></div>
      <div className="inline-form workspace-select">
        <label className="field-label">Şirket<select value={companyId} onChange={e => setCompanyId(e.target.value)}><option value="">Şirket seçin</option>{companies.map(x => <option key={x.id} value={x.id}>{x.code} · {x.name}</option>)}</select></label>
        <label className="field-label">ERP sistemi<select value={systemId} onChange={e => setSystemId(e.target.value)}><option value="">ERP sistemi seçin</option>{erpSystems.map(x => <option key={x.id} value={x.id}>{x.code} · {x.name}</option>)}</select></label>
      </div>
    </section>

    <div className="content-stack">
      <section className="panel">
        <div className="panel-heading"><div><span className="eyebrow dark">Hesap planı</span><h2>Maliyet hesabı eşlemeleri</h2><p>Maliyet türlerinin ERP hesap ve karşı hesap kodlarını yönetin.</p></div><strong>{mappings.length}</strong></div>
        <div className="form-surface"><div className="form-surface-heading"><div><strong>Yeni hesap eşlemesi</strong><span>Aktarımdan önce ilgili maliyet türünün hesap kodunu tanımlayın.</span></div></div><form className="inline-form" onSubmit={createMapping}>
          <label className="field-label">Maliyet türü<input value={mappingForm.costCategory} onChange={e => setMappingForm({ ...mappingForm, costCategory: e.target.value })} required /></label>
          <label className="field-label">ERP hesap kodu<input value={mappingForm.accountCode} onChange={e => setMappingForm({ ...mappingForm, accountCode: e.target.value })} required /></label>
          <label className="field-label">Karşı hesap kodu <span className="muted">(isteğe bağlı)</span><input value={mappingForm.counterAccountCode} onChange={e => setMappingForm({ ...mappingForm, counterAccountCode: e.target.value })} /></label>
          <button className="primary-button" disabled={busy || !systemId}><Icon name="plus" size={17}/>Eşleme ekle</button>
        </form></div>
        <div className="table-wrap"><table className="data-table"><thead><tr><th>Maliyet türü</th><th>Hesap kodu</th><th>Karşı hesap</th><th>Durum</th><th>İşlem</th></tr></thead><tbody>{mappings.length === 0 ? <tr><td className="empty-row" colSpan={5}>Bu ERP sistemi için hesap eşlemesi bulunmuyor.</td></tr> : mappings.map(x => <tr key={x.id}><td><strong>{categoryLabels[x.costCategory] ?? x.costCategory}</strong><small>{x.costCategory}</small></td><td>{x.accountCode}</td><td>{x.counterAccountCode ?? "—"}</td><td><span className={`status-badge ${x.isActive ? "success" : ""}`}>{x.isActive ? "Aktif" : "Pasif"}</span></td><td><button className="secondary-button" type="button" disabled={busy} onClick={() => void toggleMapping(x)}>{x.isActive ? "Pasife al" : "Aktife al"}</button></td></tr>)}</tbody></table></div>
      </section>

      <section className="panel">
        <div className="panel-heading"><div><span className="eyebrow dark">Dönem aktarımı</span><h2>ERP aktarım paketleri</h2><p>Seçilen tarih aralığındaki maliyet kayıtlarını yeni bir pakette toplayın.</p></div></div>
        <div className="form-surface"><form className="inline-form" onSubmit={createBatch}><label className="field-label">Başlangıç tarihi<input type="date" value={from} onChange={e => setFrom(e.target.value)} required /></label><label className="field-label">Bitiş tarihi<input type="date" value={to} onChange={e => setTo(e.target.value)} required /></label><button className="primary-button" disabled={busy || !systemId}><Icon name="plus" size={17}/>Paket oluştur</button></form></div>
        <div className="table-wrap"><table className="data-table selectable-table"><thead><tr><th>Dönem</th><th>Durum</th><th>Satır</th><th>Kabul / Red</th><th>Tutar özeti</th><th>İşlemler</th></tr></thead><tbody>{batches.length === 0 ? <tr><td className="empty-row" colSpan={6}>Henüz ERP aktarım paketi oluşturulmadı.</td></tr> : batches.map(x => { const status = statusOf(x.status); return <tr key={x.id} className={selectedBatch?.id === x.id ? "selected-row" : ""} onClick={() => void loadLines(x)}><td><strong>{new Date(x.fromDate).toLocaleDateString("tr-TR")} – {new Date(x.toDate).toLocaleDateString("tr-TR")}</strong><small>{new Date(x.createdAt).toLocaleString("tr-TR")}</small></td><td><span className={`status-badge ${status.tone}`}>{status.label}</span></td><td>{x.lineCount}</td><td>{x.acceptedLines} / <span className={x.rejectedLines ? "review-count" : ""}>{x.rejectedLines}</span></td><td><small>{totals(x)}</small></td><td><div className="action-row" onClick={e => e.stopPropagation()}><button className="secondary-button" type="button" onClick={() => void loadLines(x)}>İncele</button><button className="secondary-button" type="button" onClick={() => void downloadBatch(x)}>CSV indir</button>{x.status === "DRAFT" ? <button className="secondary-button" type="button" onClick={() => void sendBatch(x)}>ERP'ye gönder</button> : null}{x.status === "ACCEPTED" ? <button className="secondary-button button-success" type="button" onClick={() => void closeBatch(x)}>Kapat</button> : null}</div></td></tr>; })}</tbody></table></div>
      </section>

      {selectedBatch ? <section className={`panel attention-panel ${selectedBatch.rejectedLines ? "danger" : ""}`}>
        <div className="panel-heading"><div><span className="eyebrow dark">Seçili paket</span><h2>ERP mutabakatı</h2><p>Paket #{selectedBatch.id.slice(0, 8)} için kabul edilen tutarları ve ERP sonucunu kontrol edin.</p></div><span className={`status-badge ${statusOf(selectedBatch.status).tone}`}>{statusOf(selectedBatch.status).label}</span></div>
        <div className="detail-grid"><div className="detail-item"><span>Toplam satır</span><strong>{selectedBatch.lineCount}</strong></div><div className="detail-item"><span>Kabul / Red</span><strong>{selectedBatch.acceptedLines} / {selectedBatch.rejectedLines}</strong></div><div className="detail-item"><span>Tutar durumu</span><strong>{selectedBatch.totals.some(x => x.varianceAmount !== 0) ? "Fark var" : "Dengede"}</strong></div></div>
        <p className="panel-description table-section-heading">{totals(selectedBatch)}</p>
        <div className="table-wrap"><table className="data-table"><thead><tr><th>Anahtar / Tarih</th><th>Kaynak</th><th>Maliyet / Hesap</th><th>Gönderilen</th><th>ERP sonucu</th><th>Kabul edilen</th><th>Fark</th><th>Referans / Hata</th></tr></thead><tbody>{lines.length === 0 ? <tr><td className="empty-row" colSpan={8}>Bu pakette gösterilecek maliyet satırı bulunmuyor.</td></tr> : lines.map(line => { const draft = recon[line.externalLineKey]; const status = lineStatusOf(line.status); return <tr key={line.id}><td><code>{line.externalLineKey}</code><small>{new Date(line.costDate).toLocaleDateString("tr-TR")}</small></td><td>{line.sourceType}<small>{line.sourceId}</small></td><td>{categoryLabels[line.costCategory] ?? line.costCategory}<small>{line.accountCode}</small></td><td><strong>{money(line.sentAmount, line.currency)}</strong></td><td>{canReconcile ? <select aria-label={`${line.externalLineKey} ERP sonucu`} value={draft?.status ?? "ACCEPTED"} onChange={e => setRecon({ ...recon, [line.externalLineKey]: { ...(draft ?? { acceptedAmount: String(line.sentAmount), externalReference: "", errorCode: "", errorMessage: "" }), status: e.target.value } })}><option value="ACCEPTED">Kabul</option><option value="REJECTED">Red</option></select> : <span className={`status-badge ${status.tone}`}>{status.label}</span>}</td><td>{canReconcile ? <input aria-label={`${line.externalLineKey} kabul edilen tutar`} type="number" step="0.01" value={draft?.acceptedAmount ?? String(line.sentAmount)} onChange={e => setRecon({ ...recon, [line.externalLineKey]: { ...(draft ?? { status: "ACCEPTED", externalReference: "", errorCode: "", errorMessage: "" }), acceptedAmount: e.target.value } })} /> : line.acceptedAmount === null ? "—" : money(line.acceptedAmount, line.currency)}</td><td>{line.varianceAmount === null ? "—" : <span className={line.varianceAmount !== 0 ? "amount-negative" : "amount-positive"}>{money(line.varianceAmount, line.currency)}</span>}</td><td>{canReconcile ? <div className="stack"><input aria-label={`${line.externalLineKey} ERP referansı`} value={draft?.externalReference ?? ""} onChange={e => setRecon({ ...recon, [line.externalLineKey]: { ...(draft ?? { status: "ACCEPTED", acceptedAmount: String(line.sentAmount), errorCode: "", errorMessage: "" }), externalReference: e.target.value } })} /><input aria-label={`${line.externalLineKey} hata açıklaması`} value={draft?.errorMessage ?? ""} onChange={e => setRecon({ ...recon, [line.externalLineKey]: { ...(draft ?? { status: "ACCEPTED", acceptedAmount: String(line.sentAmount), externalReference: "", errorCode: "" }), errorMessage: e.target.value } })} /></div> : <>{line.externalReference ?? "—"}<small>{line.errorMessage ?? ""}</small></>}</td></tr>; })}</tbody></table></div>
        {canReconcile ? <div className="detail-actions"><button className="primary-button" disabled={busy || lines.length === 0} onClick={() => void reconcileBatch()}><Icon name="workflow" size={17}/>Mutabakatı kaydet</button></div> : null}
      </section> : null}
    </div>
  </main>;
}
