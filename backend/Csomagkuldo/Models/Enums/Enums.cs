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
        AssignedForPickup,
        AssignedForDelivery,
        Delivered
    }

    public enum CourierTaskType
    {
        Pickup = 0,
        Delivery = 1
        
    }

    public enum CourierTaskStatus
    {
        Assigned,
        InProgress,
        Completed
    }
}
