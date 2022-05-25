using System;
using System.Threading.Tasks;
using CDR.DataHolder.Domain.Entities;
using CDR.DataHolder.Domain.ValueObjects;

namespace CDR.DataHolder.Domain.Repositories
{
	public interface IResourceRepository
	{
		Task<Customer> GetCustomer(Guid customerId);
		Task<Customer> GetCustomerByLoginId(string loginId);
		Task<bool> CanAccessAccount(string accountId, Guid customerId);
		Task<Page<EnergyAccount[]>> GetAllEnergyAccounts(EnergyAccountFilter filter, int page, int pageSize);
		Task<EnergyAccount[]> GetAllAccountsByCustomerIdForConsent(Guid customerId);
		Task<EnergyAccountConcession[]> GetEnergyAccountConcessions(AccountConsessionsFilter filter);
	}
}
