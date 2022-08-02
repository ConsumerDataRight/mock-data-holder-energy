using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CDR.DataHolder.Repository.Entities
{
	public class AccountConcession
	{
		[Key, MaxLength(100)]
		public string AccountConcessionId { get; set; }

		[Required]
		public string AccountId { get; set; }
		public virtual Account Account { get; set; }

		[MaxLength(1000), Required]
		public string Type { get; set; }
		[Required, MaxLength(100)]
		public string DisplayName { get; set; }
		[MaxLength(1000)]
		public string AdditionalInfo { get; set; }
		[MaxLength(1000)] 
		public string AdditionalInfoUri { get; set; }
		public DateTime? StartDate { get; set; }
		public DateTime? EndDate { get; set; }
		[MaxLength(1000)] 
		public string DiscountFrequency { get; set; }
		[MaxLength(1000)]
		public string Amount { get; set; }
		[MaxLength(1000)]
		public string Percentage { get; set; }
		[MaxLength(1000)]
		public string AppliedTo { get; set; }
	}
}