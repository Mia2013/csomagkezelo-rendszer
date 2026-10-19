# Csomagkezelő rendszer

## Indítás

Szükséges: .NET 10 SDK, SQL Server Express LocalDB (`MSSQLLocalDB`), Node.js 24 és npm.
Visual Studióban a `backend/Csomagkuldo/Csomagkuldo.slnx` tartalmazza a backendet és tesztprojektjét.
A React frontend az alábbi külön paranccsal indul.

### 1. Backend

```powershell
dotnet tool restore
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet ef database update --project backend/Csomagkuldo
dotnet run --project backend/Csomagkuldo --launch-profile https
```

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

```powershell
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

A frontendtesztek Vitestet és React Testing Libraryt használnak. A komponenspróbákban az API válaszai helyettesítettek;
ezeket a böngészős, valódi backenddel végzett ellenőrzés egészíti ki.

A `docs` mappa helyi dokumentáció, a `.gitignore` kizárja az új fájljait.
Az aktuális működési leírás, követelmények és tesztjegyzőkönyv ott található.

mert mindig elfelejtem:

cd C:\...\csomagkezelo-rendszer-uj
dotnet run --project backend/Csomagkuldo --launch-profile https

cd C:\...\csomagkezelo-rendszer-uj\frontend
npm run dev
