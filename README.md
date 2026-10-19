# Csomagkezelő rendszer

Egyetemi projekt: ASP.NET Core Web API backend (.NET 10) és React + Vite frontend.
## Indítás

## Szükséges eszközök

- .NET 10 SDK (vagy Visual Studio 2026, webes fejlesztés workloaddal)
- SQL Server Express LocalDB (`MSSQLLocalDB`)
- Node.js 24 LTS és npm

## Backend indítása

A projekt gyökeréből:

```powershell
dotnet tool restore
dotnet ef database update --project backend/Csomagkuldo
dotnet run --project backend/Csomagkuldo --launch-profile https
```

- Az API címe: `https://localhost:7259/api`
- Az első `database update` létrehozza a `Csomagkezelo_Uj` adatbázist LocalDB-ben.
- Ha a böngésző nem fogadja el a tanúsítványt: `dotnet dev-certs https --trust`

**Visual Studióból:** nyisd meg a `backend/Csomagkuldo/Csomagkuldo.slnx` fájlt, a Package Manager Console-ban futtasd az `Update-Database` parancsot, majd indítsd a projektet a **https** profillal.

## Frontend indítása

Egy másik terminálban:

API: `https://localhost:7259/api`. OpenAPI: `https://localhost:7259/openapi/v1.json`.
Ha a helyi fejlesztői tanúsítvány még nincs megbízhatónak jelölve: `dotnet dev-certs https --trust`.
Az alkalmazás nem futtat automatikus adatbázis-migrációt induláskor.

### 2. Frontend, másik terminálban

```powershell
cd frontend
npm install
npm run dev
```

Megnyitás: `http://localhost:5173`. A port szándékosan rögzített, mert ezt engedélyezi a backend CORS-beállítása.
Eltérő API-címhez a `VITE_API_URL` környezeti változó használható.

### 3. Tesztek

Ha a modellek változnak:

```powershell
dotnet ef migrations add <MigracioNeve> --project backend/Csomagkuldo
dotnet ef database update --project backend/Csomagkuldo
```
dotnet test backend/Csomagkuldo/Csomagkuldo.slnx
cd frontend
npm test
npm run lint
npm run build
```

A backendtesztek valódi SQL Server LocalDB-t használnak, futásonként külön
`Csomagkezelo_Tests_<uuid>` adatbázissal. Létrehozzák, migrálják, majd a végén törlik a saját adatbázisukat.
Névellenőrzés védi a fejlesztői adatbázist. A tesztesetek egymás után futnak; egy külön próba párhuzamos előjegyzéseket küld.
Az adatbázis nem EF InMemory helyettesítő, ezért az egyedi index és a mentés visszagörgetése is ellenőrzött.

2026. szeptember 30-án ellenőrizve:

- Backend fordítás: 0 hiba, 0 figyelmeztetés.
- Az első migráció sikeresen lefutott LocalDB-n, nincs migrációból kimaradt modellváltozás.
- 15 adatbázisos ellenőrzés sikeres: Identity-felhasználók és egész számú azonosítók, jelszókivonat és jelszóellenőrzés, ismétlődő e-mail elutasítása, feladó/címzett/futár kapcsolatok, visszafelé mutató listák, több futárfeladat, státusztörténet, vendégcsomagok, szerepkörök, raktárnapló és a futár feladatainak megőrzése törlési próbánál.
- A próbák ideiglenes ellenőrzőprogramban, visszagörgetett tranzakcióban futottak; nem maradtak tesztfiókok vagy tesztcsomagok az adatbázisban.
- HTTPS-indulás és az OpenAPI-végpont ellenőrizve: 200-as válasz.
- Frontend build és lint: sikeres. A változatlan frontendfájlokat egyező ideiglenes másolaton ellenőriztem.

## Következő fejlesztések

A bejelentkezési végpont, a jogosultságellenőrzés és az üzleti műveletek még hátravannak. Csomag létrehozásakor majd az első státuszbejegyzést is létre kell hozni: a lista önmagában nem biztosítja az UML szerinti legalább egy elemet. A React frontend a jelenlegi projekt része; a korábbi Blazor-tervtől ez technológiai eltérés.

A választott Identity-alapú modellhez a [Microsoft Identity dokumentációja](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/customize-identity-model?view=aspnetcore-10.0), az adatbázis bekötéséhez az [EF Core dokumentációja](https://learn.microsoft.com/en-us/ef/core/dbcontext-configuration/) ad támpontot.
A választott Identity-alapú modellhez a [Microsoft Identity dokumentációja](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/customize-identity-model?view=aspnetcore-10.0), az adatbázis bekötéséhez az [EF Core dokumentációja](https://learn.microsoft.com/en-us/ef/core/dbcontext-configuration/) ad támpontot.
A választott Identity-alapú modellhez a [Microsoft Identity dokumentációja](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/customize-identity-model?view=aspnetcore-10.0), az adatbázis bekötéséhez az [EF Core dokumentációja](https://learn.microsoft.com/en-us/ef/core/dbcontext-configuration/) ad támpontot.
