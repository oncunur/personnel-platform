"use client";

import { FormEvent, useEffect, useMemo, useState } from "react";

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
  const [branches, setBranches] = useState<Branch[]>([]);
  const [departments, setDepartments] = useState<Department[]>([]);
  const [positions, setPositions] = useState<Position[]>([]);
  const [projects, setProjects] = useState<Project[]>([]);
  const [types, setTypes] = useState<EmployeeType[]>([]);
  const [employees, setEmployees] = useState<PagedEmployees>({ items: [], page: 1, pageSize: 25, totalCount: 0 });
  const [companyId, setCompanyId] = useState("");
  const [departmentId, setDepartmentId] = useState("");
  const [search, setSearch] = useState("");
  const [status, setStatus] = useState("");
  const [typeId, setTypeId] = useState("");
  const [projectId, setProjectId] = useState("");
  const [message, setMessage] = useState("Personel verileri yükleniyor…");
  const [busy, setBusy] = useState(false);

  const permissions = useMemo(() => new Set(me?.permissions.map((x) => x.code) ?? []), [me]);

  useEffect(() => { void initialize(); }, []);
  useEffect(() => { if (companyId) void loadOrganization(companyId); else { setBranches([]); setDepartments([]); setProjects([]); } }, [companyId]);
  useEffect(() => { if (departmentId) void loadPositions(departmentId); else setPositions([]); }, [departmentId]);

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

  async function loadOrganization(id: string) {
    const [branchRows, departmentRows, projectRows] = await Promise.all([
      json<Branch[]>(`/api/v1/organization/branches?companyId=${id}`),
      json<Department[]>(`/api/v1/organization/departments?companyId=${id}`),
      json<Project[]>(`/api/v1/organization/projects?companyId=${id}`),
    ]);
    setBranches(branchRows ?? []); setDepartments(departmentRows ?? []); setProjects(projectRows ?? []);
  }

  async function loadPositions(id: string) { setPositions((await json<Position[]>(`/api/v1/organization/positions?departmentId=${id}`)) ?? []); }

  async function loadEmployees(page = 1) {
    const params = new URLSearchParams({ page: String(page), pageSize: "25" });
    if (search.trim()) params.set("search", search.trim());
    if (companyId) params.set("companyId", companyId);
    if (departmentId) params.set("departmentId", departmentId);
    if (status) params.set("status", status);
    if (typeId) params.set("employeeTypeId", typeId);
    if (projectId) params.set("projectId", projectId);
    const rows = await json<PagedEmployees>(`/api/v1/personnel/employees?${params}`);
    if (rows) setEmployees(rows);
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
      event.currentTarget.reset(); setMessage("Personel oluşturuldu."); await loadEmployees();
      window.location.href = `/personnel/${created.id}`;
    } finally { setBusy(false); }
  }

  async function json<T>(path: string): Promise<T | null> {
    const response = await authFetch(path);
    if (!response?.ok) return null;
    return await response.json() as T;
  }

  async function authFetch(path: string, init?: RequestInit): Promise<Response | null> {
    let token = sessionStorage.getItem("pp_access_token") ?? await refresh();
    if (!token) return null;
    let response = await fetch(`${apiBase}${path}`, { ...init, headers: { ...(init?.headers ?? {}), Authorization: `Bearer ${token}` }, credentials: "include" });
    if (response.status !== 401) return response;
    token = await refresh(); if (!token) return response;
    return fetch(`${apiBase}${path}`, { ...init, headers: { ...(init?.headers ?? {}), Authorization: `Bearer ${token}` }, credentials: "include" });
  }

  async function refresh(): Promise<string | null> {
    try {
      const response = await fetch(`${apiBase}/api/v1/auth/refresh`, { method: "POST", credentials: "include" });
      if (!response.ok) return null;
      const body = await response.json() as AuthResponse; sessionStorage.setItem("pp_access_token", body.accessToken); sessionStorage.setItem("pp_access_token_expires_at", body.accessTokenExpiresAt); return body.accessToken;
    } catch { return null; }
  }

  const totalPages = Math.max(1, Math.ceil(employees.totalCount / employees.pageSize));

  return <main className="shell">
    <a className="back" href="/dashboard">← Dashboard</a>
    <section className="hero compact"><span className="eyebrow">SPRINT 2 · PERSONNEL CORE</span><h1>Personel</h1><p>{message}</p><div className="session-summary"><strong>{employees.totalCount} kayıt</strong><span>Sayfa {employees.page}/{totalPages}</span></div></section>

    <section className="panel organization-toolbar">
      <label>Ara<input value={search} onChange={(e) => setSearch(e.target.value)} placeholder="Sicil / ad / soyad"/></label>
      <label>Şirket<select value={companyId} onChange={(e) => { setCompanyId(e.target.value); setDepartmentId(""); }}><option value="">Tümü</option>{companies.map(x => <option key={x.id} value={x.id}>{x.code} · {x.name}</option>)}</select></label>
      <label>Departman<select value={departmentId} onChange={(e) => setDepartmentId(e.target.value)}><option value="">Tümü</option>{departments.map(x => <option key={x.id} value={x.id}>{x.name}</option>)}</select></label>
      <label>Tip<select value={typeId} onChange={(e) => setTypeId(e.target.value)}><option value="">Tümü</option>{types.map(x => <option key={x.id} value={x.id}>{x.name}</option>)}</select></label>
      <label>Proje<select value={projectId} onChange={(e) => setProjectId(e.target.value)}><option value="">Tümü</option>{projects.map(x => <option key={x.id} value={x.id}>{x.name}</option>)}</select></label>
      <label>Durum<select value={status} onChange={(e) => setStatus(e.target.value)}><option value="">Tümü</option><option value="ACTIVE">Aktif</option><option value="SUSPENDED">Askıda</option><option value="TERMINATED">Ayrıldı</option></select></label>
      <button className="primary-button" type="button" onClick={() => void loadEmployees(1)}>Filtrele</button>
    </section>

    <section className="panel audit-panel"><div className="table-wrap"><table className="data-table"><thead><tr><th>Sicil</th><th>Ad Soyad</th><th>Durum</th><th>İşe Giriş</th><th></th></tr></thead><tbody>{employees.items.map(x => <tr key={x.id}><td>{x.employeeNo}</td><td><strong>{x.firstName} {x.lastName}</strong></td><td><span className={`status-badge ${x.status === "ACTIVE" ? "success" : "danger"}`}>{x.status}</span></td><td>{x.hireDate}</td><td><a href={`/personnel/${x.id}`}>Personel 360 →</a></td></tr>)}</tbody></table></div><div className="action-row"><button className="table-button" disabled={employees.page <= 1} onClick={() => void loadEmployees(employees.page - 1)}>Önceki</button><button className="table-button" disabled={employees.page >= totalPages} onClick={() => void loadEmployees(employees.page + 1)}>Sonraki</button></div></section>

    {permissions.has("personnel.create") ? <section className="panel org-create"><div className="panel-heading"><h2>Yeni Personel</h2></div><form className="inline-form" onSubmit={onCreate}>
      <label className="field-label">Şirket<select name="companyId" required onChange={(e) => { setCompanyId(e.target.value); setDepartmentId(""); }}><option value="">Seçin</option>{companies.map(x => <option key={x.id} value={x.id}>{x.code} · {x.name}</option>)}</select></label>
      <label className="field-label">Şube<select name="branchId"><option value="">—</option>{branches.map(x => <option key={x.id} value={x.id}>{x.name}</option>)}</select></label>
      <label className="field-label">Departman<select name="departmentId" required onChange={(e) => setDepartmentId(e.target.value)}><option value="">Seçin</option>{departments.map(x => <option key={x.id} value={x.id}>{x.name}</option>)}</select></label>
      <label className="field-label">Pozisyon<select name="positionId" required><option value="">Seçin</option>{positions.map(x => <option key={x.id} value={x.id}>{x.name}</option>)}</select></label>
      <label className="field-label">Personel Tipi<select name="employeeTypeId" required><option value="">Seçin</option>{types.map(x => <option key={x.id} value={x.id}>{x.name}</option>)}</select></label>
      <Field name="employeeNo" label="Sicil"/><Field name="firstName" label="Ad"/><Field name="lastName" label="Soyad"/><Field name="preferredName" label="Tercih edilen ad"/><Field name="birthDate" label="Doğum" type="date"/><Field name="hireDate" label="İşe giriş" type="date"/><Field name="phone" label="Telefon"/><Field name="email" label="E-posta" type="email"/>
      <button className="primary-button" disabled={busy} type="submit">{busy ? "Kaydediliyor…" : "Personel Oluştur"}</button>
    </form></section> : null}
  </main>;
}

function Field({ name, label, type = "text" }: { name: string; label: string; type?: string }) { return <label className="field-label">{label}<input name={name} type={type} required={["employeeNo","firstName","lastName","hireDate"].includes(name)}/></label>; }
