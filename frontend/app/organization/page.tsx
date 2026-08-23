"use client";

import { FormEvent, useEffect, useMemo, useState } from "react";

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
    event.preventDefault(); const fd = new FormData(event.currentTarget);
    const row = await createEntity<Company>("/api/v1/organization/companies", { code: fd.get("code"), name: fd.get("name"), defaultCurrency: fd.get("currency") || "TRY" });
    if (row) { setCompanies((current) => [...current, row].sort((a,b) => a.code.localeCompare(b.code))); setSelectedCompanyId(row.id); event.currentTarget.reset(); }
  }

  async function onBranchSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); if (!selectedCompanyId) return; const fd = new FormData(event.currentTarget);
    const row = await createEntity<Branch>("/api/v1/organization/branches", { companyId: selectedCompanyId, code: fd.get("code"), name: fd.get("name"), location: fd.get("location") || null });
    if (row) { setBranches((current) => [...current, row]); event.currentTarget.reset(); }
  }

  async function onDepartmentSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); if (!selectedCompanyId) return; const fd = new FormData(event.currentTarget);
    const branchId = String(fd.get("branchId") ?? ""); const parentDepartmentId = String(fd.get("parentId") ?? "");
    const row = await createEntity<Department>("/api/v1/organization/departments", { companyId: selectedCompanyId, branchId: branchId || null, parentDepartmentId: parentDepartmentId || null, code: fd.get("code"), name: fd.get("name") });
    if (row) { setDepartments((current) => [...current, row]); setSelectedDepartmentId(row.id); event.currentTarget.reset(); }
  }

  async function onPositionSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); if (!selectedDepartmentId) return; const fd = new FormData(event.currentTarget);
    const row = await createEntity<Position>("/api/v1/organization/positions", { departmentId: selectedDepartmentId, code: fd.get("code"), name: fd.get("name") });
    if (row) { setPositions((current) => [...current, row]); event.currentTarget.reset(); }
  }

  async function onProjectSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); if (!selectedCompanyId) return; const fd = new FormData(event.currentTarget);
    const row = await createEntity<Project>("/api/v1/organization/projects", { companyId: selectedCompanyId, code: fd.get("code"), name: fd.get("name"), location: fd.get("location") || null, countryCode: fd.get("countryCode") || null, startDate: fd.get("startDate") || null, plannedEndDate: fd.get("plannedEndDate") || null });
    if (row) { setProjects((current) => [...current, row]); event.currentTarget.reset(); }
  }

  async function onCostCenterSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); if (!selectedCompanyId) return; const fd = new FormData(event.currentTarget); const projectId = String(fd.get("projectId") ?? ""); const parentId = String(fd.get("parentId") ?? "");
    const row = await createEntity<CostCenter>("/api/v1/organization/cost-centers", { companyId: selectedCompanyId, projectId: projectId || null, parentCostCenterId: parentId || null, code: fd.get("code"), name: fd.get("name") });
    if (row) { setCostCenters((current) => [...current, row]); event.currentTarget.reset(); }
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

  return (
    <main className="shell">
      <a className="back" href="/dashboard">← Dashboard</a>
      <section className="hero compact">
        <span className="eyebrow">SPRINT 2 · ORGANIZATION CORE</span><h1>Organizasyon Yönetimi</h1><p>{message}</p>
        <div className="session-summary"><strong>{selectedCompany?.name ?? "Şirket seçilmedi"}</strong><span>{selectedCompany?.code ?? "—"}</span><span>{selectedCompany?.defaultCurrency ?? "—"}</span></div>
      </section>

      <section className="panel organization-toolbar">
        <label>Aktif şirket<select value={selectedCompanyId} onChange={(e) => setSelectedCompanyId(e.target.value)}><option value="">Şirket seçin</option>{companies.map((x) => <option key={x.id} value={x.id}>{x.code} · {x.name}</option>)}</select></label>
        <div className="org-kpis"><span><strong>{branches.length}</strong> Şube</span><span><strong>{departments.length}</strong> Departman</span><span><strong>{projects.length}</strong> Proje</span><span><strong>{costCenters.length}</strong> Cost Center</span></div>
      </section>

      {permissions.has("organization.company.manage") ? <OrgForm title="Yeni Şirket" onSubmit={onCompanySubmit} busy={busy}><Text name="code" label="Kod"/><Text name="name" label="Şirket adı"/><Text name="currency" label="Para birimi" defaultValue="TRY"/></OrgForm> : null}

      {selectedCompanyId ? <section className="organization-grid">
        <OrgSection title="Şubeler" rows={branches.map((x) => `${x.code} · ${x.name}${x.location ? ` · ${x.location}` : ""}`)}>
          {permissions.has("organization.branch.manage") ? <form className="inline-form" onSubmit={onBranchSubmit}><Text name="code" label="Kod"/><Text name="name" label="Ad"/><Text name="location" label="Lokasyon"/><Submit busy={busy}/></form> : null}
        </OrgSection>

        <OrgSection title="Departmanlar" rows={departments.map((x) => `${x.code} · ${x.name}`)}>
          {permissions.has("organization.department.manage") ? <form className="inline-form" onSubmit={onDepartmentSubmit}><Text name="code" label="Kod"/><Text name="name" label="Ad"/><Select name="branchId" label="Şube" options={branches.map(x => [x.id, `${x.code} · ${x.name}`])}/><Select name="parentId" label="Üst departman" options={departments.map(x => [x.id, `${x.code} · ${x.name}`])}/><Submit busy={busy}/></form> : null}
        </OrgSection>

        <OrgSection title="Pozisyonlar" rows={positions.map((x) => `${x.code} · ${x.name}`)}>
          <label className="field-label">Departman<select value={selectedDepartmentId} onChange={(e) => setSelectedDepartmentId(e.target.value)}><option value="">Seçin</option>{departments.map(x => <option key={x.id} value={x.id}>{x.code} · {x.name}</option>)}</select></label>
          {selectedDepartmentId && permissions.has("organization.position.manage") ? <form className="inline-form" onSubmit={onPositionSubmit}><Text name="code" label="Kod"/><Text name="name" label="Ad"/><Submit busy={busy}/></form> : null}
        </OrgSection>

        <OrgSection title="Projeler" rows={projects.map((x) => `${x.code} · ${x.name} · ${x.status}`)}>
          {permissions.has("organization.project.manage") ? <form className="inline-form" onSubmit={onProjectSubmit}><Text name="code" label="Kod"/><Text name="name" label="Ad"/><Text name="location" label="Lokasyon"/><Text name="countryCode" label="Ülke"/><Text name="startDate" label="Başlangıç" type="date"/><Text name="plannedEndDate" label="Planlanan bitiş" type="date"/><Submit busy={busy}/></form> : null}
        </OrgSection>

        <OrgSection title="Cost Center" rows={costCenters.map((x) => `${x.code} · ${x.name}`)}>
          {permissions.has("organization.costcenter.manage") ? <form className="inline-form" onSubmit={onCostCenterSubmit}><Text name="code" label="Kod"/><Text name="name" label="Ad"/><Select name="projectId" label="Proje" options={projects.map(x => [x.id, `${x.code} · ${x.name}`])}/><Select name="parentId" label="Üst cost center" options={costCenters.map(x => [x.id, `${x.code} · ${x.name}`])}/><Submit busy={busy}/></form> : null}
        </OrgSection>
      </section> : null}
    </main>
  );
}

function OrgForm({ title, onSubmit, busy, children }: { title: string; onSubmit: (e: FormEvent<HTMLFormElement>) => void; busy: boolean; children: React.ReactNode }) { return <section className="panel org-create"><div className="panel-heading"><h2>{title}</h2></div><form className="inline-form" onSubmit={onSubmit}>{children}<Submit busy={busy}/></form></section>; }
function OrgSection({ title, rows, children }: { title: string; rows: string[]; children?: React.ReactNode }) { return <article className="panel"><div className="panel-heading"><h2>{title}</h2><strong>{rows.length}</strong></div>{children}<div className="compact-list">{rows.length ? rows.map((row, i) => <div key={`${row}-${i}`}>{row}</div>) : <p className="muted">Henüz kayıt yok.</p>}</div></article>; }
function Text({ name, label, defaultValue, type = "text" }: { name: string; label: string; defaultValue?: string; type?: string }) { return <label className="field-label">{label}<input name={name} type={type} defaultValue={defaultValue}/></label>; }
function Select({ name, label, options }: { name: string; label: string; options: [string,string][] }) { return <label className="field-label">{label}<select name={name}><option value="">—</option>{options.map(([value,text]) => <option key={value} value={value}>{text}</option>)}</select></label>; }
function Submit({ busy }: { busy: boolean }) { return <button className="primary-button" disabled={busy} type="submit">{busy ? "Kaydediliyor…" : "Kaydet"}</button>; }
