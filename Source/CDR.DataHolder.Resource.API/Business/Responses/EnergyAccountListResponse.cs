using CDR.DataHolder.Resource.API.Business.Models;

namespace CDR.DataHolder.Resource.API.Business.Responses
{
    public class EnergyAccountListResponse<T> where T : class
    {
		public Links Links { get; set; } = new Links();
		public MetaPaginated Meta { get; set; } = new MetaPaginated();
        public EnergyAccounts<T> Data { get; set; }
    }
}
