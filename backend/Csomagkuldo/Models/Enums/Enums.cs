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
        AssignedForPickup,
        InTransitToWarehouse,
        AtWarehouse,
        AssignedForDelivery,
        OutForDelivery,
        Delivered,
        FailedDelivery
    }


    public enum ShippingFeeType
    {
        Small,
        Medium,
        Large
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
