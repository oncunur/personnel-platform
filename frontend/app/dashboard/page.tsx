"use client";

import { useEffect, useState } from "react";

const apiBase = process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:8080";

type MeResponse = {
  userId: string;
  username: string;
  email: string | null;
  securityVersion: number;
};

type AuthResponse = {
  accessToken: string;
  accessTokenExpiresAt: string;
};

export default function DashboardPage() {
  const [me, setMe] = useState<MeResponse | null>(null);
  const [message, setMessage] = useState("Oturum doğrulanıyor…");

  useEffect(() => {
    void loadSession();
  }, []);

  async function loadSession() {
    let accessToken = sessionStorage.getItem("pp_access_token");
    if (!accessToken) {
      accessToken = await refreshAccessToken();
    }

    if (!accessToken) {
      window.location.replace("/login");
      return;
    }

    let response = await fetchMe(accessToken);
    if (response.status === 401) {
      accessToken = await refreshAccessToken();
      if (!accessToken) {
        window.location.replace("/login");
        return;
      }
      response = await fetchMe(accessToken);
    }

    if (!response.ok) {
      setMessage("Oturum bilgisi alınamadı.");
      return;
    }

    const body = (await response.json()) as MeResponse;
    setMe(body);
    setMessage("Oturum aktif.");
  }

  async function refreshAccessToken(): Promise<string | null> {
    try {
      const response = await fetch(`${apiBase}/api/v1/auth/refresh`, {
        method: "POST",
        credentials: "include",
      });

      if (!response.ok) {
        clearLocalSession();
        return null;
      }

      const body = (await response.json()) as AuthResponse;
      sessionStorage.setItem("pp_access_token", body.accessToken);
      sessionStorage.setItem("pp_access_token_expires_at", body.accessTokenExpiresAt);
      return body.accessToken;
    } catch {
      return null;
    }
  }

  function fetchMe(accessToken: string) {
    return fetch(`${apiBase}/api/v1/auth/me`, {
      headers: { Authorization: `Bearer ${accessToken}` },
      credentials: "include",
    });
  }

  async function logout() {
    try {
      await fetch(`${apiBase}/api/v1/auth/logout`, {
        method: "POST",
        credentials: "include",
      });
    } finally {
      clearLocalSession();
      window.location.replace("/login");
    }
  }

  function clearLocalSession() {
    sessionStorage.removeItem("pp_access_token");
    sessionStorage.removeItem("pp_access_token_expires_at");
  }

  return (
    <main className="shell">
      <section className="hero compact">
        <span className="eyebrow">SPRINT 1 · AUTHENTICATED AREA</span>
        <h1>Platform Dashboard</h1>
        <p>{message}</p>
        {me ? (
          <div className="session-summary">
            <strong>{me.username}</strong>
            <span>{me.email ?? "E-posta tanımlı değil"}</span>
            <span>Security version: {me.securityVersion}</span>
          </div>
        ) : null}
        <div className="actions"><button className="secondary-button" type="button" onClick={logout}>Çıkış yap</button></div>
      </section>

      <section className="grid" aria-label="Sprint 1 durum kartları">
        <article className="card"><span>Aktif</span><h2>JWT Access Token</h2></article>
        <article className="card"><span>Aktif</span><h2>Refresh Token Rotation</h2></article>
        <article className="card"><span>Aktif</span><h2>PBKDF2 Password Hashing</h2></article>
        <article className="card"><span>Sıradaki</span><h2>Role / Permission / Scope</h2></article>
      </section>
    </main>
  );
}
