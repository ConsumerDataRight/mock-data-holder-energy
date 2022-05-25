using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CDR.DataHolder.Repository.Entities
{
	public class PlanAdditionalInformation
	{
		[MaxLength(100)]
		public string PlanId { get; set; }
		public Plan Plan { get; set; }

		[MaxLength(1000)]
		public string OverviewUri { get; set; }
		[MaxLength(1000)]
		public string TermsUri { get; set; }
		[MaxLength(1000)]
		public string EligibilityUri { get; set; }
		[MaxLength(1000)]
		public string PricingUri { get; set; }
		[MaxLength(1000)]
		public string BundleUri { get; set; }
	}
}
