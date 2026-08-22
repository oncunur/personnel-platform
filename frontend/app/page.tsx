const modules = [
  "Personel Yönetimi",
  "Özlük & Belgeler",
  "İzin Yönetimi",
  "Puantaj & Fazla Mesai",
  "Kamp & Konaklama",
  "Yemek Yönetimi",
  "Bordro & Ücret",
  "Raporlama & Maliyet",
];

export default function Home() {
  return (
    <main className="shell">
      <section className="hero">
        <span className="eyebrow">SPRINT 1 · IDENTITY IN PROGRESS</span>
        <h1>Personel & İdari İşler Platformu</h1>
        <p>
          Platform temeli tamamlandı. Kimlik doğrulama, güvenli parola saklama,
          JWT access token ve refresh token rotation geliştirmesi başladı.
        </p>
        <div className="actions action-row">
          <a className="primary" href="/login">Platforma giriş yap</a>
          <a className="ghost-link" href="/api-health">API sağlık durumunu kontrol et</a>
        </div>
      </section>

      <section className="grid" aria-label="Planlanan modüller">
        {modules.map((module) => (
          <article className="card" key={module}>
            <span>Planlandı</span>
            <h2>{module}</h2>
          </article>
        ))}
      </section>
    </main>
  );
}
