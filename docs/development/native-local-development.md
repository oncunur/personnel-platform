# Docker Kullanmadan Yerel Çalıştırma

Bu yöntem macOS üzerinde PostgreSQL ve Redis'i küçük yerel servisler olarak çalıştırır; API ve web arayüzü doğrudan bilgisayarda açılır. Docker Desktop, sanal makine veya sürekli çalışan container derlemesi kullanılmaz.

## Neden daha hafif?

- API ve web arayüzü doğrudan işletim sisteminde çalışır.
- PostgreSQL ve Redis yalnız birer küçük arka plan servisi olarak kalır.
- Varsayılan düşük kaynak modunda Worker çalıştırılmaz.
- Kaynak kodu değişmediği sürece container imajı yeniden oluşturulmaz.

Worker kapalıyken temel personel, izin, puantaj, araç, varlık ve yönetim ekranları kullanılabilir. Zamanlanmış bildirimler, rapor dışa aktarımları ve kuyruk işleme gibi arka plan görevleri için gerektiğinde Worker açılmalıdır.

## Bir kez yapılacak kurulum

Homebrew kurulu bir macOS bilgisayarda:

```bash
brew install postgresql@18 redis node@24
brew install --cask dotnet-sdk
brew services start postgresql@18
brew services start redis
```

Yeni kurulan `node@24` veya `postgresql@18` komutları terminalde bulunamazsa başlatma komutu Homebrew kurulum klasörlerini otomatik olarak arar. Paket kurulumunu elle yapmanız gerekirse Homebrew'ün ekranda gösterdiği PATH satırını uygulayın. Ardından proje paketlerini bir kez kurun:

```bash
cd frontend
npm install
cd ..
```

## Günlük kullanım

Proje kökünde:

```bash
bash scripts/native-dev-up.sh
```

Komut şunları otomatik yapar:

1. Gerekli araçların ve yerel servislerin hazır olduğunu kontrol eder.
2. `personnel_platform` veritabanı yoksa oluşturur.
3. Veritabanı güncellemelerini API üzerinden uygular.
4. API ve web arayüzünü başlatır.
5. API canlılık kontrolü başarılı olduğunda adresleri gösterir.

Varsayılan adresler:

- Uygulama: `http://localhost:3000`
- API: `http://localhost:8080`
- Geliştirme kullanıcısı: `admin`
- Geliştirme parolası: `Admin123!ChangeMe`

Bu bilgiler yalnızca yerel geliştirme içindir.

## Durdurma

```bash
bash scripts/native-dev-down.sh
```

Bu komut yalnızca platformun API, web ve varsa Worker işlemlerini durdurur. PostgreSQL ve Redis küçük, ortak yerel servisler olabileceği için açık bırakılır. Onları da kapatmak isterseniz:

```bash
brew services stop postgresql@18
brew services stop redis
```

## Arka plan görevlerini açma

Rapor dışa aktarımı, zamanlanmış bildirimler ve entegrasyon kuyruğu gibi görevleri test ederken:

```bash
NATIVE_START_WORKER=1 bash scripts/native-dev-up.sh
```

Günlük ekran geliştirmesinde Worker'ı kapalı tutmak işlemci kullanımını azaltır.

## Farklı veritabanı kullanma

Varsayılan olarak macOS kullanıcı adınız PostgreSQL kullanıcısı kabul edilir. Farklı bir bağlantı gerekiyorsa komutu şu değişkenlerle çalıştırabilirsiniz:

```bash
NATIVE_DB_USER=personnel \
NATIVE_DB_PASSWORD='yerel-parola' \
NATIVE_DB_NAME=personnel_platform \
bash scripts/native-dev-up.sh
```

Portlar da `NATIVE_DB_PORT`, `NATIVE_REDIS_PORT`, `NATIVE_API_PORT` ve `NATIVE_WEB_PORT` ile değiştirilebilir.

## Sorun giderme

Çalışma kayıtları `.local-dev/` klasörüne yazılır:

- `.local-dev/api.log`
- `.local-dev/web.log`
- `.local-dev/worker.log` (Worker açıksa)

API başlamıyorsa önce `api.log` dosyasının son satırlarını kontrol edin. PostgreSQL veya Redis hazır değilse başlatma komutu işlem oluşturmadan açık bir uyarı verir.
