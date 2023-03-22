@echo off
echo Run solutions from .Net CLI using localhost and localdb from appsettings.Development.json
pause

setx ASPNETCORE_ENVIRONMENT Development

dotnet build CDR.DataHolder.API.Gateway.mTLS
dotnet build CDR.DataHolder.Resource.API
dotnet build CDR.DataHolder.Public.API
dotnet build CDR.DataHolder.Manage.API

wt --maximized ^
--title MDHE_MTLS -d CDR.DataHolder.API.Gateway.mTLS dotnet run --no-launch-profile; ^
--title MDHE_Res_API -d CDR.DataHolder.Resource.API dotnet run --no-launch-profile; ^
--title MDHE_Pub_API -d CDR.DataHolder.Public.API dotnet run --no-launch-profile; ^
--title MDHE_Mgr_API -d CDR.DataHolder.Manage.API dotnet run --no-launch-profile
