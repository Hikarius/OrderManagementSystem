# Mini Sipariş Yönetim Sistemi

Bu proje, .NET 9 teknolojileri kullanılarak geliştirilmiş bir Mini Sipariş Yönetim Sistemi'ni içermektedir. Sistem, mikroservis mimarisi prensiplerini benimseyerek bağımsız olarak geliştirilebilen ve ölçeklenebilen servisler aracılığıyla işlevsellik sunar.

Uygulamaya admin-pass veya operator-pass ile giriş yapılabilir.

## Mimari Genel Bakış

Sistem, aşağıdaki ana bileşenlerden oluşmaktadır:

- **Catalog Service**: Ürün bilgilerini yönetir.
- **Order Service**: Sipariş işlemlerini yönetir ve Catalog Service ile etkileşim kurar.
- **Notification Service**: Order Service'ten gelen olaylara göre bildirimler üretir.
- **Backoffice Portal**: Kullanıcıların ürün ve sipariş yönetimi yapabileceği bir yönetim arayüzüdür.
- **Shared**: Servisler arası veri transferi için ortak kontrat sınıflarını içerir. ALtyapı ile ilgili kök kütüphanler burada tutulur.

Bu servisler, RESTful API'ler ve mesaj kuyrukları (RabbitMQ) aracılığıyla birbirleriyle iletişim kurar. Veritabanı olarak PostgreSQL kullanılır.

graph TD
A[Backoffice Portal] -->|HTTP/REST| B(Catalog Service API)
A -->|HTTP/REST| C(Order Service API)
A -->|HTTP/REST| D(Notification Service API)
B -->|PostgreSQL| E(Catalog DB)
C -->|PostgreSQL| F(Order DB)
D -->|PostgreSQL| G(Notification DB)

C -->|RabbitMQ| H(RabbitMQ)
H -->|OrderCreatedEvent| D

C -->|HTTP/REST (Polly)| B

subgraph Services
B
C
D
end

subgraph Databases
E
F
G
end

## Servislerin ve Portalın Sorumlulukları

- **Catalog Service**:
  - Ürün ekleme, listeleme (filtreleme, sayfalama, sıralama ile), detay görüntüleme, güncelleme ve silme (soft delete).
  - Optimistic concurrency (RowVersion/xmin) ile stok güncellemeleri.
  - Ürün listeleme endpoint'inin Redis ile cache'lenmesi ve değişiklik sonrası cache invalidation.

- **Order Service**:
  - Sipariş oluşturma (OrderItem'lar ile), listeleme (sayfalama) ve detay görüntüleme.
  - Sipariş iptali ve stoklerin geri eklenmesi.
  - Catalog Service'ten ürün varlığı ve stok kontrolü.
  - Idempotency-Key header desteği.
  - Başarılı sipariş sonrası `OrderCreatedEvent`'i RabbitMQ'ya publish etme.
  - Catalog Service ile iletişimde Polly kullanarak retry ve circuit breaker uygulama.

- **Notification Service**:
  - RabbitMQ üzerinden `OrderCreatedEvent` mesajlarını dinleme.
  - Bildirimleri veritabanında saklama ve listeleme API'si sunma.
  - Mesaj işleme hataları için retry ve DLQ mekanizmalarını değerlendirme.

- **Backoffice Portal**:
  - Ürün CRUD işlemleri.
  - Sipariş işlemleri (oluşturma, listeleme, detay, iptal).
  - Bildirimleri listeleme.
  - JWT tabanlı kimlik doğrulama ve rol bazlı yetkilendirme (Admin, Operator).
  - Servis API'leri ile HttpClientFactory, typed client ve Polly kullanarak etkileşim.

## Kullanılan Teknolojiler ve Seçim Gerekçeleri

- **.NET 9**: Modern, performanslı ve platformlar arası geliştirme imkanı sunan güncel .NET sürümü.
- **ASP.NET Core Web API / Razor Pages**: Servisler için RESTful API'ler ve Backoffice Portal için modern web arayüzü geliştirme çatısı.
- **Entity Framework Core**: Veritabanı erişim katmanı için ORM (Object-Relational Mapper).
- **PostgreSQL**: Güvenilir, açık kaynaklı ve güçlü bir ilişkisel veritabanı yönetim sistemi.
- **RabbitMQ (MassTransit)**: Servisler arası asenkron iletişimi sağlamak için mesaj kuyruğu sistemi.
- **Redis**: Catalog Service'te ürün listeleme verilerini cache'lemek için hızlı anahtar-değer deposu.
- **Docker / Docker Compose**: Uygulama ve bağımlılıklarının (veritabanı, mesaj kuyruğu) konteynerize edilerek kolayca çalıştırılmasını sağlar.
- **xUnit**: Birim ve entegrasyon testleri için tercih edilen test framework'ü.
- **Serilog**: Yapılandırılmış (structured) loglama için esnek ve güçlü bir kütüphane.
- **Polly**: Dağıtık sistemlerde hata toleransını artırmak için retry ve circuit breaker implementasyonları.
- **FluentValidation**: Uygulama genelinde güçlü ve merkezi doğrulama kuralları tanımlamak için kullanılır.
- **MediatR (CQRS)**: Command ve Query ayrımını netleştirerek uygulamanın daha ölçeklenebilir ve yönetilebilir olmasını sağlar.
- **JWT**: Backoffice Portal için güvenli kimlik doğrulama mekanizması.

## Adresler

- **Backoffice Portal**: `http://localhost:7000` (Varsayılan port, docker-compose.yml'den kontrol edilebilir)
- **RabbitMQ Management UI**: `http://localhost:15672` (Kullanıcı adı/şifre: guest/guest)

## Örnek Kullanım Akışı (Curl ile Ürün Ekleme ve Listeleme)

Aşağıdaki örnekler, Catalog Service API'sine yönelik `curl` komutlarını göstermektedir. API versioning (`/api/v1/`) kullanılmıştır.

**1. Ürün Ekleme:**
curl -X POST "http://localhost:5001/api/v1/products"
-H "Content-Type: application/json"
-d '{ "name": "Laptop Pro", "description": "Yüksek performanslı profesyonel laptop", "price": 1500.50, "stock": 50, "isActive": true }'

**2. Ürün Listeleme (Sayfalama ve Filtreleme ile):**

curl -X GET "http://localhost:5001/api/v1/products?page=1&pageSize=10&minPrice=1000&maxPrice=2000&sortBy=price_desc&isActive=true"

## Alınan Teknik / Mimari Kararlar (Özet)

- **Mikroservis Mimarisi**: İşlevselliği bağımsız olarak dağıtılabilen ve ölçeklenebilen servislere ayırma kararı alınmıştır. Bu, her servisin kendi veritabanına sahip olması (database-per-service) ve servisler arası iletişimin HTTP/REST ve mesaj kuyrukları (RabbitMQ) ile sağlanması anlamına gelir.
- **Clean Architecture / Hexagonal Architecture**: Domain, Application, Infrastructure ve API katmanlarının net ayrımı ile kodun sürdürülebilirliği ve test edilebilirliği artırılmıştır. Bağımlılık yönü içe doğrudur.
- **CQRS + MediatR**: Komutlar (veri değiştirme işlemleri) ve sorgular (veri okuma işlemleri) için ayrı handler'lar kullanılarak iş mantığının daha temiz yönetilmesi hedeflenmiştir.
- **Repository Pattern + Unit of Work**: Veritabanı erişimini soyutlamak ve Unit of Work ile birden fazla işlemi atomik olarak yönetmek için kullanılmıştır.
- **FluentValidation**: Tüm API isteklerinin ve komutların doğrulanması için merkezi ve okunabilir bir yöntem sunar.
- **Polly ile Resilience**: Catalog Service ile Order Service arasındaki HTTP iletişiminde oluşabilecek ağ sorunlarına karşı retry ve circuit breaker mekanizmaları eklenmiştir.
- **Idempotency Key**: Sipariş oluşturma gibi kritik operasyonlarda istemci hataları veya ağ sorunları nedeniyle tekrar eden isteklerin istenmeyen sonuçlar doğurmasını engellemek için Idempotency-Key header desteği eklenmiştir.
- **Containerization (Docker)**: Geliştirme, test ve dağıtım süreçlerini kolaylaştırmak, ortam tutarlılığını sağlamak amacıyla tüm servisler ve bağımlılıkları Docker ile konteynerize edilmiştir. `docker-compose up --build` komutu ile tüm sistem tek adımda ayağa kaldırılabilir.
- **JWT Authentication**: Backoffice Portal için güvenli ve ölçeklenebilir bir kimlik doğrulama çözümü olarak JWT kullanılmıştır.
- **Structured Logging (Serilog)**: Uygulama loglarının makine tarafından okunabilir ve sorgulanabilir olması için JSON formatında ve yapılandırılmış olarak kaydedilmesi tercih edilmiştir.

## Eksik bırakılan noktalar

- Bazı gereksinimler tamamlanamadı. Kısa süre içerisinde geliştirmelere devam edilecek.
- .Net9 ile default swagger desteği kaldırıldı. Bu sebeple manuel olarak eklenecek. Swagger üzerinden API'lerin test edilmesi sağlanacak.
- Postman collection hazırlanacak ve paylaşılacak.
- API tasarımı ile ilgili düzeltmeler yapılacak.
