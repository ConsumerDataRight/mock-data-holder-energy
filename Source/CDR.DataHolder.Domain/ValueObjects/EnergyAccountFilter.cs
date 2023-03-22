using System;

namespace CDR.DataHolder.Domain.ValueObjects
{
	public class EnergyAccountFilter
	{
		public EnergyAccountFilter(string[] allowedAccountIds)
		{
			AllowedAccountIds = allowedAccountIds;
		}		
		public bool? IsOwned { get; set; }
		public string ProductCategory { get; set; }
		public string OpenStatus { get; set; }
		public string[] AllowedAccountIds { get; set; }
	}
}
