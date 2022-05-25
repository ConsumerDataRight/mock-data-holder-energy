using AutoMapper;
using CDR.DataHolder.Repository.Entities;
using DomainEntities = CDR.DataHolder.Domain.Entities;
namespace CDR.DataHolder.Repository.Infrastructure
{
	public class MappingProfile : Profile
	{
		public MappingProfile()
		{
			CreateMap<AccountPlan, DomainEntities.EnergyAccountPlan>()
				.ReverseMap();
			CreateMap<AccountConcession, DomainEntities.EnergyAccountConcession>()
				.ForMember(dest => dest.StartDate, source => source.MapFrom(source => source.StartDate.HasValue ? source.StartDate.Value.ToString("yyyy-MM-dd") : null))
				.ForMember(dest => dest.EndDate, source => source.MapFrom(source => source.EndDate.HasValue ? source.EndDate.Value.ToString("yyyy-MM-dd") : null))
				.ForMember(dest => dest.DailyDiscount, source => source.MapFrom(source => source.DailyDiscount.HasValue ? source.DailyDiscount.Value.ToString("F2") : null))
				.ForMember(dest => dest.MonthlyDiscount, source => source.MapFrom(source => source.MonthlyDiscount.HasValue ? source.MonthlyDiscount.Value.ToString("F2") : null))
				.ForMember(dest => dest.YearlyDiscount, source => source.MapFrom(source => source.YearlyDiscount.HasValue ? source.YearlyDiscount.Value.ToString("F2") : null))
				.ForMember(dest => dest.PercentageDiscount, source => source.MapFrom(source => source.PercentageDiscount.HasValue ? source.PercentageDiscount.Value.ToString(): null))
				.ReverseMap();
			CreateMap<ServicePoint, DomainEntities.EnergyServicePoint>()
				.ReverseMap();
			CreateMap<PlanOverview, DomainEntities.EnergyPlanOverview>()
				.ForMember(dest => dest.StartDate, source => source.MapFrom(source => source.StartDate.ToString("yyyy-MM-dd")))
				.ForMember(dest => dest.EndDate, source => source.MapFrom(source => source.EndDate.HasValue ? source.EndDate.Value.ToString("yyyy-MM-dd"): null))
				.ReverseMap();

			CreateMap<Account, DomainEntities.EnergyAccount>()
				.ForMember(dest => dest.Plans, source => source.MapFrom(source => source.AccountPlans))
				.ForMember(dest => dest.Concessions, source => source.MapFrom(source => source.AccountConcessions))
				.ReverseMap();

			CreateMap<Person, DomainEntities.Person>()
				.ForMember(dest => dest.MiddleNames,
					source => source.MapFrom(source => string.IsNullOrEmpty(source.MiddleNames) ? null : source.MiddleNames.Split(',', System.StringSplitOptions.TrimEntries)))
				.ReverseMap();

			CreateMap<Organisation, DomainEntities.Organisation>()
				.ForMember(dest => dest.EstablishmentDate, 
					source => source.MapFrom(source => source.EstablishmentDate == null ? null : source.EstablishmentDate.Value.ToString("yyyy-MM-dd")))
				.ReverseMap();

			CreateMap<Customer, DomainEntities.Customer>()
				.ForMember(dest => dest.Accounts, source => source.MapFrom(source => source.Accounts))
				.ReverseMap();

			CreateMap<Customer, DomainEntities.Person>()
				.IncludeMembers(source => source.Person, source => source)
				.ReverseMap();

			CreateMap<Customer, DomainEntities.Organisation>()
				.IncludeMembers(source => source.Organisation)
				.ReverseMap();

			CreateMap<Brand, DomainEntities.Brand>()
				.ForMember(dest => dest.LegalEntity, source => source.MapFrom(source => source.LegalEntity))
				.ReverseMap();

			CreateMap<LegalEntity, DomainEntities.LegalEntity>()
				.ReverseMap();

			CreateMap<SoftwareProduct, DomainEntities.SoftwareProduct>()
				.ForMember(dest => dest.Brand, source => source.MapFrom(source => source.Brand))
				.ReverseMap();
		}
	}
}
