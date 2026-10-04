# Csomagkezelő rendszer – 10.16-os etap

Egyetemi projekt, külön ASP.NET Core Web API backenddel és React + Vite frontenddel.
Az adatokat az Entity Framework Core tárolja SQL Server LocalDB-ben.

## Elkészült funkciók

- Regisztráció, JWT-alapú bejelentkezés és kijelentkezés a meglévő frontend továbbépítésével.
- Csomagelőjegyzés vendégként és bejelentkezve; belépéskor a szerver kapcsolja a feladóhoz a csomagot.
- Bővített űrlap: feladó/címzett neve, címe, telefonszáma, címzett e-mailje, érték, tömeg, méret, felvételi nap és idősáv.
- A backend UUID-t készít, és egy mentéssel létrehozza a csomagot és első `PreBooked` státuszbejegyzését.
- Nyilvános követés UUID alapján. A válasz csak az azonosítót, a legutóbbi státuszt és annak időpontját tartalmazza.
- Automatikus backend- és frontendtesztek.

A csomagérték egész forint, a tömeg egész gramm (1–30000 g). Méret: S/M/L.
Felvétel legkorábban a következő budapesti naptári napra kérhető, 09:00–12:00 vagy 12:00–16:00 idősávban.
Az eseményidők UTC-ben tárolódnak; a felület budapesti időt mutat.
Az e-mail megadása nem igazolja a címzett személyét: `RecipientId` egyelőre null.

## Indítás

Szükséges: .NET 10 SDK, SQL Server Express LocalDB (`MSSQLLocalDB`), Node.js 24 és npm.
Visual Studióban a `backend/Csomagkuldo/Csomagkuldo.slnx` tartalmazza a backendet és tesztprojektjét.
A React frontend az alábbi külön paranccsal indul.

### 1. Helyi JWT-kulcs

Ezen a gépen az `appsettings.Local.json` már elkészült. Nincs Gitben.
Másik gépen a projekt gyökerében PowerShellből egyszer futtasd:

```powershell
$keyFile = "backend/Csomagkuldo/appsettings.Local.json"
if (!(Test-Path $keyFile)) {
    $keyBytes = New-Object byte[] 48
    $keyGenerator = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    $keyGenerator.GetBytes($keyBytes)
    $keyGenerator.Dispose()
    @{ Jwt = @{ Key = [Convert]::ToBase64String($keyBytes) } } |
        ConvertTo-Json | Set-Content $keyFile -Encoding utf8
}
```

A helyi fájl csak Development környezetben töltődik be. Más környezetben `Jwt__Key`,
`Jwt__Issuer`, `Jwt__Audience` és `ConnectionStrings__DefaultConnection` környezeti változókkal állítható be az alkalmazás.
A kulcsot ne másold Gitbe. A tesztek külön, csak tesztelésre használt kulcsot állítanak be.

### 2. Backend

```powershell
dotnet tool restore
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet ef database update --project backend/Csomagkuldo
dotnet run --project backend/Csomagkuldo --launch-profile https
```

API: `https://localhost:7259/api`. OpenAPI: `https://localhost:7259/openapi/v1.json`.
Ha a helyi fejlesztői tanúsítvány még nincs megbízhatónak jelölve: `dotnet dev-certs https --trust`.
Az alkalmazás nem futtat automatikus adatbázis-migrációt induláskor.

### 3. Frontend, másik terminálban

```powershell
cd frontend
npm ci
npm run dev
```

Megnyitás: `http://localhost:5173`. A port szándékosan rögzített, mert ezt engedélyezi a backend CORS-beállítása.
Eltérő API-címhez a `VITE_API_URL` környezeti változó használható.

## API

| Művelet | Kérés | Válasz |
| --- | --- | --- |
| Regisztráció | `POST /api/Auth/register` | Sikerüzenet |
| Bejelentkezés | `POST /api/Auth/login` | `{ token }` |
| Előjegyzés | `POST /api/Package` | 201: `{ trackingNumber, status, createdAt }` |
| Követés | `GET /api/Package/track?trackingNumber=<uuid>` | 200: `{ trackingNumber, status, changedAt }` |

A mezőhibák 400-as, az ismeretlen UUID 404-es választ kap. A követéshez nincs szükség belépésre.
Előjegyzésnél a hibás/lejárt token 401-es válaszhoz vezet, nem vendégfeladáshoz.
A kliens nem állíthat státuszt, azonosítót, felhasználókapcsolatot vagy futárt.
Példakérések: `backend/Csomagkuldo/Csomagkuldo.http`.

A korábbi listázó, törlő és státuszállító csomagvégpontok nem elérhetők.
A `UserController` ideiglenesen `[NonController]`, így nem ad ki nyilvánosan felhasználói adatokat.
Raktárosi, futár- és adminműveletek, saját csomaglisták, csomagmódosítás és a teljes követési történet későbbi etapok.

## Tesztek

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
