# API Dokumentacija

Detaljni vodi? za korištenje Cijene Scraper API-ja.

## ?? Autentifikacija

API koristi API klju?eve za autentifikaciju. Dodajte klju? u header:

```http
X-API-Key: vaš-api-klju?
```

**Napomena**: Ako je `ApiKey:Enabled` postavljen na `false` u konfiguraciji, autentifikacija se preska?e.

## ?? Base URL

```
http://localhost:8080
```

## ? Health Check

### GET /health

Provjera statusa aplikacije.

**Odgovor:**
```
OK
```

---

## ??? Scraper endpoints

### POST /api/scraper/start/{chain}

Pokretanje scraping posla za odre?eni lanac.

**Parametri:**
- `chain` (path, obavezno): Naziv lanca
  - Podržani lanci: `konzum`, `kaufland`, `plodine`, `spar`, `lidl`, `*` (svi lanci)
- `date` (query, opcionalno): Datum u formatu `YYYY-MM-DD`. Default: danas
- `force` (query, opcionalno): Prisilno pokretanje (prekida postoje?i posao). Default: `false`

**Primjeri:**
```http
POST /api/scraper/start/konzum
POST /api/scraper/start/konzum?date=2025-01-15
POST /api/scraper/start/*?force=true
```

**Odgovori:**
- **202 Accepted**: Posao je uspješno uvršten u red
- **409 Conflict**: Scraping posao ve? je pokrenut

### GET /api/scraper/status

Dohva?anje statusa najnovijih scraping poslova.

**Odgovor:**
```json
[
  {
    "id": 123,
    "chain": "konzum",
    "date": "2025-01-15",
    "startedAt": "2025-01-15T10:00:00Z",
    "completedAt": "2025-01-15T10:15:00Z",
    "initiatedBy": "API:192.168.1.100",
    "isForced": false,
    "priceChanges": 1250,
    "detailedLog": {
      "status": "Completed",
      "storesProcessed": 125,
      "productsFound": 15000,
      "durationMs": 900000,
      "errorMessage": null
    }
  }
]
```

### GET /api/scraper/logs

Dohva?anje detaljnih logova scraping poslova.

**Parametri:**
- `chain` (query, opcionalno): Filtriranje po lancu
- `take` (query, opcionalno): Broj rezultata. Default: 50

**Primjer:**
```http
GET /api/scraper/logs?chain=konzum&take=20
```

### GET /api/scraper/statistics

Statistike scraping poslova.

**Parametri:**
- `fromDate` (query, opcionalno): Od datuma
- `toDate` (query, opcionalno): Do datuma
- `chain` (query, opcionalno): Filtriranje po lancu

---

## ?? Price endpoints

### GET /api/prices

Dohva?anje cijena s filtriranjem.

**Parametri:**
- `dates` (query, obavezno): Niz datuma u formatu `YYYY-MM-DD`
- `take` (query, opcionalno): Maksimalni broj rezultata. Default: 100
- `chain` (query, opcionalno): Naziv lanca za filtriranje

**Primjer:**
```http
GET /api/prices?dates=2025-01-15&dates=2025-01-16&take=50&chain=konzum
```

**Odgovor:**
```json
[
  {
    "date": "2025-01-15",
    "chainName": "konzum",
    "storeName": "Ilica 1, 10000 Zagreb",
    "productName": "Coca Cola 0.5L",
    "price": 1.89,
    "specialPrice": null
  }
]
```

### GET /api/prices/ByBarcode

Cijene proizvoda po barkodu za odre?eni dan.

**Parametri:**
- `barcode` (query, obavezno): Barkod proizvoda
- `date` (query, opcionalno): Datum. Default: danas

**Primjer:**
```http
GET /api/prices/ByBarcode?barcode=1234567890123&date=2025-01-15
```

**Odgovor:**
```json
[
  {
    "date": "2025-01-15",
    "chainName": "konzum",
    "storeName": "Ilica 1, 10000 Zagreb",
    "productName": "Coca Cola 0.5L",
    "price": 1.89
  },
  {
    "date": "2025-01-15",
    "chainName": "kaufland",
    "storeName": "Avenue Mall, 10000 Zagreb",
    "productName": "Coca Cola 0.5L",
    "price": 1.75
  }
]
```

### GET /api/prices/CheapestLocation

Pronalaženje najjeftinijih lokacija za proizvod.

**Parametri:**
- `barcode` (query, obavezno): Barkod proizvoda
- `date` (query, opcionalno): Datum. Default: danas

**Primjer:**
```http
GET /api/prices/CheapestLocation?barcode=1234567890123
```

**Odgovor:**
```json
[
  {
    "chain": "kaufland",
    "date": "2025-01-15",
    "productName": "Coca Cola 0.5L",
    "address": "Avenue Mall",
    "postalCode": "10000",
    "city": "Zagreb",
    "price": 1.75
  }
]
```

### GET /api/prices/ByProductNamesGrouped

Cijene grupirane po lancima i nazivima proizvoda.

**Parametri:**
- `productNames` (query, obavezno): Lista naziva proizvoda za pretraživanje
- `city` (query, opcionalno): Filtriranje po gradu

**Primjer:**
```http
GET /api/prices/ByProductNamesGrouped?productNames=coca%20cola&productNames=pepsi&city=Zagreb
```

**Odgovor:**
```json
{
  "konzum": {
    "Coca Cola 0.5L": [
      {
        "date": "2025-01-15",
        "chainName": "konzum",
        "storeName": "Ilica 1, 10000 Zagreb",
        "productName": "Coca Cola 0.5L",
        "price": 1.89,
        "specialPrice": null
      }
    ]
  },
  "kaufland": {
    "Coca Cola 0.5L": [
      {
        "date": "2025-01-15",
        "chainName": "kaufland",
        "storeName": "Avenue Mall, 10000 Zagreb",
        "productName": "Coca Cola 0.5L",
        "price": 1.75,
        "specialPrice": null
      }
    ]
  }
}
```

### GET /api/prices/ByCodesNearby

Cijene proizvoda u blizini GPS koordinata.

**Parametri:**
- `codes` (query, obavezno): Lista Product.Id-jeva
- `latitude` (query, obavezno): Geografska širina
- `longitude` (query, obavezno): Geografska dužina
- `radiusKm` (query, opcionalno): Radijus pretrage u kilometrima. Default: 5.0

**Primjer:**
```http
GET /api/prices/ByCodesNearby?codes=123&codes=456&latitude=45.815&longitude=15.982&radiusKm=10
```

**Odgovor:**
```json
[
  {
    "productId": 123,
    "date": "2025-01-15",
    "chainName": "konzum",
    "storeName": "Ilica 1, 10000 Zagreb",
    "productName": "Coca Cola 0.5L",
    "price": 1.89,
    "specialPrice": null,
    "distanceKm": 2.3
  }
]
```

### GET /api/prices/SearchProducts

Pretraživanje proizvoda po nazivu ili marki.

**Parametri:**
- `q` (query, obavezno): Pretraživani pojam
- `datum` (query, opcionalno): Datum za statistike cijena. Default: danas
- `chains` (query, opcionalno): Lista lanaca za filtriranje

**Primjer:**
```http
GET /api/prices/SearchProducts?q=coca%20cola&chains=konzum&chains=kaufland
```

**Odgovor:**
```json
[
  {
    "id": 123,
    "barcode": "1234567890123",
    "brand": "Coca Cola",
    "name": "Coca Cola 0.5L",
    "chains": [
      {
        "chain": "konzum",
        "storeProductCode": "KON123",
        "name": "Coca Cola 0.5L",
        "brand": "Coca Cola",
        "category": "Napitci",
        "priceStatistics": {
          "minPrice": 1.75,
          "maxPrice": 1.99,
          "avgPrice": 1.85
        }
      }
    ]
  }
]
```

### GET /api/prices/Statistics

Statistike cijena po lancima i datumima.

**Odgovor:**
```json
[
  {
    "chainName": "konzum",
    "date": "2025-01-15",
    "numPrices": 15250
  },
  {
    "chainName": "kaufland",
    "date": "2025-01-15",
    "numPrices": 12800
  }
]
```

---

## ?? Response formati

### Standardni error odgovori

**400 Bad Request:**
```json
{
  "error": "Parametar 'barcode' je obavezan."
}
```

**401 Unauthorized:**
```json
{
  "error": "API klju? nije valjan ili nedostaje."
}
```

**404 Not Found:**
```json
{
  "error": "Resurs nije prona?en."
}
```

**409 Conflict:**
```json
{
  "error": "Scraping posao ve? je pokrenut."
}
```

**500 Internal Server Error:**
```json
{
  "error": "Došlo je do greške na serveru."
}
```

### Date format

Svi datumi koriste format `YYYY-MM-DD` (ISO 8601):
- `2025-01-15`
- `2025-12-31`

### Decimal format

Sve cijene su u decimalnom formatu s to?kom kao separatorom:
- `1.89`
- `125.50`
- `0.99`

---

## ?? Swagger UI

Za interaktivno testiranje svih endpointova, posjetite:

```
http://localhost:8080/swagger
```

Swagger UI omogu?uje:
- Pregled svih dostupnih endpointova
- Testiranje API poziva direktno iz browsera
- Automatska generacija zahtjeva
- Prikaz schematizma odgovora

---

## ?? Primjeri integracije

### cURL primjeri

```bash
# Health check
curl http://localhost:8080/health

# Pokretanje scraping-a s API klju?em
curl -X POST \
  -H "X-API-Key: vaš-api-klju?" \
  "http://localhost:8080/api/scraper/start/konzum?date=2025-01-15"

# Dohva?anje cijena
curl -H "X-API-Key: vaš-api-klju?" \
  "http://localhost:8080/api/prices?dates=2025-01-15&take=10&chain=konzum"

# Pretraživanje najjeftinijih lokacija
curl -H "X-API-Key: vaš-api-klju?" \
  "http://localhost:8080/api/prices/CheapestLocation?barcode=1234567890123"
```

### JavaScript/Node.js

```javascript
const API_BASE = 'http://localhost:8080';
const API_KEY = 'vaš-api-klju?';

const headers = {
  'X-API-Key': API_KEY,
  'Content-Type': 'application/json'
};

// Pokretanje scraping-a
async function startScraping(chain, date) {
  const response = await fetch(`${API_BASE}/api/scraper/start/${chain}?date=${date}`, {
    method: 'POST',
    headers
  });
  return response.text();
}

// Dohva?anje cijena
async function getPrices(dates, chain = null, take = 100) {
  const params = new URLSearchParams();
  dates.forEach(date => params.append('dates', date));
  if (chain) params.append('chain', chain);
  params.append('take', take);
  
  const response = await fetch(`${API_BASE}/api/prices?${params}`, { headers });
  return response.json();
}

// Pretraživanje proizvoda
async function searchProducts(query, chains = []) {
  const params = new URLSearchParams();
  params.append('q', query);
  chains.forEach(chain => params.append('chains', chain));
  
  const response = await fetch(`${API_BASE}/api/prices/SearchProducts?${params}`, { headers });
  return response.json();
}
```

### Python

```python
import requests
from datetime import date

API_BASE = 'http://localhost:8080'
API_KEY = 'vaš-api-klju?'

headers = {
    'X-API-Key': API_KEY,
    'Content-Type': 'application/json'
}

# Pokretanje scraping-a
def start_scraping(chain, scrape_date=None):
    if scrape_date is None:
        scrape_date = date.today().isoformat()
    
    response = requests.post(
        f'{API_BASE}/api/scraper/start/{chain}',
        params={'date': scrape_date},
        headers=headers
    )
    return response.text

# Dohva?anje cijena
def get_prices(dates, chain=None, take=100):
    params = {'dates': dates, 'take': take}
    if chain:
        params['chain'] = chain
    
    response = requests.get(f'{API_BASE}/api/prices', params=params, headers=headers)
    return response.json()

# Najjeftinija lokacija
def get_cheapest_location(barcode, scrape_date=None):
    params = {'barcode': barcode}
    if scrape_date:
        params['date'] = scrape_date
    
    response = requests.get(f'{API_BASE}/api/prices/CheapestLocation', params=params, headers=headers)
    return response.json()
```

---

## ?? Performance savjeti

### Optimizacija zahtjeva

1. **Koristite paginaciju**: Postavite razuman `take` parametar
2. **Filtrirajte po datumu**: Specificirajte to?ne datume umjesto širokih raspona
3. **Cache rezultate**: API ne implementira vlastiti cache, dodajte ga na klijentskoj strani
4. **Batch zahtjevi**: Koristite endpointove koji vra?aju više podataka odjednom

### Rate limiting

Trenutno nema implementiran rate limiting, ali preporu?uje se:
- Maksimalno 100 zahtjeva po minuti
- Maksimalno 1000 rezultata po zahtjevu
- Izbjegavanje simultanih scraping zahtjeva

### Monitoring

Pratite:
- Response vrijeme endpointova
- Success rate API poziva
- Availability Swagger UI-ja na `/swagger`
- Health check status na `/health`