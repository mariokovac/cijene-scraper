# cijene-scraper

## Opis
cijene-scraper je ASP.NET Core Web API projekt za scraping i pohranu cijena proizvoda iz maloprodajnih lanaca.

## Značajke
- Web API za pokretanje scraping zadataka
- Parsiranje cijena i informacija o proizvodima
- Pohrana podataka u PostgreSQL bazu (EF Core)
- Keširanje podataka u CSV i Parquet formatu
- Docker i Docker Compose podrška
- Automatsko primjenjivanje EF Core migracija pri pokretanju

## Tehnologije
- .NET 9 (ASP.NET Core)
- Entity Framework Core (Npgsql)
- HtmlAgilityPack
- CsvHelper
- Parquet.Net
- ZstdSharp
- Docker, Docker Compose
- NSwag (Swagger UI)

## Instalacija
1. Klonirajte repozitorij:
   ```bash
   git clone <URL repozitorija>
   ```
2. Prijeđite u direktorij projekta:
   ```bash
   cd cijene-scraper
   ```
3. Instalirajte .NET 9 SDK (ako već nije instaliran).
4. Konfigurirajte varijable okoline u `.env` datoteci:
   ```ini
   POSTGRES_USER=scraper_user
   POSTGRES_PASSWORD=scraper_password
   POSTGRES_DB=cjene_scraper
   ```
5. Ažurirajte `appsettings.json` po potrebi (poveznica na bazu podataka).

## Pokretanje

### Lokalno
```bash
dotnet run
```
API će biti dostupan na `http://localhost:8080`.

### Docker Compose
```bash
docker-compose up -d
```
Servis će biti dostupan na `http://localhost:8080`.

## API

### Provjera zdravlja (health check)
- `GET /health`  
  Vraća `OK` ako je aplikacija u redu.

### Pokretanje scraping zadatka
- `POST /api/scraper/start/{lanac}?date=YYYY-MM-DD`  
  Pokreće scraping za zadani lanac (npr. `konzum`). Parametar `date` je opcionalan; ako nije naveden, koristi se trenutni datum.

Primjer:
```http
POST http://localhost:8080/api/scraper/start/konzum?date=2025-07-04
```

## Struktura projekta
```
cijene-scraper/
├── Controllers/         // API kontroleri (Health, Scraper)
├── Models/              // Modeli podataka i DTO
│   └── Database/        // EF Core entiteti (Chain, ChainProduct, Price, Store)
├── Data/                // ApplicationDbContext
├── Services/            // ScrapingQueue, ScrapingWorker, Crawlers, Caching
│   ├── Crawlers/        // Implementacija ICrawler za pojedine lance
│   └── Caching/         // ICacheProvider: CSV i Parquet
├── Program.cs           // Glavna konfiguracija i startup
├── appsettings.json     // Postavke aplikacije
├── .env                 // Varijable okoline za Docker Compose
├── Dockerfile
├── docker-compose.yml
└── LICENSE.txt
```

## Dodavanje novog crawlera
1. Implementirajte sučelje `ICrawler` u `Services/Crawlers/Chains/`.
2. Registrirajte novi crawler u `Program.cs` pomoću:
   ```csharp
   builder.Services.AddTransient<ICrawler, NoviLanacCrawler>();
   ```

## Licenca
Projekt je dostupan pod MIT licencom. Pogledajte `LICENSE.txt` za više informacija.

