export const ROLES = {
    GUEST: "Guest",
    CUSTOMER: "Customer",
    ADMIN: "Admin",
    COURIER: "Courier",
    WAREHOUSE_OPERATOR: "WarehouseOperator",
};

export const ALL_ROLES = Object.values(ROLES);

export const PACKAGE_STATUS_LABELS = {
    PreBooked: "Előjegyezve",
    AssignedForPickup: "Felvételre kiosztva",
    InTransitToWarehouse: "Úton a raktárba",
    AtWarehouse: "Raktárban",
    AssignedForDelivery: "Kézbesítésre kiosztva",
    OutForDelivery: "Kiszállítás alatt",
    Delivered: "Kézbesítve",
    FailedDelivery: "Sikertelen kézbesítés",
};

export const PACKAGE_STATUS_COLORS = {
    PreBooked: "default",
    AssignedForPickup: "info",
    InTransitToWarehouse: "info",
    AtWarehouse: "warning",
    AssignedForDelivery: "info",
    OutForDelivery: "info",
    Delivered: "success",
    FailedDelivery: "error",
};

export const EXPIRED_JWT_STATUS = 440;