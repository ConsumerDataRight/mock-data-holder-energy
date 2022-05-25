using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace CDR.DataHolder.Repository.Entities
{
	public class AccountPlan
	{
		[Key, MaxLength(100)]
		public string AccountPlanId { get; set; }

		[Required]
		public string AccountId { get; set; }
		public virtual Account Account { get; set; }

		[Required]
		public string PlanId { get; set; }
		public virtual Plan Plan { get; set; }

		[MaxLength(100)]
		public string Nickname { get; set; }

		public virtual ICollection<ServicePoint> ServicePoints { get; set; }
		// configure 1-1 relationship
		public virtual PlanOverview PlanOverview { get; set; }
	}
}