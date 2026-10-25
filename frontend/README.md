# Frontend

React + Vite + Material UI. Indítás: `npm ci`, majd `npm run dev`.
Az alkalmazás címe http://localhost:5173, a backend alapcíme https://localhost:7259/api.
A teljes indítási útmutató a projekt gyökérmappájának README.md fájljában van.

- `src/pages/pages.js`: oldalak és menüpontok.
- `src/provider/AuthProvider.jsx`: bejelentkezési állapot, regisztráció és kijelentkezés.
- `src/api/apiCalls.js`: backendhívások és mezőhibák feldolgozása.
- `src/components/PackageForm.jsx`: kibővített csomagelőjegyzési űrlap.
- `src/pages/CreatePackage.jsx`: előjegyzés, visszakapott UUID, másolás és követési link.
- `src/pages/TrackPackage.jsx`: nyilvános követés, legutóbbi státusz és időpont.

Ellenőrzés: `npm test`, `npm run lint`, `npm run build`.
A böngészőben tárolt token csak a kliens állapotát segíti; a jogosultságot a backend ellenőrzi.
