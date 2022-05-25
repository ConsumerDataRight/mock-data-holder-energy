namespace CDR.DataHolder.Resource.API.Business.Models
{
	public class EnergyAccountPlan
	{
		public string Nickname { get; set; }
		public string[] ServicePointIds { get; set; }
		public EnergyPlanOverview PlanOverview { get; set; }
	}
}