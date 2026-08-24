"use client";

import { FormEvent, useEffect, useMemo, useState } from "react";
import { Icon } from "../components/Icon";
import { PageHeader } from "../components/PageHeader";

const apiBase = process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:8080";
type Permission = { code: string };
type Me = { userId: string; username: string; permissions: Permission[] };
type Company = { id: string; code: string; name: string };
type Employee = { id: string; companyId: string; employeeNo: string; firstName: string; lastName: string; status: string };
type EmployeePage = { items: Employee[] };
type RequestType = { id: string; companyId: string; code: string; name: string; description: string | null; slaMinutes: number; requiredFieldsJson: string; isActive: boolean; version: number };
type Step = { id: string; requestTypeId: string; stepOrder: number; name: string; targetKind: string; approverUserId: string | null; approverUsername: string | null; approverRoleId: string | null; approverRoleCode: string | null };
type RequestRow = { id: string; companyId: string; requestNo: string; requestTypeId: string; requestTypeCode: string; requestTypeName: string; requesterUserId: string; requesterUsername: string; employeeId: string | null; employeeNo: string | null; employeeName: string | null; priority: string; requestDataJson: string; status: string; currentStepOrder: number; slaMinutesSnapshot: number; submittedAt: string | null; dueAt: string | null; resolvedAt: string | null; version: number };
type Approval = { id: string; stepOrder: number; stepName: string; targetKind: string; approverUsername: string | null; approverRoleCode: string | null; status: string; actionByUsername: string | null; actionAt: string | null; comment: string | null };
type Timeline = { id: string; eventType: string; fromStatus: string | null; toStatus: string; actorUsername: string; occurredAt: string; detailsJson: string };
type RequestDetail = { request: RequestRow; approvals: Approval[]; timeline: Timeline[] };
type SlaEvent = { id: string; requestId: string; requestNo: string; eventType: string; severity: string; message: string; createdAt: string };
type AuthResponse = { accessToken: string };
type DraftStep = { stepOrder: number; name: string; targetKind: "USER" | "ROLE"; targetId: string };

const requestStatus = (value: string) => value === "DRAFT" ? "Taslak" : value === "IN_APPROVAL" ? "Onay bekliyor" : value === "APPROVED" ? "Onaylandı" : value === "REJECTED" ? "Reddedildi" : value === "CANCELLED" ? "İptal edildi" : value;
const priorityLabel = (value: string) => value === "INFO" ? "Bilgi" : value === "NORMAL" ? "Normal" : value === "IMPORTANT" ? "Önemli" : value === "CRITICAL" ? "Kritik" : value;
const approvalStatus = (value: string) => value === "PENDING" ? "Bekliyor" : value === "APPROVED" ? "Onaylandı" : value === "REJECTED" ? "Reddedildi" : value === "SKIPPED" ? "Atlandı" : value;

export default function WorkflowPage() {
  const [me, setMe] = useState<Me | null>(null);
  const [companies, setCompanies] = useState<Company[]>([]);
  const [employees, setEmployees] = useState<Employee[]>([]);
  const [types, setTypes] = useState<RequestType[]>([]);
  const [steps, setSteps] = useState<Step[]>([]);
  const [requests, setRequests] = useState<RequestRow[]>([]);
  const [detail, setDetail] = useState<RequestDetail | null>(null);
  const [slaEvents, setSlaEvents] = useState<SlaEvent[]>([]);
  const [companyId, setCompanyId] = useState("");
  const [selectedTypeId, setSelectedTypeId] = useState("");
  const [draftSteps, setDraftSteps] = useState<DraftStep[]>([]);
  const [message, setMessage] = useState("Workflow merkezi yükleniyor…");
  const [busy, setBusy] = useState(false);
  const permissions = useMemo(() => new Set(me?.permissions.map(x => x.code) ?? []), [me]);
  const selectedType = types.find(x => x.id === selectedTypeId) ?? null;
  const companyEmployees = employees.filter(x => x.companyId === companyId);
  const approvalQueue = requests.filter(x => x.status === "IN_APPROVAL");
  const drafts = requests.filter(x => x.status === "DRAFT");
  const resolved = requests.filter(x => ["APPROVED", "REJECTED", "CANCELLED"].includes(x.status));
  const criticalSla = slaEvents.filter(x => ["HIGH", "CRITICAL"].includes(x.severity));

  useEffect(() => { void initialize(); }, []);
  useEffect(() => { if (companyId) void reloadCompany(); }, [companyId]);
  useEffect(() => { if (selectedTypeId) void loadSteps(selectedTypeId); else { setSteps([]); setDraftSteps([]); } }, [selectedTypeId]);

  async function initialize() {
    const current = await json<Me>("/api/v1/auth/me"); if (!current) { window.location.replace("/login"); return; }
    setMe(current);
    const codes = new Set(current.permissions.map(x => x.code));
    const [cs, ps] = await Promise.all([
      codes.has("organization.company.view") ? json<Company[]>("/api/v1/organization/companies") : Promise.resolve(null),
      codes.has("personnel.view") ? json<EmployeePage>("/api/v1/personnel/employees?status=ACTIVE&pageSize=500") : Promise.resolve(null),
    ]);
    setCompanies(cs ?? []); setEmployees(ps?.items ?? []); if (cs?.length) setCompanyId(cs[0].id); setMessage("Talep, onay ve SLA akışları hazır.");
  }

  async function reloadCompany() {
    const [typeRows, requestRows, events] = await Promise.all([
      permissions.has("workflow.request_type.view") ? json<RequestType[]>(`/api/v1/workflow/request-types?companyId=${companyId}`) : Promise.resolve(null),
      permissions.has("workflow.request.view") ? json<RequestRow[]>(`/api/v1/workflow/requests?companyId=${companyId}&take=200`) : Promise.resolve(null),
      permissions.has("workflow.sla.view") ? json<SlaEvent[]>(`/api/v1/workflow/sla/events?companyId=${companyId}&take=100`) : Promise.resolve(null),
    ]);
    const rt = typeRows ?? []; setTypes(rt); setRequests(requestRows ?? []); setSlaEvents(events ?? []);
    setSelectedTypeId(current => rt.some(x => x.id === current) ? current : rt[0]?.id ?? "");
    if (detail && !requestRows?.some(x => x.id === detail.request.id)) setDetail(null);
  }

  async function loadSteps(typeId: string) {
    const rows = await json<Step[]>(`/api/v1/workflow/request-types/${typeId}/steps`) ?? [];
    setSteps(rows); setDraftSteps(rows.map(x => ({ stepOrder: x.stepOrder, name: x.name, targetKind: x.targetKind as "USER" | "ROLE", targetId: x.approverUserId ?? x.approverRoleId ?? "" })));
  }

  async function createType(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); const form = event.currentTarget; const fd = new FormData(form); setBusy(true);
    try {
      const r = await authFetch("/api/v1/workflow/request-types", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ companyId, code: fd.get("code"), name: fd.get("name"), description: fd.get("description") || null, slaMinutes: Number(fd.get("slaMinutes")), requiredFieldsJson: fd.get("requiredFieldsJson") }) });
      if (!r?.ok) { setMessage(await errorMessage(r, "Talep türü oluşturulamadı.")); return; } form.reset(); setMessage("Talep türü oluşturuldu."); await reloadCompany();
    } finally { setBusy(false); }
  }

  function addDraftStep() { setDraftSteps(current => [...current, { stepOrder: current.length + 1, name: "", targetKind: "USER", targetId: "" }]); }
  function changeDraftStep(index: number, patch: Partial<DraftStep>) { setDraftSteps(current => current.map((x, i) => i === index ? { ...x, ...patch, stepOrder: i + 1 } : { ...x, stepOrder: i + 1 })); }
  function removeDraftStep(index: number) { setDraftSteps(current => current.filter((_, i) => i !== index).map((x, i) => ({ ...x, stepOrder: i + 1 }))); }

  async function saveSteps() {
    if (!selectedType) return; setBusy(true);
    try {
      const r = await authFetch(`/api/v1/workflow/request-types/${selectedType.id}/steps`, { method: "PUT", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ requestTypeVersion: selectedType.version, steps: draftSteps.map(x => ({ stepOrder: x.stepOrder, name: x.name, targetKind: x.targetKind, approverUserId: x.targetKind === "USER" ? x.targetId : null, approverRoleId: x.targetKind === "ROLE" ? x.targetId : null })) }) });
      if (!r?.ok) { setMessage(await errorMessage(r, "Onay akışı kaydedilemedi.")); return; }
      setMessage("Onay akışı güncellendi. Yeni talepler bu tanımın snapshot'ını kullanacak."); await reloadCompany(); await loadSteps(selectedType.id);
    } finally { setBusy(false); }
  }

  async function createRequest(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); const form = event.currentTarget; const fd = new FormData(form); setBusy(true);
    try {
      const r = await authFetch("/api/v1/workflow/requests", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ companyId, requestTypeId: fd.get("requestTypeId"), employeeId: fd.get("employeeId") || null, priority: fd.get("priority"), requestDataJson: fd.get("payload") }) });
      if (!r?.ok) { setMessage(await errorMessage(r, "Talep oluşturulamadı.")); return; } form.reset(); setMessage("Taslak talep oluşturuldu."); await reloadCompany();
    } finally { setBusy(false); }
  }

  async function openRequest(id: string) { const row = await json<RequestDetail>(`/api/v1/workflow/requests/${id}`); if (row) setDetail(row); }

  async function action(kind: "submit" | "cancel" | "approve" | "reject") {
    if (!detail) return; const comment = kind === "submit" ? null : window.prompt("Açıklama / not", "") ?? null; setBusy(true);
    try {
      const r = await authFetch(`/api/v1/workflow/requests/${detail.request.id}/${kind}`, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ version: detail.request.version, comment }) });
      if (!r?.ok) { setMessage(await errorMessage(r, "İşlem tamamlanamadı.")); return; }
      const body = await r.json() as RequestDetail; setDetail(body); setMessage(`Talep işlemi tamamlandı: ${kind}.`); await reloadCompany();
    } finally { setBusy(false); }
  }

  async function processSla() { setBusy(true); try { const r = await authFetch("/api/v1/workflow/sla/process", { method: "POST" }); setMessage(r?.ok ? "SLA threshold işlemi tamamlandı." : await errorMessage(r, "SLA işlenemedi.")); await reloadCompany(); } finally { setBusy(false); } }

  async function json<T>(path: string): Promise<T | null> { const r = await authFetch(path); return r?.ok ? await r.json() as T : null; }
  async function authFetch(path: string, init?: RequestInit): Promise<Response | null> {
    let token = sessionStorage.getItem("pp_access_token") ?? await refresh(); if (!token) return null;
    let r = await fetch(`${apiBase}${path}`, { ...init, headers: { ...(init?.headers ?? {}), Authorization: `Bearer ${token}` }, credentials: "include" });
    if (r.status !== 401) return r; token = await refresh(); if (!token) return r;
    return fetch(`${apiBase}${path}`, { ...init, headers: { ...(init?.headers ?? {}), Authorization: `Bearer ${token}` }, credentials: "include" });
  }
  async function refresh(): Promise<string | null> { try { const r = await fetch(`${apiBase}/api/v1/auth/refresh`, { method: "POST", credentials: "include" }); if (!r.ok) return null; const b = await r.json() as AuthResponse; sessionStorage.setItem("pp_access_token", b.accessToken); return b.accessToken; } catch { return null; } }
  async function errorMessage(r: Response | null, fallback: string) { if (!r) return fallback; const b = await r.json().catch(() => null) as { error?: { code?: string; message?: string } } | null; return b?.error?.code ? `${b.error.code}: ${b.error.message ?? fallback}` : b?.error?.message ?? fallback; }

  return <main className="page-shell">
    <PageHeader eyebrow="Talep ve onay" title="İş akışı merkezi" description="Talep türlerini, onay adımlarını, devam eden talepleri ve hizmet süresi uyarılarını yönetin." status={message}/>

    <section className="stat-grid" aria-label="İş akışı özeti">
      <article className="stat-card"><span className="stat-icon"><Icon name="workflow"/></span><span className="stat-copy"><strong>{drafts.length}</strong><span>Taslak talep</span></span></article>
      <article className="stat-card"><span className="stat-icon"><Icon name="bell"/></span><span className="stat-copy"><strong>{approvalQueue.length}</strong><span>Onay bekleyen</span></span></article>
      <article className="stat-card"><span className="stat-icon"><Icon name="chart"/></span><span className="stat-copy"><strong>{resolved.length}</strong><span>Sonuçlanan talep</span></span></article>
      <article className="stat-card"><span className="stat-icon"><Icon name="calendar"/></span><span className="stat-copy"><strong>{criticalSla.length}</strong><span>Kritik süre uyarısı</span></span></article>
    </section>

    <section className="panel workspace-panel"><div className="workspace-copy"><span className="eyebrow dark">Çalışma kapsamı</span><h2>Şirket seçimi</h2><p>Talep türleri, kayıtlar ve süre uyarıları seçili şirkete göre güncellenir.</p></div><div className="workspace-select"><label className="field-label">Şirket<select value={companyId} onChange={e => setCompanyId(e.target.value)}><option value="">Şirket seçin</option>{companies.map(x => <option key={x.id} value={x.id}>{x.code} · {x.name}</option>)}</select></label>{permissions.has("workflow.sla.process") ? <button className="secondary-button workspace-button" disabled={busy||!companyId} onClick={() => void processSla()}>Süre uyarılarını tara</button> : null}</div></section>

    <div className="content-stack">
      {permissions.has("workflow.request_type.manage") ? <section className="panel"><div className="panel-heading"><div><span className="eyebrow dark">Talep türleri</span><h2>Tür ve hizmet süresi tanımları</h2><p>Yeni talep türünü ve sonuçlandırma hedefini oluşturun; satıra tıklayarak onay akışını seçin.</p></div><strong>{types.length}</strong></div><div className="form-surface"><form className="inline-form" onSubmit={createType}><label className="field-label">Tür kodu<input name="code" required/></label><label className="field-label">Talep türü adı<input name="name" required/></label><label className="field-label">Açıklama<input name="description"/></label><label className="field-label">Hedef süre (dakika)<input name="slaMinutes" type="number" min="1" defaultValue="1440" required/></label><label className="field-label">Zorunlu alan tanımı<input name="requiredFieldsJson" defaultValue='["reason"]' required/></label><button className="primary-button" disabled={busy || !companyId}>Türü kaydet</button></form></div><div className="table-wrap"><table className="data-table selectable-table"><thead><tr><th>Kod</th><th>Ad</th><th>Hedef süre</th><th>Zorunlu alanlar</th><th>Durum</th></tr></thead><tbody>{types.length?types.map(x => <tr key={x.id} className={x.id===selectedTypeId?"selected-row":""} onClick={() => setSelectedTypeId(x.id)}><td><strong>{x.code}</strong></td><td>{x.name}</td><td>{x.slaMinutes.toLocaleString("tr-TR")} dakika</td><td><code>{x.requiredFieldsJson}</code></td><td><span className={`status-badge ${x.isActive?"success":"danger"}`}>{x.isActive?"Aktif":"Pasif"}</span></td></tr>):<tr><td className="empty-row" colSpan={5}>Henüz talep türü yok.</td></tr>}</tbody></table></div></section> : null}

      {selectedType && permissions.has("workflow.request_type.manage") ? <section className="panel"><div className="panel-heading"><div><span className="eyebrow dark">Onay akışı</span><h2>{selectedType.code} · {selectedType.name}</h2><p>Adımlar sırasıyla çalışır ve talep gönderildiğinde değiştirilemez kopya olarak saklanır.</p></div><strong>v{selectedType.version}</strong></div><div className="workflow-step-list">{draftSteps.map((x, i) => <div className="workflow-step" key={i}><span className="workflow-step-number">{i + 1}</span><label className="field-label">Adım adı<input value={x.name} onChange={e => changeDraftStep(i,{ name:e.target.value })}/></label><label className="field-label">Onaylayıcı türü<select value={x.targetKind} onChange={e => changeDraftStep(i,{ targetKind:e.target.value as "USER"|"ROLE" })}><option value="USER">Kullanıcı</option><option value="ROLE">Rol</option></select></label><label className="field-label">{x.targetKind==="USER"?"Kullanıcı":"Rol"} kimliği<input value={x.targetId} onChange={e => changeDraftStep(i,{ targetId:e.target.value })}/></label><button className="secondary-button button-danger" type="button" onClick={() => removeDraftStep(i)}>Adımı sil</button></div>)}</div>{draftSteps.length===0?<p className="notice">Bu tür onaysız çalışır; gönderilen talep doğrudan onaylanır.</p>:null}<div className="action-row"><button className="secondary-button" type="button" onClick={addDraftStep}><Icon name="plus" size={16}/>Adım ekle</button><button className="primary-button" disabled={busy || draftSteps.some(x => !x.name || !x.targetId)} onClick={() => void saveSteps()}>Akışı kaydet</button></div>{steps.length?<p className="panel-description">Aktif akış: {steps.map(x => `${x.stepOrder}. ${x.name} → ${x.approverUsername ?? x.approverRoleCode ?? "Atanmamış"}`).join(" · ")}</p>:null}</section> : null}

      {permissions.has("workflow.request.create") ? <section className="panel"><div className="panel-heading"><div><span className="eyebrow dark">Yeni talep</span><h2>Taslak oluşturun</h2><p>Talep taslak olarak kaydedilir; detaydan onaya gönderebilirsiniz.</p></div></div><div className="form-surface"><form className="inline-form" onSubmit={createRequest}><label className="field-label">Talep türü<select name="requestTypeId" required><option value="">Tür seçin</option>{types.filter(x => x.isActive).map(x => <option key={x.id} value={x.id}>{x.code} · {x.name}</option>)}</select></label><label className="field-label">İlgili personel<select name="employeeId"><option value="">Personel bağlantısı yok</option>{companyEmployees.map(x => <option key={x.id} value={x.id}>{x.employeeNo} · {x.firstName} {x.lastName}</option>)}</select></label><label className="field-label">Öncelik<select name="priority" defaultValue="NORMAL"><option value="INFO">Bilgi</option><option value="NORMAL">Normal</option><option value="IMPORTANT">Önemli</option><option value="CRITICAL">Kritik</option></select></label><label className="field-label">Talep verisi<input name="payload" defaultValue='{"reason":""}' required/></label><button className="primary-button" disabled={busy || !companyId}>Taslağı kaydet</button></form></div></section> : null}

      {permissions.has("workflow.request.view") ? <section className="panel"><div className="panel-heading"><div><span className="eyebrow dark">Talep listesi</span><h2>Güncel ve geçmiş talepler</h2><p>Detay, onay ve zaman çizelgesi için bir satıra tıklayın.</p></div><strong>{requests.length}</strong></div><div className="table-wrap"><table className="data-table selectable-table"><thead><tr><th>No</th><th>Tür</th><th>Personel</th><th>Öncelik</th><th>Durum</th><th>Adım</th><th>Hedef tarih</th></tr></thead><tbody>{requests.length?requests.map(x => <tr key={x.id} className={detail?.request.id===x.id?"selected-row":""} onClick={() => void openRequest(x.id)}><td><strong>{x.requestNo}</strong></td><td>{x.requestTypeCode}<small>{x.requestTypeName}</small></td><td>{x.employeeNo ? `${x.employeeNo} · ${x.employeeName}` : "—"}</td><td><span className={`status-badge ${x.priority==="CRITICAL"?"danger":x.priority==="IMPORTANT"?"warning":""}`}>{priorityLabel(x.priority)}</span></td><td><span className={`status-badge ${x.status==="APPROVED"?"success":x.status==="REJECTED"?"danger":x.status==="IN_APPROVAL"?"warning":""}`}>{requestStatus(x.status)}</span></td><td>{x.currentStepOrder || "—"}</td><td>{x.dueAt ? new Date(x.dueAt).toLocaleString("tr-TR") : "—"}</td></tr>):<tr><td className="empty-row" colSpan={7}>Henüz talep yok.</td></tr>}</tbody></table></div></section> : null}

      {detail ? <section className={`panel attention-panel ${detail.request.status==="IN_APPROVAL"?"warning":detail.request.status==="APPROVED"?"success":detail.request.status==="REJECTED"?"danger":""}`}><div className="panel-heading"><div><span className="eyebrow dark">Talep detayı</span><h2>{detail.request.requestNo} · {requestStatus(detail.request.status)}</h2><p>{detail.request.requestTypeName} · {detail.request.requesterUsername}</p></div><strong>v{detail.request.version}</strong></div><details className="technical-details"><summary>Teknik talep verisini göster</summary><pre>{detail.request.requestDataJson}</pre></details><div className="action-row detail-actions">{detail.request.status === "DRAFT" && permissions.has("workflow.request.create") ? <button className="primary-button" disabled={busy} onClick={() => void action("submit")}>Onaya gönder</button> : null}{["DRAFT","IN_APPROVAL"].includes(detail.request.status) && permissions.has("workflow.request.create") ? <button className="secondary-button button-danger" disabled={busy} onClick={() => void action("cancel")}>Talebi iptal et</button> : null}{detail.request.status === "IN_APPROVAL" && permissions.has("workflow.request.approve") ? <><button className="secondary-button button-success" disabled={busy} onClick={() => void action("approve")}>Onayla</button><button className="secondary-button button-danger" disabled={busy} onClick={() => void action("reject")}>Reddet</button></> : null}</div><div className="organization-grid"><div><div className="form-surface-heading"><div><strong>Onay adımları</strong><span>Mevcut karar ve işlem yapan kullanıcı.</span></div></div><div className="compact-list">{detail.approvals.length?detail.approvals.map(x => <div className="role-row" key={x.id}><strong>{x.stepOrder}. {x.stepName}</strong><span>{x.approverUsername ?? x.approverRoleCode ?? "Atanmamış"} · {approvalStatus(x.status)}{x.actionByUsername ? ` · ${x.actionByUsername}` : ""}</span></div>):<p className="muted">Onay adımı yok.</p>}</div></div><div><div className="form-surface-heading"><div><strong>Zaman çizelgesi</strong><span>Talebin tüm durum değişiklikleri.</span></div></div><div className="compact-list">{detail.timeline.map(x => <div className="role-row" key={x.id}><strong>{requestStatus(x.toStatus)}</strong><span>{x.actorUsername} · {new Date(x.occurredAt).toLocaleString("tr-TR")}</span></div>)}</div></div></div></section> : null}

      {permissions.has("workflow.sla.view") ? <section className={`panel attention-panel ${criticalSla.length?"danger":"success"}`}><div className="panel-heading"><div><span className="eyebrow dark">Hizmet süresi uyarıları</span><h2>Yaklaşan ve aşılan hedefler</h2><p>{criticalSla.length?`${criticalSla.length} kritik veya yüksek öncelikli uyarı bulunuyor.`:"Kritik hizmet süresi uyarısı yok."}</p></div><strong>{slaEvents.length}</strong></div><div className="compact-list">{slaEvents.length?slaEvents.map(x => <div className="role-row" key={x.id}><strong><span className={`status-badge ${["HIGH","CRITICAL"].includes(x.severity)?"danger":"warning"}`}>{x.severity==="CRITICAL"?"Kritik":x.severity==="HIGH"?"Yüksek":"Uyarı"}</span> {x.requestNo}</strong><span>{x.message} · {new Date(x.createdAt).toLocaleString("tr-TR")}</span></div>):<p className="muted">Hizmet süresi uyarısı yok.</p>}</div></section> : null}
    </div>
  </main>;
}
