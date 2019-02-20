set version=200.0.7


mkdir SQLite.Net
copy /y ..\src\SQLite.Net\bin\Release\netstandard1.1\SQLite.Net.dll SQLite.Net
type nul >SQLite.Net\_._

rem mkdir SQLite.Net.Async
rem copy /y ..\src\SQLite.Net.Async\bin\Release\SQLite.Net.Async.dll SQLite.Net.Async

mkdir SQLite.Net.Platform.Generic
copy /y ..\src\SQLite.Net.Platform.Generic\bin\Release\SQLite.Net.Platform.Generic.dll SQLite.Net.Platform.Generic

mkdir SQLite.Net.Platform.Win32
copy /y ..\src\SQLite.Net.Platform.Win32\bin\Release\SQLite.Net.Platform.Win32.dll SQLite.Net.Platform.Win32

mkdir SQLite.Net.Platform.NetCore
copy /y ..\src\SQLite.Net.Platform.NetCore\bin\Release\netcoreapp2.0\SQLite.Net.Platform.NetCore.dll SQLite.Net.Platform.NetCore

mkdir SQLite.Net.Platform.XamarinAndroid
copy /y ..\src\SQLite.Net.Platform.XamarinAndroid\bin\Release\SQLite.Net.Platform.XamarinAndroid.dll SQLite.Net.Platform.XamarinAndroid

mkdir SQLite.Net.Platform.XamarinIOS.Unified
copy /y ..\src\SQLite.Net.Platform.XamarinIOS.Unified\bin\Release\SQLite.Net.Platform.XamarinIOS.Unified.dll SQLite.Net.Platform.XamarinIOS.Unified

mkdir SQLite.Net.Platform.Uwp
copy /y ..\src\SQLite.Net.Platform.Uwp\bin\Release\SQLite.Net.Platform.Uwp.dll SQLite.Net.Platform.Uwp


@mkdir output
del /q	output\*.*
nuget pack SQLite.Net.nuspec -OutputDirectory output -Version %version%
rem nuget pack SQLite.Net.Async.nuspec -OutputDirectory output -Version %version%


nuget push output\*.nupkg -Source http://nugets.vapolia.fr/

