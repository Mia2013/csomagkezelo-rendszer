import { getData, postData, putData } from "./apiCalls";

export async function getWarehouseData() {
  const [warehouse, pickups, waiting, assigned, couriers] = await Promise.all([
    getData("Package/warehouse"),
    getData("Package/unassignedPrebooked"),
    getData("Package/unassignedWarehouse"),
    getData("Package/assigned"),
    getData("User/couriers"),
  ]);
  return { warehouse, pickups, waiting, assigned, couriers };
}

export function assignCourier(packageId, courierId, kind) {
  const actions = {
    pickup: "assignforpickup",
    delivery: "assignfordelivery",
    reassign: "reassigncourier",
  };
  return putData(`Package/${packageId}/${actions[kind]}`, { courierId });
}

export function receivePackage(packageId, courierId) {
  return putData(`Package/${packageId}/receivetowarehouse`, { courierId });
}

export function assignDeliveries() {
  return postData("Package/autoassignfordelivery");
}

export function getCourierCapacity(courierId) {
  return getData(`CourierCar/courier/${courierId}/capacity`);
}
