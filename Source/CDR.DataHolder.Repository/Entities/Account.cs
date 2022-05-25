using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace CDR.DataHolder.Repository.Entities
{
	public class Account
	{
		[Key, MaxLength(100)]
		public string AccountId { get; set; }

		[MaxLength(100)]
		public string AccountNumber { get; set; }

		[MaxLength(100)]
		public string DisplayName { get; set; }

		[Required]
		public DateTime CreationDate { get; set; }

		public Guid CustomerId { get; set; }
		public virtual Customer Customer { get; set; }
		public virtual ICollection<AccountPlan> AccountPlans { get; set; }
		public virtual ICollection<AccountConcession> AccountConcessions { get; set; }
	}
}
