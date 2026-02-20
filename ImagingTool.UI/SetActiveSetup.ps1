$Key = "HKLM:\SOFTWARE\Microsoft\Active Setup\Installed Components\POS_SetVolumeZero"

New-Item -Path $Key -Force | Out-Null

Set-ItemProperty -Path $Key -Name "StubPath" -Value '"D:\Image\SoundHelper\AudioHelper.exe"'
Set-ItemProperty -Path $Key -Name "Version" -Value "1,0,0,0"
Set-ItemProperty -Path $Key -Name "IsInstalled" -Value 1 -Type DWord