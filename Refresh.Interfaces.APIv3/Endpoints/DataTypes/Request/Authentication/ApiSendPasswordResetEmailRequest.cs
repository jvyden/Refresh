namespace Refresh.Interfaces.APIv3.Endpoints.DataTypes.Request.Authentication;

#nullable disable

[JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy))]
public class ApiSendPasswordResetEmailRequest
{
    public string EmailAddress { get; set; }
}