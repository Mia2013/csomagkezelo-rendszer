import { beforeEach, expect, it, vi } from "vitest";
import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router";
import { AuthContext } from "../provider/AuthContext";
import { ROLES } from "../constants/constants";
import Warehouse from "./Warehouse";
import Nav from "../components/Nav";
import { assignCourier, assignDeliveries, getCourierCapacity, getWarehouseData, receivePackage } from "../api/warehouseApi";

vi.mock("../api/warehouseApi", () => ({
  assignCourier: vi.fn(), assignDeliveries: vi.fn(), getCourierCapacity: vi.fn(),
  getWarehouseData: vi.fn(), receivePackage: vi.fn(),
}));

const codes = [1, 2, 3, 4, 5, 6, 7].map((id) => `00000000-0000-4000-8000-${String(id).padStart(12, "0")}`);
const couriers = [
  { id: 11, lastName: "Kiss", firstName: "Anna" },
  { id: 12, lastName: "Tóth", firstName: "Béla" },
];
let data;

function parcel(id, currentStatus, courierId = null, flags = {}) {
  return {
    id, trackingNumber: codes[id - 1], currentStatus, courierId,
    senderName: `Feladó ${id}`, senderAddress: "Veszprém, Teszt utca 1.",
    recipientName: `Címzett ${id}`, recipientAddress: "Pápa, Minta utca 2.",
    size: "M", weight: 500, pickupDate: "2026-10-25T00:00:00", pickupSlot: "09:00-12:00",
    canReassign: false, canReceive: false, ...flags,
  };
}

function auth(role) {
  return {
    user: { id: 8, firstName: "Laci", role }, isAuthenticated: role !== ROLES.GUEST,
    isAdmin: role === ROLES.ADMIN, isWarehouseOperator: role === ROLES.WAREHOUSE_OPERATOR,
    logOut: vi.fn(),
  };
}

function show(role = ROLES.WAREHOUSE_OPERATOR, navbar = false) {
  return render(
    <MemoryRouter initialEntries={["/raktar"]}>
      <AuthContext.Provider value={auth(role)}>{navbar ? <Nav /> : <Warehouse />}</AuthContext.Provider>
    </MemoryRouter>,
  );
}

async function ready() {
  await screen.findByRole("link", { name: codes[1] });
}

async function chooseCourier(user, name = "Kiss Anna") {
  const dialog = within(screen.getByRole("dialog"));
  await user.click(dialog.getByRole("combobox", { name: "Futár" }));
  await user.click(await screen.findByRole("option", { name }));
  await waitFor(() => expect(dialog.getByRole("button", { name: "Mentés" })).toBeEnabled());
  return dialog;
}

beforeEach(() => {
  vi.resetAllMocks();
  const waiting = parcel(2, "AtWarehouse");
  const delivery = parcel(3, "AssignedForDelivery", 11, { canReassign: true });
  data = {
    warehouse: [waiting, delivery], pickups: [parcel(1, "PreBooked")], waiting: [waiting],
    assigned: [delivery, parcel(4, "InTransitToWarehouse", 11, { canReceive: true }),
      parcel(5, "AssignedForPickup", 11), parcel(6, "AssignedForPickup", 12, { canReassign: true }),
      parcel(7, "OutForDelivery", 12)],
    couriers,
  };
  getWarehouseData.mockResolvedValue(data);
  getCourierCapacity.mockResolvedValue({ isActive: true, capacity: 10, usedCapacity: 2, freeCapacity: 8 });
  assignCourier.mockResolvedValue("Kiosztva");
  receivePackage.mockResolvedValue("Bevételezve");
});

it.each([ROLES.GUEST, ROLES.CUSTOMER, ROLES.COURIER])("%s szerepkörrel nem kér raktári adatokat", (role) => {
  show(role);
  expect(screen.getByRole("alert")).toHaveTextContent(/raktáros/i);
  expect(getWarehouseData).not.toHaveBeenCalled();
});

it.each([ROLES.WAREHOUSE_OPERATOR, ROLES.ADMIN])("%s szerepkörrel betölti a raktárat", async (role) => {
  show(role);
  await ready();
  expect(screen.getByRole("heading", { name: "Raktárkezelés" })).toBeInTheDocument();
  expect(screen.getByRole("tab", { name: "Raktári készlet (2)" })).toBeInTheDocument();
  expect(screen.getByRole("tab", { name: "Felvételi igények (3)" })).toBeInTheDocument();
  expect(screen.getByRole("tab", { name: "Futároknál (2)" })).toBeInTheDocument();
  expect(screen.getByRole("tab", { name: "Kiszállításra vár (1)" })).toBeInTheDocument();
  expect(screen.getByText("4 helyegység foglalt")).toBeInTheDocument();
});

it.each([ROLES.WAREHOUSE_OPERATOR, ROLES.ADMIN, ROLES.CUSTOMER])("a Raktár menüpont szerepkör alapján jelenik meg: %s", (role) => {
  show(role, true);
  const link = screen.queryByRole("link", { name: "Raktár" });
  if (role === ROLES.CUSTOMER) expect(link).not.toBeInTheDocument();
  else expect(link).toHaveAttribute("href", "/raktar");
});

it("a kiválasztott listában csomagazonosítóra keres", async () => {
  const user = userEvent.setup();
  show(); await ready();
  await user.type(screen.getByRole("textbox", { name: "Keresés a listában" }), codes[2]);
  expect(screen.getByRole("link", { name: codes[2] })).toBeInTheDocument();
  expect(screen.queryByRole("link", { name: codes[1] })).not.toBeInTheDocument();
});

it("kiosztás után a backend friss listáját mutatja", async () => {
  const user = userEvent.setup();
  const updated = structuredClone(data);
  updated.pickups = [];
  updated.assigned.push(parcel(1, "AssignedForPickup", 11, { canReassign: true }));
  getWarehouseData.mockResolvedValueOnce(data).mockResolvedValue(updated);
  show(); await ready();
  await user.click(screen.getByRole("tab", { name: /^Felvételi igények/ }));
  await user.click(screen.getByRole("button", { name: `Felvétel kiosztása: ${codes[0]}` }));
  const dialog = await chooseCourier(user);
  await user.click(dialog.getByRole("button", { name: "Mentés" }));
  await waitFor(() => expect(assignCourier).toHaveBeenCalledWith(1, 11, "pickup"));
  await waitFor(() => expect(getWarehouseData).toHaveBeenCalledTimes(2));
  expect(await screen.findByRole("button", { name: `Futár módosítása: ${codes[0]}` })).toBeInTheDocument();
  expect(screen.queryByRole("button", { name: `Felvétel kiosztása: ${codes[0]}` })).not.toBeInTheDocument();
});

it("kézzel kiosztja a raktári kiszállítást", async () => {
  const user = userEvent.setup();
  show(); await ready();
  await user.click(screen.getByRole("button", { name: `Kiszállítás kiosztása: ${codes[1]}` }));
  const dialog = await chooseCourier(user);
  await user.click(dialog.getByRole("button", { name: "Mentés" }));
  await waitFor(() => expect(assignCourier).toHaveBeenCalledWith(2, 11, "delivery"));
});

it("indulás előtt másik futárhoz rendeli a feladatot", async () => {
  const user = userEvent.setup();
  show(); await ready();
  await user.click(screen.getByRole("button", { name: `Futár módosítása: ${codes[2]}` }));
  const dialog = await chooseCourier(user, "Tóth Béla");
  await user.click(dialog.getByRole("button", { name: "Mentés" }));
  await waitFor(() => expect(assignCourier).toHaveBeenCalledWith(3, 12, "reassign"));
});

it("zárolt felvételhez nem kínál átrendelést", async () => {
  const user = userEvent.setup();
  show(); await ready();
  await user.click(screen.getByRole("tab", { name: /^Felvételi igények/ }));
  expect(screen.queryByRole("button", { name: `Futár módosítása: ${codes[4]}` })).not.toBeInTheDocument();
  expect(screen.getByRole("button", { name: `Futár módosítása: ${codes[5]}` })).toBeInTheDocument();
});

it("a tényleges beszállító futárral rögzíti a bevételezést", async () => {
  const user = userEvent.setup();
  show(); await ready();
  await user.click(screen.getByRole("tab", { name: /^Futároknál/ }));
  expect(screen.queryByRole("button", { name: `Bevételezés: ${codes[6]}` })).not.toBeInTheDocument();
  await user.click(screen.getByRole("button", { name: `Bevételezés: ${codes[3]}` }));
  const dialog = within(screen.getByRole("dialog"));
  expect(dialog.getByText("Beszállító futár: Kiss Anna")).toBeInTheDocument();
  await user.click(dialog.getByRole("button", { name: "Bevételezés" }));
  await waitFor(() => expect(receivePackage).toHaveBeenCalledWith(4, 11));
  await waitFor(() => expect(getWarehouseData).toHaveBeenCalledTimes(2));
});

it("futárválasztás nélkül nem enged menteni", async () => {
  const user = userEvent.setup();
  show(); await ready();
  await user.click(screen.getByRole("button", { name: `Kiszállítás kiosztása: ${codes[1]}` }));
  expect(within(screen.getByRole("dialog")).getByRole("button", { name: "Mentés" })).toBeDisabled();
  expect(assignCourier).not.toHaveBeenCalled();
});

it("kevés autóhely esetén nem engedi a kiosztást", async () => {
  const user = userEvent.setup();
  getCourierCapacity.mockResolvedValue({ isActive: true, capacity: 10, freeCapacity: 1 });
  show(); await ready();
  await user.click(screen.getByRole("button", { name: `Kiszállítás kiosztása: ${codes[1]}` }));
  const dialog = within(screen.getByRole("dialog"));
  await user.click(dialog.getByRole("combobox", { name: "Futár" }));
  await user.click(await screen.findByRole("option", { name: "Kiss Anna" }));
  expect(await dialog.findByText(/A csomag nem fér be/)).toBeInTheDocument();
  expect(dialog.getByRole("button", { name: "Mentés" })).toBeDisabled();
  expect(assignCourier).not.toHaveBeenCalled();
});

it("autó nélküli futárnál megmutatja a backend hibáját", async () => {
  const user = userEvent.setup();
  getCourierCapacity.mockRejectedValue(new Error("A futárhoz nincs autó rendelve."));
  show(); await ready();
  await user.click(screen.getByRole("button", { name: `Kiszállítás kiosztása: ${codes[1]}` }));
  const dialog = within(screen.getByRole("dialog"));
  await user.click(dialog.getByRole("combobox", { name: "Futár" }));
  await user.click(await screen.findByRole("option", { name: "Kiss Anna" }));
  expect(await dialog.findByRole("alert")).toHaveTextContent("A futárhoz nincs autó rendelve.");
  expect(dialog.getByRole("button", { name: "Mentés" })).toBeDisabled();
});

it.each([0, 1])("az automatikus kiosztás a kiosztott és a várakozó csomagokat is megmutatja (%s kiosztott)", async (assignedCount) => {
  const user = userEvent.setup();
  assignDeliveries.mockResolvedValue({
    assigned: assignedCount ? [{ id: 2, trackingNumber: codes[1], courierId: 11 }] : [],
    unassigned: [{ id: 40, trackingNumber: "varakozo-csomag", reason: "Nincs elegendő szabad hely." }],
  });
  show(); await ready();
  await user.click(screen.getByRole("button", { name: "Automatikus kiosztás" }));
  await user.click(within(screen.getByRole("dialog")).getByRole("button", { name: "Kiosztás indítása" }));
  expect(await screen.findByRole("heading", { name: "Kiosztás eredménye" })).toBeInTheDocument();
  expect(screen.getByText(`Kiosztva: ${assignedCount} csomag. Kiosztatlan maradt: 1 csomag.`)).toBeInTheDocument();
  expect(screen.getByText("Nincs elegendő szabad hely.")).toBeInTheDocument();
  expect(assignDeliveries).toHaveBeenCalledTimes(1);
});

it("várakozó kiszállítás nélkül letiltja az automatikus kiosztást", async () => {
  data.waiting = [];
  show(); await ready();
  expect(screen.getByRole("button", { name: "Automatikus kiosztás" })).toBeDisabled();
});

it("409-es ütközésnél nem jelez sikert és frissíti a listákat", async () => {
  const user = userEvent.setup();
  assignCourier.mockRejectedValue(Object.assign(new Error("A feladat közben megkezdődött."), { status: 409 }));
  show(); await ready();
  await user.click(screen.getByRole("button", { name: `Futár módosítása: ${codes[2]}` }));
  const dialog = await chooseCourier(user, "Tóth Béla");
  await user.click(dialog.getByRole("button", { name: "Mentés" }));

  await waitFor(() => expect(dialog.getByText("A feladat közben megkezdődött.")).toBeInTheDocument());
  expect(screen.queryByText("A futárt sikeresen módosítottuk.")).not.toBeInTheDocument();
  await waitFor(() => expect(getWarehouseData).toHaveBeenCalledTimes(2));
});

it("kettős beküldésből csak egy mentési kérést indít", async () => {
  const user = userEvent.setup();
  let finish;
  assignCourier.mockReturnValue(new Promise((resolve) => { finish = resolve; }));
  show(); await ready();
  await user.click(screen.getByRole("button", { name: `Kiszállítás kiosztása: ${codes[1]}` }));
  const dialog = await chooseCourier(user);
  const form = dialog.getByRole("button", { name: "Mentés" }).closest("form");
  fireEvent.submit(form);
  fireEvent.submit(form);
  expect(assignCourier).toHaveBeenCalledTimes(1);
  finish("Siker");
  expect(await screen.findByText("A feladatot kiosztottuk a futárnak.")).toBeInTheDocument();
});

it("betöltés alatt nem enged módosító műveletet", () => {
  getWarehouseData.mockReturnValue(new Promise(() => {}));
  show();
  expect(screen.getByRole("progressbar")).toBeInTheDocument();
  expect(screen.getByRole("button", { name: "Automatikus kiosztás" })).toBeDisabled();
  expect(screen.getByRole("button", { name: "Frissítés" })).toBeDisabled();
});

it("üres készletnél érthető üres állapotot mutat", async () => {
  getWarehouseData.mockResolvedValue({ warehouse: [], pickups: [], waiting: [], assigned: [], couriers: [] });
  show();
  expect(await screen.findByRole("heading", { name: "Nem található csomag." })).toBeInTheDocument();
});

it("betöltési hiba után újra lehet kérni az adatokat", async () => {
  const user = userEvent.setup();
  getWarehouseData.mockRejectedValueOnce(new Error("A szerver nem érhető el.")).mockResolvedValue(data);
  show();
  expect(await screen.findByRole("alert")).toHaveTextContent("A szerver nem érhető el.");
  await user.click(screen.getByRole("button", { name: "Frissítés" }));
  await ready();
  expect(screen.queryByText("A szerver nem érhető el.")).not.toBeInTheDocument();
});

it("lejárt belépésnél új bejelentkezést kér", async () => {
  getWarehouseData.mockRejectedValue(Object.assign(new Error("Unauthorized"), { status: 401 }));
  show();
  expect(await screen.findByRole("alert")).toHaveTextContent("A munkamenet lejárt.");
  expect(screen.getByRole("link", { name: "Bejelentkezés" })).toHaveAttribute("href", "/#belepes");
});

it("gyors futárcserénél nem mutatja a korábbi választás késői kapacitását", async () => {
  const user = userEvent.setup();
  let finishFirst;
  const firstCapacity = new Promise((resolve) => { finishFirst = resolve; });
  getCourierCapacity.mockImplementation((id) => id === 11
    ? firstCapacity
    : Promise.resolve({ isActive: true, capacity: 10, freeCapacity: 8 }));
  show(); await ready();
  await user.click(screen.getByRole("button", { name: `Kiszállítás kiosztása: ${codes[1]}` }));
  const dialog = within(screen.getByRole("dialog"));
  await user.click(dialog.getByRole("combobox", { name: "Futár" }));
  await user.click(await screen.findByRole("option", { name: "Kiss Anna" }));
  const selected = await chooseCourier(user, "Tóth Béla");
  finishFirst({ isActive: true, capacity: 10, freeCapacity: 0 });
  await waitFor(() => expect(selected.getByRole("button", { name: "Mentés" })).toBeEnabled());
  await user.click(selected.getByRole("button", { name: "Mentés" }));
  await waitFor(() => expect(assignCourier).toHaveBeenCalledWith(2, 12, "delivery"));
});

it("ismeretlen csomagméretnél nem enged kiosztást", async () => {
  const user = userEvent.setup();
  data.waiting[0].size = "XL";
  show(); await ready();
  await user.click(screen.getByRole("button", { name: `Kiszállítás kiosztása: ${codes[1]}` }));
  const dialog = within(screen.getByRole("dialog"));
  expect(dialog.getByText(/A csomag mérete ismeretlen/)).toBeInTheDocument();
  expect(dialog.getByRole("button", { name: "Mentés" })).toBeDisabled();
  expect(assignCourier).not.toHaveBeenCalled();
});

it("bevételezés megszakításakor nem küld módosító kérést", async () => {
  const user = userEvent.setup();
  show(); await ready();
  await user.click(screen.getByRole("tab", { name: /^Futároknál/ }));
  await user.click(screen.getByRole("button", { name: `Bevételezés: ${codes[3]}` }));
  await user.click(within(screen.getByRole("dialog")).getByRole("button", { name: "Mégse" }));
  expect(receivePackage).not.toHaveBeenCalled();
  expect(getWarehouseData).toHaveBeenCalledTimes(1);
});
it("a futárok mellett a szabad helyet mutatja a teljes kapacitás helyett, a nullát is", async () => {
  const user = userEvent.setup();
  getCourierCapacity.mockImplementation((id) => Promise.resolve({
    isActive: true, capacity: id === 11 ? 20 : 50, freeCapacity: id === 11 ? 17 : 0,
  }));
  show(); await ready();
  await user.click(screen.getByRole("button", { name: `Kiszállítás kiosztása: ${codes[1]}` }));
  await user.click(within(screen.getByRole("dialog")).getByRole("combobox", { name: "Futár" }));
  await waitFor(() => {
    expect(screen.getByRole("option", { name: "Kiss Anna" })).toHaveTextContent("17 helyegység szabad");
    expect(screen.getByRole("option", { name: "Tóth Béla" })).toHaveTextContent("0 helyegység szabad");
  });
});

it("egy futár kapacitáshibája mellett a többi futár szabad helye továbbra is megjelenik", async () => {
  const user = userEvent.setup();
  getCourierCapacity.mockImplementation((id) => id === 11
    ? Promise.reject(new Error("A futárhoz nincs autó rendelve."))
    : Promise.resolve({ isActive: true, capacity: 22, freeCapacity: 19 }));
  show(); await ready();
  await user.click(screen.getByRole("button", { name: `Kiszállítás kiosztása: ${codes[1]}` }));
  await user.click(within(screen.getByRole("dialog")).getByRole("combobox", { name: "Futár" }));
  await waitFor(() => {
    expect(screen.getByRole("option", { name: "Kiss Anna" })).toHaveTextContent("Nem érhető el");
    expect(screen.getByRole("option", { name: "Tóth Béla" })).toHaveTextContent("19 helyegység szabad");
  });
});

it("nem vár a lassú futárlekérdezésre a többi kapacitás megjelenítésével", async () => {
  const user = userEvent.setup();
  getCourierCapacity.mockImplementation((id) => id === 11
    ? new Promise(() => {})
    : Promise.resolve({ isActive: true, capacity: 22, freeCapacity: 22 }));
  show(); await ready();
  await user.click(screen.getByRole("button", { name: `Kiszállítás kiosztása: ${codes[1]}` }));
  await user.click(within(screen.getByRole("dialog")).getByRole("combobox", { name: "Futár" }));
  await waitFor(() => expect(screen.getByRole("option", { name: "Tóth Béla" })).toHaveTextContent("22 helyegység szabad"));
  expect(screen.getByRole("option", { name: "Kiss Anna" })).toHaveTextContent("Betöltés…");
});

it("a futárlista újbóli megnyitásakor frissíti a szabad helyeket", async () => {
  const user = userEvent.setup();
  let freeCapacity = 8;
  getCourierCapacity.mockImplementation(() => Promise.resolve({ isActive: true, capacity: 20, freeCapacity }));
  show(); await ready();
  await user.click(screen.getByRole("button", { name: `Kiszállítás kiosztása: ${codes[1]}` }));
  const select = within(screen.getByRole("dialog")).getByRole("combobox", { name: "Futár" });
  await user.click(select);
  await waitFor(() => expect(screen.getByRole("option", { name: "Kiss Anna" })).toHaveTextContent("8 helyegység szabad"));
  await user.keyboard("{Escape}");
  freeCapacity = 3;
  await user.click(select);
  await waitFor(() => expect(screen.getByRole("option", { name: "Kiss Anna" })).toHaveTextContent("3 helyegység szabad"));
});
