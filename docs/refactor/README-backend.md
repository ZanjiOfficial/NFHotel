# NFHotel 2.0

Personale-backoffice på web, porteret fra WPF. Blazor Server oven på Clean Architecture med
EF Core og PostgreSQL.

Arkitekturen og dens beslutninger står i `Architecture.md`. Denne fil handler kun om at få
det til at køre lokalt.

---

## Forudsætninger

- .NET SDK 9
- **En PostgreSQL 16-database du kan nå** — hvordan den hostes er DevOps' beslutning, ikke
  projektets. Applikationen kender kun en connection string.
- `psql` eller et andet SQL-værktøj, hvis du vil køre scripts i `db/`
- `dotnet-ef`, hvis du skal køre migrationer:

```bash
dotnet tool install --global dotnet-ef
```

---

## Kom i gang

### 1. Skaf en database

Projektet stiller ét krav: en **PostgreSQL 16**-instans og en connection string til den.
Hvordan den kører — container, lokal installation, managed hosting — er DevOps' område og
skal ikke stå i denne fil.

Databasen skal have en bruger med rettighed til at oprette extensions. Den første migration
opretter `btree_gist`, som bookingens exclusion constraint bygger på.

Sæt din connection string i `appsettings.Development.json` (se **Konfiguration** nedenfor).

### 2. Kør migrationerne

Designtidskonteksten læser sin connection string fra `NFHOTEL_CONNECTION` og falder
ellers tilbage til `Username=postgres;Password=postgres` — sæt derfor variablen:

```bash
export NFHOTEL_CONNECTION="Host=localhost;Port=5432;Database=nfhotel;Username=nfhotel;Password=nfhotel_dev"

dotnet ef database update \
  --project src/NFHotel.Infrastructure \
  --startup-project src/NFHotel.Web
```

Migrationen opretter selv `btree_gist`-extensionen og bookingens exclusion constraint
(B-03), så dobbeltbooking er umulig på databaseniveau.

### 3. Start appen

```bash
dotnet run --project src/NFHotel.Web
```

`http://localhost:5062` eller `https://localhost:7060` (se `Properties/launchSettings.json`).

`http://localhost:5062` — forside. `/book` er kundens bookingflow, `/admin` er personalets
backoffice.

### 4. Testdata

Databasen er tom efter migrationen, så alle lister starter tomme. Seed-scriptet giver
10 rum, 8 gæster og 12 bookinger med datoer relative til i dag, så check-in kan afprøves:

```bash
psql "$NFHOTEL_CONNECTION" -f db/seed_dev.sql
```

`db/constraint_check.sql` verificerer at exclusion constrainten faktisk virker — kør den
efter seed-scriptet på samme måde. Se `docs/Testplan.md` for hvad der er værd at afprøve.

---

## Konfiguration

**Har du lige klonet repoet?** `appsettings.Development.json` er med vilje ikke i git.
Kopiér skabelonen og udfyld den:

```bash
cp src/NFHotel.Web/appsettings.Development.json.example \
   src/NFHotel.Web/appsettings.Development.json
```

Generér din egen krypteringsnøgle:

```bash
openssl rand -base64 32
```

`appsettings.Development.json` indeholder tre ting appen nægter at starte uden:

| Nøgle | Betydning |
|---|---|
| `ConnectionStrings:HotelDatabase` | PostgreSQL på `localhost:5432` |
| `Encryption:PassportKey` | 32 bytes base64. Pasnumre krypteres at-rest med AES-256-GCM (B-09) |
| `HotelTime:TimeZoneId` | Hotellets tidszone. "I dag" beregnes herfra, aldrig fra `DateTime.Now` (A-05) |

Krypteringsnøglen og kodeordet i filen er **kun til udvikling**. I produktion kommer begge
fra en secret store — de må aldrig committes.

Mangler nøglen, eller er den ikke gyldig base64, stopper appen ved opstart i stedet for ved
den første gæsteopdatering. Det er med vilje.

---

## Byg og test

```bash
dotnet build
dotnet test
```

---

## Hvor tingene bor

```
src/
  NFHotel.Domain/          entiteter, værdiobjekter, regler. Nul afhængigheder
  NFHotel.Application/     use cases, DTOer, Result og fejlkoder
  NFHotel.Infrastructure/  EF Core, Npgsql, kryptering, ur. Eneste sted med SQL
  NFHotel.Web/             Blazor Server, feature-først, composition root
```

I `Web` ligger hver skærm i `Features/<Feature>/` som en `.razor` med markup og en
ViewModel med logikken. Komponenter kalder kun Application-interfaces — ingen `DbContext`,
ingen repositories, ingen forretningsregler (B-10).
