namespace CDR.DataHolder.Resource.API.Business.Models
{
	public class EnergyConcession
	{
		public string Type { get; set; }
		public string DisplayName { get; set; }
		public string AdditionalInfo { get; set; }
		public string AdditionalInfoUri { get; set; }
		public string StartDate { get; set; }
		public string EndDate { get; set; }
		public string DiscountFrequency { get; set; }
		public string Amount { get; set; }
		public string Percentage { get; set; }
		public string[] AppliedTo { get; set; }
	}
}
