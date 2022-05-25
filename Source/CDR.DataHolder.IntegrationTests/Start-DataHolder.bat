@echo off
echo Start DataHolder projects?
pause

dotnet build ../CDR.DataHolder.API.Gateway.mTLS
dotnet build ../CDR.DataHolder.IdentityServer
dotnet build ../CDR.DataHolder.Resource.API
dotnet build ../CDR.DataHolder.Manage.API

pause

wt --maximized ^
--title Energy_Gateway_MTLS -d ../CDR.DataHolder.API.Gateway.mTLS dotnet run --no-build; ^
--title Energy_IdentityServer -d ../CDR.DataHolder.IdentityServer dotnet run --no-build; ^
--title Energy_Resource_API -d ../CDR.DataHolder.Resource.API dotnet run --no-build; ^
--title Energy_Manage_API -d ../CDR.DataHolder.Manage.API dotnet run --no-build


pause
 
 