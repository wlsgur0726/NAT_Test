@echo off
cd %~dp0\..\..\..
xcopy /C /Y *.cs Sample\SampleUnityClient\Assets\
xcopy /C /Y ref\Newtonsoft.Json\Debug\DotNet_3.5_Unity\*.dll Sample\SampleUnityClient\Assets\
