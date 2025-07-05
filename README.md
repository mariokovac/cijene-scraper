# Cijene Scraper

ASP.NET Core Web API projekt za scraping i pohranu cijena proizvoda iz maloprodajnih lanaca u Hrvatskoj.

## 🚀 Značajke

- **Web API** za pokretanje scraping zadataka
- **Parsiranje cijena** i informacija o proizvodima
- **Pohrana podataka** u PostgreSQL bazu (Entity Framework Core)
- **Keširanje podataka** u CSV i Parquet formatu
- **Docker podrška** s Docker Compose
- **Automatsko primjenjivanje** EF Core migracija pri pokretanju
- **Swagger UI** za testiranje API-ja

## 🛠️ Tehnologije

- **.NET 9** (ASP.NET Core)
- **Entity Framework Core** (Npgsql)
- **PostgreSQL** baza podataka
- **HtmlAgilityPack** za HTML parsiranje
- **CsvHelper** za CSV export
- **Parquet.Net** za Parquet format
- **ZstdSharp** za kompresiju
- **Docker & Docker Compose**
- **NSwag** (Swagger UI)

## 📋 Preduvjeti

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Docker](https://www.docker.com/) i [Docker Compose](https://docs.docker.com/compose/) (za Docker pokretanje)
- [PostgreSQL](https://www.postgresql.org/) (ako pokretate izvan Dockera)

## 🔧 Instalacija

### Kloniranje repozitorija

```bash
git clone https://github.com/mariokovac/cijene-scraper.git
cd cijene-scraper
```

### Konfiguracija

1. Prilagodite varijable okoline u `.env`:
```bash
POSTGRES_USER=scraper_user
POSTGRES_PASSWORD=scraper_password
POSTGRES_DB=cijene_scraper
```

2. Ažurirajte `appsettings.json` po potrebi (poveznica na bazu podataka).

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

## 📚 API Dokumentacija

### Health Check
```http
GET /health
```
Vraća `OK` ako je aplikacija u redu.

### Pokretanje scrapinga
```http
POST /api/scraper/start/{lanac}?date=YYYY-MM-DD
```

**Parametri:**
- `lanac` (obavezno): Naziv lanca (npr. `konzum`)
- `date` (opcionalno): Datum u formatu YYYY-MM-DD. Ako nije naveden, koristi se trenutni datum.

**Primjer:**
```http
POST http://localhost:8080/api/scraper/start/konzum?date=2025-07-04
```

### Dohvaćanje cijena
```http
GET /api/prices?dates=2025-07-04&dates=2025-07-05&take=100&chain=konzum
```

**Parametri:**
- `dates` (obavezno): Niz datuma u formatu YYYY-MM-DD
- `take` (opcionalno): Maksimalni broj rezultata (default: 100)
- `chain` (opcionalno): Naziv lanca za filtriranje

**Primjer:**
```http
GET http://localhost:8080/api/prices?dates=2025-07-04&dates=2025-07-05&chain=konzum
```

### Pronalaženje najjeftinijih lokacija
```http
GET /api/prices/CheapestLocation?barcode=1234567890123&date=2025-07-04
```

**Parametri:**
- `barcode` (obavezno): Barkod proizvoda
- `date` (opcionalno): Datum pretrage (default: danas)

**Primjer:**
```http
GET http://localhost:8080/api/prices/CheapestLocation?barcode=1234567890123
```

### Swagger UI
Dostupan na http://localhost:8080/swagger za interaktivno testiranje API-ja.



## 🔧 Dodavanje novog lanca

1. Implementirajte sučelje `ICrawler` u `Services/Crawlers/Chains/`:
```csharp
public class NoviLanacCrawler : ICrawler
{
    // Implementacija crawlera
}
```

2. Registrirajte novi crawler u `Program.cs`:
```csharp
builder.Services.AddTransient<ICrawler, NoviLanacCrawler>();
```

## 🗄️ Baza podataka

Projekt koristi PostgreSQL bazu podataka s Entity Framework Core. Migracije se automatski primjenjuju pri pokretanju aplikacije.

### Glavne tablice:
- `Chains` - Informacije o lancima
- `Stores` - Trgovine po lancima
- `ChainProducts` - Proizvodi po lancima
- `Prices` - Povijesne cijene proizvoda

## 📊 Keširanje podataka

Podaci se automatski keširaju u dva formata:
- **CSV** - Za jednostavno čitanje i analizu
- **Parquet** - Za efikasniju pohranu i analizu velikih količina podataka

## 🐛 Debugging

### Logovi
Aplikacija koristi standardni .NET logging. Logovi su dostupni u konzoli ili Docker logs:

```bash
docker-compose logs -f
```

### Baza podataka
Za pristup bazi podataka možete koristiti:

```bash
docker exec -it cijene-scraper-db-1 psql -U scraper_user -d cijene_scraper
```

## 📄 Licenca

Ovaj projekt je licenciran pod MIT licencom. Pogledajte [LICENSE.txt](LICENSE.txt) za više informacija.

## 📞 Kontakt

Mario Kovač - [@mariokovac](https://github.com/mariokovac)

Project Link: [https://github.com/mariokovac/cijene-scraper](https://github.com/mariokovac/cijene-scraper)

---

⭐ Ako vam je ovaj projekt koristan, molimo dajte mu zvjezdicu!