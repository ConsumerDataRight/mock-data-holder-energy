using System;
using System.ComponentModel.DataAnnotations;

namespace CDR.DataHolder.Repository.Entities
{
	public class ServicePoint
	{
		[Key, MaxLength(100)]
		public string ServicePointId { get; set; }

		[Required]
		public string AccountPlanId { get; set; }
		public virtual AccountPlan AccountPlan { get; set; }

		[Required, MaxLength(100)]
		public string NationalMeteringId { get; set; }
		[Required, MaxLength(100)]
		public string ServicePointClassification { get; set; }
		[Required, MaxLength(100)]
		public string ServicePointStatus { get; set; }
		[Required, MaxLength(100)]
		public string JurisdictionCode { get; set; }
		public bool? IsGenerator { get; set; }
		public DateTime ValidFromDate { get; set; }
		public DateTime LastUpdateDateTime { get; set; }
	}
}