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
        <span className="eyebrow">SPRINT 0 · IMPLEMENTATION STARTED</span>
        <h1>Personel & İdari İşler Platformu</h1>
        <p>
          Teknik temel oluşturuldu. Bir sonraki kod dilimi kimlik doğrulama, kullanıcı,
          rol, yetki ve scope altyapısıdır.
        </p>
        <div className="actions">
          <a className="primary" href="/api-health">API sağlık durumunu kontrol et</a>
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
