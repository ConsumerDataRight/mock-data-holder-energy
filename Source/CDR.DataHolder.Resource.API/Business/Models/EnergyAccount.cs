namespace CDR.DataHolder.Resource.API.Business.Models
{
	public class EnergyAccount
    {
		public string AccountId { get; set; }
		public string AccountNumber { get; set; }
		public string DisplayName { get; set; }
		public string CreationDate { get; set; }
		public EnergyAccountPlan[] Plans { get; set; }
	}
}
