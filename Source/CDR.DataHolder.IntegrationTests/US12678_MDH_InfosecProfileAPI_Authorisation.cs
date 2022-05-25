using CDR.DataHolder.IntegrationTests.Fixtures;
using System;
using System.Threading.Tasks;
using Xunit;

#nullable enable

namespace CDR.DataHolder.IntegrationTests
{
    public class US12678_MDH_InfosecProfileAPI_Authorisation : BaseTest, IClassFixture<RegisterSoftwareProductFixture>
    {
        static private void Arrange()
        {
            TestSetup.DataHolder_PurgeIdentityServer(true);
        }

        // Authorisation Request Object method is no longer supported by the data holder.
        [Theory]
        [InlineData(TokenType.MARY_MOSS)]
        public async Task AC01_Get_WithValidRequest_ShouldRespondWith_302Redirect_RedirectToRedirectURI_IdToken(TokenType tokenType)
        {
            // Arrange
            Arrange();

            // Act and Assert.
            await Assert.ThrowsAsync<NotSupportedException>(async () => await GetTokenWithRequestObject(tokenType)); // Perform E2E authorisaton/consentflow
        }

    }
}
