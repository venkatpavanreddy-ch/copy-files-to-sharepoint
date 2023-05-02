dotnet tool install Cake.Tool --tool-path %~dp0\tools --ignore-failed-sources
%~dp0\tools\dotnet-cake.exe "%~dp0\build.cake" --target="Deploy" --verbosity=verbose --environment=prod
pause
