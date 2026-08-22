"use client";

import { FormEvent, useState } from "react";

const apiBase = process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:8080";

type AuthResponse = {
  userId: string;
  username: string;
  email: string | null;
  accessToken: string;
  accessTokenExpiresAt: string;
};

type ErrorResponse = {
  error?: {
    code?: string;
    message?: string;
  };
};

export default function LoginPage() {
  const [username, setUsername] = useState("admin");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);
    setSubmitting(true);

    try {
      const response = await fetch(`${apiBase}/api/v1/auth/login`, {
        method: "POST",
        credentials: "include",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ username, password }),
      });

      if (!response.ok) {
        const body = (await response.json().catch(() => ({}))) as ErrorResponse;
        setError(body.error?.message ?? "Giriş başarısız.");
        return;
      }

      const body = (await response.json()) as AuthResponse;
      sessionStorage.setItem("pp_access_token", body.accessToken);
      sessionStorage.setItem("pp_access_token_expires_at", body.accessTokenExpiresAt);
      window.location.assign("/dashboard");
    } catch {
      setError("API bağlantısı kurulamadı. Servislerin çalıştığını kontrol edin.");
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <main className="shell auth-shell">
      <section className="auth-card">
        <a className="back" href="/">← Ana sayfa</a>
        <span className="eyebrow dark">SPRINT 1 · IDENTITY</span>
        <h1>Giriş yap</h1>
        <p className="muted">Personel & İdari İşler Platformu kimlik doğrulama ekranı.</p>

        <form className="auth-form" onSubmit={handleSubmit}>
          <label>
            Kullanıcı adı
            <input autoComplete="username" value={username} onChange={(event) => setUsername(event.target.value)} maxLength={100} required />
          </label>
          <label>
            Parola
            <input type="password" autoComplete="current-password" value={password} onChange={(event) => setPassword(event.target.value)} maxLength={512} required />
          </label>
          {error ? <div className="error-box" role="alert">{error}</div> : null}
          <button className="primary-button" type="submit" disabled={submitting}>{submitting ? "Giriş yapılıyor…" : "Giriş yap"}</button>
        </form>

        <p className="auth-hint">
          Docker geliştirme ortamında varsayılan kullanıcı <strong>admin</strong> olarak oluşturulur.
          Parola <code>.env</code> dosyasındaki <code>BOOTSTRAP_ADMIN_PASSWORD</code> değeridir.
        </p>
      </section>
    </main>
  );
}
