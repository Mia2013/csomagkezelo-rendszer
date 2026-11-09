using System;
using Csomagkuldo.Enums;
using Csomagkuldo.Models;

public class ShippingFee
{
   

    public int Id { get; set; }

    public PackageSize Type { get; set; }

    public string MaxSize { get; set; }

    public int RegisteredPrice { get; set; }

    public int UnregisteredPrice { get; set; }

    public virtual List<Package> Packages { get; set; } = new List<Package>();


}
