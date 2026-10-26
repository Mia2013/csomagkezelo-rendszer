import { beforeEach, expect, it, vi } from "vitest";
import { getData, postData, putData } from "./apiCalls";
import { assignCourier, assignDeliveries, getCourierCapacity, getWarehouseData, receivePackage } from "./warehouseApi";

vi.mock("./apiCalls", () => ({ getData: vi.fn(), postData: vi.fn(), putData: vi.fn() }));
beforeEach(() => vi.resetAllMocks());

it("az öt raktári adatforrást a meglévő API-val kéri le", async () => {
  getData.mockImplementation((endpoint) => Promise.resolve([endpoint]));
  const data = await getWarehouseData();
  expect(data).toEqual({
    warehouse: ["Package/warehouse"], pickups: ["Package/unassignedPrebooked"],
    waiting: ["Package/unassignedWarehouse"], assigned: ["Package/assigned"], couriers: ["User/couriers"],
  });
});

it.each([["pickup", "assignforpickup"], ["delivery", "assignfordelivery"], ["reassign", "reassigncourier"]])(
  "%s műveletnél a megfelelő útvonalat és futárt küldi", async (kind, endpoint) => {
    await assignCourier(42, 11, kind);
    expect(putData).toHaveBeenCalledWith(`Package/42/${endpoint}`, { courierId: 11 });
  },
);

it("a bevételezés a csomag és a beszállító futár azonosítóját küldi", async () => {
  await receivePackage(42, 11);
  expect(putData).toHaveBeenCalledWith("Package/42/receivetowarehouse", { courierId: 11 });
});

it("az automatikus kiosztás nem küld kitalált csomag- vagy futárlistát", async () => {
  await assignDeliveries();
  expect(postData).toHaveBeenCalledWith("Package/autoassignfordelivery");
});

it("a kiválasztott futár kapacitását ellenőrzi", async () => {
  await getCourierCapacity(11);
  expect(getData).toHaveBeenCalledWith("CourierCar/courier/11/capacity");
});
