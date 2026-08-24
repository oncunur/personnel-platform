"use client";

import { FormEvent, useEffect, useMemo, useState } from "react";
import { Icon } from "../components/Icon";
import { OperationDisclosure } from "../components/OperationDisclosure";
import { PageHeader } from "../components/PageHeader";

const apiBase = process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:8080";

type Permission = { code: string };
type Me = { userId: string; username: string; permissions: Permission[] };
type Company = { id: string; code: string; name: string; defaultCurrency: string; isActive: boolean };
type Branch = { id: string; companyId: string; code: string; name: string; location: string | null; isActive: boolean };
type Department = { id: string; companyId: string; branchId: string | null; parentDepartmentId: string | null; code: string; name: string; isActive: boolean };
type Position = { id: string; departmentId: string; code: string; name: string; isActive: boolean };
type Project = { id: string; companyId: string; code: string; name: string; status: string; location: string | null; countryCode: string | null; isActive: boolean };
type CostCenter = { id: string; companyId: string; projectId: string | null; parentCostCenterId: string | null; code: string; name: string; isActive: boolean };
type AuthResponse = { accessToken: string; accessTokenExpiresAt: string };

const projectStatuses: Record<string, { label: string; tone: string }> = {
  PLANNED: { label: "Planlandı", tone: "warning" },
  ACTIVE: { label: "Aktif", tone: "success" },
  ON_HOLD: { label: "Beklemede", tone: "warning" },
  COMPLETED: { label: "Tamamlandı", tone: "success" },
  CANCELLED: { label: "İptal", tone: "danger" },
};

function projectStatusOf(value: string) {
  return projectStatuses[value] ?? { label: value, tone: "" };
}

export default function OrganizationPage() {
  const [me, setMe] = useState<Me | null>(null);
  const [companies, setCompanies] = useState<Company[]>([]);
  const [selectedCompanyId, setSelectedCompanyId] = useState("");
  const [branches, setBranches] = useState<Branch[]>([]);
  const [departments, setDepartments] = useState<Department[]>([]);
  const [positions, setPositions] = useState<Position[]>([]);
  const [projects, setProjects] = useState<Project[]>([]);
  const [costCenters, setCostCenters] = useState<CostCenter[]>([]);
  const [selectedDepartmentId, setSelectedDepartmentId] = useState("");
  const [message, setMessage] = useState("Organizasyon yükleniyor…");
  const [busy, setBusy] = useState(false);

  const permissions = useMemo(() => new Set(me?.permissions.map((x) => x.code) ?? []), [me]);

  useEffect(() => { void initialize(); }, []);
  useEffect(() => { if (selectedCompanyId) void loadCompanyDetails(selectedCompanyId); }, [selectedCompanyId]);
  useEffect(() => { if (selectedDepartmentId) void loadPositions(selectedDepartmentId); else setPositions([]); }, [selectedDepartmentId]);

  async function initialize() {
    const current = await authorizedJson<Me>("/api/v1/auth/me");
    if (!current) { window.location.replace("/login"); return; }
    setMe(current);
    const companyRows = await authorizedJson<Company[]>("/api/v1/organization/companies");
    const rows = companyRows ?? [];
    setCompanies(rows);
    if (rows.length > 0) setSelectedCompanyId(rows[0].id);
    setMessage("Organizasyon verileri güncel.");
  }

  async function loadCompanyDetails(companyId: string) {
    const [branchRows, departmentRows, projectRows, costRows] = await Promise.all([
      authorizedJson<Branch[]>(`/api/v1/organization/branches?companyId=${companyId}`),
      authorizedJson<Department[]>(`/api/v1/organization/departments?companyId=${companyId}`),
      authorizedJson<Project[]>(`/api/v1/organization/projects?companyId=${companyId}`),
      authorizedJson<CostCenter[]>(`/api/v1/organization/cost-centers?companyId=${companyId}`),
    ]);
    setBranches(branchRows ?? []);
    setDepartments(departmentRows ?? []);
    setProjects(projectRows ?? []);
    setCostCenters(costRows ?? []);
    setSelectedDepartmentId((departmentRows ?? [])[0]?.id ?? "");
  }

  async function loadPositions(departmentId: string) {
    setPositions((await authorizedJson<Position[]>(`/api/v1/organization/positions?departmentId=${departmentId}`)) ?? []);
  }

  async function createEntity<T>(path: string, body: unknown): Promise<T | null> {
    setBusy(true);
    try {
      const response = await authorizedFetch(path, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(body) });
      if (!response?.ok) {
        const error = response ? await response.json().catch(() => null) as { error?: { message?: string } } | null : null;
        setMessage(error?.error?.message ?? `İşlem başarısız (${response?.status ?? "network"}).`);
        return null;
      }
      setMessage("Kayıt oluşturuldu.");
      return await response.json() as T;
    } finally { setBusy(false); }
  }

  async function onCompanySubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); const form = event.currentTarget; const fd = new FormData(form);
    const row = await createEntity<Company>("/api/v1/organization/companies", { code: fd.get("code"), name: fd.get("name"), defaultCurrency: fd.get("currency") || "TRY" });
    if (row) { setCompanies((current) => [...current, row].sort((a,b) => a.code.localeCompare(b.code))); setSelectedCompanyId(row.id); form.reset(); }
  }

  async function onBranchSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); if (!selectedCompanyId) return; const form = event.currentTarget; const fd = new FormData(form);
    const row = await createEntity<Branch>("/api/v1/organization/branches", { companyId: selectedCompanyId, code: fd.get("code"), name: fd.get("name"), location: fd.get("location") || null });
    if (row) { setBranches((current) => [...current, row]); form.reset(); }
  }

  async function onDepartmentSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); if (!selectedCompanyId) return; const form = event.currentTarget; const fd = new FormData(form);
    const branchId = String(fd.get("branchId") ?? ""); const parentDepartmentId = String(fd.get("parentId") ?? "");
    const row = await createEntity<Department>("/api/v1/organization/departments", { companyId: selectedCompanyId, branchId: branchId || null, parentDepartmentId: parentDepartmentId || null, code: fd.get("code"), name: fd.get("name") });
    if (row) { setDepartments((current) => [...current, row]); setSelectedDepartmentId(row.id); form.reset(); }
  }

  async function onPositionSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); if (!selectedDepartmentId) return; const form = event.currentTarget; const fd = new FormData(form);
    const row = await createEntity<Position>("/api/v1/organization/positions", { departmentId: selectedDepartmentId, code: fd.get("code"), name: fd.get("name") });
    if (row) { setPositions((current) => [...current, row]); form.reset(); }
  }

  async function onProjectSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); if (!selectedCompanyId) return; const form = event.currentTarget; const fd = new FormData(form);
    const row = await createEntity<Project>("/api/v1/organization/projects", { companyId: selectedCompanyId, code: fd.get("code"), name: fd.get("name"), location: fd.get("location") || null, countryCode: fd.get("countryCode") || null, startDate: fd.get("startDate") || null, plannedEndDate: fd.get("plannedEndDate") || null });
    if (row) { setProjects((current) => [...current, row]); form.reset(); }
  }

  async function onCostCenterSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); if (!selectedCompanyId) return; const form = event.currentTarget; const fd = new FormData(form); const projectId = String(fd.get("projectId") ?? ""); const parentId = String(fd.get("parentId") ?? "");
    const row = await createEntity<CostCenter>("/api/v1/organization/cost-centers", { companyId: selectedCompanyId, projectId: projectId || null, parentCostCenterId: parentId || null, code: fd.get("code"), name: fd.get("name") });
    if (row) { setCostCenters((current) => [...current, row]); form.reset(); }
  }

  async function authorizedJson<T>(path: string): Promise<T | null> {
    const response = await authorizedFetch(path);
    if (!response) return null;
    if (response.status === 401) { clearLocalSession(); return null; }
    if (!response.ok) { setMessage(`İstek tamamlanamadı (${response.status}).`); return null; }
    return await response.json() as T;
  }

  async function authorizedFetch(path: string, init?: RequestInit): Promise<Response | null> {
    let token = sessionStorage.getItem("pp_access_token") ?? await refreshAccessToken();
    if (!token) return null;
    let response = await fetch(`${apiBase}${path}`, { ...init, headers: { ...(init?.headers ?? {}), Authorization: `Bearer ${token}` }, credentials: "include" });
    if (response.status !== 401) return response;
    token = await refreshAccessToken(); if (!token) return response;
    return fetch(`${apiBase}${path}`, { ...init, headers: { ...(init?.headers ?? {}), Authorization: `Bearer ${token}` }, credentials: "include" });
  }

  async function refreshAccessToken(): Promise<string | null> {
    try {
      const response = await fetch(`${apiBase}/api/v1/auth/refresh`, { method: "POST", credentials: "include" });
      if (!response.ok) { clearLocalSession(); return null; }
      const body = await response.json() as AuthResponse;
      sessionStorage.setItem("pp_access_token", body.accessToken); sessionStorage.setItem("pp_access_token_expires_at", body.accessTokenExpiresAt); return body.accessToken;
    } catch { return null; }
  }

  function clearLocalSession() { sessionStorage.removeItem("pp_access_token"); sessionStorage.removeItem("pp_access_token_expires_at"); }

  const selectedCompany = companies.find((x) => x.id === selectedCompanyId);
  const selectedDepartment = departments.find((x) => x.id === selectedDepartmentId);
  const branchName = (id: string | null) => branches.find(x => x.id === id)?.name ?? "Şubeden bağımsız";
  const departmentName = (id: string | null) => departments.find(x => x.id === id)?.name ?? "Üst departman yok";
  const projectName = (id: string | null) => projects.find(x => x.id === id)?.name ?? "Projeden bağımsız";
  const costCenterName = (id: string | null) => costCenters.find(x => x.id === id)?.name ?? "Üst merkez yok";

  return (
    <main className="page-shell">
      <PageHeader eyebrow="Şirket yapısı" title="Organizasyon yönetimi" description="Şirket, şube, departman, pozisyon, proje ve maliyet merkezi yapısını tek yerden yönetin." status={message}/>

      <section className="stat-grid" aria-label="Organizasyon özeti">
        <article className="stat-card"><span className="stat-icon"><Icon name="building"/></span><span className="stat-copy"><strong>{branches.length}</strong><span>Şube</span></span></article>
        <article className="stat-card"><span className="stat-icon"><Icon name="people"/></span><span className="stat-copy"><strong>{departments.length}</strong><span>Departman</span></span></article>
        <article className="stat-card"><span className="stat-icon"><Icon name="workflow"/></span><span className="stat-copy"><strong>{projects.length}</strong><span>Proje</span></span></article>
        <article className="stat-card"><span className="stat-icon"><Icon name="wallet"/></span><span className="stat-copy"><strong>{costCenters.length}</strong><span>Maliyet merkezi</span></span></article>
      </section>

      <section className="panel workspace-panel">
        <div className="workspace-copy"><span className="eyebrow dark">Çalışma alanı</span><h2>{selectedCompany?.name ?? "Şirket seçilmedi"}</h2><p>{selectedCompany ? `${selectedCompany.code} kodlu şirket · Varsayılan para birimi ${selectedCompany.defaultCurrency}` : "Organizasyon yapısını görüntülemek için bir şirket seçin."}</p></div>
        <label className="field-label workspace-select">Aktif şirket<select value={selectedCompanyId} onChange={(e) => setSelectedCompanyId(e.target.value)}><option value="">Şirket seçin</option>{companies.map((x) => <option key={x.id} value={x.id}>{x.code} · {x.name}</option>)}</select></label>
      </section>

      <div className="content-stack">
        {permissions.has("organization.company.manage") ? <OrgForm title="Yeni şirket oluştur" description="Platforma eklenecek tüzel kişi ve varsayılan para birimini tanımlayın." onSubmit={onCompanySubmit} busy={busy}><Text name="code" label="Şirket kodu"/><Text name="name" label="Şirket adı"/><Text name="currency" label="Para birimi" defaultValue="TRY"/></OrgForm> : null}

        {selectedCompanyId ? <section className="organization-grid">
          <OrgSection eyebrow="Lokasyon" title="Şubeler" operationTitle="Yeni şube ekle" description="Şirketin fiziksel veya idari çalışma noktaları." rows={branches.map((x) => <div className="role-row" key={x.id}><strong>{x.name}</strong><span>{x.code} · {x.location || "Lokasyon belirtilmedi"}</span></div>)}>
            {permissions.has("organization.branch.manage") ? <form className="inline-form" onSubmit={onBranchSubmit}><Text name="code" label="Şube kodu"/><Text name="name" label="Şube adı"/><Text name="location" label="Lokasyon"/><Submit busy={busy}/></form> : null}
          </OrgSection>

          <OrgSection eyebrow="Hiyerarşi" title="Departmanlar" operationTitle="Yeni departman ekle" description="Departmanların bağlı şube ve üst departman ilişkileri." rows={departments.map((x) => <button className={`organization-row ${selectedDepartmentId === x.id ? "selected" : ""}`} type="button" key={x.id} onClick={() => setSelectedDepartmentId(x.id)}><span><strong>{x.name}</strong><small>{x.code}</small></span><span>{branchName(x.branchId)}<small>{departmentName(x.parentDepartmentId)}</small></span></button>)}>
            {permissions.has("organization.department.manage") ? <form className="inline-form" onSubmit={onDepartmentSubmit}><Text name="code" label="Departman kodu"/><Text name="name" label="Departman adı"/><Select name="branchId" label="Bağlı şube" options={branches.map(x => [x.id, `${x.code} · ${x.name}`])}/><Select name="parentId" label="Üst departman" options={departments.map(x => [x.id, `${x.code} · ${x.name}`])}/><Submit busy={busy}/></form> : null}
          </OrgSection>

          <OrgSection eyebrow="Kadro" title="Pozisyonlar" operationTitle="Yeni pozisyon ekle" description={selectedDepartment ? `${selectedDepartment.name} departmanındaki pozisyonlar.` : "Pozisyonları görmek için departman seçin."} rows={positions.map((x) => <div className="role-row" key={x.id}><strong>{x.name}</strong><span>{x.code}</span></div>)} controls={<div className="selection-bar"><label className="field-label">Departman<select value={selectedDepartmentId} onChange={(e) => setSelectedDepartmentId(e.target.value)}><option value="">Departman seçin</option>{departments.map(x => <option key={x.id} value={x.id}>{x.code} · {x.name}</option>)}</select></label><span className="selection-context"><span>Seçili departman</span><strong>{selectedDepartment?.name ?? "—"}</strong></span></div>}>
            {selectedDepartmentId && permissions.has("organization.position.manage") ? <form className="inline-form" onSubmit={onPositionSubmit}><Text name="code" label="Pozisyon kodu"/><Text name="name" label="Pozisyon adı"/><Submit busy={busy}/></form> : null}
          </OrgSection>

          <OrgSection eyebrow="Operasyon" title="Projeler" operationTitle="Yeni proje ekle" description="Şirket bünyesinde yürütülen saha ve merkez projeleri." rows={projects.map((x) => { const status = projectStatusOf(x.status); return <div className="organization-row static" key={x.id}><span><strong>{x.name}</strong><small>{x.code}</small></span><span><span className={`status-badge ${status.tone}`}>{status.label}</span><small>{[x.location, x.countryCode].filter(Boolean).join(" · ") || "Lokasyon belirtilmedi"}</small></span></div>; })}>
            {permissions.has("organization.project.manage") ? <form className="inline-form" onSubmit={onProjectSubmit}><Text name="code" label="Proje kodu"/><Text name="name" label="Proje adı"/><Text name="location" label="Lokasyon"/><Text name="countryCode" label="Ülke kodu"/><Text name="startDate" label="Başlangıç" type="date"/><Text name="plannedEndDate" label="Planlanan bitiş" type="date"/><Submit busy={busy}/></form> : null}
          </OrgSection>

          <OrgSection eyebrow="Finansal yapı" title="Maliyet merkezleri" operationTitle="Yeni maliyet merkezi ekle" description="Proje ve üst merkez bağlantılarıyla maliyet dağıtım yapısı." rows={costCenters.map((x) => <div className="organization-row static" key={x.id}><span><strong>{x.name}</strong><small>{x.code}</small></span><span>{projectName(x.projectId)}<small>{costCenterName(x.parentCostCenterId)}</small></span></div>)}>
            {permissions.has("organization.costcenter.manage") ? <form className="inline-form" onSubmit={onCostCenterSubmit}><Text name="code" label="Merkez kodu"/><Text name="name" label="Merkez adı"/><Select name="projectId" label="Bağlı proje" options={projects.map(x => [x.id, `${x.code} · ${x.name}`])}/><Select name="parentId" label="Üst maliyet merkezi" options={costCenters.map(x => [x.id, `${x.code} · ${x.name}`])}/><Submit busy={busy}/></form> : null}
          </OrgSection>
        </section> : <section className="panel"><p className="empty-row">Organizasyon detaylarını görüntülemek için bir şirket seçin.</p></section>}
      </div>
    </main>
  );
}

function OrgForm({ title, description, onSubmit, busy, children }: { title: string; description: string; onSubmit: (e: FormEvent<HTMLFormElement>) => void; busy: boolean; children: React.ReactNode }) { return <section className="panel"><OperationDisclosure title={title} description={description}><form className="inline-form" onSubmit={onSubmit}>{children}<Submit busy={busy}/></form></OperationDisclosure></section>; }
function OrgSection({ eyebrow, title, operationTitle, description, rows, controls, children }: { eyebrow: string; title: string; operationTitle: string; description: string; rows: React.ReactNode[]; controls?: React.ReactNode; children?: React.ReactNode }) { return <article className="panel"><div className="panel-heading"><div><span className="eyebrow dark">{eyebrow}</span><h2>{title}</h2><p>{description}</p></div><strong>{rows.length}</strong></div>{controls}<div className="compact-list">{rows.length ? rows : <p className="muted">Bu bölümde henüz kayıt yok.</p>}</div>{children ? <OperationDisclosure title={operationTitle} description="Formu yalnızca yeni bir kayıt oluşturacağınız zaman açın.">{children}</OperationDisclosure> : null}</article>; }
function Text({ name, label, defaultValue, type = "text" }: { name: string; label: string; defaultValue?: string; type?: string }) { return <label className="field-label">{label}<input name={name} type={type} defaultValue={defaultValue}/></label>; }
function Select({ name, label, options }: { name: string; label: string; options: [string,string][] }) { return <label className="field-label">{label}<select name={name}><option value="">Bağlantı yok</option>{options.map(([value,text]) => <option key={value} value={value}>{text}</option>)}</select></label>; }
function Submit({ busy }: { busy: boolean }) { return <button className="primary-button" disabled={busy} type="submit"><Icon name="plus" size={17}/>{busy ? "Kaydediliyor…" : "Kaydet"}</button>; }
