using System;

namespace CDR.DataHolder.Domain.ValueObjects
{
    public class AccountConsessionsFilter
    {
        public string AccountId { get; set; }

        public Guid CustomerId { get; set; }
    }
}
