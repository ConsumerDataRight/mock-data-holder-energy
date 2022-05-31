﻿namespace CDR.DataHolder.Domain.Entities
{
	public class EnergyAccountConcession
	{
		public string DisplayName { get; set; }
		public string AdditionalInfo { get; set; }
		public string AdditionalInfoUri { get; set; }
		public string StartDate { get; set; }
		public string EndDate { get; set; }
		public string DailyDiscount { get; set; }
		public string MonthlyDiscount { get; set; }
		public string YearlyDiscount { get; set; }
		public string PercentageDiscount { get; set; }
	}
}