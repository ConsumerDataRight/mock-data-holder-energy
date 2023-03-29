using System.Linq;
using AutoMapper;
using CDR.DataHolder.Domain.Entities;
using CDR.DataHolder.Domain.ValueObjects;
using CDR.DataHolder.Resource.API.Business.Models;
using CDR.DataHolder.Resource.API.Business.Responses;

namespace CDR.DataHolder.Resource.API.Business
{
	public class MappingProfile : Profile
	{
		public MappingProfile()
		{
			CreateMap<RequestAccountConsessions, AccountConsessionsFilter>();

			CreateMap(typeof(Page<>), typeof(MetaPaginated))
				.ReverseMap();

			CreateMap<Person, CommonPerson>()
				.ReverseMap();

			CreateMap<Organisation, CommonOrganisation>()
				.ReverseMap();

			CreateMap<Person, CustomerModel>()
				.ForMember(dest => dest.Person, source => source.MapFrom(source => source))
				.ReverseMap();

			CreateMap<Organisation, CustomerModel>()
				.ForMember(dest => dest.Organisation, source => source.MapFrom(source => source))
				.ReverseMap();

			CreateMap<Person, ResponseCommonCustomer>()
				.ForMember(dest => dest.Data, source => source.MapFrom(source => source))
				.ReverseMap();

			CreateMap<Organisation, ResponseCommonCustomer>()
				.ForMember(dest => dest.Data, source => source.MapFrom(source => source))
				.ReverseMap();

			CreateMap<Domain.Entities.EnergyAccountPlan, Models.EnergyAccountPlan>()
				.ForMember(dest => dest.ServicePointIds, source => source.MapFrom(source => source.ServicePoints.Select(sp => sp.ServicePointId)))
				.ReverseMap();
			CreateMap<Domain.Entities.EnergyPlanOverview, Models.EnergyPlanOverview>()
				.ReverseMap();

            CreateMap<Domain.Entities.EnergyAccount, BaseEnergyAccount>()
                .ForMember(dest => dest.CreationDate, source => source.MapFrom(source => source.CreationDate.ToString("yyyy-MM-dd")))
                .ForMember(dest => dest.Plans, source => source.MapFrom(source => source.Plans))
                .ReverseMap();
            CreateMap<Domain.Entities.EnergyAccount, Models.EnergyAccount>()
                .IncludeBase<Domain.Entities.EnergyAccount, BaseEnergyAccount>()
                .ReverseMap();
            CreateMap<Domain.Entities.EnergyAccount, EnergyAccountV2>()
				.IncludeBase<Domain.Entities.EnergyAccount, BaseEnergyAccount>()
                .ForMember(dest => dest.OpenStatus, source => source.MapFrom(source => source.OpenStatus))
                .ReverseMap();
            CreateMap<Page<Domain.Entities.EnergyAccount[]>, EnergyAccountListResponse<Models.EnergyAccount>>()
				.ForPath(dest => dest.Data.Accounts, source => source.MapFrom(source => source.Data))
				.ForMember(dest => dest.Meta, source => source.MapFrom(source => source))
				.ReverseMap();
            CreateMap<Page<Domain.Entities.EnergyAccount[]>, EnergyAccountListResponse<Models.EnergyAccountV2>>()
                .ForPath(dest => dest.Data.Accounts, source => source.MapFrom(source => source.Data))
                .ForMember(dest => dest.Meta, source => source.MapFrom(source => source))
                .ReverseMap();

            CreateMap<EnergyAccountConcession[], EnergyConcessionsResponse>()
				.ForPath(dest => dest.Data.Concessions, source => source.MapFrom(source => source));
			CreateMap<EnergyAccountConcession, EnergyConcession>()
				.ReverseMap();
		}
	}
}
