# Testplan — manuel afprøvning

> Opdateret 2026-09-07 efter UI-overhaulen. Dækker forside, kundebooking og admin.
> Formålet er ikke at klikke alt igennem, men at bevise de fem ting der er svære at få rigtige.

---

## 0. Opsætning

```powershell
cd "C:\Users\Outlashed\Development\New folder\NFHotel"

# 1. Database: en PostgreSQL 16-instans du kan naa.
#    Hvordan den hostes er DevOps' omraade — projektet kender kun en connection string.
$env:NFHOTEL_CONNECTION = "Host=...;Port=5432;Database=nfhotel;Username=...;Password=..."

# 2. Skema
dotnet ef database update --project src\NFHotel.Infrastructure --startup-project src\NFHotel.Web

# 3. Testdata
psql $env:NFHOTEL_CONNECTION -f db\seed_dev.sql

# 4. Kør
dotnet run --project src\NFHotel.Web
```

Den samme connection string skal stå i `src\NFHotel.Web\appsettings.Development.json`.
Databasebrugeren skal kunne oprette extensions — første migration opretter `btree_gist`.

10 rum, 8 gæster, 12 bookinger. Alle datoer er relative til i dag, så check-in altid kan afprøves.

---

## 1. Automatiske tests først

```powershell
dotnet test
```

Forventet: **291 grønne** — 285 domænetests og 6 arkitekturtests.

De 6 arkitekturtests er værd at kigge på: de fejler hvis nogen kommer til at referere EF Core fra Application, giver Domain en afhængighed, eller lader en service returnere en domæneentitet i stedet for en DTO. Det er den slags der ellers først opdages tre uger senere.

---

## 2. Forsiden — `/`

Skal virke **uden** database. Stop containeren og genindlæs, hvis du vil bevise det:

Afbryd forbindelsen — stop instansen, eller peg connection stringen mod en port der ikke svarer.
Forsiden skal stadig svare normalt.

- [ ] Hero, tre sektioner, knap til `/book`
- [ ] Ingen sidemenu (det er `PublicLayout`)
- [ ] Vindue smalt ned til ~400px: intet vandret scroll

---

## 3. Kundens bookingflow — `/book`

**Det vigtigste forløb i hele systemet.** Det skriver rigtige data og rammer hele stakken: Blazor → ViewModel → Application → Domain → EF → PostgreSQL.

**Lykkelig vej:**

1. Ankomst i dag, afrejse om tre dage → antal nætter skal tælle med
2. Videre → ledige rum vises. **Rum 203 og 204 må ikke være med** — de er vedligehold og ude af drift (BR-C-02)
3. Vælg et rum → udfyld oplysninger → send
4. Kvittering med bookingnummer `FLZ-0000xx`
5. Kopiér adressen, åbn den i en ny fane → **samme kvittering** (den er bogmærkbar)

**Det du skal kigge efter på kvitteringen:** der står rum, datoer, nætter og status — men **hverken navn, e-mail, telefon eller pasnummer**. Det er BR-C-06, og det er bevidst: uden login er `FLZ-000123` trivielt at gætte, så siden må ikke lække persondata til den der gætter.

**Negative tests — disse skal blive afvist:**

| Prøv | Forventet |
|---|---|
| Afrejse før ankomst | Fejl, kan ikke gå videre |
| Ankomst i går | Fejl — fortiden er ikke bookbar (BR-44) |
| Tom e-mail, eller `anne` uden `@` og `.` | Feltfejl på det rigtige felt (BR-55) |
| Gå tilbage til trin 1 og skift dato | Indtastede oplysninger skal **ikke** være væk |

---

## 4. Admin — `/admin`

**Dashboard:** dagens ankomster, afrejser, rum der kræver rengøring. Tallene skal stemme med hvad du ser på undersiderne — de kommer fra samme kilde.

### `/admin/bookinger`

Seed-dataene er lavet så knapperne kan afprøves. Det er her tilstandsmaskinen bevises:

| Booking | Prøv | Forventet |
|---|---|---|
| Pending, ankomst i dag | **Check ind** | Virker — uden at bekræfte først. Det er B-01, walk-in |
| Pending, ankomst om 7 dage | — | **Check ind-knappen findes ikke.** For tidligt (BR-06) |
| CheckedIn | **Check ud** | Virker. Bekræft og Annullér findes ikke (BR-11) |
| CheckedOut | — | Ingen knapper. Slutstatus |
| Pending | **Annullér** | Bekræftelse først (BR-04), derefter status `Annulleret` — rækken slettes ikke (BR-13) |

**Vigtigt:** knapperne kommer fra `Actions` på hver booking, beregnet i domænet. Ser du en knap der giver en fejl når du trykker, er det en fejl værd at rapportere — så er UI og domæne uenige.

**Opret booking:** datoer → ledige rum → eksisterende gæst *eller* ny. Prøv begge veje.

**Omlæg booking:** vælg en `Pending`, flyt datoerne. Prøv at flytte den oven i en anden bookings periode på samme rum → skal afvises.

### `/admin/rum`

- [ ] Filtre på etage, størrelse, status
- [ ] Rum 104 er "rengøring i gang" → **Afslut rengøring** virker
- [ ] Rum 101 er allerede rent → **knappen findes ikke** (ingen grund til at afslutte en rengøring der ikke er i gang)
- [ ] Opret et rum. Prøv etage `0` → afvist (BR-98). Prøv et eksisterende værelsesnummer → afvist (BR-N-01)
- [ ] Rediger et rum: **der er intet statusfelt** — status ændres med sine egne knapper (A-08)

### `/admin/gaester`

- [ ] Opret en gæst **med pasnummer**. Det er den eneste vej til at se krypteringen virke
- [ ] Bekræft i databasen at det er krypteret:

```powershell
psql $env:NFHOTEL_CONNECTION -c "SELECT first_name, passport_number FROM guest WHERE passport_number IS NOT NULL;"
```

Feltet skal være ulæselig base64 — ikke det du skrev. Åbn samme gæst i UI'et: der står den rigtige værdi. Det er B-09.

- [ ] Søg på et fornavn, et efternavn og et land — alle tre skal give træf (BR-68)

---

## 5. Det UI'et ikke kan teste

```powershell
psql $env:NFHOTEL_CONNECTION -f db\constraint_check.sql
```

Fem inserts direkte mod databasen, uden om applikationen.

- **Test 1 skal fejle** med `ex_booking_room_period`
- **Test 2 til 5 skal give `INSERT 0 1`**

Test 4 er den vigtigste: en booking på rum 10 løber fire dage frem, men gæsten tjekkede ud i går. Rummet skal kunne genudlejes fra i dag. Fejler den, er A-01 brækket — og det var netop den fejl der blev fanget i designfasen.

Test 1 beviser at dobbeltbooking er **fysisk umulig**, også hvis to receptionister trykker samtidig. Det gamle WPF-system havde intet overlapstjek ved oprettelse overhovedet.

---

## 6. Robusthed

**Databasen forsvinder mens appen kører.** Stop instansen og genindlæs `/admin/rum`. Forventet: en neutral dansk fejlbesked inden for få sekunder — **ikke** en stack trace, og ikke en side der hænger i et minut. Start databasen igen; siden skal virke ved næste genindlæsning.

**To faner samtidig:** åbn `/admin/bookinger` i to faner og udfør handlinger i begge. Hver fane har sin egen tilstand, og hvert kald sin egen `DbContext`.

---

## 7. Hvad du IKKE kan teste endnu

- **Adgangskontrol.** `/admin/**` er åben for alle der kender adressen. Auth er udskudt (D-01 til D-03). Båndet øverst i admin siger det
- **Priser og omsætning.** `/admin/omsaetning` er en placeholder. `RoomSize` er en kategori uden pris
- **Mobilappen.** Ikke bygget. Rengøringsforløbet i `/admin/rum` er den flade den senere skal kalde
- **Persondata-sletning.** Retention (D-05) er ikke implementeret; X'erne er ikke fastsat

---

## 8. Fejl der er værd at rapportere

Ikke alt er lige interessant. Disse peger på noget reelt:

- En knap der giver en fejl når man trykker → UI og domæne er uenige om hvad der er lovligt
- En rå exception eller en engelsk fejlkode i browseren → fejlhåndteringen har et hul
- `constraint_check.sql` test 1 lykkes → constrainten er ikke aktiv, migrationen er ikke kørt
- `constraint_check.sql` test 4 fejler → `effective_end_date` er forkert
- Persondata på kvitteringssiden → BR-C-06 er brudt
- Et pasnummer i klartekst i databasen → B-09 er brudt
