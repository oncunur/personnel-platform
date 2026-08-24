"use client";

import { FormEvent, useEffect, useMemo, useState } from "react";
import { useParams } from "next/navigation";
import { Icon } from "../../components/Icon";
import { OperationDisclosure } from "../../components/OperationDisclosure";
import { PageHeader } from "../../components/PageHeader";

const apiBase = process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:8080";

type Permission = { code: string };
type Me = { permissions: Permission[] };
type Employee = { id: string; employeeNo: string; firstName: string; lastName: string; preferredName: string | null; birthDate: string | null; phone: string | null; email: string | null; status: string; companyId: string; branchId: string | null; departmentId: string; positionId: string; employeeTypeId: string; managerEmployeeId: string | null; hireDate: string; terminationDate: string | null; notes: string | null; version: number };
type Assignment = { id: string; projectId: string; costCenterId: string | null; validFrom: string; validUntil: string | null; allocationPercent: number; status: string };
type Named = { id: string; code: string; name: string };
type AuthResponse = { accessToken: string; accessTokenExpiresAt: string };
type DocumentType = { id: string; code: string; name: string; fileRequired: boolean; documentNumberRequired: boolean; expirationRequired: boolean; multipleAllowed: boolean };
type EmployeeDocument = { id: string; employeeId: string; documentTypeId: string; documentTypeCode: string; documentTypeName: string; documentNumber: string | null; issueDate: string | null; validFrom: string | null; validUntil: string | null; status: string; fileName: string | null; contentType: string | null; fileSizeBytes: number | null; replacesDocumentId: string | null; version: number };
type MissingDocument = { documentTypeId: string; code: string; name: string; fileRequired: boolean; documentNumberRequired: boolean; expirationRequired: boolean };
type LeaveRow = { id: string; employeeId: string; employeeNo: string; employeeName: string; leaveTypeId: string; leaveTypeCode: string; leaveTypeName: string; startDate: string; endDate: string; startDayPart: string; endDayPart: string; requestedDays: number; reason: string | null; status: string; submittedAt: string | null; version: number };
type LeavePage = { items: LeaveRow[]; totalCount: number };
type LeaveBalance = { id: string; employeeId: string; leaveTypeId: string; leaveTypeCode: string; leaveTypeName: string; periodStart: string; periodEnd: string; entitledDays: number; carryOverDays: number; adjustmentDays: number; reservedDays: number; usedDays: number; availableDays: number; version: number };
type WorkflowRequest = { id: string; requestNo: string; requestTypeCode: string; requestTypeName: string; requesterUsername: string; employeeId: string | null; priority: string; requestDataJson: string; status: string; currentStepOrder: number; submittedAt: string | null; dueAt: string | null; resolvedAt: string | null; version: number };
type WorkflowTimeline = { id: string; eventType: string; fromStatus: string | null; toStatus: string; actorUsername: string; occurredAt: string; detailsJson: string };
type WorkflowApproval = { id: string; stepOrder: number; stepName: string; targetKind: string; approverUsername: string | null; approverRoleCode: string | null; status: string; actionByUsername: string | null; actionAt: string | null; comment: string | null };
type WorkflowRequestDetail = { request: WorkflowRequest; approvals: WorkflowApproval[]; timeline: WorkflowTimeline[] };

const statusLabels: Record<string, { label: string; tone: string }> = {
  ACTIVE: { label: "Aktif", tone: "success" }, SUSPENDED: { label: "Askıda", tone: "warning" }, TERMINATED: { label: "İşten ayrıldı", tone: "danger" },
  VALID: { label: "Geçerli", tone: "success" }, EXPIRED: { label: "Süresi doldu", tone: "danger" }, ARCHIVED: { label: "Arşivlendi", tone: "" },
  DRAFT: { label: "Taslak", tone: "" }, SUBMITTED: { label: "Gönderildi", tone: "warning" }, PENDING_APPROVAL: { label: "Onay bekliyor", tone: "warning" },
  PENDING: { label: "Bekliyor", tone: "warning" }, IN_PROGRESS: { label: "İşlemde", tone: "warning" }, APPROVED: { label: "Onaylandı", tone: "success" },
  COMPLETED: { label: "Tamamlandı", tone: "success" }, REJECTED: { label: "Reddedildi", tone: "danger" }, CANCELLED: { label: "İptal edildi", tone: "danger" },
};

const priorityLabels: Record<string, string> = { LOW: "Düşük", NORMAL: "Normal", IMPORTANT: "Önemli", HIGH: "Yüksek", CRITICAL: "Kritik" };

function statusOf(value: string) { return statusLabels[value] ?? { label: value, tone: "" }; }
function formatDate(value: string | null) { return value ? new Date(value).toLocaleDateString("tr-TR") : "—"; }

export default function Personel360Page() {
  const params = useParams<{ id: string }>();
  const employeeId = params.id;
  const [me, setMe] = useState<Me | null>(null);
  const [employee, setEmployee] = useState<Employee | null>(null);
  const [assignments, setAssignments] = useState<Assignment[]>([]);
  const [companies, setCompanies] = useState<Named[]>([]);
  const [branches, setBranches] = useState<Named[]>([]);
  const [departments, setDepartments] = useState<Named[]>([]);
  const [positions, setPositions] = useState<Named[]>([]);
  const [projects, setProjects] = useState<Named[]>([]);
  const [costCenters, setCostCenters] = useState<Named[]>([]);
  const [types, setTypes] = useState<Named[]>([]);
  const [documentTypes, setDocumentTypes] = useState<DocumentType[]>([]);
  const [documents, setDocuments] = useState<EmployeeDocument[]>([]);
  const [missingDocuments, setMissingDocuments] = useState<MissingDocument[]>([]);
  const [leaveRows, setLeaveRows] = useState<LeaveRow[]>([]);
  const [leaveBalances, setLeaveBalances] = useState<LeaveBalance[]>([]);
  const [workflowRequests, setWorkflowRequests] = useState<WorkflowRequest[]>([]);
  const [workflowDetail, setWorkflowDetail] = useState<WorkflowRequestDetail | null>(null);
  const [message, setMessage] = useState("Personel 360 yükleniyor…");
  const [busy, setBusy] = useState(false);

  const permissions = useMemo(() => new Set(me?.permissions.map(x => x.code) ?? []), [me]);
  const lookup = (rows: Named[], id: string | null | undefined) => rows.find(x => x.id === id)?.name ?? "—";

  useEffect(() => { void initialize(); }, [employeeId]);

  async function initialize() {
    const [current, person, typeRows, companyRows] = await Promise.all([
      json<Me>("/api/v1/auth/me"),
      json<Employee>(`/api/v1/personnel/employees/${employeeId}`),
      json<Named[]>("/api/v1/personnel/employee-types"),
      json<Named[]>("/api/v1/organization/companies"),
    ]);
    if (!current || !person) { setMessage("Personel bulunamadı veya erişim yok."); return; }
    setMe(current); setEmployee(person); setTypes(typeRows ?? []); setCompanies(companyRows ?? []);

    const [branchRows, departmentRows, projectRows, costRows] = await Promise.all([
      json<Named[]>(`/api/v1/organization/branches?companyId=${person.companyId}`),
      json<Named[]>(`/api/v1/organization/departments?companyId=${person.companyId}`),
      json<Named[]>(`/api/v1/organization/projects?companyId=${person.companyId}`),
      json<Named[]>(`/api/v1/organization/cost-centers?companyId=${person.companyId}`),
    ]);
    setBranches(branchRows ?? []); setDepartments(departmentRows ?? []); setProjects(projectRows ?? []); setCostCenters(costRows ?? []);
    setPositions((await json<Named[]>(`/api/v1/organization/positions?departmentId=${person.departmentId}`)) ?? []);

    if (current.permissions.some(x => x.code === "personnel.project.view"))
      setAssignments((await json<Assignment[]>(`/api/v1/personnel/employees/${employeeId}/project-assignments`)) ?? []);

    if (current.permissions.some(x => x.code === "documents.employee.view"))
      setDocuments((await json<EmployeeDocument[]>(`/api/v1/documents/employees/${employeeId}`)) ?? []);
    if (current.permissions.some(x => x.code === "documents.type.view"))
      setDocumentTypes((await json<DocumentType[]>("/api/v1/documents/types")) ?? []);
    if (current.permissions.some(x => x.code === "documents.missing.view"))
      setMissingDocuments((await json<MissingDocument[]>(`/api/v1/documents/employees/${employeeId}/missing`)) ?? []);

    if (current.permissions.some(x => x.code === "leave.view")) {
      const page = await json<LeavePage>(`/api/v1/leave/requests?employeeId=${employeeId}&pageSize=100`);
      setLeaveRows(page?.items ?? []);
    }
    if (current.permissions.some(x => x.code === "leave.balance.view"))
      setLeaveBalances((await json<LeaveBalance[]>(`/api/v1/leave/employees/${employeeId}/balances`)) ?? []);

    if (current.permissions.some(x => x.code === "workflow.request.view")) {
      const requestRows = (await json<WorkflowRequest[]>(`/api/v1/workflow/requests?employeeId=${employeeId}&take=100`)) ?? [];
      setWorkflowRequests(requestRows);
      if (requestRows.length > 0) setWorkflowDetail(await json<WorkflowRequestDetail>(`/api/v1/workflow/requests/${requestRows[0].id}`));
      else setWorkflowDetail(null);
    }

    setMessage("Personel 360 güncel.");
  }

  async function changeStatus(active: boolean) {
    if (!employee) return; setBusy(true);
    try {
      const response = await authFetch(`/api/v1/personnel/employees/${employee.id}/${active ? "activate" : "suspend"}`, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ version: employee.version }) });
      if (!response?.ok) { setMessage("Durum değiştirilemedi. Kayıt güncellenmiş olabilir."); return; }
      setEmployee(await response.json() as Employee); setMessage(active ? "Personel aktifleştirildi." : "Personel askıya alındı.");
    } finally { setBusy(false); }
  }

  async function assignProject(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); setBusy(true);
    try {
      const fd = new FormData(event.currentTarget);
      const response = await authFetch(`/api/v1/personnel/employees/${employeeId}/project-assignments`, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ projectId: fd.get("projectId"), costCenterId: fd.get("costCenterId") || null, validFrom: fd.get("validFrom"), validUntil: fd.get("validUntil") || null, allocationPercent: Number(fd.get("allocationPercent")) }) });
      if (!response?.ok) { const error = response ? await response.json().catch(() => null) as { error?: { message?: string } } | null : null; setMessage(error?.error?.message ?? "Proje atanamadı."); return; }
      const row = await response.json() as Assignment; setAssignments(current => [row, ...current]); event.currentTarget.reset(); setMessage("Proje ataması oluşturuldu.");
    } finally { setBusy(false); }
  }

  async function uploadDocument(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); setBusy(true);
    try {
      const form = new FormData(event.currentTarget);
      const response = await authFetch(`/api/v1/documents/employees/${employeeId}`, { method: "POST", body: form });
      if (!response?.ok) {
        const error = response ? await response.json().catch(() => null) as { error?: { message?: string } } | null : null;
        setMessage(error?.error?.message ?? "Belge yüklenemedi."); return;
      }
      const row = await response.json() as EmployeeDocument;
      setDocuments(current => [row, ...current]);
      if (permissions.has("documents.missing.view")) setMissingDocuments((await json<MissingDocument[]>(`/api/v1/documents/employees/${employeeId}/missing`)) ?? []);
      event.currentTarget.reset(); setMessage("Belge güvenli depolama alanına kaydedildi.");
    } finally { setBusy(false); }
  }

  async function openDocument(documentId: string) {
    setBusy(true);
    try {
      const response = await authFetch(`/api/v1/documents/employee-documents/${documentId}/file`);
      if (!response?.ok) { setMessage("Belge dosyası açılamadı."); return; }
      const blob = await response.blob();
      const url = URL.createObjectURL(blob);
      window.open(url, "_blank", "noopener,noreferrer");
      window.setTimeout(() => URL.revokeObjectURL(url), 60000);
    } finally { setBusy(false); }
  }

  async function loadWorkflowDetail(requestId: string) {
    setBusy(true);
    try {
      const detail = await json<WorkflowRequestDetail>(`/api/v1/workflow/requests/${requestId}`);
      if (!detail) { setMessage("Talep timeline bilgisi alınamadı."); return; }
      setWorkflowDetail(detail); setMessage(`${detail.request.requestNo} timeline yüklendi.`);
    } finally { setBusy(false); }
  }

  async function json<T>(path: string): Promise<T | null> { const response = await authFetch(path); return response?.ok ? await response.json() as T : null; }
  async function authFetch(path: string, init?: RequestInit): Promise<Response | null> {
    let token = sessionStorage.getItem("pp_access_token") ?? await refresh(); if (!token) return null;
    let response = await fetch(`${apiBase}${path}`, { ...init, headers: { ...(init?.headers ?? {}), Authorization: `Bearer ${token}` }, credentials: "include" });
    if (response.status !== 401) return response; token = await refresh(); if (!token) return response;
    return fetch(`${apiBase}${path}`, { ...init, headers: { ...(init?.headers ?? {}), Authorization: `Bearer ${token}` }, credentials: "include" });
  }
  async function refresh(): Promise<string | null> { try { const response = await fetch(`${apiBase}/api/v1/auth/refresh`, { method: "POST", credentials: "include" }); if (!response.ok) return null; const body = await response.json() as AuthResponse; sessionStorage.setItem("pp_access_token", body.accessToken); return body.accessToken; } catch { return null; } }

  const availableLeaveDays = leaveBalances.reduce((sum, row) => sum + row.availableDays, 0);
  const openRequests = workflowRequests.filter(row => !["APPROVED", "COMPLETED", "REJECTED", "CANCELLED"].includes(row.status));
  const activeAssignments = assignments.filter(row => row.status === "ACTIVE");

  if (!employee) return <main className="page-shell"><PageHeader eyebrow="Personel 360" title="Personel kaydı" description="Personel bilgileri yükleniyor veya kayıt erişimi kontrol ediliyor." status={message} actions={<a className="secondary-button" href="/personnel">Personel listesine dön</a>}/></main>;

  const employeeStatus = statusOf(employee.status);
  return <main className="page-shell">
    <PageHeader eyebrow="Personel 360" title={`${employee.firstName} ${employee.lastName}`} description={`${employee.employeeNo} · ${lookup(departments, employee.departmentId)} · ${lookup(positions, employee.positionId)}`} status={message} actions={<><a className="secondary-button" href="/personnel">Personel listesi</a>{permissions.has("personnel.update") ? <button className={`secondary-button ${employee.status === "ACTIVE" ? "button-danger" : "button-success"}`} type="button" disabled={busy} onClick={() => void changeStatus(employee.status !== "ACTIVE")}>{employee.status === "ACTIVE" ? "Askıya al" : "Aktifleştir"}</button> : null}</>}/>

    <div className="selected-summary"><span className="selected-summary-copy"><strong>{lookup(companies, employee.companyId)}</strong><small>{lookup(branches, employee.branchId)} · {lookup(departments, employee.departmentId)} · {lookup(positions, employee.positionId)}</small></span><span className={`status-badge ${employeeStatus.tone}`}>{employeeStatus.label}</span></div>

    <section className="stat-grid" aria-label="Personel özeti">
      <article className="stat-card"><span className="stat-icon"><Icon name="box"/></span><span className="stat-copy"><strong>{documents.length}</strong><span>Özlük belgesi</span></span></article>
      <article className="stat-card"><span className="stat-icon"><Icon name="bell"/></span><span className="stat-copy"><strong>{missingDocuments.length}</strong><span>Eksik zorunlu belge</span></span></article>
      <article className="stat-card"><span className="stat-icon"><Icon name="calendar"/></span><span className="stat-copy"><strong>{availableLeaveDays.toLocaleString("tr-TR")}</strong><span>Kullanılabilir izin günü</span></span></article>
      <article className="stat-card"><span className="stat-icon"><Icon name="workflow"/></span><span className="stat-copy"><strong>{openRequests.length + activeAssignments.length}</strong><span>Açık talep ve aktif proje</span></span></article>
    </section>

    <div className="content-stack">

    <section className="security-grid">
      <article className="panel"><div className="panel-heading"><div><span className="eyebrow dark">Kimlik ve iletişim</span><h2>Personel bilgileri</h2><p>Temel özlük ve iletişim kayıtları.</p></div><strong>v{employee.version}</strong></div><div className="detail-grid"><Item label="Ad soyad" value={`${employee.firstName} ${employee.lastName}`}/><Item label="Tercih edilen ad" value={employee.preferredName}/><Item label="Personel türü" value={lookup(types, employee.employeeTypeId)}/><Item label="İşe giriş" value={formatDate(employee.hireDate)}/><Item label="Telefon" value={employee.phone}/><Item label="E-posta" value={employee.email}/><Item label="Doğum tarihi" value={formatDate(employee.birthDate)}/><Item label="İşten ayrılma" value={formatDate(employee.terminationDate)}/></div></article>
      <article className="panel"><div className="panel-heading"><div><span className="eyebrow dark">İş ve organizasyon</span><h2>Organizasyon konumu</h2><p>Personelin güncel şirket, birim ve pozisyon bağlantıları.</p></div></div><div className="detail-grid"><Item label="Şirket" value={lookup(companies, employee.companyId)}/><Item label="Şube" value={lookup(branches, employee.branchId)}/><Item label="Departman" value={lookup(departments, employee.departmentId)}/><Item label="Pozisyon" value={lookup(positions, employee.positionId)}/></div></article>
    </section>

    {permissions.has("documents.employee.view") ? <section className="panel audit-panel">
      <div className="panel-heading"><div><span className="eyebrow dark">Özlük ve belgeler</span><h2>Dijital özlük dosyası</h2><p>Personelin geçerli belgelerini, dosyalarını ve eksik evraklarını izleyin.</p></div><strong>{documents.length}</strong></div>
      {permissions.has("documents.missing.view") && missingDocuments.length > 0 ? <div className="missing-strip" role="status"><strong>{missingDocuments.length} zorunlu belge eksik</strong><span>{missingDocuments.map(x => x.name).join(" · ")}</span></div> : null}
      {permissions.has("documents.employee.upload") && permissions.has("documents.type.view") ? <OperationDisclosure title="Yeni belge ekle" description="Belge türünün gerektirdiği tarih, numara ve dosya bilgilerini girin."><form className="inline-form document-form" onSubmit={uploadDocument}>
        <label className="field-label">Belge Türü<select name="documentTypeId" required><option value="">Seçin</option>{documentTypes.map(x => <option key={x.id} value={x.id}>{x.code} · {x.name}</option>)}</select></label>
        <label className="field-label">Belge No<input name="documentNumber" maxLength={150}/></label>
        <label className="field-label">Düzenlenme<input name="issueDate" type="date"/></label>
        <label className="field-label">Geçerli Başlangıç<input name="validFrom" type="date"/></label>
        <label className="field-label">Geçerli Bitiş<input name="validUntil" type="date"/></label>
        <label className="field-label">Ülke kodu<input name="countryCode" maxLength={3} defaultValue="TR"/></label>
        <label className="field-label">Dosya<input name="file" type="file" accept="application/pdf,image/jpeg,image/png"/></label>
        <button className="primary-button" disabled={busy}><Icon name="plus" size={17}/>Belge ekle</button>
      </form></OperationDisclosure> : null}
      <div className="table-wrap responsive-table-wrap" role="region" aria-label="Personel belgeleri" tabIndex={0}><table className="data-table responsive-table"><thead><tr><th>Belge</th><th>No</th><th>Geçerlilik</th><th>Durum</th><th>Dosya</th></tr></thead><tbody>{documents.length === 0 ? <tr><td className="empty-row" colSpan={5}>Bu personel için henüz belge eklenmedi.</td></tr> : documents.map(x => { const status = statusOf(x.status); return <tr key={x.id}><td data-label="Belge"><strong>{x.documentTypeName}</strong><small>{x.documentTypeCode}</small></td><td data-label="No">{x.documentNumber ?? "—"}</td><td data-label="Geçerlilik">{formatDate(x.validFrom ?? x.issueDate)} → {x.validUntil ? formatDate(x.validUntil) : "Süresiz"}</td><td data-label="Durum"><span className={`status-badge ${status.tone}`}>{status.label}</span></td><td data-label="Dosya">{x.fileName ? <div className="action-row"><span>{x.fileName}</span>{permissions.has("documents.file.view") ? <button className="table-button document-open" type="button" disabled={busy} onClick={() => void openDocument(x.id)}>Dosyayı aç</button> : null}</div> : "—"}</td></tr>; })}</tbody></table></div>
    </section> : null}

    {permissions.has("leave.view") || permissions.has("leave.balance.view") ? <section className="panel audit-panel">
      <div className="panel-heading"><div><span className="eyebrow dark">İzin</span><h2>İzin geçmişi ve bakiye</h2><p>Talep durumlarını ve dönemsel kullanılabilir hakları birlikte inceleyin.</p></div><div className="action-row"><span className="status-badge">{leaveRows.length} kayıt</span><a className="table-button" href="/leave">İzin merkezine git</a></div></div>
      {permissions.has("leave.view") ? <><div className="table-section-heading"><div className="panel-heading"><div><h3>İzin talepleri</h3></div></div></div><div className="table-wrap responsive-table-wrap" role="region" aria-label="Personel izin talepleri" tabIndex={0}><table className="data-table responsive-table"><thead><tr><th>İzin türü</th><th>Tarih</th><th>Gün</th><th>Durum</th><th>Açıklama</th></tr></thead><tbody>{leaveRows.length === 0 ? <tr><td className="empty-row" colSpan={5}>Bu personele ait izin talebi bulunmuyor.</td></tr> : leaveRows.map(x => { const status = statusOf(x.status); return <tr key={x.id}><td data-label="İzin türü"><strong>{x.leaveTypeName}</strong><small>{x.leaveTypeCode}</small></td><td data-label="Tarih">{formatDate(x.startDate)} → {formatDate(x.endDate)}<small>{x.startDayPart} / {x.endDayPart}</small></td><td data-label="Gün">{x.requestedDays}</td><td data-label="Durum"><span className={`status-badge ${status.tone}`}>{status.label}</span></td><td data-label="Açıklama">{x.reason ?? "—"}</td></tr>; })}</tbody></table></div></> : null}
      {permissions.has("leave.balance.view") ? <><div className="table-section-heading"><div className="panel-heading"><div><h3>İzin bakiyeleri</h3></div></div></div><div className="table-wrap responsive-table-wrap" role="region" aria-label="Personel izin bakiyeleri" tabIndex={0}><table className="data-table responsive-table"><thead><tr><th>Bakiye türü</th><th>Dönem</th><th>Hakediş + Devir</th><th>Rezerve</th><th>Kullanılan</th><th>Kullanılabilir</th></tr></thead><tbody>{leaveBalances.length === 0 ? <tr><td className="empty-row" colSpan={6}>Bu personel için izin bakiyesi bulunmuyor.</td></tr> : leaveBalances.map(x => <tr key={x.id}><td data-label="Bakiye türü"><strong>{x.leaveTypeName}</strong><small>{x.leaveTypeCode}</small></td><td data-label="Dönem">{formatDate(x.periodStart)} → {formatDate(x.periodEnd)}</td><td data-label="Hakediş ve devir">{x.entitledDays} + {x.carryOverDays}{x.adjustmentDays !== 0 ? ` (${x.adjustmentDays > 0 ? "+" : ""}${x.adjustmentDays})` : ""}</td><td data-label="Rezerve">{x.reservedDays}</td><td data-label="Kullanılan">{x.usedDays}</td><td data-label="Kullanılabilir"><strong className="amount-positive">{x.availableDays}</strong></td></tr>)}</tbody></table></div></> : null}
    </section> : null}

    {permissions.has("workflow.request.view") ? <section className="panel audit-panel">
      <div className="panel-heading"><div><span className="eyebrow dark">Talep ve onaylar</span><h2>Personel talep geçmişi</h2><p>Personelle ilişkili taleplerin güncel durumunu ve onay adımlarını inceleyin.</p></div><div className="action-row"><span className="status-badge">{workflowRequests.length} kayıt</span><a className="table-button" href="/workflow">Talep merkezine git</a></div></div>
      <div className="table-wrap responsive-table-wrap" role="region" aria-label="Personel talep geçmişi" tabIndex={0}><table className="data-table responsive-table"><thead><tr><th>Talep no</th><th>Tür</th><th>Talep eden</th><th>Öncelik</th><th>Durum</th><th>Son tarih</th><th>İşlem</th></tr></thead><tbody>{workflowRequests.length === 0 ? <tr><td className="empty-row" colSpan={7}>Bu personele bağlı talep bulunmuyor.</td></tr> : workflowRequests.map(x => { const status = statusOf(x.status); return <tr key={x.id} className={workflowDetail?.request.id === x.id ? "selected-row" : ""}><td data-label="Talep no"><strong>{x.requestNo}</strong></td><td data-label="Tür">{x.requestTypeName}<small>{x.requestTypeCode}</small></td><td data-label="Talep eden">{x.requesterUsername}</td><td data-label="Öncelik">{priorityLabels[x.priority] ?? x.priority}</td><td data-label="Durum"><span className={`status-badge ${status.tone}`}>{status.label}</span></td><td data-label="Son tarih">{x.dueAt ? new Date(x.dueAt).toLocaleString("tr-TR") : "—"}</td><td data-label="İşlem"><button className="table-button" type="button" disabled={busy} onClick={() => void loadWorkflowDetail(x.id)}>Süreci incele</button></td></tr>; })}</tbody></table></div>
      {workflowDetail ? <div className="security-grid">
        <article className="panel"><div className="panel-heading"><div><span className="eyebrow dark">Onay adımları</span><h3>{workflowDetail.request.requestNo}</h3></div><span className={`status-badge ${statusOf(workflowDetail.request.status).tone}`}>{statusOf(workflowDetail.request.status).label}</span></div><div className="compact-list">{workflowDetail.approvals.length === 0 ? <p className="muted">Onay adımı bulunmuyor.</p> : workflowDetail.approvals.map(x => { const status = statusOf(x.status); return <div className="role-row" key={x.id}><strong>{x.stepOrder}. {x.stepName}</strong><span>{x.approverUsername ?? x.approverRoleCode ?? x.targetKind} · {status.label}{x.actionByUsername ? ` · İşlem: ${x.actionByUsername}` : ""}{x.comment ? ` · ${x.comment}` : ""}</span></div>; })}</div></article>
        <article className="panel"><div className="panel-heading"><div><span className="eyebrow dark">Değiştirilemeyen kayıt</span><h3>Durum geçmişi</h3></div><strong>{workflowDetail.timeline.length}</strong></div><div className="compact-list">{workflowDetail.timeline.length === 0 ? <p className="muted">Durum geçmişi bulunmuyor.</p> : workflowDetail.timeline.map(x => <div className="role-row" key={x.id}><strong>{x.eventType}</strong><span>{x.fromStatus ? statusOf(x.fromStatus).label : "Başlangıç"} → {statusOf(x.toStatus).label}</span><small>{new Date(x.occurredAt).toLocaleString("tr-TR")} · {x.actorUsername}</small></div>)}</div></article>
      </div> : null}
    </section> : null}

    {permissions.has("personnel.project.view") ? <section className="panel audit-panel"><div className="panel-heading"><div><span className="eyebrow dark">Proje dağılımı</span><h2>Proje atamaları</h2><p>Personelin çalışma dönemlerini ve proje bazlı kapasite dağılımını izleyin.</p></div><strong>{assignments.length}</strong></div><div className="table-wrap responsive-table-wrap" role="region" aria-label="Personel proje atamaları" tabIndex={0}><table className="data-table responsive-table"><thead><tr><th>Proje</th><th>Maliyet merkezi</th><th>Tarih</th><th>Kapasite</th><th>Durum</th></tr></thead><tbody>{assignments.length === 0 ? <tr><td className="empty-row" colSpan={5}>Bu personelin proje ataması bulunmuyor.</td></tr> : assignments.map(x => { const status = statusOf(x.status); return <tr key={x.id}><td data-label="Proje"><strong>{lookup(projects, x.projectId)}</strong></td><td data-label="Maliyet merkezi">{lookup(costCenters, x.costCenterId)}</td><td data-label="Tarih">{formatDate(x.validFrom)} → {x.validUntil ? formatDate(x.validUntil) : "Devam ediyor"}</td><td data-label="Kapasite">%{x.allocationPercent}</td><td data-label="Durum"><span className={`status-badge ${status.tone}`}>{status.label}</span></td></tr>; })}</tbody></table></div>{permissions.has("personnel.project.assign") ? <OperationDisclosure title="Yeni proje ataması" description="Toplam kapasite dağılımının aynı dönemde yüzde 100'ü aşmamasına dikkat edin."><form className="inline-form" onSubmit={assignProject}><label className="field-label">Proje<select name="projectId" required><option value="">Proje seçin</option>{projects.map(x => <option key={x.id} value={x.id}>{x.code} · {x.name}</option>)}</select></label><label className="field-label">Maliyet merkezi<select name="costCenterId"><option value="">Merkez seçilmedi</option>{costCenters.map(x => <option key={x.id} value={x.id}>{x.code} · {x.name}</option>)}</select></label><Field name="validFrom" label="Başlangıç" type="date"/><Field name="validUntil" label="Bitiş" type="date"/><Field name="allocationPercent" label="Kapasite oranı (%)" type="number"/><button className="primary-button" disabled={busy}><Icon name="plus" size={17}/>Atama oluştur</button></form></OperationDisclosure> : null}</section> : null}

    </div>
  </main>;
}

function Item({ label, value }: { label: string; value?: string | null }) { return <div className="detail-item"><span>{label}</span><strong>{value || "—"}</strong></div>; }
function Field({ name, label, type = "text" }: { name: string; label: string; type?: string }) { return <label className="field-label">{label}<input name={name} type={type} required={name === "validFrom" || name === "allocationPercent"} min={name === "allocationPercent" ? 1 : undefined} max={name === "allocationPercent" ? 100 : undefined}/></label>; }
