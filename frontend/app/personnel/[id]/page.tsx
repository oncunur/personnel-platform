"use client";

import { FormEvent, useEffect, useMemo, useState } from "react";
import { useParams } from "next/navigation";

const apiBase = process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:8080";

type Permission = { code: string };
type Me = { permissions: Permission[] };
type Employee = { id: string; employeeNo: string; firstName: string; lastName: string; preferredName: string | null; birthDate: string | null; phone: string | null; email: string | null; status: string; companyId: string; branchId: string | null; departmentId: string; positionId: string; employeeTypeId: string; managerEmployeeId: string | null; hireDate: string; terminationDate: string | null; notes: string | null; version: number };
type Assignment = { id: string; projectId: string; costCenterId: string | null; validFrom: string; validUntil: string | null; allocationPercent: number; status: string };
type Named = { id: string; code: string; name: string };
type AuthResponse = { accessToken: string; accessTokenExpiresAt: string };

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
  const [message, setMessage] = useState("Personel 360 yükleniyor…");
  const [busy, setBusy] = useState(false);

  const permissions = useMemo(() => new Set(me?.permissions.map(x => x.code) ?? []), [me]);
  const lookup = (rows: Named[], id: string | null | undefined) => rows.find(x => x.id === id)?.name ?? "—";

  useEffect(() => { void initialize(); }, [employeeId]);

  async function initialize() {
    const [current, person, typeRows, companyRows] = await Promise.all([
      json<Me>("/api/v1/auth/me"), json<Employee>(`/api/v1/personnel/employees/${employeeId}`), json<Named[]>("/api/v1/personnel/employee-types"), json<Named[]>("/api/v1/organization/companies"),
    ]);
    if (!current || !person) { setMessage("Personel bulunamadı veya erişim yok."); return; }
    setMe(current); setEmployee(person); setTypes(typeRows ?? []); setCompanies(companyRows ?? []);
    const [branchRows, departmentRows, projectRows, costRows] = await Promise.all([
      json<Named[]>(`/api/v1/organization/branches?companyId=${person.companyId}`), json<Named[]>(`/api/v1/organization/departments?companyId=${person.companyId}`), json<Named[]>(`/api/v1/organization/projects?companyId=${person.companyId}`), json<Named[]>(`/api/v1/organization/cost-centers?companyId=${person.companyId}`),
    ]);
    setBranches(branchRows ?? []); setDepartments(departmentRows ?? []); setProjects(projectRows ?? []); setCostCenters(costRows ?? []);
    setPositions((await json<Named[]>(`/api/v1/organization/positions?departmentId=${person.departmentId}`)) ?? []);
    if (current.permissions.some(x => x.code === "personnel.project.view")) setAssignments((await json<Assignment[]>(`/api/v1/personnel/employees/${employeeId}/project-assignments`)) ?? []);
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
      if (!response?.ok) {
        const error = response ? await response.json().catch(() => null) as { error?: { message?: string } } | null : null; setMessage(error?.error?.message ?? "Proje atanamadı."); return;
      }
      const row = await response.json() as Assignment; setAssignments(current => [row, ...current]); event.currentTarget.reset(); setMessage("Proje ataması oluşturuldu.");
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

  if (!employee) return <main className="shell"><a className="back" href="/personnel">← Personel</a><section className="panel"><p>{message}</p></section></main>;

  return <main className="shell">
    <a className="back" href="/personnel">← Personel Listesi</a>
    <section className="hero compact"><span className="eyebrow">PERSONEL 360</span><h1>{employee.firstName} {employee.lastName}</h1><p>{message}</p><div className="session-summary"><strong>{employee.employeeNo}</strong><span>{employee.status}</span><span>{lookup(companies, employee.companyId)}</span><span>{lookup(departments, employee.departmentId)}</span><span>{lookup(positions, employee.positionId)}</span></div><div className="actions action-row">{permissions.has("personnel.update") ? <button className="secondary-button" disabled={busy} onClick={() => void changeStatus(employee.status !== "ACTIVE")}>{employee.status === "ACTIVE" ? "Askıya al" : "Aktifleştir"}</button> : null}</div></section>

    <section className="security-grid">
      <article className="panel"><div className="panel-heading"><div><span className="eyebrow dark">GENEL</span><h2>Personel Bilgileri</h2></div><strong>v{employee.version}</strong></div><div className="detail-grid"><Item label="Ad Soyad" value={`${employee.firstName} ${employee.lastName}`}/><Item label="Tercih Edilen Ad" value={employee.preferredName}/><Item label="Personel Tipi" value={lookup(types, employee.employeeTypeId)}/><Item label="İşe Giriş" value={employee.hireDate}/><Item label="Telefon" value={employee.phone}/><Item label="E-posta" value={employee.email}/><Item label="Doğum Tarihi" value={employee.birthDate}/></div></article>
      <article className="panel"><div className="panel-heading"><div><span className="eyebrow dark">İŞ / ORGANİZASYON</span><h2>Organizasyon</h2></div></div><div className="detail-grid"><Item label="Şirket" value={lookup(companies, employee.companyId)}/><Item label="Şube" value={lookup(branches, employee.branchId)}/><Item label="Departman" value={lookup(departments, employee.departmentId)}/><Item label="Pozisyon" value={lookup(positions, employee.positionId)}/></div></article>
    </section>

    {permissions.has("personnel.project.view") ? <section className="panel audit-panel"><div className="panel-heading"><div><span className="eyebrow dark">PROJE</span><h2>Proje Atamaları</h2></div><strong>{assignments.length}</strong></div>{permissions.has("personnel.project.assign") ? <form className="inline-form" onSubmit={assignProject}><label className="field-label">Proje<select name="projectId" required><option value="">Seçin</option>{projects.map(x => <option key={x.id} value={x.id}>{x.code} · {x.name}</option>)}</select></label><label className="field-label">Cost Center<select name="costCenterId"><option value="">—</option>{costCenters.map(x => <option key={x.id} value={x.id}>{x.code} · {x.name}</option>)}</select></label><Field name="validFrom" label="Başlangıç" type="date"/><Field name="validUntil" label="Bitiş" type="date"/><Field name="allocationPercent" label="Allocation %" type="number"/><button className="primary-button" disabled={busy}>Ata</button></form> : null}<div className="table-wrap"><table className="data-table"><thead><tr><th>Proje</th><th>Cost Center</th><th>Tarih</th><th>Allocation</th><th>Durum</th></tr></thead><tbody>{assignments.map(x => <tr key={x.id}><td>{lookup(projects, x.projectId)}</td><td>{lookup(costCenters, x.costCenterId)}</td><td>{x.validFrom} → {x.validUntil ?? "Devam"}</td><td>%{x.allocationPercent}</td><td>{x.status}</td></tr>)}</tbody></table></div></section> : null}

    <section className="grid"><article className="card"><span>Yakında</span><h2>Özlük</h2></article><article className="card"><span>Yakında</span><h2>İzin</h2></article><article className="card"><span>Yakında</span><h2>Puantaj</h2></article><article className="card"><span>Yakında</span><h2>Kamp & Yemek</h2></article><article className="card"><span>Yakında</span><h2>Bordro</h2></article></section>
  </main>;
}

function Item({ label, value }: { label: string; value?: string | null }) { return <div className="detail-item"><small>{label}</small><strong>{value || "—"}</strong></div>; }
function Field({ name, label, type = "text" }: { name: string; label: string; type?: string }) { return <label className="field-label">{label}<input name={name} type={type} required={name === "validFrom" || name === "allocationPercent"} min={name === "allocationPercent" ? 1 : undefined} max={name === "allocationPercent" ? 100 : undefined}/></label>; }
