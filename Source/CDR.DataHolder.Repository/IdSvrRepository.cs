using System;
using System.Linq;
using System.Threading.Tasks;
using CDR.DataHolder.Domain.Entities;
using CDR.DataHolder.Domain.Repositories;
using CDR.DataHolder.Repository.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CDR.DataHolder.Repository
{
    public class IdSvrRepository : IIdSvrRepository
	{
		private readonly DataHolderDatabaseContext _dataHolderDatabaseContext;

		public IdSvrRepository(DataHolderDatabaseContext dataHolderDatabaseContext)
		{
			_dataHolderDatabaseContext = dataHolderDatabaseContext;
		}

		public async Task<UserInfoClaims> GetUserInfoClaims(Guid customerId)
		{
			return await _dataHolderDatabaseContext.Customers
				.Include(x => x.Person)
				.Include(x => x.Organisation)
				.Where(x => x.CustomerId == customerId)
				.Select(x => new UserInfoClaims
				{
					GivenName = x.GivenName,
					FamilyName = x.FamilyName,
					Name = x.Name,
					LastUpdated = x.LastUpdated,
				})
				.FirstOrDefaultAsync();
		}

	}
}
