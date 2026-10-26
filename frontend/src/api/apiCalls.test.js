import { afterEach, expect, it, vi } from "vitest";
import { getData, instance, postData } from "./apiCalls";

// Régi megoldás:
// afterEach(() => vi.restoreAllMocks());
// Új megoldás:
const originalAdapter = instance.defaults.adapter;
afterEach(() => {
  instance.defaults.adapter = originalAdapter;
  vi.restoreAllMocks();
});

it("nyilvános kereséshez nem küld tokent", async () => {
  localStorage.setItem("csomagkezelo_token", "expired");
  const get = vi.spyOn(instance, "get").mockResolvedValue({ data: {} });
  await getData("Package/track", { trackingNumber: "abc" }, true);
  expect(get).toHaveBeenCalledWith("Package/track", {
    params: { trackingNumber: "abc" }, headers: { Authorization: null },
  });
});

it("előjegyzéshez elküldi a meglévő tokent", async () => {
  localStorage.setItem("csomagkezelo_token", "token");
  const post = vi.spyOn(instance, "post").mockResolvedValue({ data: {} });
  await postData("Package", { size: "M" });
  expect(post).toHaveBeenCalledWith("Package", { size: "M" }, { headers: { Authorization: "Bearer token" } });
});

it("a backend mezőhibáit megtartja", async () => {
  // Régi megoldás:
  // vi.spyOn(instance, "post").mockRejectedValue({ response: { status: 400, data: { errors: { PickupDate: ["Túl korai dátum."] } } } });
  // Új megoldás:
  instance.defaults.adapter = vi.fn().mockRejectedValue({
    response: { status: 400, data: { errors: { PickupDate: ["Túl korai dátum."] } } },
  });
  await expect(postData("Package", {})).rejects.toMatchObject({ status: 400, errors: { pickupDate: "Túl korai dátum." } });
});
