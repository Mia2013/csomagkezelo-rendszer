import { afterEach, expect, it, vi } from "vitest";
import { deleteData, getData, instance, postData, putData } from "./apiCalls";

const originalAdapter = instance.defaults.adapter;
afterEach(() => {
  instance.defaults.adapter = originalAdapter;
  vi.restoreAllMocks();
});

const requests = [
  ["GET", () => getData("User/5")],
  ["POST", () => postData("Package", { size: "M" })],
  ["PUT", () => putData("Package/1/assignforpickup", { courierId: 5 })],
  ["DELETE", () => deleteData("CourierCar/1")],
];

it.each(requests)("%s mindig az aktuálisan tárolt tokent küldi, kilépés után vendég", async (method, request) => {
  const headers = [];
  instance.defaults.adapter = async (config) => {
    headers.push(config.headers.toJSON().Authorization);
    return { data: {}, status: 200, statusText: "OK", headers: {}, config };
  };
  localStorage.setItem("csomagkezelo_token", "first");
  await request();
  localStorage.setItem("csomagkezelo_token", "second");
  await request();
  localStorage.removeItem("csomagkezelo_token");
  await request();
  expect(headers).toEqual(["Bearer first", "Bearer second", undefined]);
});

it("a nyilvános követés az eltárolt hibás tokent sem küldi", async () => {
  localStorage.setItem("csomagkezelo_token", "invalid");
  const adapter = vi.fn(async (config) => ({
    data: {}, status: 200, statusText: "OK", headers: {}, config,
  }));
  instance.defaults.adapter = adapter;
  await getData("Package/track", { trackingNumber: "abc" }, true);
  expect(adapter.mock.calls[0][0].headers.toJSON()).not.toHaveProperty("Authorization");
  expect(localStorage.getItem("csomagkezelo_token")).toBe("invalid");
});
