import { afterEach, expect, it, vi } from "vitest";
import { deleteData, getData, instance, postData, putData } from "./apiCalls";

const originalAdapter = instance.defaults.adapter;
afterEach(() => {
  instance.defaults.adapter = originalAdapter;
  vi.restoreAllMocks();
});

const requests = [
  { method: "GET", call: () => getData("Package/track", { trackingNumber: "abc" }) },
  { method: "POST", call: () => postData("Package", { size: "M" }) },
  { method: "PUT", call: () => putData("Package/1/assignfordelivery", { courierId: 10 }) },
  { method: "DELETE", call: () => deleteData("CourierCar/1", { confirmed: true }) },
];

for (const request of requests) {
  it.each([409, 500])(request.method + " kérésnél megőrzi a központi %s hibaválasz üzenetét és státuszát", async (status) => {
    const detail = status === 409
      ? "Az adatok közben megváltoztak. Frissítsd a listát!"
      : "A művelet nem sikerült. Próbáld újra később!";
    instance.defaults.adapter = vi.fn().mockRejectedValue({
      response: { status, data: { status, title: "Hiba", detail } },
    });

    await expect(request.call()).rejects.toMatchObject({ status, message: detail, errors: {} });
  });

  it(request.method + " kérésnél a sikeres válasz adatait változatlanul adja vissza", async () => {
    const data = { id: 1, message: "Sikeres művelet" };
    instance.defaults.adapter = async (config) => ({
      data, status: 200, statusText: "OK", headers: {}, config,
    });

    await expect(request.call()).resolves.toEqual(data);
  });
}

it("hálózati hibánál magyar üzenetet ad szerverválasz nélkül is", async () => {
  instance.defaults.adapter = vi.fn().mockRejectedValue(new Error("Network Error"));

  await expect(getData("Package/warehouse")).rejects.toMatchObject({
    message: "A kérés nem sikerült. Ellenőrizd a szerverkapcsolatot!",
    errors: {},
  });
});
