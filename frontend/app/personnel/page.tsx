"use client";

import { FormEvent, useEffect, useMemo, useState } from "react";
import { Icon } from "../components/Icon";
import { PageHeader } from "../components/PageHeader";

const apiBase = process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:8080";
type Permission = { code: string };
type Me = { userId: string; username: string; permissions: Permission[] };
type Company = { id: string; code: string; name: string };
type Branch = { id: string; code: string; name: string };
type Department = { id: string; code: string; name: string };
type Position = { id: string; code: string; name: string };
type Project = { id: string; code: string; name: string };
type EmployeeType = { id: string; code: string; name: string };
type Employee = { id: string; employeeNo: string; firstName: string; lastName: string; status: string; companyId: string; branchId: string | null; departmentId: string; positionId: string; employeeTypeId: string; hireDate: string; version: number };
type PagedEmployees = { items: Employee[]; page: number; pageSize: number; totalCount: number };
type AuthResponse = { accessToken: string; accessTokenExpiresAt: string };

export default function PersonnelPage() {
  const [me, setMe] = useState<Me | null>(null);
  const [companies, setCompanies] = useState<Company[]>([]);
  const [filterDepartments, setFilterDepartments] = useState<Department[]>([]);
  const [filterProjects, setFilterProjects] = useState<Project[]>([]);
  const [createBranches, setCreateBranches] = useState<Branch[]>([]);
  const [createDepartments, setCreateDepartments] = useState<Department[]>([]);
  const [createPositions, setCreatePositions] = useState<Position[]>([]);
  const [types, setTypes] = useState<EmployeeType[]>([]);
  const [employees, setEmployees] = useState<PagedEmployees>({ items: [], page: 1, pageSize: 25, totalCount: 0 });
  const [companyId, setCompanyId] = useState("");
  const [departmentId, setDepartmentId] = useState("");
  const [search, setSearch] = useState("");
  const [status, setStatus] = useState("");
  const [typeId, setTypeId] = useState("");
  const [projectId, setProjectId] = useState("");
  const [createCompanyId, setCreateCompanyId] = useState("");
  const [createDepartmentId, setCreateDepartmentId] = useState("");
  const [showCreate, setShowCreate] = useState(false);
  const [showAdvancedFilters, setShowAdvancedFilters] = useState(false);
  const [message, setMessage] = useState("Personel verileri yükleniyor…");
  const [busy, setBusy] = useState(false);

  const permissions = useMemo(() => new Set(me?.permissions.map((item) => item.code) ?? []), [me]);
  useEffect(() => { void initialize(); }, []);
  useEffect(() => {
    if (companyId) void loadFilterOrganization(companyId);
    else { setFilterDepartments([]); setFilterProjects([]); }
  }, [companyId]);

  async function initialize() {
    const current = await json<Me>("/api/v1/auth/me");
    if (!current) { window.location.replace("/login"); return; }
    setMe(current);
    const [companyRows, typeRows] = await Promise.all([
      json<Company[]>("/api/v1/organization/companies"),
      json<EmployeeType[]>("/api/v1/personnel/employee-types"),
    ]);
    setCompanies(companyRows ?? []);
    setTypes(typeRows ?? []);
    setMessage("Personel listesi hazır.");
    await loadEmployees();
  }

  async function loadFilterOrganization(id: string) {
    const [departmentRows, projectRows] = await Promise.all([
      json<Department[]>(`/api/v1/organization/departments?companyId=${id}`),
      json<Project[]>(`/api/v1/organization/projects?companyId=${id}`),
    ]);
    setFilterDepartments(departmentRows ?? []);
    setFilterProjects(projectRows ?? []);
  }

  async function loadCreateOrganization(id: string) {
    setCreateCompanyId(id); setCreateDepartmentId(""); setCreatePositions([]);
    if (!id) { setCreateBranches([]); setCreateDepartments([]); return; }
    const [branchRows, departmentRows] = await Promise.all([
      json<Branch[]>(`/api/v1/organization/branches?companyId=${id}`),
      json<Department[]>(`/api/v1/organization/departments?companyId=${id}`),
    ]);
    setCreateBranches(branchRows ?? []); setCreateDepartments(departmentRows ?? []);
  }

  async function loadCreatePositions(id: string) {
    setCreateDepartmentId(id);
    setCreatePositions(id ? (await json<Position[]>(`/api/v1/organization/positions?departmentId=${id}`)) ?? [] : []);
  }

  async function loadEmployees(page = 1, reset = false) {
    const params = new URLSearchParams({ page: String(page), pageSize: "25" });
    if (!reset) {
      if (search.trim()) params.set("search", search.trim());
      if (companyId) params.set("companyId", companyId);
      if (departmentId) params.set("departmentId", departmentId);
      if (status) params.set("status", status);
      if (typeId) params.set("employeeTypeId", typeId);
      if (projectId) params.set("projectId", projectId);
    }
    const rows = await json<PagedEmployees>(`/api/v1/personnel/employees?${params}`);
    if (rows) { setEmployees(rows); setMessage(`${rows.totalCount} personel kaydı listelendi.`); }
  }

  function clearFilters() {
    setSearch(""); setCompanyId(""); setDepartmentId(""); setStatus(""); setTypeId(""); setProjectId("");
    setFilterDepartments([]); setFilterProjects([]); setShowAdvancedFilters(false); void loadEmployees(1, true);
  }

  async function onCreate(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); setBusy(true);
    try {
      const fd = new FormData(event.currentTarget);
      const body = {
        companyId: fd.get("companyId"), branchId: fd.get("branchId") || null, departmentId: fd.get("departmentId"), positionId: fd.get("positionId"), employeeTypeId: fd.get("employeeTypeId"), managerEmployeeId: null,
        employeeNo: fd.get("employeeNo"), firstName: fd.get("firstName"), lastName: fd.get("lastName"), preferredName: fd.get("preferredName") || null,
        birthDate: fd.get("birthDate") || null, phone: fd.get("phone") || null, email: fd.get("email") || null, hireDate: fd.get("hireDate"), notes: null,
      };
      const response = await authFetch("/api/v1/personnel/employees", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(body) });
      if (!response?.ok) {
        const error = response ? await response.json().catch(() => null) as { error?: { message?: string } } | null : null;
        setMessage(error?.error?.message ?? "Personel oluşturulamadı."); return;
      }
      const created = await response.json() as { id: string };
      event.currentTarget.reset(); setCreateCompanyId(""); setCreateDepartmentId(""); setShowCreate(false);
      setMessage("Personel başarıyla oluşturuldu."); await loadEmployees();
      window.location.href = `/personnel/${created.id}`;
    } finally { setBusy(false); }
  }

  async function json<T>(path: string): Promise<T | null> {
    const response = await authFetch(path); if (!response?.ok) return null; return await response.json() as T;
  }
  async function authFetch(path: string, init?: RequestInit): Promise<Response | null> {
    let token = sessionStorage.getItem("pp_access_token") ?? await refresh(); if (!token) return null;
    let response = await fetch(`${apiBase}${path}`, { ...init, headers: { ...(init?.headers ?? {}), Authorization: `Bearer ${token}` }, credentials: "include" });
    if (response.status !== 401) return response;
    token = await refresh(); if (!token) return response;
    return fetch(`${apiBase}${path}`, { ...init, headers: { ...(init?.headers ?? {}), Authorization: `Bearer ${token}` }, credentials: "include" });
  }
  async function refresh(): Promise<string | null> {
    try {
      const response = await fetch(`${apiBase}/api/v1/auth/refresh`, { method: "POST", credentials: "include" }); if (!response.ok) return null;
      const body = await response.json() as AuthResponse; sessionStorage.setItem("pp_access_token", body.accessToken); sessionStorage.setItem("pp_access_token_expires_at", body.accessTokenExpiresAt); return body.accessToken;
    } catch { return null; }
  }

  const totalPages = Math.max(1, Math.ceil(employees.totalCount / employees.pageSize));
  const filterCount = [search.trim(), companyId, departmentId, status, typeId, projectId].filter(Boolean).length;
  const advancedFilterCount = [companyId, departmentId, status, typeId, projectId].filter(Boolean).length;

  return <main className="page-shell">
    <PageHeader
      eyebrow="İnsan Kaynakları"
      title="Personel Yönetimi"
      description="Personel kayıtlarını bulun, filtreleyin ve tüm özlük süreçlerine Personel 360 üzerinden ulaşın."
      status={message}
      actions={permissions.has("personnel.create") ? <button className="primary-button" type="button" aria-expanded={showCreate} aria-controls="personnel-create-form" onClick={() => setShowCreate((value) => !value)}><Icon name={showCreate ? "close" : "plus"} size={17}/>{showCreate ? "Formu kapat" : "Yeni personel"}</button> : null}
    />

    {showCreate && permissions.has("personnel.create") ? <section className="panel org-create" id="personnel-create-form">
      <div className="panel-heading"><div><span className="page-eyebrow">Yeni kayıt</span><h2>Personel bilgileri</h2><p>Zorunlu organizasyon ve kimlik alanlarını doldurun.</p></div></div>
      <form className="record-form" onSubmit={onCreate}>
        <fieldset className="form-section">
          <legend>Organizasyon ve işe giriş</legend>
          <p className="form-section-description">Personelin çalışma kapsamını ve göreve başlangıcını belirleyin.</p>
          <div className="form-grid">
            <label className="field-label">Şirket<select name="companyId" required value={createCompanyId} onChange={(event) => void loadCreateOrganization(event.target.value)}><option value="">Şirket seçin</option>{companies.map((item) => <option key={item.id} value={item.id}>{item.code} · {item.name}</option>)}</select></label>
            <label className="field-label">Şube<select name="branchId"><option value="">Şube yok</option>{createBranches.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}</select></label>
            <label className="field-label">Departman<select name="departmentId" required value={createDepartmentId} onChange={(event) => void loadCreatePositions(event.target.value)}><option value="">Departman seçin</option>{createDepartments.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}</select></label>
            <label className="field-label">Pozisyon<select name="positionId" required><option value="">Pozisyon seçin</option>{createPositions.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}</select></label>
            <label className="field-label">Personel tipi<select name="employeeTypeId" required><option value="">Personel tipi seçin</option>{types.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}</select></label>
            <Field name="hireDate" label="İşe giriş" type="date"/>
          </div>
        </fieldset>
        <fieldset className="form-section">
          <legend>Kimlik bilgileri</legend>
          <p className="form-section-description">Personel kartında ve resmi kayıtlarda kullanılacak temel bilgiler.</p>
          <div className="form-grid">
            <Field name="employeeNo" label="Sicil numarası"/><Field name="firstName" label="Ad"/><Field name="lastName" label="Soyad"/><Field name="preferredName" label="Tercih edilen ad"/><Field name="birthDate" label="Doğum tarihi" type="date"/>
          </div>
        </fieldset>
        <fieldset className="form-section">
          <legend>İletişim bilgileri</legend>
          <p className="form-section-description">İsteğe bağlı iletişim alanlarını daha sonra Personel 360 üzerinden güncelleyebilirsiniz.</p>
          <div className="form-grid form-grid-compact"><Field name="phone" label="Telefon"/><Field name="email" label="E-posta" type="email"/></div>
        </fieldset>
        <div className="form-actions"><span>Zorunlu alanlar boş bırakılamaz.</span><button className="primary-button" disabled={busy} type="submit">{busy ? "Kaydediliyor…" : "Personeli kaydet"}</button></div>
      </form>
    </section> : null}

    <section className="panel filter-panel">
      <div className="panel-heading"><div><span className="page-eyebrow">Arama ve filtreler</span><h2>Aradığınız kişiyi bulun</h2><p>Önce ad veya sicil ile arayın; gerekirse ayrıntılı filtreleri açın.</p></div>{filterCount ? <strong>{filterCount}</strong> : null}</div>
      <div className="filter-toolbar">
        <label className="field-label filter-search">Personel ara<Icon name="search" size={17}/><input value={search} onChange={(event) => setSearch(event.target.value)} onKeyDown={(event) => { if (event.key === "Enter") void loadEmployees(1); }} placeholder="Sicil, ad veya soyad"/></label>
        <div className="filter-toolbar-actions"><button className="secondary-button" type="button" aria-expanded={showAdvancedFilters} aria-controls="personnel-advanced-filters" onClick={() => setShowAdvancedFilters((value) => !value)}>Ayrıntılı filtreler{advancedFilterCount ? ` (${advancedFilterCount})` : ""}</button><button className="primary-button" type="button" onClick={() => void loadEmployees(1)}><Icon name="search" size={16}/>Sonuçları getir</button></div>
      </div>
      {showAdvancedFilters ? <div className="filter-details" id="personnel-advanced-filters">
        <div className="filter-grid">
          <label className="field-label">Şirket<select value={companyId} onChange={(event) => { setCompanyId(event.target.value); setDepartmentId(""); }}><option value="">Tüm şirketler</option>{companies.map((item) => <option key={item.id} value={item.id}>{item.code} · {item.name}</option>)}</select></label>
          <label className="field-label">Departman<select value={departmentId} onChange={(event) => setDepartmentId(event.target.value)}><option value="">Tüm departmanlar</option>{filterDepartments.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}</select></label>
          <label className="field-label">Personel tipi<select value={typeId} onChange={(event) => setTypeId(event.target.value)}><option value="">Tüm tipler</option>{types.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}</select></label>
          <label className="field-label">Proje<select value={projectId} onChange={(event) => setProjectId(event.target.value)}><option value="">Tüm projeler</option>{filterProjects.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}</select></label>
          <label className="field-label">Durum<select value={status} onChange={(event) => setStatus(event.target.value)}><option value="">Tüm durumlar</option><option value="ACTIVE">Aktif</option><option value="SUSPENDED">Askıda</option><option value="TERMINATED">Ayrıldı</option></select></label>
        </div>
        <div className="filter-actions"><button className="secondary-button" type="button" disabled={!filterCount} onClick={clearFilters}>Tümünü temizle</button></div>
      </div> : null}
    </section>

    <section className="panel">
      <div className="panel-heading"><div><span className="page-eyebrow">Personel listesi</span><h2>Kayıtlar</h2><p>{employees.totalCount} sonuçtan sayfa başına {employees.pageSize} kayıt gösteriliyor.</p></div><strong>{employees.totalCount}</strong></div>
      <div className="table-wrap responsive-table-wrap" role="region" aria-label="Personel kayıtları" tabIndex={0}><table className="data-table responsive-table"><thead><tr><th>Personel</th><th>Sicil</th><th>Durum</th><th>İşe giriş</th><th>İşlem</th></tr></thead><tbody>
        {employees.items.map((employee) => <tr key={employee.id}><td data-label="Personel"><div className="person-cell"><span className="person-avatar">{initials(employee)}</span><span className="person-cell-copy"><strong>{employee.firstName} {employee.lastName}</strong><small>Personel kaydı</small></span></div></td><td data-label="Sicil">{employee.employeeNo}</td><td data-label="Durum"><span className={`status-badge ${employee.status === "ACTIVE" ? "success" : employee.status === "SUSPENDED" ? "warning" : "danger"}`}>{statusLabel(employee.status)}</span></td><td data-label="İşe giriş">{formatDate(employee.hireDate)}</td><td data-label="İşlem"><a href={`/personnel/${employee.id}`}>Personel 360 <Icon name="arrow" size={14}/></a></td></tr>)}
        {employees.items.length === 0 ? <tr><td className="empty-row" colSpan={5}>Filtrelere uygun personel kaydı bulunamadı.</td></tr> : null}
      </tbody></table></div>
      <div className="table-footer"><span>Sayfa {employees.page} / {totalPages}</span><div className="action-row"><button className="table-button" disabled={employees.page <= 1} onClick={() => void loadEmployees(employees.page - 1)}>Önceki</button><button className="table-button" disabled={employees.page >= totalPages} onClick={() => void loadEmployees(employees.page + 1)}>Sonraki</button></div></div>
    </section>
  </main>;
}

function Field({ name, label, type = "text" }: { name: string; label: string; type?: string }) {
  return <label className="field-label">{label}<input name={name} type={type} required={["employeeNo", "firstName", "lastName", "hireDate"].includes(name)}/></label>;
}
function initials(employee: Employee) { return `${employee.firstName[0] ?? ""}${employee.lastName[0] ?? ""}`.toLocaleUpperCase("tr-TR"); }
function statusLabel(status: string) { return status === "ACTIVE" ? "Aktif" : status === "SUSPENDED" ? "Askıda" : status === "TERMINATED" ? "Ayrıldı" : status; }
function formatDate(value: string) { return new Intl.DateTimeFormat("tr-TR", { day: "2-digit", month: "short", year: "numeric" }).format(new Date(`${value}T00:00:00`)); }
