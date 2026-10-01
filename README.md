# Csomagkezelő rendszer

Egyetemi projekt: ASP.NET Core Web API backend (.NET 10) és React + Vite frontend.

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

```powershell
cd frontend
npm install
npm run dev
```

A frontend a `http://localhost:5173` címen fut. A backend csak erről a címről fogad kéréseket (CORS), ezért a portot ne változtasd meg.

## Adatbázis módosítása

Ha a modellek változnak:

```powershell
dotnet ef migrations add <MigracioNeve> --project backend/Csomagkuldo
dotnet ef database update --project backend/Csomagkuldo
```

