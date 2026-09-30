namespace Csomagkuldo.Enums
{
    public enum UserRole
    {
        Customer = 0,
        Courier = 1,
        WarehouseOperator = 2,
        Admin = 3
    }

    public enum PackageStatus
    {
        PreBooked = 0,
        InTransitToWarehouse = 1,
        AtWarehouse = 2,
        AssignedToCourier = 3,
        Delivered = 4
    }

    public enum CourierTaskType
    {
        Pickup = 0,
        Delivery = 1
        //esetleg később egy visszaküldták vagy átvétel megtagadva még jöhet ide
    }

    public enum CourierTaskStatus
    {
        Assigned = 0,
        InProgress = 1,
        Completed = 2
    }
}
