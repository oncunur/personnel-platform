"use client";

import { FormEvent, useEffect, useMemo, useState } from "react";

const apiBase = process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:8080";

type Permission = { code: string };
type Me = { permissions: Permission[] };
type AuthResponse = { accessToken: string; accessTokenExpiresAt: string };
type EmployeeType = { id: string; code: string; name: string };
type DocumentType = {
  id: string; code: string; name: string; description: string | null; requiredByDefault: boolean;
  expirationRequired: boolean; defaultValidityDays: number | null; fileRequired: boolean;
  documentNumberRequired: boolean; multipleAllowed: boolean; reminderDays: number[];
  isActive: boolean; displayOrder: number; requiredEmployeeTypeIds: string[];
};
type Attention = {
  documentId: string; employeeId: string; companyId: string; employeeNo: string; employeeName: string;
  documentTypeCode: string; documentTypeName: string; validUntil: string; status: string; daysRemaining: number;
};
type Missing = { employeeId: string; companyId: string; employeeNo: string; employeeName: string; code: string; name: string };
type DashboardList<T> = { items: T[]; totalCount: number };

export default function DocumentsPage() {
  const [me, setMe] = useState<Me | null>(null);
  const [types, setTypes] = useState<DocumentType[]>([]);
  const [employeeTypes, setEmployeeTypes] = useState<EmployeeType[]>([]);
  const [missing, setMissing] = useState<Missing[]>([]);
  const [expiring, setExpiring] = useState<Attention[]>([]);
  const [expired, setExpired] = useState<Attention[]>([]);
  const [message, setMessage] = useState("Belge merkezi yükleniyor…");
  const [busy, setBusy] = useState(false);

  const permissions = useMemo(() => new Set(me?.permissions.map(x => x.code) ?? []), [me]);

  useEffect(() => { void initialize(); }, []);

  async function initialize() {
    const current = await json<Me>("/api/v1/auth/me");
    if (!current) { window.location.replace("/login"); return; }
    setMe(current);
    const codes = new Set(current.permissions.map(x => x.code));

    const [typeRows, employeeTypeRows, missingRows, expiringRows, expiredRows] = await Promise.all([
      codes.has("documents.type.view") ? json<DocumentType[]>("/api/v1/documents/types") : Promise.resolve(null),
      codes.has("personnel.view") ? json<EmployeeType[]>("/api/v1/personnel/employee-types") : Promise.resolve(null),
      codes.has("documents.missing.view") ? json<DashboardList<Missing>>("/api/v1/documents/missing?limit=200") : Promise.resolve(null),
      codes.has("documents.expiring.view") ? json<DashboardList<Attention>>("/api/v1/documents/expiring?days=30&limit=200") : Promise.resolve(null),
      codes.has("documents.expiring.view") ? json<DashboardList<Attention>>("/api/v1/documents/expired?limit=200") : Promise.resolve(null),
    ]);

    setTypes(typeRows ?? []);
    setEmployeeTypes(employeeTypeRows ?? []);
    setMissing(missingRows?.items ?? []);
    setExpiring(expiringRows?.items ?? []);
    setExpired(expiredRows?.items ?? []);
    setMessage("Belge merkezi güncel.");
  }

  async function createType(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); setBusy(true);
    try {
      const form = new FormData(event.currentTarget);
      const reminderDays = String(form.get("reminderDays") ?? "90,60,30,15,7,1,0")
        .split(",").map(x => Number(x.trim())).filter(x => Number.isInteger(x) && x >= 0);
      const body = {
        code: form.get("code"), name: form.get("name"), description: form.get("description") || null,
        requiredByDefault: form.get("requiredByDefault") === "on",
        expirationRequired: form.get("expirationRequired") === "on",
        defaultValidityDays: form.get("defaultValidityDays") ? Number(form.get("defaultValidityDays")) : null,
        fileRequired: form.get("fileRequired") === "on",
        documentNumberRequired: form.get("documentNumberRequired") === "on",
        multipleAllowed: form.get("multipleAllowed") === "on",
        reminderDays, displayOrder: Number(form.get("displayOrder") || 0),
        requiredEmployeeTypeIds: form.getAll("requiredEmployeeTypeIds").map(String),
      };
      const response = await authFetch("/api/v1/documents/types", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(body) });
      if (!response?.ok) {
        const error = response ? await response.json().catch(() => null) as { error?: { message?: string } } | null : null;
        setMessage(error?.error?.message ?? "Belge türü oluşturulamadı."); return;
      }
      setTypes(current => [...current, await response.json() as DocumentType].sort((a, b) => a.displayOrder - b.displayOrder));
      event.currentTarget.reset(); setMessage("Belge türü oluşturuldu.");
    } finally { setBusy(false); }
  }

  async function json<T>(path: string): Promise<T | null> { const response = await authFetch(path); return response?.ok ? await response.json() as T : null; }
  async function authFetch(path: string, init?: RequestInit): Promise<Response | null> {
    let token = sessionStorage.getItem("pp_access_token") ?? await refresh(); if (!token) return null;
    let response = await fetch(`${apiBase}${path}`, { ...init, headers: { ...(init?.headers ?? {}), Authorization: `Bearer ${token}` }, credentials: "include" });
    if (response.status !== 401) return response;
    token = await refresh(); if (!token) return response;
    return fetch(`${apiBase}${path}`, { ...init, headers: { ...(init?.headers ?? {}), Authorization: `Bearer ${token}` }, credentials: "include" });
  }
  async function refresh(): Promise<string | null> {
    try {
      const response = await fetch(`${apiBase}/api/v1/auth/refresh`, { method: "POST", credentials: "include" });
      if (!response.ok) return null;
      const body = await response.json() as AuthResponse; sessionStorage.setItem("pp_access_token", body.accessToken); return body.accessToken;
    } catch { return null; }
  }

  return <main className="shell">
    <a className="back" href="/dashboard">← Dashboard</a>
    <section className="hero compact"><span className="eyebrow">SPRINT 3 · ÖZLÜK & BELGELER</span><h1>Belge Merkezi</h1><p>{message}</p></section>

    <section className="grid document-kpis">
      <article className="card"><span>Eksik Zorunlu</span><h2>{permissions.has("documents.missing.view") ? missing.length : "—"}</h2></article>
      <article className="card"><span>30 Gün İçinde</span><h2>{permissions.has("documents.expiring.view") ? expiring.length : "—"}</h2></article>
      <article className="card"><span>Süresi Geçmiş</span><h2>{permissions.has("documents.expiring.view") ? expired.length : "—"}</h2></article>
      <article className="card"><span>Belge Türü</span><h2>{permissions.has("documents.type.view") ? types.length : "—"}</h2></article>
    </section>

    {permissions.has("documents.missing.view") ? <AttentionPanel title="Eksik Zorunlu Belgeler" empty="Eksik zorunlu belge bulunmuyor.">
      <table className="data-table"><thead><tr><th>Personel</th><th>Sicil</th><th>Eksik Belge</th><th></th></tr></thead><tbody>{missing.map((x, index) => <tr key={`${x.employeeId}-${x.code}-${index}`}><td>{x.employeeName}</td><td>{x.employeeNo}</td><td><strong>{x.name}</strong><small>{x.code}</small></td><td><a className="table-button" href={`/personnel/${x.employeeId}`}>Personel 360</a></td></tr>)}</tbody></table>
    </AttentionPanel> : null}

    {permissions.has("documents.expiring.view") ? <section className="security-grid document-attention-grid">
      <AttentionPanel title="30 Gün İçinde Süresi Dolacak" empty="Yaklaşan belge bulunmuyor."><DocumentAttentionTable rows={expiring}/></AttentionPanel>
      <AttentionPanel title="Süresi Geçmiş" empty="Süresi geçmiş belge bulunmuyor."><DocumentAttentionTable rows={expired}/></AttentionPanel>
    </section> : null}

    {permissions.has("documents.type.view") ? <section className="panel audit-panel">
      <div className="panel-heading"><div><span className="eyebrow dark">MASTER DATA</span><h2>Belge Türleri</h2></div><strong>{types.length}</strong></div>
      {permissions.has("documents.type.manage") ? <form className="inline-form document-type-form" onSubmit={createType}>
        <label className="field-label">Kod<input name="code" required maxLength={80}/></label>
        <label className="field-label">Ad<input name="name" required maxLength={150}/></label>
        <label className="field-label">Açıklama<input name="description" maxLength={1000}/></label>
        <label className="field-label">Varsayılan Geçerlilik (gün)<input name="defaultValidityDays" type="number" min={1}/></label>
        <label className="field-label">Hatırlatma Günleri<input name="reminderDays" defaultValue="90,60,30,15,7,1,0"/></label>
        <label className="field-label">Sıra<input name="displayOrder" type="number" defaultValue={100}/></label>
        <label className="field-label">Zorunlu Personel Tipleri<select name="requiredEmployeeTypeIds" multiple size={Math.min(5, Math.max(2, employeeTypes.length))}>{employeeTypes.map(x => <option key={x.id} value={x.id}>{x.code} · {x.name}</option>)}</select></label>
        <label className="check-label"><input name="fileRequired" type="checkbox" defaultChecked/> Dosya zorunlu</label>
        <label className="check-label"><input name="documentNumberRequired" type="checkbox"/> Belge no zorunlu</label>
        <label className="check-label"><input name="expirationRequired" type="checkbox"/> Son kullanma zorunlu</label>
        <label className="check-label"><input name="multipleAllowed" type="checkbox"/> Çoklu kayıt</label>
        <label className="check-label"><input name="requiredByDefault" type="checkbox"/> Tüm personel için zorunlu</label>
        <button className="primary-button" disabled={busy}>Belge Türü Ekle</button>
      </form> : null}
      <div className="table-wrap"><table className="data-table"><thead><tr><th>Kod</th><th>Ad</th><th>Kurallar</th><th>Zorunlu Tipler</th></tr></thead><tbody>{types.map(x => <tr key={x.id}><td>{x.code}</td><td>{x.name}</td><td>{[x.fileRequired ? "Dosya" : null, x.documentNumberRequired ? "Belge No" : null, x.expirationRequired ? "Süre" : null, x.multipleAllowed ? "Çoklu" : null].filter(Boolean).join(" · ") || "—"}</td><td>{x.requiredEmployeeTypeIds.map(id => employeeTypes.find(t => t.id === id)?.name ?? id).join(" · ") || (x.requiredByDefault ? "Tüm personel" : "—")}</td></tr>)}</tbody></table></div>
    </section> : null}
  </main>;
}

function AttentionPanel({ title, empty, children }: { title: string; empty: string; children: React.ReactNode }) {
  const table = children as React.ReactElement<{ children?: unknown }>;
  return <section className="panel audit-panel"><div className="panel-heading"><div><span className="eyebrow dark">DİKKAT GEREKTİREN</span><h2>{title}</h2></div></div><div className="table-wrap">{table ?? <p className="muted">{empty}</p>}</div></section>;
}

function DocumentAttentionTable({ rows }: { rows: Attention[] }) {
  if (rows.length === 0) return <p className="muted">Kayıt bulunmuyor.</p>;
  return <table className="data-table"><thead><tr><th>Personel</th><th>Belge</th><th>Bitiş</th><th>Durum</th><th></th></tr></thead><tbody>{rows.map(x => <tr key={x.documentId}><td><strong>{x.employeeName}</strong><small>{x.employeeNo}</small></td><td>{x.documentTypeName}<small>{x.documentTypeCode}</small></td><td>{x.validUntil}<small>{x.daysRemaining < 0 ? `${Math.abs(x.daysRemaining)} gün geçti` : `${x.daysRemaining} gün kaldı`}</small></td><td><span className={`status-badge ${x.daysRemaining < 0 ? "danger" : "success"}`}>{x.status}</span></td><td><a className="table-button" href={`/personnel/${x.employeeId}`}>Personel 360</a></td></tr>)}</tbody></table>;
}
