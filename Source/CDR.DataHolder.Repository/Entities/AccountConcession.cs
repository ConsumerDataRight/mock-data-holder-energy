using System;
using System.ComponentModel.DataAnnotations;
using CDR.DataHolder.Repository.Entities;

namespace CDR.DataHolder.Repository.Entities
{
	public class AccountConcession
	{
		[Key, MaxLength(100)]
		public string AccountConcessionId { get; set; }

		[Required]
		public string AccountId { get; set; }
		public virtual Account Account { get; set; }

		[Required, MaxLength(100)]
		public string DisplayName { get; set; }
		[MaxLength(1000)]
		public string AdditionalInfo { get; set; }
		[MaxLength(1000)] 
		public string AdditionalInfoUri { get; set; }
		public DateTime? StartDate { get; set; }
		public DateTime? EndDate { get; set; }
		public decimal? DailyDiscount { get; set; }
		public decimal? MonthlyDiscount { get; set; }
		public decimal? YearlyDiscount { get; set; }
		public decimal? PercentageDiscount { get; set; }
	}
}