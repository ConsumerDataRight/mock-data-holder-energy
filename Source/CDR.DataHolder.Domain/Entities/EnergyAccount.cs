using System;

namespace CDR.DataHolder.Domain.Entities
{
	public class EnergyAccount
	{
		public string AccountId { get; set; }
		public string AccountNumber { get; set; }
		public string DisplayName { get; set; }
        public string OpenStatus { get; set; }
        public DateTime CreationDate { get; set; }
		public EnergyAccountPlan[] Plans { get; set; }
		public EnergyAccountConcession[] Concessions { get; set; }
	}
}