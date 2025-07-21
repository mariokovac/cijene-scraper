# Cijene Scraper

ASP.NET Core Web API projekt za scraping i pohranu cijena proizvoda iz maloprodajnih lanaca u Hrvatskoj.

## 🚀 Značajke

### Osnovna funkcionalnost
- **RESTful Web API** za upravljanje scraping zadacima
- **Automatski scraping cijena** iz službenih izvora
- **Parsiranje i normalizacija podataka** o proizvodima i cijenama
- **Pohrana podataka** u PostgreSQL bazu (Entity Framework Core)
- **Dvostruko keširanje** - CSV i Parquet format za različite potrebe

### Maloprodajni lanci
Trenutno podržava sljedeće lance:
- **Konzum** - CSV datoteke s cjenikom
- **Kaufland** - CSV datoteke s cjenikom
- **Plodine** - CSV datoteke s cjenikom
- **Spar** - CSV datoteke s cjenikom
- **Lidl** - ZIP datoteke s CSV cjenicima

### Napredne funkcionalnosti
- **Geolokacija trgovina** - Google Geocoding API integracija
- **API autentifikacija** - Podrška za API ključeve
- **Detaljno logiranje** - Baza podataka i file logging
- **Background servisi** - Asinkroni scraping sa queue sustavom
- **Email notifikacije** - Automatsko obavještavanje o statusu poslova
- **Enkodiranje podataka** - Automatska detekcija encoding-a za hrvatske znakove
- **Kompresija podataka** - Parquet format s optimiziranom kompresijom

### Docker i deployment
- **Docker podrška** s Docker Compose
- **Automatske EF Core migracije** pri pokretanju
- **Health check endpointi**
- **Swagger UI** za interaktivno testiranje API-ja

## 🛠️ Tehnologije

- **.NET 9** (ASP.NET Core)
- **Entity Framework Core** (Npgsql)
- **PostgreSQL** baza podataka
- **HtmlAgilityPack** za HTML parsiranje
- **CsvHelper** za CSV operacije
- **Parquet.Net** za efikasnu pohranu podataka
- **Google Geocoding API** za geolokaciju
- **Docker & Docker Compose** za kontejnerizaciju
- **NSwag** (OpenAPI/Swagger)

## 📋 Preduvjeti

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Docker](https://www.docker.com/) i [Docker Compose](https://docs.docker.com/compose/) (za Docker pokretanje)
- [PostgreSQL](https://www.postgresql.org/) (ako pokrećete izvan Dockera)
- **Google Geocoding API ključ** (opcionalno, za geolokaciju trgovina)

## 🔧 Instalacija

### Kloniranje repozitorija

```bash
git clone https://github.com/mariokovac/cijene-scraper.git
cd cijene-scraper
```

### Konfiguracija

1. **Varijable okoline** (`.env`):
```bash
POSTGRES_USER=scraper_user
POSTGRES_PASSWORD=scraper_password
POSTGRES_DB=cijene_scraper
```

2. **Aplikacijske postavke** (`appsettings.json`):
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=cijene_scraper;Username=scraper_user;Password=scraper_password"
  },
  "Google": {
    "GeocodingApiKey": "vaš-google-api-ključ"
  },
  "ApiKey": {
    "Enabled": true,
    "MobileApp": "vaš-api-ključ"
  },
  "Caching": {
    "Engine": "parquet"
  },
  "MailSettings": {
    "Host": "smtp.gmail.com",
    "Port": 587,
    "UserName": "vaš-email@gmail.com",
    "Password": "vaša-lozinka",
    "DisplayName": "Cijene Scraper"
  }
}
```

## 🐳 Pokretanje s Dockerom (preporučeno)

```bash
docker-compose up -d
```

API će biti dostupan na http://localhost:8080

## 🏃‍♂️ Pokretanje lokalno

```bash
dotnet restore
dotnet run
```

API će biti dostupan na http://localhost:8080

## 📚 API pregled

Za detaljnu API dokumentaciju, pogledajte [API_DOCUMENTATION.md](API_DOCUMENTATION.md).

### Glavni endpointi:

- `GET /health` - Health check
- `POST /api/scraper/start/{chain}` - Pokretanje scraping-a
- `GET /api/scraper/status` - Status scraping poslova
- `GET /api/prices` - Dohvaćanje cijena s filtriranjem
- `GET /api/prices/ByBarcode` - Cijene po barkodu
- `GET /api/prices/CheapestLocation` - Najjeftinija lokacija
- `GET /api/prices/ByCodesNearby` - Cijene u blizini GPS koordinata
- `GET /api/prices/SearchProducts` - Pretraživanje proizvoda
- `GET /api/prices/Statistics` - Statistike cijena

### Swagger UI
Dostupan na http://localhost:8080/swagger za interaktivno testiranje API-ja.

## ⚙️ Konfiguracija

### Keširanje podataka
Podaci se automatski keširaju u dva formata:
- **CSV** - Za jednostavno čitanje i analizu (`"Caching": {"Engine": "csv"}`)
- **Parquet** - Za efikasniju pohranu velikih količina podataka (`"Caching": {"Engine": "parquet"}`)

### API autentifikacija
```json
{
  "ApiKey": {
    "Enabled": true,
    "MobileApp": "vaš-sigurni-api-ključ"
  }
}
```

### Google Geocoding
Za automatsko geocodiranje adresa trgovina:
```json
{
  "Google": {
    "GeocodingApiKey": "vaš-google-api-ključ"
  }
}
```

### Email notifikacije
```json
{
  "MailSettings": {
    "Host": "smtp.gmail.com",
    "Port": 587,
    "UserName": "vaš-email@gmail.com",
    "Password": "app-password",
    "DisplayName": "Cijene Scraper"
  }
}
```

## 🔧 Dodavanje novog lanca

1. **Stvori novu CSV record klasu**:
```csharp
public class NoviLanacCsvRecord : CsvRecordBase
{
    [Name("Naziv")]
    public override string Product { get; set; }
    
    [Name("Cijena")]
    public override string Price { get; set; }
    
    // implementiraj ostala svojstva
    
    public override PriceInfo ToPriceInfo()
    {
        // implementiraj konverziju
    }
}
```

2. **Stvori crawler**:
```csharp
public class NoviLanacCrawler : ICrawler
{
    public string ChainName => "novilanac";
    
    public async Task<IEnumerable<PriceInfo>> ScrapeAsync(DateOnly date, CancellationToken cancellationToken)
    {
        // implementiraj scraping logiku
    }
}
```

3. **Registriraj crawler** u `Program.cs`:
```csharp
builder.Services.AddTransient<ICrawler, NoviLanacCrawler>();
```

## 🗄️ Baza podataka

### Schema
Glavne tablice:
- `Chains` - Informacije o maloprodajnim lancima
- `Stores` - Popis trgovina po lancima s geolokacijom
- `Products` - Katalog proizvoda s normaliziranim podacima
- `ChainProducts` - Proizvodi specifični za lance
- `Prices` - Povijesne cijene proizvoda po trgovinama
- `ScrapingJobs` - Logovi scraping poslova
- `ApplicationLogs` - Sistemski logovi

### Migracije
EF Core migracije se automatski primjenjuju pri pokretanju:
```bash
dotnet ef migrations add NewMigration
dotnet ef database update
```

## 📊 Logiranje i monitoring

### Database logging
Aplikacija automatski sprema logove u bazu:
- **Minimum level**: Information
- **Buffer size**: 50 zapisa
- **Flush interval**: 15 sekundi

### Scraping job logovi
Detaljno praćenje scraping poslova:
- Status izvršavanja
- Broj obrađenih trgovina i proizvoda
- Trajanje poslova
- Error handling i retry logika

### Docker logovi
```bash
docker-compose logs -f cijene-scraper
docker-compose logs -f cijene-scraper-db
```

## 🛡️ Sigurnost

### API ključevi
Za production environment obavezno konfigurirajte API ključeve:
```bash
curl -H "X-API-Key: vaš-api-ključ" http://localhost:8080/api/prices
```

### CORS
Trenutno omogućen za sve domene u development-u:
```csharp
.AllowAnyOrigin()
.AllowAnyMethod()
.AllowAnyHeader()
```

## 🚀 Performance optimizacije

### Caching strategije
- **Parquet format** - do 10x manja veličina datoteka
- **Asinkroni queue sustav** - ne blokira API zahtjeve
- **Database indexi** - optimizirani za česte upite
- **Connection pooling** - PostgreSQL optimizacije

### Skalabilnost
- **Background worker pattern** - jedan scraping job po času
- **Cancellation token support** - graceful shutdown
- **Memory-efficient** - streaming čitanje velikih CSV datoteka

## 🐛 Debugging

### Aplikacijski logovi
```bash
# Docker environment
docker-compose logs -f cijene-scraper

# Local development
dotnet run --verbosity detailed
```

### Baza podataka
```bash
# Pristup PostgreSQL konzoli
docker exec -it cijene-scraper-db-1 psql -U scraper_user -d cijene_scraper

# Backup baze
docker exec cijene-scraper-db-1 pg_dump -U scraper_user cijene_scraper > backup.sql
```

### Cache datoteke
```bash
# CSV format
ls -la cache/csv/
head cache/csv/konzum/2024-01-15.csv

# Parquet format
ls -la cache/parquet/
# Koristi Apache Arrow alate za čitanje
```

## 🔄 CI/CD

### Docker build
```bash
docker build -t cijene-scraper .
docker tag cijene-scraper:latest registry/cijene-scraper:v1.0
docker push registry/cijene-scraper:v1.0
```

### Zdravstveni provjeri
```bash
curl http://localhost:8080/health
# Očekivani odgovor: "OK"
```

## 📄 Licenca

Ovaj projekt je licenciran pod MIT licencom. Pogledajte [LICENSE.txt](LICENSE.txt) za više informacija.

## 🤝 Doprinosi

Dobrodošli su doprinosi! Molimo:

1. Fork repozitorij
2. Stvori feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit promjene (`git commit -m 'Add some AmazingFeature'`)
4. Push u branch (`git push origin feature/AmazingFeature`)
5. Otvori Pull Request

## 📞 Kontakt

Mario Kovač - [@mariokovac](https://github.com/mariokovac)

Project Link: [https://github.com/mariokovac/cijene-scraper](https://github.com/mariokovac/cijene-scraper)

---

⭐ Ako vam je ovaj projekt koristan, molimo dajte mu zvjezdicu!