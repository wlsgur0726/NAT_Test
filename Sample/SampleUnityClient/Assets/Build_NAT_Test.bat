:: %1  :  Debug or Release
@echo off

:: MSBuild.exe�� ��ã�� ��� �Ʒ� �ڵ带 �����Ͽ� PATH�� ����
::set PATH=%PATH%;"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\"

set Config=%1
if "%Config%"=="" set Config=Debug
cd %~dp0\..\..\..
MSBuild.exe NAT_Test_3.5_Unity.csproj /t:rebuild /p:Configuration=%Config%

xcopy /C /Y bin\%Config%\*.dll Sample\SampleUnityClient\Assets\NAT_Test\