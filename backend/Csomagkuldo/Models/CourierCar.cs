using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace Csomagkuldo.Models
{
	public class CourierCar
	{
		public int Id { get; set; }

		public int Capacity { get; set; }

		public int? CourierId { get; set; }

		public virtual User? Courier { get; set; }

		public string? LicensePlate { get; set; }

		public string? Manufacturer { get; set; }

		public string? Model { get; set; }

		public int Year { get; set; }

	}
}