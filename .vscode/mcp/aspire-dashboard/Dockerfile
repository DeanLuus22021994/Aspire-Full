FROM mcr.microsoft.com/dotnet/aspire-dashboard:latest

ENV DOTNET_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS=true \
    DASHBOARD__OTLP__AUTHMODE=Unsecured \
    DASHBOARD__FRONTEND__AUTHMODE=Unsecured \
    DASHBOARD__RESOURCESERVICE__AUTHMODE=Unsecured \
    ASPIRE_DASHBOARD_MCP_ENDPOINT_URL=http://0.0.0.0:16036 \
    ASPIRE_ALLOW_UNSECURED_TRANSPORT=true

EXPOSE 18888 18889 16036
VOLUME ["/app/data"]

CMD ["dotnet", "Microsoft.Aspire.Dashboard.Web.dll"]
