"use client";

import { FormEvent, useEffect, useMemo, useState } from "react";

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

  return <main className="shell">
    <a className="back" href="/dashboard">← Dashboard</a>
    <section className="hero compact"><span className="eyebrow">SPRINT 11 · WORKFLOW</span><h1>Talep & Workflow Merkezi</h1><p>{message}</p></section>
    <section className="panel audit-panel"><div className="inline-form"><label className="field-label">Şirket<select value={companyId} onChange={e => setCompanyId(e.target.value)}><option value="">Seçin</option>{companies.map(x => <option key={x.id} value={x.id}>{x.code} · {x.name}</option>)}</select></label><span><strong>{requests.length}</strong> talep · <strong>{slaEvents.length}</strong> SLA event</span>{permissions.has("workflow.sla.process") ? <button className="secondary-button" disabled={busy} onClick={() => void processSla()}>SLA’yı işle</button> : null}</div></section>

    {permissions.has("workflow.request_type.manage") ? <section className="panel audit-panel"><h2>Talep Türü / SLA</h2><form className="inline-form" onSubmit={createType}><input name="code" placeholder="Kod (örn. TRAVEL)" required/><input name="name" placeholder="Talep türü adı" required/><input name="description" placeholder="Açıklama"/><input name="slaMinutes" type="number" min="1" defaultValue="1440" required/><input name="requiredFieldsJson" defaultValue='["reason"]' placeholder='["reason","amount"]' required/><button className="primary-button" disabled={busy || !companyId}>Tür ekle</button></form><div className="table-wrap"><table className="data-table"><thead><tr><th>Kod</th><th>Ad</th><th>SLA</th><th>Zorunlu alanlar</th><th>Durum</th></tr></thead><tbody>{types.map(x => <tr key={x.id} onClick={() => setSelectedTypeId(x.id)}><td>{x.code}</td><td>{x.name}</td><td>{x.slaMinutes} dk</td><td><code>{x.requiredFieldsJson}</code></td><td>{x.isActive ? "ACTIVE" : "INACTIVE"}</td></tr>)}</tbody></table></div></section> : null}

    {selectedType && permissions.has("workflow.request_type.manage") ? <section className="panel audit-panel"><div className="panel-heading"><div><span className="eyebrow dark">ONAY AKIŞI</span><h2>{selectedType.code} · {selectedType.name}</h2></div><strong>v{selectedType.version}</strong></div><p>Hedef ID alanına kullanıcı veya rol UUID’si girilir. Snapshot, talep submit edildiğinde sabitlenir.</p>{draftSteps.map((x, i) => <div className="inline-form" key={i}><strong>{i + 1}</strong><input value={x.name} placeholder="Adım adı" onChange={e => changeDraftStep(i,{ name:e.target.value })}/><select value={x.targetKind} onChange={e => changeDraftStep(i,{ targetKind:e.target.value as "USER"|"ROLE" })}><option>USER</option><option>ROLE</option></select><input value={x.targetId} placeholder={`${x.targetKind} UUID`} onChange={e => changeDraftStep(i,{ targetId:e.target.value })}/><button className="secondary-button" type="button" onClick={() => removeDraftStep(i)}>Sil</button></div>)}<div className="actions action-row"><button className="secondary-button" onClick={addDraftStep}>Adım ekle</button><button className="primary-button" disabled={busy || draftSteps.some(x => !x.name || !x.targetId)} onClick={() => void saveSteps()}>Akışı kaydet</button></div><p>Aktif tanım: {steps.map(x => `${x.stepOrder}.${x.name}→${x.approverUsername ?? x.approverRoleCode ?? "—"}`).join(" · ") || "Onaysız — submit sonrası doğrudan APPROVED"}</p></section> : null}

    {permissions.has("workflow.request.create") ? <section className="panel audit-panel"><h2>Yeni Talep</h2><form className="inline-form" onSubmit={createRequest}><select name="requestTypeId" required><option value="">Talep türü</option>{types.filter(x => x.isActive).map(x => <option key={x.id} value={x.id}>{x.code} · {x.name}</option>)}</select><select name="employeeId"><option value="">Personel bağlantısı yok</option>{companyEmployees.map(x => <option key={x.id} value={x.id}>{x.employeeNo} · {x.firstName} {x.lastName}</option>)}</select><select name="priority" defaultValue="NORMAL"><option>INFO</option><option>NORMAL</option><option>IMPORTANT</option><option>CRITICAL</option></select><input name="payload" defaultValue='{"reason":""}' placeholder="JSON payload" required/><button className="primary-button" disabled={busy || !companyId}>Taslak oluştur</button></form></section> : null}

    {permissions.has("workflow.request.view") ? <section className="panel audit-panel"><h2>Talepler</h2><div className="table-wrap"><table className="data-table"><thead><tr><th>No</th><th>Tür</th><th>Personel</th><th>Öncelik</th><th>Durum</th><th>Adım</th><th>SLA</th></tr></thead><tbody>{requests.map(x => <tr key={x.id} onClick={() => void openRequest(x.id)}><td><strong>{x.requestNo}</strong></td><td>{x.requestTypeCode}</td><td>{x.employeeNo ? `${x.employeeNo} · ${x.employeeName}` : "—"}</td><td>{x.priority}</td><td>{x.status}</td><td>{x.currentStepOrder || "—"}</td><td>{x.dueAt ? new Date(x.dueAt).toLocaleString() : "—"}</td></tr>)}</tbody></table></div></section> : null}

    {detail ? <section className="panel audit-panel"><div className="panel-heading"><div><span className="eyebrow dark">TALEP DETAYI</span><h2>{detail.request.requestNo} · {detail.request.status}</h2></div><strong>v{detail.request.version}</strong></div><pre>{detail.request.requestDataJson}</pre><div className="actions action-row">{detail.request.status === "DRAFT" && permissions.has("workflow.request.create") ? <button className="primary-button" disabled={busy} onClick={() => void action("submit")}>Gönder</button> : null}{["DRAFT","IN_APPROVAL"].includes(detail.request.status) && permissions.has("workflow.request.create") ? <button className="secondary-button" disabled={busy} onClick={() => void action("cancel")}>İptal</button> : null}{detail.request.status === "IN_APPROVAL" && permissions.has("workflow.request.approve") ? <><button className="primary-button" disabled={busy} onClick={() => void action("approve")}>Onayla</button><button className="secondary-button" disabled={busy} onClick={() => void action("reject")}>Reddet</button></> : null}</div><h3>Onaylar</h3><div className="compact-list">{detail.approvals.map(x => <div key={x.id}><strong>{x.stepOrder}. {x.stepName}</strong> · {x.targetKind}:{x.approverUsername ?? x.approverRoleCode ?? "—"} · {x.status}{x.actionByUsername ? ` · ${x.actionByUsername}` : ""}</div>)}</div><h3>Timeline</h3><div className="compact-list">{detail.timeline.map(x => <div key={x.id}><strong>{x.eventType}</strong> · {x.fromStatus ?? "∅"} → {x.toStatus} · {x.actorUsername} · {new Date(x.occurredAt).toLocaleString()}</div>)}</div></section> : null}

    {permissions.has("workflow.sla.view") ? <section className="panel audit-panel"><h2>SLA Eventleri</h2><div className="compact-list">{slaEvents.map(x => <div key={x.id}><strong>{x.severity} · {x.eventType}</strong> · {x.requestNo} · {x.message} · {new Date(x.createdAt).toLocaleString()}</div>)}</div></section> : null}
  </main>;
}
