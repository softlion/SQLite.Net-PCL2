$versionSuffix=""

#####################
#Build release config
cd $PSScriptRoot
del *.nupkg

& dotnet pack -c Release --version-suffix "$versionSuffix" ../src/SQLite.Net/SQLite.Net2.csproj -o .

if ($lastexitcode -ne 0) { exit $lastexitcode; }

dotnet nuget push "sqlite-net2.*$versionSuffix.nupkg"
