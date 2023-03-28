using CDR.DataHolder.Domain.ValueObjects;
using System.Collections.Generic;
using System.Drawing;

namespace CDR.DataHolder.Resource.API.Business.Models
{
	public class EnergyAccounts<T> where T : class
	{
        public IEnumerable<T> Accounts { get; set; }
    }
}