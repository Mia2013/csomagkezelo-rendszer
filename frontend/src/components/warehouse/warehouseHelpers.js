export const statusLabels = {
  PreBooked: "Előjegyezve",
  AssignedForPickup: "Felvételre kiosztva",
  InTransitToWarehouse: "Úton a raktárba",
  AtWarehouse: "Raktárban",
  AssignedForDelivery: "Kiszállításra kiosztva",
  OutForDelivery: "Kiszállítás alatt",
  Delivered: "Kézbesítve",
  FailedDelivery: "Sikertelen kézbesítés",
};

export function getCourierName(couriers, id) {
  const courier = couriers.find((item) => item.id === id);
  const name = courier ? `${courier.lastName} ${courier.firstName}`.trim() : "";
  return name || (id ? `Futár #${id}` : "Nincs kiosztva");
}

export function getPackageSpace(size) {
  switch (size) {
    case "S": return 1;
    case "M": return 2;
    case "L": return 3;
    default: return 0;
  }
}
