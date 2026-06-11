Teknik Beklentiler 
Çalışmanın aşağıdaki teknolojiler kullanılarak hazırlanması beklenmektedir: 
• .NET 8 veya .NET 9 
• ASP.NET Core Web API 
• ASP.NET Core MVC veya Razor Pages (Backoffice) 
• Entity Framework Core 
• PostgreSQL 
• RabbitMQ (MassTransit veya RabbitMQ.Client ile) 
• Docker 
• Docker Compose 
• xUnit 
• Serilog (veya tercih edilen structured logging kütüphanesi) 
Çalışma Konusu 
Basit bir Mini Sipariş Yönetim Sistemi geliştirmeniz beklenmektedir. 
Sistem en az aşağıdaki bileşenlerden oluşmalıdır: 
• Catalog Service 
• Order Service 
• Notification Service 
• Backoffice Portal 
Servis ve Uygulama Bileşenleri 
1. Catalog Service 
Ürün bilgilerinin yönetildiği servistir. 
Beklenen özellikler: 
• ürün ekleme 
• ürün listeleme (sayfalama, sıralama ve fiyat aralığına göre filtreleme zorunlu) 
• ürün detay görüntüleme 
• ürün güncelleme 
• ürün silme (soft delete uygulanmalıdır) 
Ürün modeli en az şu alanları içermelidir: 
• Id (Guid önerilir) 
• Name 
• Description 
• Price 
• Stock 
• IsActive 
• CreatedAt / UpdatedAt 
Ek beklentiler: 
• Stok güncellemeleri için optimistic concurrency (RowVersion / xmin) uygulanmalıdır. Aynı 
anda iki istemcinin aynı ürünü güncellemeye çalışması durumu ele alınmalıdır. 
• Ürün listeleme endpoint'i Redis cache'lenmeli, ürün ekleme/güncelleme/silme sonrası cache 
invalidate edilmelidir. (Bonus değil, zorunlu) 
2. Order Service 
Sipariş işlemlerinin yönetildiği servistir. 
Beklenen özellikler: 
• sipariş oluşturma (bir sipariş birden fazla kalem içerebilir — OrderItem) 
• sipariş listeleme (sayfalama) 
• sipariş detay görüntüleme 
• sipariş iptal etme (iptal edildiğinde stok geri eklenmelidir) 
Sipariş oluşturma sırasında: 
• ürünün varlığı Catalog Service üzerinden kontrol edilmelidir 
• yeterli stok olup olmadığı doğrulanmalıdır 
• başarılı sipariş sonrasında stok düşülmelidir 
• Idempotency-Key header'ı destelenmelidir: aynı header ile gönderilen istek iki kere sipariş 
oluşturmamalıdır 
Sipariş modeli en az şu alanları içermelidir: 
• Id 
• Customer (en azından CustomerId veya basit bir email) 
• OrderItems (her biri: ProductId, Quantity, UnitPrice, LineTotal) 
• TotalPrice 
• Status (Pending, Confirmed, Cancelled) 
• CreatedAt 
• UpdatedAt 
Ek beklentiler: 
• Sipariş başarıyla oluşturulduğunda OrderCreatedEvent mesajı RabbitMQ'ya publish edilmelidir. 
• Catalog Service ile iletişimde Polly kullanılarak retry ve circuit breaker 
uygulanmalıdır. HttpClientFactory + typed client zorunludur. 
3. Notification Service 
Sipariş oluşumu sonrasında bildirim üretmekten sorumlu servistir. 
Beklenen özellikler: 
• RabbitMQ üzerinden OrderCreatedEvent mesajını dinleme 
• bildirim kaydının veritabanında saklanması (Notification tablosu: Id, OrderId, Channel, 
Message, CreatedAt) 
• bildirimlerin listelenebileceği basit bir API endpoint'i 
Not: Gerçek bir e-posta ya da SMS entegrasyonu beklenmemektedir, log + DB kaydı yeterlidir. 
Ek beklentiler: 
• Mesaj işlenirken hata oluşursa retry mekanizması ve dead-letter queue (DLQ) yaklaşımı 
düşünülmelidir. 
4. Backoffice Portal 
Servislerle entegre çalışan basit bir yönetim arayüzüdür. 
Portal aşağıdaki teknolojilerden biriyle geliştirilebilir: 
• ASP.NET Core MVC 
• Razor Pages 
Beklenen özellikler: 
• ürün CRUD ekranları 
• sipariş oluşturma, listeleme, detay görüntüleme, iptal etme 
• bildirimlerin listelenmesi 
• başarılı / hatalı işlem mesajlarının kullanıcıya gösterilmesi (TempData veya benzeri) 
• JWT tabanlı login ekranı (en az admin / operator iki rol) 
Notlar: 
• Portal, veritabanına doğrudan erişmemelidir; ilgili servislerin API'lerini kullanmalıdır. 
• API çağrılarında HttpClientFactory + typed client + Polly kullanılmalıdır. 
• Görsel açıdan ileri seviye olması beklenmemektedir; Bootstrap yeterlidir. 
Mimari Beklentiler 
Aşağıdaki kalıpların en az bir servis içinde uygulanmış olması beklenmektedir (tercihen Order Service): 
• Clean Architecture / Hexagonal Architecture: Domain, Application, Infrastructure, API 
katmanları net ayrılmalı; bağımlılık yönü içe doğru olmalıdır. 
• DDD lite: Order bir Aggregate Root olmalı; OrderItem entity, Money/Address gibi yapılar 
Value Object olmalıdır. Iş kuralları (örn. iptal şartları) domain içinde olmalıdır — anemic model 
değil. 
• CQRS + MediatR: Command ve Query handler ayrımı. 
• Repository + Unit of Work: EF Core üzerine ince bir soyutlama. 
• FluentValidation: Tüm Command/Request validation'ları. 
• Result / OperationResult pattern kullanımı tercih edilir (exception driven flow yerine). 
Diğer servisler daha pragmatik bir yapıda kurulabilir ancak katman ayrımı ve DI doğru kullanılmalıdır. 
Servisler Arası İletişim 
• Order Service ↔ Catalog Service: HTTP (REST, Polly ile resilience) 
• Order Service → Notification Service: RabbitMQ üzerinden event-driven 
• Backoffice Portal → tüm servisler: HTTP 
Mesaj kontrat sınıfları paylaşımlı bir Shared.Contracts projesinde tutulmalıdır. 
Veritabanı Beklentisi 
• PostgreSQL kullanılmalıdır. 
• Veritabanı Docker ortamında çalışmalıdır. 
• Servis başına ayrı veritabanı / şema kullanılması beklenmektedir (database-per-service 
ilkesi). Tek instance üzerinde farklı veritabanları kabul edilebilir. 
• EF Core Migrations kullanılmalı, container ayağa kalkarken otomatik apply edilmelidir. 
• Başlangıç (seed) verisi olmalıdır (en az 5 ürün). 
• Audit fields (CreatedAt, UpdatedAt) EF Core interceptor / SaveChanges override ile otomatik 
doldurulmalıdır. 
Seçilen yaklaşım README içinde gerekçeleriyle açıklanmalıdır. 
Güvenlik 
• Backoffice Portal için JWT tabanlı authentication zorunludur. 
• En az iki rol bulunmalıdır: Admin, Operator. 
• Ürün silme işlemi yalnızca Admin rolüne açık olmalıdır. 
• Servis API'leri [Authorize] ile korunmalıdır (Notification Service hariç tutulabilir). 
• Hassas bilgiler (DB şifresi, JWT secret) kesinlikle appsettings içinde plain text 
bulunmamalıdır; environment variable üzerinden okunmalıdır. 
Loglama ve Observability 
Aşağıdakiler zorunludur: 
• Serilog ile structured logging, JSON formatında console'a yazılmalıdır. 
• Tüm servisler arasında X-Correlation-Id header'ı propagate edilmelidir; log satırlarında bu ID 
görünmelidir. 
• Her servis için /health endpoint'i bulunmalı, DB ve (varsa) RabbitMQ bağlılıkları kontrol 
edilmelidir. 
• Global exception handling middleware ile Problem Details (RFC 7807) formatında hata 
response'u dönülmelidir. 
Test Beklentisi 
Aşağıdakiler zorunludur: 
• Order Service'in domain ve application katmanı için xUnit ile unit test (FluentAssertions 
kullanımı tercih edilir). 
• Sipariş oluşturma akışı için en az 1 integration 
test (WebApplicationFactory veya Testcontainers kullanımı tercih edilir). 
• Test projeleri çözüm içinde ayrı klasörde olmalıdır (tests/). 
Hedef coverage zorunlu değildir, ancak kritik iş kuralları (stok kontrolü, iptal akışı, idempotency) test 
edilmiş olmalıdır. 
API Tasarımı 
• RESTful endpoint isimlendirmesi (kaynak odaklı; /products, /orders). 
• API versioning (/api/v1/...) zorunludur. 
• Sayfalama envelope'u: { data: [...], meta: { page, pageSize, totalCount } }. 
• Tüm servisler Swagger/OpenAPI dokümantasyonu sunmalıdır; XML doc yorumları Swagger'a 
yansımalıdır. 
Çalıştırma ve Docker Zorunluluğu 
Teslim edilen çalışma, proje klasörü açıldıktan sonra yalnızca aşağıdaki komut ile çalıştırılabilir olmalıdır: 
docker compose up --build 
Aşağıdaki koşullar zorunludur: 
• tüm servisler, RabbitMQ, PostgreSQL ve Redis ayağa kalkmalıdır 
• servisler arası başlangıç sırası depends_on + condition: service_healthy ile yönetilmelidir 
• backoffice portal erişilebilir olmalıdır 
• migration'lar otomatik uygulanmalıdır 
• seed data otomatik yüklenmelidir 
• .env.example dosyası teslim edilmelidir, kullanıcı .env olarak kopyalayıp çalıştırabilmelidir 
Teknik Teslim Beklentileri 
• proje kökünde docker-compose.yml 
• her servis için ayrı multi-stage Dockerfile (build/publish/runtime) 
• container'lar non-root user ile çalışmalıdır 
• .dockerignore dosyaları 
• .editorconfig dosyası 
• Solution dosyası (.sln) ve mantıklı klasör yapısı (src/, tests/) 