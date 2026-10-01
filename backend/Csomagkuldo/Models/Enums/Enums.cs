namespace Csomagkuldo.Enums
{
    public enum UserRole
    {
        Customer,
        Courier,
        WarehouseOperator,
        Admin
    }

    public enum PackageStatus
    {
        PreBooked,
        InTransitToWarehouse,
        AtWarehouse,
        AssignedToCourier,
        Delivered
    }

    public enum CourierTaskType
    {
        Pickup,
        Delivery
        //esetleg később egy visszaküldták vagy átvétel megtagadva még jöhet ide
    }

    public enum CourierTaskStatus
    {
        Assigned,
        InProgress,
        Completed
    }
}
